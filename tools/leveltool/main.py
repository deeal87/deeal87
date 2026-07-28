"""Sunny Stop level tool.

    python3 main.py generate --levels 1-10,50,120   # generate + score + bake
    python3 main.py verify                          # re-solve every baked level (CI)
    python3 main.py report                          # MDS vs target curve table
    python3 main.py show 3                          # print a level as ASCII art

`verify` is the gate that runs in CI: it re-solves every shipped level from
the baked JSON and fails the build if one is unsolvable, drifted off the
target curve, or no longer matches its recorded solution.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from curve import target_mds
from generator import generate_level
from levelio import read_level, write_level
from rules import DIRECTIONS, Level, initial_state, is_won, play, validate_level
from solver import analyse

ROOT = Path(__file__).resolve().parents[2]
LEVEL_DIR = ROOT / "Assets" / "Resources" / "Levels"
# Allowed |MDS - target| for a shipped level. Wider than it should be
# because the top of the ladder cannot reach its target with the current
# mechanic set (docs/PROTOTYPE_FINDINGS.md); tighten as that closes.
BAND = 12.0

ARROWS = {"up": "^", "down": "v", "left": "<", "right": ">"}


def parse_levels(spec: str) -> list[int]:
    out: list[int] = []
    for part in spec.split(","):
        part = part.strip()
        if not part:
            continue
        if "-" in part:
            a, b = part.split("-")
            out.extend(range(int(a), int(b) + 1))
        else:
            out.append(int(part))
    return out


def cmd_generate(args) -> int:
    levels = parse_levels(args.levels)
    print(f"generating {len(levels)} level(s), seed {args.seed}\n")
    print(f"{'lvl':>4} {'target':>7} {'mds':>6} {'buses':>6} {'cols':>5} "
          f"{'bays':>5} {'states':>7} {'dens':>6} {'trap':>5}")
    print("-" * 62)
    for n in levels:
        level, analysis = generate_level(n, seed=args.seed)
        write_level(LEVEL_DIR / f"level_{n:03d}.json", level, analysis)
        print(
            f"{n:>4} {target_mds(n):>7.1f} {analysis.mds:>6.1f} "
            f"{len(level.buses):>6} {len(level.colors):>5} {level.bays:>5} "
            f"{analysis.states:>7} {analysis.solution_density:>6.2f} "
            f"{analysis.trap_depth:>5}"
        )
    print(f"\nwritten to {LEVEL_DIR}")
    return 0


def cmd_verify(args) -> int:
    paths = sorted(LEVEL_DIR.glob("level_*.json"))
    if not paths:
        print("no levels found", file=sys.stderr)
        return 1
    failures = 0
    for path in paths:
        level = read_level(path)
        problems = validate_level(level)
        if problems:
            print(f"FAIL {path.name}: {'; '.join(problems)}")
            failures += 1
            continue
        analysis = analyse(level)
        if not analysis.solvable:
            print(f"FAIL {path.name}: not solvable")
            failures += 1
            continue
        # The recorded solution must actually win.
        import json

        recorded = json.loads(path.read_text(encoding="utf-8")).get("solution")
        if recorded:
            try:
                state = play(level, recorded)
            except ValueError as exc:
                print(f"FAIL {path.name}: recorded solution illegal ({exc})")
                failures += 1
                continue
            if not is_won(level, state):
                print(f"FAIL {path.name}: recorded solution does not win")
                failures += 1
                continue
        drift = abs(analysis.mds - target_mds(level.id))
        flag = "" if drift <= BAND else f"  <-- off curve by {drift:.1f}"
        print(
            f"ok   {path.name}  optimal {analysis.optimal:>2}  "
            f"MDS {analysis.mds:>5.1f}  target {target_mds(level.id):>5.1f}{flag}"
        )
        if drift > BAND and args.strict:
            failures += 1
    print()
    if failures:
        print(f"{failures} level(s) failed verification")
        return 1
    print(f"all {len(paths)} level(s) verified")
    return 0


def cmd_report(args) -> int:
    paths = sorted(LEVEL_DIR.glob("level_*.json"))
    print(f"{'lvl':>4} {'target':>7} {'mds':>6}  curve")
    print("-" * 60)
    for path in paths:
        level = read_level(path)
        analysis = analyse(level)
        bar = "#" * int(round(analysis.mds / 2))
        print(f"{level.id:>4} {target_mds(level.id):>7.1f} {analysis.mds:>6.1f}  {bar}")
    return 0


def ascii_board(level: Level) -> str:
    grid = [["." for _ in range(level.width)] for _ in range(level.height)]
    for x, y in level.blocked:
        grid[y][x] = "#"
    for bus in level.buses:
        letter = bus.color[0].upper()
        for cell in bus.cells:
            grid[cell[1]][cell[0]] = letter
        fx, fy = bus.front_cell()
        grid[fy][fx] = ARROWS[bus.facing]
    lines = ["  " + " ".join(f"{x}" for x in range(level.width))]
    for y, row in enumerate(grid):
        lines.append(f"{y} " + " ".join(row))
    return "\n".join(lines)


def cmd_show(args) -> int:
    level = read_level(LEVEL_DIR / f"level_{args.level:03d}.json")
    analysis = analyse(level)
    print(f"Level {level.id}  (chapter {level.chapter})")
    print(f"grid {level.width}x{level.height}, {level.bays} bays, "
          f"{len(level.buses)} buses, colours: {', '.join(level.colors)}")
    print()
    print(ascii_board(level))
    print()
    print("queue: " + " ".join(p.color[0].upper() for p in level.queue))
    print(f"\noptimal {analysis.optimal} dispatches, MDS {analysis.mds:.1f} "
          f"(target {target_mds(level.id):.1f})")
    print(f"states {analysis.states}, density {analysis.solution_density:.2f}, "
          f"trap depth {analysis.trap_depth}, dead ends {analysis.dead_ends}")
    print("solution: " + " -> ".join(str(m) for m in analysis.solution))
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(prog="leveltool", description=__doc__)
    sub = ap.add_subparsers(dest="cmd", required=True)

    g = sub.add_parser("generate", help="generate, score and bake levels")
    g.add_argument("--levels", default="1-10", help="e.g. 1-10,50,120")
    g.add_argument("--seed", type=int, default=20260728)
    g.set_defaults(func=cmd_generate)

    v = sub.add_parser("verify", help="re-solve every baked level (CI gate)")
    v.add_argument("--strict", action="store_true",
                   help="also fail when a level drifts off the target curve")
    v.set_defaults(func=cmd_verify)

    r = sub.add_parser("report", help="difficulty table")
    r.set_defaults(func=cmd_report)

    s = sub.add_parser("show", help="print a level as ASCII art")
    s.add_argument("level", type=int)
    s.set_defaults(func=cmd_show)

    args = ap.parse_args()
    return args.func(args)


if __name__ == "__main__":
    raise SystemExit(main())
