"""Offline tests for the Chat integration's trust boundaries and wire format."""

import json
from urllib.parse import parse_qs

import httpx
import pytest
from fastapi.testclient import TestClient

from bot.app import Settings, create_app


@pytest.fixture
def settings():
    return Settings(
        chat_url="https://chat.example.test/",
        incoming_webhook_url=(
            "https://nas.example.test/webapi/entry.cgi?api=SYNO.Chat.External"
            "&method=incoming&version=2&token=incoming-secret"
        ),
        outgoing_token="outgoing-secret",
        action_token="action-secret",
        notify_token="notify-secret",
        webhook_timeout=3,
    )


@pytest.fixture
def app(settings):
    instance = create_app(settings)

    def unexpected_network(request):
        pytest.fail(f"Unexpected upstream request to {request.url.host}")

    instance.state.http_transport = httpx.MockTransport(unexpected_network)
    return instance


@pytest.fixture
def client(app):
    with TestClient(app) as instance:
        yield instance


def action_payload(token="action-secret"):
    return {
        "token": token,
        "callback_id": "erp-request",
        "actions": [{"name": "acknowledge", "value": "request-17"}],
        "user": {"username": "박범준", "user_id": 17},
    }


def post_action(client, payload):
    return client.post("/syno/action", data={"payload": json.dumps(payload)})


def post_notify(client, text="회의 시작 알림"):
    return client.post(
        "/notify", headers={"X-Notify-Token": "notify-secret"}, json={"text": text}
    )


def assert_no_secrets(response, settings):
    for secret in (
        settings.incoming_webhook_url,
        "incoming-secret",
        settings.outgoing_token,
        settings.action_token,
        settings.notify_token,
    ):
        assert secret not in response.text


def test_health_and_public_status_expose_only_connection_readiness(client, settings):
    assert client.get("/healthz").json() == {"status": "ok"}
    response = client.get("/api/status")
    assert response.status_code == 200
    status = response.json()
    assert status["chat_url"] == settings.chat_url
    assert all(
        status[key] is True
        for key in (
            "incoming_configured", "outgoing_configured", "action_configured", "notify_configured"
        )
    )
    assert_no_secrets(response, settings)
    assert "incoming-secret" not in repr(settings)


def test_unconfigured_integrations_are_disabled():
    with TestClient(create_app(Settings())) as client:
        status = client.get("/api/status").json()
        assert status["chat_url"] is None
        assert all(value is False for key, value in status.items() if key.endswith("_configured"))
        assert client.post("/syno/outgoing", data={"token": "anything"}).status_code == 503
        assert post_action(client, action_payload()).status_code == 503
        assert post_notify(client).status_code == 503


@pytest.mark.parametrize("token", [None, "incorrect", "action-secret", "notify-secret"])
def test_outgoing_rejects_missing_wrong_and_other_integration_tokens(client, token):
    fields = {"text": "/ping"}
    if token is not None:
        fields["token"] = token
    assert client.post("/syno/outgoing", data=fields).status_code == 401


def test_outgoing_commands_and_unicode_identity(client):
    response = client.post(
        "/syno/outgoing", data={"token": "outgoing-secret", "text": "/ping"}
    )
    assert response.status_code == 200
    assert "pong" in response.json()["text"].lower()
    response = client.post(
        "/syno/outgoing",
        data={"token": "outgoing-secret", "text": "/whoami", "username": "박범준", "user_id": "17"},
    )
    assert response.status_code == 200
    assert "박범준" in response.json()["text"]
    assert "17" in response.json()["text"]


def test_outgoing_unknown_privileged_commands_only_return_help(client):
    help_response = client.post(
        "/syno/outgoing", data={"token": "outgoing-secret", "text": "/help"}
    )
    for command in ("/approve request-17", "/deploy production"):
        response = client.post(
            "/syno/outgoing", data={"token": "outgoing-secret", "text": command}
        )
        assert response.status_code == 200
        assert response.json() == help_response.json()


