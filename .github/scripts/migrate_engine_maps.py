#!/usr/bin/env python3
"""RobustToolbox 294 compatibility: remove the discontinued IMapManager interface.

Only affects Content.Client and Content.Server files. It changes the *type* of
map manager references, not map behavior or gameplay values. A subsequent
compiler pass identifies former IMapManager methods that changed signatures.

Idempotent. Review the diff and regression test ships / maps / prediction.
"""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
PATTERN = re.compile(r"\bIMapManager\b")

def main():
    paths = []
    replacements = 0
    for area in ("Content.Client", "Content.Server"):
        for path in (ROOT / area).rglob("*.cs"):
            old = path.read_text(encoding="utf-8-sig")
            new, count = PATTERN.subn("SharedMapSystem", old)
            if count == 0:
                continue
            path.write_text(new, encoding="utf-8")
            paths.append(str(path.relative_to(ROOT)))
            replacements += count
    print(f"Map API type migrated: {replacements} references in {len(paths)} files")
    for path in paths:
        print("CHANGED", path)
    if not replacements:
        print("No legacy IMapManager references in client/server.")

if __name__ == "__main__":
    main()
