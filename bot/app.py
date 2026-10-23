"""A small, private-network Synology Chat webhook service.

DSM and Synology Chat own employee accounts and messages. This service never
receives DSM passwords and offers no user import, approval, or deployment API.
"""

from __future__ import annotations

import hmac
import json
import math
import os
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any
from urllib.parse import parse_qs, parse_qsl, urlsplit

import httpx
from dotenv import dotenv_values
from fastapi import FastAPI, HTTPException, Request

MAX_BODY_BYTES = 64 * 1024
MAX_TEXT_LENGTH = 4000
HELP_TEXT = (
    "사용할 수 있는 명령: /help (도움말), /ping (연동 확인), "
    "/whoami (Chat에서 전달한 사용자 정보). "
    "승인·배포 등 시스템 변경 명령은 제공하지 않습니다."
)


def _validate_url(value: str, setting_name: str, *, public: bool = False) -> None:
    if not value:
        return
    if any(character.isspace() or ord(character) < 32 for character in value):
        raise ValueError(f"{setting_name}: 공백 없는 HTTP(S) URL을 입력하세요.")
    try:
        parsed = urlsplit(value)
        invalid_port = parsed.port is not None and not (1 <= parsed.port <= 65535)
    except ValueError:
        raise ValueError(f"{setting_name}: 올바른 HTTP(S) URL을 입력하세요.") from None
    if (
        parsed.scheme not in {"http", "https"}
        or not parsed.hostname
        or parsed.username is not None
        or parsed.password is not None
        or parsed.fragment
        or "\\" in value
        or invalid_port
    ):
        raise ValueError(f"{setting_name}: 사용자 인증정보가 없는 HTTP(S) URL을 입력하세요.")
    # The Chat address is displayed to unauthenticated portal users. Integration
    # secrets belong only in the separate incoming webhook URL.
    if public and parsed.query and parse_qsl(parsed.query, keep_blank_values=True) != [
        ("launchApp", "SYNO.SDS.Chat.Application")
    ]:
        raise ValueError(f"{setting_name}: Chat 실행 경로 이외의 쿼리·토큰은 사용할 수 없습니다.")


@dataclass(frozen=True)
class Settings:
    chat_url: str = ""
    incoming_webhook_url: str = field(default="", repr=False)
    outgoing_token: str = field(default="", repr=False)
    action_token: str = field(default="", repr=False)
    notify_token: str = field(default="", repr=False)
    webhook_timeout: float = 10.0

    def __post_init__(self) -> None:
        for name in (
            "chat_url", "incoming_webhook_url", "outgoing_token", "action_token", "notify_token"
        ):
            value = getattr(self, name)
            if not isinstance(value, str):
                raise ValueError(f"{name}: 문자열을 입력하세요.")
            object.__setattr__(self, name, value.strip())
        _validate_url(self.chat_url, "SYNO_CHAT_URL", public=True)
        _validate_url(self.incoming_webhook_url, "SYNO_INCOMING_WEBHOOK_URL")
        if (
            isinstance(self.webhook_timeout, bool)
            or not isinstance(self.webhook_timeout, (int, float))
            or not math.isfinite(self.webhook_timeout)
            or not 0 < self.webhook_timeout <= 60
        ):
            raise ValueError("WEBHOOK_TIMEOUT_SECONDS: 0 초 초과 ~ 60 초를 입력하세요.")

    @classmethod
    def from_env(cls, env_file: str | Path | None = None) -> Settings:
        """Read bot/.env, then let real environment variables take precedence."""
        source = dict(dotenv_values(env_file or Path(__file__).with_name(".env")))
        source.update(os.environ)
        try:
            timeout = float(source.get("WEBHOOK_TIMEOUT_SECONDS") or "10")
        except (TypeError, ValueError):
            raise ValueError("WEBHOOK_TIMEOUT_SECONDS: 숫자를 입력하세요.") from None
        return cls(
            chat_url=source.get("SYNO_CHAT_URL") or "",
            incoming_webhook_url=source.get("SYNO_INCOMING_WEBHOOK_URL") or "",
            outgoing_token=source.get("SYNO_OUTGOING_TOKEN") or "",
            action_token=source.get("SYNO_ACTION_TOKEN") or "",
            notify_token=source.get("NOTIFY_TOKEN") or "",
            webhook_timeout=timeout,
        )


def _authenticate(expected: str, supplied: Any) -> None:
    if not expected:
        raise HTTPException(503, "이 연동의 토큰이 설정되지 않았습니다.")
    # Compare bytes so non-ASCII tokens cannot trigger compare_digest TypeError.
    if not isinstance(supplied, str) or not hmac.compare_digest(
        expected.encode("utf-8"), supplied.encode("utf-8")
    ):
        raise HTTPException(401, "인증에 실패했습니다.")


