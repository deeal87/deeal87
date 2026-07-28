"""Sunny Stop — level generator.

A level is never produced by filling a board at random and hoping. It is built
so that a solution exists by construction (CONCEPT.md §5.1):

1. Build a legal *schedule* first — an interleaving of "dispatch bus" and
   "board passenger" steps that respects the bay count and the head-of-queue
   rule. This yields the passenger queue and the order the buses come out in.
2. Place the buses into the lot in REVERSE dispatch order. When bus *i* is
   placed, the grid holds exactly the buses dispatched after it, which is
   precisely the situation bus *i* will face when its turn comes. Requiring a
   clear exit path at that moment makes the whole schedule replayable.

The result is guaranteed solvable. How *hard* it is, is then measured by the
solver, and only candidates that land near the target curve are kept.

Invariant worth knowing: every winning solution dispatches every bus exactly
once, so the optimal move count always equals the bus count. All the
difficulty lives in the ORDER, which is why the scoring leans on solution
density and trap depth rather than path length.
"""

from __future__ import annotations

import random
from typing import Optional

from curve import PALETTE, GenParams, params_for, target_mds
from rules import Bus, DIRECTIONS, Level, Passenger, validate_level
from solver import Analysis, analyse

CAPACITY = 3
BUS_LENGTH = 2


class GenerationFailure(Exception):
    pass


def _build_schedule(rng: random.Random, n_buses: int, bays: int, n_colors: int):
    """Return (dispatch_colors, queue_colors).

    Keeps at most one docked bus per colour so the "leftmost matching bay" rule
    is never ambiguous in the intended solution.
    """
    palette = PALETTE[:n_colors]
    dispatch: list[str] = []
    queue: list[str] = []
    docked: list[list] = []  # [color, remaining]
    left = n_buses
    # Guarantee every colour is used at least once, then go random.
    pending_colors = list(palette)
    rng.shuffle(pending_colors)

    guard = 0
    while left > 0 or docked:
        guard += 1
        if guard > 10_000:  # pragma: no cover - defensive
            raise GenerationFailure("schedule did not converge")

        docked_colors = {d[0] for d in docked}
        available = [c for c in palette if c not in docked_colors]
        can_dispatch = left > 0 and len(docked) < bays and bool(available)
        can_board = bool(docked)

        if can_dispatch and (not can_board or rng.random() < 0.55):
            if pending_colors:
                choice = next((c for c in pending_colors if c in available), None)
                if choice is not None:
                    pending_colors.remove(choice)
                else:
                    choice = rng.choice(available)
            else:
                choice = rng.choice(available)
            dispatch.append(choice)
            docked.append([choice, CAPACITY])
            left -= 1
        elif can_board:
            d = rng.choice(docked)
            queue.append(d[0])
            d[1] -= 1
            if d[1] == 0:
                docked.remove(d)
        else:  # pragma: no cover - defensive
            raise GenerationFailure("schedule deadlocked")

    if left != 0 or docked:  # pragma: no cover - defensive
        raise GenerationFailure("schedule incomplete")
    return dispatch, queue


def _placements(width: int, height: int):
    """All (cells, facing) options for a straight bus of BUS_LENGTH."""
    out = []
    for facing, (dx, dy) in DIRECTIONS.items():
        for x in range(width):
            for y in range(height):
                if dx != 0:
                    cells = tuple((x + i, y) for i in range(BUS_LENGTH))
                else:
                    cells = tuple((x, y + i) for i in range(BUS_LENGTH))
                if any(cx >= width or cy >= height for cx, cy in cells):
                    continue
                out.append((cells, facing))
    return out


def _path_from(width: int, height: int, cells, facing) -> list[tuple[int, int]]:
    dx, dy = DIRECTIONS[facing]
    fx, fy = max(cells, key=lambda c: c[0] * dx + c[1] * dy)
    path = []
    x, y = fx + dx, fy + dy
    while 0 <= x < width and 0 <= y < height:
        path.append((x, y))
        x, y = x + dx, y + dy
    return path


def _place_buses(
    rng: random.Random,
    p: GenParams,
    dispatch: list[str],
    blocked: frozenset[tuple[int, int]],
) -> list[Bus]:
    """Place buses in reverse dispatch order, each with a clear path at the
    moment it will be dispatched."""
    options = _placements(p.width, p.height)
    occupied: set[tuple[int, int]] = set(blocked)
    placed: dict[int, tuple[tuple, str]] = {}

    for i in range(len(dispatch) - 1, -1, -1):
        candidates = []
        for cells, facing in options:
            if any(c in occupied for c in cells):
                continue
            path = _path_from(p.width, p.height, cells, facing)
            if any(c in occupied for c in path):
                continue
            # Prefer deep spots: they create the congestion that makes the
            # extraction order matter.
            weight = 1 + len(path) * 2
            candidates.append((cells, facing, weight))
        if not candidates:
            raise GenerationFailure(f"no placement left for bus {i}")
        total = sum(c[2] for c in candidates)
        pick = rng.random() * total
        acc = 0.0
        for cells, facing, weight in candidates:
            acc += weight
            if acc >= pick:
                placed[i] = (cells, facing)
                occupied.update(cells)
                break

    # Shuffle the ids so they do not encode the dispatch order - otherwise the
    # baked JSON would hand out the solution to anyone who opens it.
    ids = list(range(1, len(dispatch) + 1))
    rng.shuffle(ids)
    buses = [
        Bus(id=ids[i], color=dispatch[i], cells=placed[i][0], facing=placed[i][1],
            capacity=CAPACITY)
        for i in range(len(dispatch))
    ]
    return sorted(buses, key=lambda b: b.id)


def _blocked_cells(rng: random.Random, p: GenParams) -> frozenset[tuple[int, int]]:
    if p.blocked == 0:
        return frozenset()
    cells = [(x, y) for x in range(p.width) for y in range(p.height)]
    rng.shuffle(cells)
    return frozenset(cells[: p.blocked])


def generate_candidate(rng: random.Random, p: GenParams) -> Level:
    dispatch, queue_colors = _build_schedule(rng, p.buses, p.bays, p.colors)
    blocked = _blocked_cells(rng, p)
    buses = _place_buses(rng, p, dispatch, blocked)
    level = Level(
        id=p.level,
        width=p.width,
        height=p.height,
        bays=p.bays,
        buses=tuple(buses),
        queue=tuple(Passenger(c) for c in queue_colors),
        blocked=blocked,
        chapter=p.chapter,
        move_limit=None,
    )
    problems = validate_level(level)
    if problems:
        raise GenerationFailure("; ".join(problems))
    return level


def generate_level(
    n: int,
    seed: int,
    attempts: int = 260,
    tolerance: float = 2.5,
) -> tuple[Level, Analysis]:
    """Generate candidates and keep the one closest to the target curve."""
    rng = random.Random(seed * 7919 + n)
    p = params_for(n)
    target = target_mds(n)

    best: Optional[tuple[float, Level, Analysis]] = None
    for _ in range(attempts):
        try:
            level = generate_candidate(rng, p)
            analysis = analyse(level)
        except (GenerationFailure, RuntimeError):
            continue
        if not analysis.solvable:  # pragma: no cover - impossible by construction
            continue
        err = abs(analysis.mds - target)
        if best is None or err < best[0]:
            best = (err, level, analysis)
        if err <= tolerance:
            break

    if best is None:
        raise GenerationFailure(f"level {n}: no viable candidate")
    return best[1], best[2]