def test_duplicate_form_tokens_are_rejected(client):
    response = client.post(
        "/syno/outgoing",
        content="token=outgoing-secret&token=other&text=%2Fping",
        headers={"Content-Type": "application/x-www-form-urlencoded"},
    )
    assert response.status_code == 422


def test_body_limit_is_enforced_with_and_without_content_length(client):
    headers = {"Content-Type": "application/x-www-form-urlencoded"}
    assert client.post("/syno/outgoing", content=b"x" * 65537, headers=headers).status_code == 413
    chunks = iter((b"token=outgoing-secret&text=", b"x" * 65536))
    assert client.post("/syno/outgoing", content=chunks, headers=headers).status_code == 413


def test_valid_action_uses_token_inside_form_payload_and_does_not_execute(client):
    response = post_action(client, action_payload())
    assert response.status_code == 200
    assert "text" in response.json()
    assert "수행되지" in response.json()["text"]


@pytest.mark.parametrize("wrapped", [True, False])
def test_bot_outgoing_url_accepts_interactive_callbacks(client, wrapped):
    payload = action_payload()
    if wrapped:
        response = client.post("/syno/outgoing", data={"payload": json.dumps(payload)})
    else:
        response = client.post("/syno/outgoing", json=payload)
    assert response.status_code == 200
    assert response.json() == post_action(client, payload).json()
    assert "수행되지" in response.json()["text"]


@pytest.mark.parametrize("token", [None, "outgoing-secret", "notify-secret"])
def test_bot_outgoing_callback_requires_action_token_even_with_valid_outer_token(client, token):
    payload = action_payload(token=token)
    if token is None:
        payload.pop("token")
    response = client.post(
        "/syno/outgoing", data={"token": "action-secret", "payload": json.dumps(payload)}
    )
    assert response.status_code == 401


def test_bot_action_disabled_does_not_disable_configured_text_commands():
    with TestClient(create_app(Settings(outgoing_token="outgoing-secret"))) as client:
        response = client.post("/syno/outgoing", data={"payload": json.dumps(action_payload())})
        assert response.status_code == 503
        response = client.post("/syno/outgoing", data={"token": "outgoing-secret", "text": "/ping"})
        assert response.status_code == 200
        assert "pong" in response.json()["text"].lower()


def test_action_does_not_trust_outer_or_outgoing_token(client):
    payload = action_payload(token="outgoing-secret")
    response = client.post(
        "/syno/action", data={"token": "action-secret", "payload": json.dumps(payload)}
    )
    assert response.status_code == 401
    payload.pop("token")
    assert post_action(client, payload).status_code == 401


@pytest.mark.parametrize(
    "changes",
    [
        {"callback_id": ""},
        {"actions": []},
        {"actions": [{"name": "approve", "value": {}, "type": "button"}]},
        {"user": []},
    ],
)
def test_action_rejects_malformed_required_fields(client, changes):
    payload = action_payload()
    payload.update(changes)
    assert post_action(client, payload).status_code == 422


def test_action_rejects_invalid_payload_json(client):
    assert client.post("/syno/action", data={"payload": "{invalid"}).status_code == 422
    assert client.post("/syno/action", data={"payload": "[]"}).status_code == 422


def test_notify_requires_its_own_header_token(client):
    for token in (None, "wrong", "outgoing-secret", "action-secret"):
        headers = {} if token is None else {"X-Notify-Token": token}
        assert client.post("/notify", headers=headers, json={"text": "hello"}).status_code == 401
    response = client.post("/notify", json={"text": "hello", "token": "notify-secret"})
    assert response.status_code == 401


def test_notify_requires_incoming_webhook_configuration():
    with TestClient(create_app(Settings(notify_token="notify-secret"))) as client:
        assert post_notify(client).status_code == 503