async def _bounded_body(request: Request) -> bytes:
    content_length = request.headers.get("content-length")
    if content_length is not None:
        try:
            length = int(content_length)
        except ValueError:
            raise HTTPException(400, "올바르지 않은 요청 크기입니다.") from None
        if length < 0:
            raise HTTPException(400, "올바르지 않은 요청 크기입니다.")
        if length > MAX_BODY_BYTES:
            raise HTTPException(413, "요청 본문은 64 KiB 이하여야 합니다.")
    chunks = bytearray()
    async for chunk in request.stream():
        if len(chunks) + len(chunk) > MAX_BODY_BYTES:
            raise HTTPException(413, "요청 본문은 64 KiB 이하여야 합니다.")
        chunks.extend(chunk)
    return bytes(chunks)


def _json_object(body: str | bytes) -> dict[str, Any]:
    try:
        value = json.loads(body)
    except (ValueError, UnicodeDecodeError, RecursionError):
        raise HTTPException(422, "올바른 JSON 객체를 입력하세요.") from None
    if not isinstance(value, dict):
        raise HTTPException(422, "JSON 객체를 입력하세요.")
    return value


async def _webhook_fields(request: Request) -> dict[str, Any]:
    body = await _bounded_body(request)
    content_type = request.headers.get("content-type", "").split(";", 1)[0].lower()
    if content_type == "application/json":
        return _json_object(body)
    if content_type != "application/x-www-form-urlencoded":
        raise HTTPException(415, "URL 인코딩 폼 또는 JSON을 사용하세요.")
    try:
        form = parse_qs(
            body.decode("utf-8"), keep_blank_values=True, errors="strict", max_num_fields=64
        )
    except (ValueError, UnicodeDecodeError):
        raise HTTPException(422, "올바른 UTF-8 폼을 입력하세요.") from None
    # Ambiguous repeated values, especially repeated tokens, are rejected.
    if any(len(values) != 1 for values in form.values()):
        raise HTTPException(422, "같은 폼 필드를 중복해서 보낼 수 없습니다.")
    return {key: values[0] for key, values in form.items()}


def _text(value: Any, name: str, *, maximum: int, allow_empty: bool = False) -> str:
    if not isinstance(value, str) or len(value) > maximum or (
        not allow_empty and not value.strip()
    ):
        raise HTTPException(422, f"{name}: 1~{maximum}자의 문자열을 입력하세요.")
    return value


def _identity(value: Any, name: str, maximum: int) -> str:
    if value is None or value == "":
        return "미제공"
    if isinstance(value, int) and not isinstance(value, bool):
        value = str(value)
    text = _text(value, name, maximum=maximum)
    return "".join(
        " " if ord(character) < 32 else {"@": "＠", "<": "＜", ">": "＞"}.get(character, character)
        for character in text
    )


def _validate_actions(payload: dict[str, Any]) -> None:
    _text(payload.get("callback_id"), "callback_id", maximum=200)
    actions = payload.get("actions")
    if isinstance(actions, str):
        try:
            actions = json.loads(actions)
        except (ValueError, RecursionError):
            raise HTTPException(422, "actions는 JSON 배열이어야 합니다.") from None
    if not isinstance(actions, list) or not 1 <= len(actions) <= 10:
        raise HTTPException(422, "actions에는 1~10개의 버튼을 입력하세요.")
    for action in actions:
        if not isinstance(action, dict) or action.get("type", "button") != "button":
            raise HTTPException(422, "버튼 action 객체를 입력하세요.")
        name = action.get("name", "")
        value = action.get("value", "")
        _text(name, "action.name", maximum=200, allow_empty=True)
        _text(value, "action.value", maximum=1000, allow_empty=True)
        if not name.strip() and not value.strip():
            raise HTTPException(422, "버튼 name 또는 value를 입력하세요.")
    user = payload.get("user", {})
    if isinstance(user, str):
        user = _json_object(user)
    if not isinstance(user, dict):
        raise HTTPException(422, "user는 JSON 객체여야 합니다.")
    _identity(user.get("username", payload.get("username")), "username", 200)
    _identity(user.get("user_id", payload.get("user_id")), "user_id", 100)


def _acknowledge_action(fields: dict[str, Any], configured: Settings) -> dict[str, str]:
    if not configured.action_token:
        _authenticate("", None)
    payload = fields
    if "payload" in fields:
        wrapped = fields["payload"]
        if isinstance(wrapped, str):
            payload = _json_object(wrapped)
        elif isinstance(wrapped, dict):
            payload = wrapped
        else:
            raise HTTPException(422, "payload는 JSON 객체여야 합니다.")
    _authenticate(configured.action_token, payload.get("token"))
    _validate_actions(payload)
    return {
        "text": "버튼 요청을 접수했습니다. 승인·거절·배포 작업은 수행되지 않았습니다."
    }


