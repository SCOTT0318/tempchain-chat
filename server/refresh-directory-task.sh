#!/bin/sh
set -eu
umask 077
PYTHON=$(command -v python3 || true)
if [ -z "$PYTHON" ]; then echo 'TChat directory refresh: Python 3 unavailable'; exit 1; fi
"$PYTHON" - <<'TCHAT_DIRECTORY_PY'
"""Run as an existing DSM user; export ERP members and non-secret profile fields only."""
import grp
import json
import os
from pathlib import Path
import pwd
import re
import subprocess
import time
import uuid


def export():
    group = grp.getgrnam("erp")
    users = []
    lookup_status = {}
    for user in pwd.getpwall():
        if user.pw_uid == 0 or (user.pw_gid != group.gr_gid and user.pw_name not in group.gr_mem):
            continue
        email = ""
        try:
            result = subprocess.run(["/usr/syno/sbin/synouser", "--get", user.pw_name],
                                    capture_output=True, text=True, timeout=2, check=True)
            owner = re.search(r"^\s*User Name\s*:\s*\[([^\]\r\n]*)\]", result.stdout, re.M)
            field = re.search(r"^\s*User Mail\s*:\s*\[([^\]\r\n]*)\]", result.stdout, re.M)
            status = "matched" if owner and field else "format:" + ",".join(re.findall(r"^\s*([A-Za-z][A-Za-z ]+)\s*:", result.stdout, re.M))
            lookup_status[status] = lookup_status.get(status, 0) + 1
            if owner and owner.group(1) == user.pw_name and field:
                candidate = field.group(1)
                if len(candidate) <= 254 and (not candidate or re.fullmatch(r"[^\s@]+@[^\s@]+\.[^\s@]+", candidate)):
                    email = candidate
        except (OSError, subprocess.SubprocessError) as error:
            status = type(error).__name__
            lookup_status[status] = lookup_status.get(status, 0) + 1
        users.append({"account": user.pw_name, "uid": user.pw_uid, "description": user.pw_gecos,
                      "email": email, "groups": ["erp"]})
    root = Path("/volume1/docker/tchat-access/data")
    if root.resolve() != root or not root.is_dir():
        raise RuntimeError("invalid_directory")
    temporary = root / (".identity-" + uuid.uuid4().hex + ".tmp")
    try:
        with temporary.open("x", encoding="utf-8") as output:
            json.dump({"version": 1, "generatedAt": int(time.time() * 1000), "users": users}, output, ensure_ascii=False)
        temporary.chmod(0o644)
        os.replace(temporary, root / "identity.json")
        (root / "email-status.txt").write_text(json.dumps(lookup_status), encoding="utf-8")
        print("TChat ERP directory refreshed: " + str(len(users)))
    finally:
        if temporary.exists():
            temporary.unlink()


if __name__ == "__main__":
    try:
        export()
    except Exception:
        print("TChat ERP directory refresh failed; access will stop when the previous snapshot expires.")
        raise SystemExit(1)
TCHAT_DIRECTORY_PY