def test_notify_sends_unicode_as_urlencoded_payload_json(client, app, settings):
    captured = []

    def upstream(request):
        captured.append(request)
        return httpx.Response(200, json={"success": True})

    app.state.http_transport = httpx.MockTransport(upstream)
    text = "[ERP] 박범준 · 출하 완료 ✅\n상자 3개 & 확인=필요"
    response = post_notify(client, text)
    assert response.status_code == 200
    assert response.json() == {"ok": True}
    assert len(captured) == 1
    request = captured[0]
    assert request.method == "POST"
    assert str(request.url) == settings.incoming_webhook_url
    assert request.headers["content-type"].startswith("application/x-www-form-urlencoded")
    form = parse_qs(request.content.decode("utf-8"), strict_parsing=True)
    assert set(form) == {"payload"}
    assert json.loads(form["payload"][0]) == {"text": text}
    assert request.headers.get("X-Notify-Token") is None


@pytest.mark.parametrize("payload", [{"text": ""}, {"text": " "}, {"text": "x" * 4001}, {"text": 7}, {"text": "ok", "channel": "other"}])
def test_notify_rejects_invalid_text_or_extra_fields_without_upstream(client, payload):
    response = client.post("/notify", headers={"X-Notify-Token": "notify-secret"}, json=payload)
    assert response.status_code == 422


def test_notify_accepts_maximum_text_length(client, app):
    app.state.http_transport = httpx.MockTransport(lambda request: httpx.Response(200, json={"success": True}))
    assert post_notify(client, "한" * 4000).status_code == 200


@pytest.mark.parametrize(
    "response_kwargs",
    [
        {"status_code": 500, "json": {"success": True}},
        {"status_code": 200, "json": {"success": False, "error": {"code": 102}}},
        {"status_code": 200, "json": {"success": "true"}},
        {"status_code": 200, "text": "<html>NAS login page</html>"},
    ],
)
def test_notify_reports_upstream_rejection_without_leaking_secrets(client, app, settings, response_kwargs):
    app.state.http_transport = httpx.MockTransport(lambda request: httpx.Response(**response_kwargs))
    response = post_notify(client)
    assert response.status_code == 502
    assert_no_secrets(response, settings)


def test_notify_does_not_follow_redirects(client, app):
    requests = []

    def upstream(request):
        requests.append(request)
        return httpx.Response(302, headers={"Location": "https://elsewhere.example.test/collect"})

    app.state.http_transport = httpx.MockTransport(upstream)
    assert post_notify(client).status_code == 502
    assert len(requests) == 1


def test_notify_timeout_does_not_expose_webhook_url(client, app, settings):
    def upstream(request):
        raise httpx.ReadTimeout(f"Timeout sending to {request.url}", request=request)

    app.state.http_transport = httpx.MockTransport(upstream)
    response = post_notify(client)
    assert response.status_code == 502
    assert_no_secrets(response, settings)


@pytest.mark.parametrize(
    "kwargs",
    [
        {"chat_url": "javascript:alert(1)"},
        {"chat_url": "https://chat.example.test/?token=public-secret"},
        {"chat_url": "https://chat.example.test/?launchApp=SYNO.SDS.Chat.Application&token=public-secret"},
        {"chat_url": "https://chat.example.test/?launchApp=SYNO.SDS.Chat.Application&launchApp=SYNO.SDS.Chat.Application"},
        {"incoming_webhook_url": "https://admin:password@nas.example.test/hook"},
        {"incoming_webhook_url": "file:///etc/passwd"},
    ],
)
def test_settings_reject_unsafe_or_secret_bearing_public_urls(kwargs):
    with pytest.raises(ValueError):
        Settings(**kwargs)


def test_settings_allow_official_dsm_chat_launcher_url():
    url = "https://nas.example.test:5001/?launchApp=SYNO.SDS.Chat.Application"
    configured = Settings(chat_url=url)
    assert configured.chat_url == url
    with TestClient(create_app(configured)) as client:
        assert client.get("/api/status").json()["chat_url"] == url