def create_app(settings: Settings | None = None) -> FastAPI:
    app = FastAPI(
        title="사내 메신저 연동", docs_url=None, redoc_url=None, openapi_url=None
    )
    app.state.settings = settings if settings is not None else Settings.from_env()
    # A transport may be injected on this app instance by tests. Production uses
    # httpx's normal TLS-verified transport and never reads proxy environment vars.
    app.state.http_transport = None

    @app.middleware("http")
    async def security_headers(request: Request, call_next: Any) -> Any:
        response = await call_next(request)
        response.headers["X-Content-Type-Options"] = "nosniff"
        response.headers["Referrer-Policy"] = "no-referrer"
        response.headers["X-Frame-Options"] = "DENY"
        response.headers["Cache-Control"] = "no-store"
        response.headers["Content-Security-Policy"] = (
            "default-src 'self'; script-src 'self' 'unsafe-inline'; "
            "style-src 'self' 'unsafe-inline'; img-src 'self' data:; "
            "connect-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'"
        )
        return response

    @app.get("/", include_in_schema=False)
    async def home() -> dict[str, str]:
        return {"service": "TEMPCHAIN Chat 알림 연동 API", "status": "ok"}

    @app.get("/healthz")
    async def health() -> dict[str, str]:
        return {"status": "ok"}

    @app.get("/api/status")
    async def status() -> dict[str, Any]:
        configured: Settings = app.state.settings
        return {
            "service": "Synology Chat 연동",
            "chat_url": configured.chat_url or None,
            "incoming_configured": bool(configured.incoming_webhook_url),
            "outgoing_configured": bool(configured.outgoing_token),
            "notify_configured": bool(configured.notify_token),
            "action_configured": bool(configured.action_token),
        }

    @app.post("/syno/outgoing")
    async def outgoing(request: Request) -> dict[str, str]:
        configured: Settings = app.state.settings
        if not configured.outgoing_token and not configured.action_token:
            _authenticate("", None)
        payload = await _webhook_fields(request)
        # Synology sends both text and interactive events to a Bot's one outgoing
        # URL. Both paths must therefore work here; /syno/action is an alias for
        # services configured with a dedicated interactive callback URL.
        if "payload" in payload or "actions" in payload:
            return _acknowledge_action(payload, configured)
        _authenticate(configured.outgoing_token, payload.get("token"))
        text = _text(payload.get("text", ""), "text", maximum=MAX_TEXT_LENGTH, allow_empty=True)
        username = _identity(payload.get("username"), "username", 200)
        user_id = _identity(payload.get("user_id"), "user_id", 100)
        command = text.strip().split(maxsplit=1)[0].lstrip("/").lower() if text.strip() else "help"
        if command == "ping":
            return {"text": "pong · Synology Chat 연동 서버가 응답했습니다."}
        if command == "whoami":
            return {"text": f"Chat에서 전달한 사용자명: {username}\n사용자 ID: {user_id}"}
        return {"text": HELP_TEXT}

    @app.post("/syno/action")
    async def action(request: Request) -> dict[str, str]:
        configured: Settings = app.state.settings
        if not configured.action_token:
            _authenticate("", None)
        fields = await _webhook_fields(request)
        return _acknowledge_action(fields, configured)

    @app.post("/notify")
    async def notify(request: Request) -> dict[str, bool]:
        configured: Settings = app.state.settings
        _authenticate(configured.notify_token, request.headers.get("X-Notify-Token"))
        if not configured.incoming_webhook_url:
            raise HTTPException(503, "Incoming Webhook URL이 설정되지 않았습니다.")
        body = await _bounded_body(request)
        if request.headers.get("content-type", "").split(";", 1)[0].lower() != "application/json":
            raise HTTPException(415, "application/json으로 전송하세요.")
        payload = _json_object(body)
        if set(payload) != {"text"}:
            raise HTTPException(422, "text 필드만 입력하세요.")
        text = _text(payload["text"], "text", maximum=MAX_TEXT_LENGTH)
        try:
            async with httpx.AsyncClient(
                timeout=configured.webhook_timeout,
                trust_env=False,
                follow_redirects=False,
                transport=app.state.http_transport,
            ) as client:
                response = await client.post(
                    configured.incoming_webhook_url,
                    data={"payload": json.dumps({"text": text}, ensure_ascii=False)},
                )
            if not 200 <= response.status_code < 300:
                raise HTTPException(502, "Synology Chat에서 알림을 수락하지 않았습니다.")
            result = response.json()
            if not isinstance(result, dict) or result.get("success") is not True or result.get("error"):
                raise HTTPException(502, "Synology Chat에서 알림을 수락하지 않았습니다.")
        except (httpx.HTTPError, ValueError, RecursionError):
            # Upstream exceptions may contain the full webhook URL and token.
            raise HTTPException(502, "Synology Chat 알림 전달에 실패했습니다.") from None
        return {"ok": True}

    return app


app = create_app()
