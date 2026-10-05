from __future__ import annotations

import json
import os
import re
from pathlib import Path

USER_ROOT = Path.home() / "Documents" / "Paradox Interactive" / "Hearts of Iron IV"
DLC_LOAD = USER_ROOT / "dlc_load.json"
MOD_DIR = USER_ROOT / "mod"


def parse_descriptor(path: Path) -> dict[str, str]:
    if not path.exists():
        return {}
    text = path.read_text(encoding="utf-8-sig", errors="replace")
    out: dict[str, str] = {}
    for key in ("name", "path", "remote_file_id", "supported_version", "version"):
        match = re.search(rf'(?m)^\s*{re.escape(key)}\s*=\s*"([^"]*)"', text)
        if match:
            out[key] = match.group(1)
    return out


def main() -> None:
    payload = json.loads(DLC_LOAD.read_text(encoding="utf-8"))
    rows = []
    for entry in payload.get("enabled_mods", []):
        rel = entry.replace("/", os.sep)
        mod_file = USER_ROOT / rel
        meta = parse_descriptor(mod_file)
        workshop_id = meta.get("remote_file_id")
        if not workshop_id:
            match = re.search(r"(\d+)\.mod$", entry)
            if match:
                workshop_id = match.group(1)
        content_path = Path(meta.get("path", "")) if meta.get("path") else None
        if content_path and not content_path.is_absolute():
            content_path = USER_ROOT / content_path
        descriptor = parse_descriptor(content_path / "descriptor.mod") if content_path and content_path.exists() else {}
        merged = {**descriptor, **meta}
        rows.append(
            {
                "entry": entry,
                "name": merged.get("name") or mod_file.stem,
                "workshop_id": workshop_id,
                "supported_version": merged.get("supported_version"),
                "path": str(content_path) if content_path else None,
                "mod_file": str(mod_file),
            }
        )
    print(json.dumps({"dlc_load": str(DLC_LOAD), "enabled_count": len(rows), "mods": rows}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
