#!/usr/bin/env python3
"""Package a previously cross-published Windows build; this does NOT execute or certify it."""
import argparse
import hashlib
import os
from pathlib import Path
import re
import shutil
import subprocess
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[2]


def inside(path):
    path = Path(path).absolute()
    if path.is_symlink():
        raise ValueError(f"Symlink is not allowed: {path}")
    path = path.resolve()
    if path == ROOT or not path.is_relative_to(ROOT):
        raise ValueError(f"Path must be inside repository: {path}")
    return path


def sha(path):
    with path.open("rb") as source:
        return hashlib.file_digest(source, "sha256").hexdigest()


def nsis_string(text):
    if any(c in text for c in '\r\n'):
        raise ValueError("Newline in payload path")
    return text.replace("$", "$$").replace('"', '$\\"')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--publish-dir", required=True)
    parser.add_argument("--makensis", required=True)
    parser.add_argument("--nsis-dir", required=True)
    args = parser.parse_args()
    published = inside(args.publish_dir)
    if not (published / "TempChain.Chat.exe").is_file():
        raise ValueError("Missing Windows executable")
    version = ET.parse(ROOT / "desktop/TempChain.Chat/TempChain.Chat.csproj").findtext("PropertyGroup/Version")
    if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", version or ""):
        raise ValueError("Version must have three numeric components")
    dist = inside(ROOT / "dist")
    payload = inside(dist / f"TChat-{version}-win-x64")
    archive = inside(dist / f"TChat-{version}-win-x64.zip")
    installer = inside(dist / f"TChat-Setup-{version}.exe")
    if any(path.exists() for path in (payload, archive, installer)):
        raise ValueError("Version output exists; move it aside explicitly before rebuilding")
    sources = []
    for source in sorted(published.rglob("*")):
        if source.is_symlink():
            raise ValueError(f"Symlink in payload: {source}")
        if not source.is_file():
            continue
        relative = source.relative_to(published)
        lower = source.name.lower()
        if (lower.startswith(".env") or lower == "settings.json" or lower.startswith("login.dat") or "webview2" in lower
                or source.suffix.lower() in (".pfx", ".p12", ".pdb")
                or any(part.lower() == "user data" for part in relative.parts)):
            raise ValueError(f"Forbidden profile, secret or debug payload: {relative}")
        sources.append((source, relative))
    dist.mkdir(exist_ok=True)
    payload.mkdir()
    for source, relative in sources:
        dest = payload / relative
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, dest)
        if sha(source) != sha(dest):
            raise ValueError(f"Copy verification failed: {relative}")
    stage = inside(ROOT / "artifacts/preview-packaging")
    stage.mkdir(parents=True, exist_ok=True)
    include = stage / "uninstall-payload.nsh"
    lines = ["; Exact application files only. Never remove employee settings."]
    for _, relative in sources:
        target = "$INSTDIR\\" + nsis_string(str(relative).replace("/", "\\"))
        lines += [f'Delete "{target}"', f'IfFileExists "{target}" 0 +2', '  StrCpy $PayloadDeleteFailed "1"']
    for directory in sorted((p for p in payload.rglob("*") if p.is_dir()), key=lambda p:len(p.parts), reverse=True):
        relative = str(directory.relative_to(payload)).replace("/", "\\")
        lines.append('RMDir "$INSTDIR\\' + nsis_string(relative) + '"')
    include.write_text("\n".join(lines) + "\n", encoding="utf-8-sig")
    env = dict(os.environ, NSISDIR=str(Path(args.nsis_dir).resolve()))
    command = [str(Path(args.makensis).resolve()), "-V3", "-INPUTCHARSET", "UTF8",
               f"-DAPP_VERSION={version}", f"-DPUBLISH_DIR={payload}", f"-DOUTPUT_FILE={installer}",
               f"-DPAYLOAD_UNINSTALL_INCLUDE={include}", f"-DAPP_ICON={ROOT / 'desktop/TempChain.Chat/Assets/app.ico'}",
               f"-DESTIMATED_SIZE_KB={(sum(p.stat().st_size for p,_ in sources)+1023)//1024}",
               str(ROOT / "desktop/packaging/TempChainChat.nsi")]
    subprocess.run(command, env=env, check=True, cwd=ROOT)
    if installer.read_bytes()[:2] != b"MZ":
        raise ValueError("Installer is not a PE executable")
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as output:
        for _, relative in sources:
            output.write(payload / relative, f"{payload.name}/{relative.as_posix()}")
    with zipfile.ZipFile(archive) as output:
        if output.testzip() is not None:
            raise ValueError("ZIP verification failed")
        for _, relative in sources:
            if hashlib.sha256(output.read(f"{payload.name}/{relative.as_posix()}")).hexdigest() != sha(payload/relative):
                raise ValueError(f"Archive hash mismatch: {relative}")
    targets = [installer, archive] + [payload/relative for _,relative in sources]
    (dist / "SHA256SUMS.txt").write_text("".join(f"{sha(path)}  {path.relative_to(dist).as_posix()}\n" for path in targets), encoding="utf-8")
    print(f"VERIFIED: {len(sources)} payload copies and ZIP entries; installer and SHA256SUMS generated")
    print("NOT VERIFIED: Windows installation, WPF execution, shortcut COM actions and live two-account NAS operations")


if __name__ == "__main__":
    main()
