"""Sunny Stop — build the standalone browser preview.

Packs the rules engine, the UI, all 200 levels and all 240 postcards into one
self-contained HTML file with no external requests. The point is to let someone
play the game and judge whether it is fun before the Unity project is set up -
the one question no amount of measurement answers.

    python3 tools/webpreview/build.py [output.html]

Then validate the packed data actually plays:

    node tools/webpreview/validate.js
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
LEVEL_DIR = ROOT / "Assets" / "Resources" / "Levels"
MESSAGE_FILE = ROOT / "Assets" / "Resources" / "Messages" / "messages.de.json"


def compact_level(path: Path) -> dict:
    """Shrink a level to what the player needs. Keys are short on purpose:
    200 levels of verbose JSON would triple the page weight."""
    d = json.loads(path.read_text(encoding="utf-8"))
    return {
        "id": d["id"],
        "ch": d["chapter"],
        "w": d["grid"]["w"],
        "h": d["grid"]["h"],
        "bays": d["bays"],
        "bl": d.get("blocked", []),
        "bs": [
            [b["id"], b["color"], b["facing"], b.get("capacity", 3),
             [v for cell in b["cells"] for v in cell]]
            for b in d["buses"]
        ],
        "q": [[p["color"], 1 if p.get("luggage") else 0] for p in d["queue"]],
        "sol": d["solution"],
        "mds": d.get("analysis", {}).get("mds", 0),
    }


def collect_levels() -> list[dict]:
    paths = sorted(LEVEL_DIR.glob("level_*.json"))
    if not paths:
        raise SystemExit(f"no levels in {LEVEL_DIR}")
    return [compact_level(p) for p in paths]


def collect_messages() -> list[list]:
    book = json.loads(MESSAGE_FILE.read_text(encoding="utf-8"))
    out = []
    for m in book["messages"]:
        row = [m["id"], m["category"], m["tone"], m["text"]]
        if "level" in m:
            row.append(m["level"])
        out.append(row)
    return out


def build(out_path: Path) -> None:
    levels = collect_levels()
    messages = collect_messages()

    engine = (HERE / "engine.js").read_text(encoding="utf-8")
    game = (HERE / "game.js").read_text(encoding="utf-8")
    styles = (HERE / "styles.css").read_text(encoding="utf-8")
    shell = (HERE / "shell.html").read_text(encoding="utf-8")

    data = json.dumps({"levels": levels, "messages": messages},
                      separators=(",", ":"), ensure_ascii=False)

    html = (shell
            .replace("/*STYLES*/", styles)
            .replace("/*DATA*/", f"window.SUNNY_DATA={data};")
            .replace("/*ENGINE*/", engine)
            .replace("/*GAME*/", game))

    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(html, encoding="utf-8")

    size = len(html.encode("utf-8")) / 1024
    print(f"{out_path}  {size:.0f} KB  "
          f"({len(levels)} levels, {len(messages)} postcards)")


if __name__ == "__main__":
    target = Path(sys.argv[1]) if len(sys.argv) > 1 else HERE / "sunny-stop.html"
    build(target)
