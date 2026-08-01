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

SMALL_CAPACITY = 3
SMALL_LENGTH = 2
DECKER_CAPACITY = 6
DECKER_LENGTH = 3


class GenerationFailure(Exception):
    pass


def _build_schedule(rng: random.Random, p: GenParams):
    """Return (dispatch, queue).

    `dispatch` is a list of (colour, capacity) in the order the buses leave the
    lot; `queue` is a list of (colour, luggage) in boarding order.

    Keeps at most one docked bus per colour so the "leftmost matching bay" rule
    is never ambiguous in the intended solution.
    """
    palette = PALETTE[: p.colors]
    dispatch: list[tuple[str, int]] = []
    queue: list[tuple[str, bool]] = []
    docked: list[list] = []  # [colour, remaining]
    left = p.buses
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
        can_dispatch = left > 0 and len(docked) < p.bays and bool(available)
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
            capacity = (
                DECKER_CAPACITY if rng.random() < p.decker_chance else SMALL_CAPACITY
            )
            dispatch.append((choice, capacity))
            docked.append([choice, capacity])
            left -= 1
        elif can_board:
            d = rng.choice(docked)
            # A luggage passenger takes two seats, so it only fits while the bus
            # still has room for two. That is the whole point of the mechanic:
            # a bus with one seat left can no longer take one.
            luggage = d[1] >= 2 and rng.random() < p.luggage_chance
            seats = 2 if luggage else 1
            queue.append((d[0], luggage))
            d[1] -= seats
            if d[1] == 0:
                docked.remove(d)
        else:  # pragma: no cover - defensive
            raise GenerationFailure("schedule deadlocked")

    if left != 0 or docked:  # pragma: no cover - defensive
        raise GenerationFailure("schedule incomplete")
    return dispatch, queue


def _placements(width: int, height: int, length: int):
    """All (cells, facing) options for a straight bus of the given length."""
    out = []
    for facing, (dx, dy) in DIRECTIONS.items():
        for x in range(width):
            for y in range(height):
                if dx != 0:
                    cells = tuple((x + i, y) for i in range(length))
                else:
                    cells = tuple((x, y + i) for i in range(length))
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
    dispatch: list[tuple[str, int]],
    blocked: frozenset[tuple[int, int]],
) -> list[Bus]:
    """Place buses in reverse dispatch order, each with a clear path at the
    moment it will be dispatched."""
    options = {
        SMALL_LENGTH: _placements(p.width, p.height, SMALL_LENGTH),
        DECKER_LENGTH: _placements(p.width, p.height, DECKER_LENGTH),
    }
    occupied: set[tuple[int, int]] = set(blocked)
    placed: dict[int, tuple[tuple, str]] = {}

    for i in range(len(dispatch) - 1, -1, -1):
        _color, capacity = dispatch[i]
        length = DECKER_LENGTH if capacity == DECKER_CAPACITY else SMALL_LENGTH
        candidates = []
        for cells, facing in options[length]:
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

    return placed, occupied


def _place_decoys(
    rng: random.Random,
    p: GenParams,
    dispatch: list[tuple[str, int]],
    placed: dict[int, tuple[tuple, str]],
    blocked: frozenset[tuple[int, int]],
) -> list[tuple[tuple, str, str, int]]:
    """Add surplus buses the intended solution never needs.

    This is what makes a mistake take time to show up. Without surplus, every
    bus eventually fills and every bay eventually frees, so the only way to
    lose is instant gridlock - which the player sees immediately. A surplus bus
    can be sent into a bay and sit there forever, costing a bay silently while
    play continues. That is the difference between a puzzle that punishes and a
    puzzle that is hard.

    They are placed off every intended bus's route, so the reference solution
    still works and the level stays solvable by construction.
    """
    if p.decoys == 0:
        return []

    forbidden = set(blocked)
    for i, (cells, facing) in placed.items():
        forbidden.update(cells)
        forbidden.update(_path_from(p.width, p.height, cells, facing))

    colors_in_play = sorted({c for c, _ in dispatch})
    decoys: list[tuple[tuple, str, str, int]] = []
    options = _placements(p.width, p.height, SMALL_LENGTH)

    for _ in range(p.decoys):
        candidates = []
        for cells, facing in options:
            if any(c in forbidden for c in cells):
                continue
            path = _path_from(p.width, p.height, cells, facing)
            # A decoy walled in by cones can never move, which makes it scenery
            # rather than a trap. It has to be dispatchable to be tempting.
            if any(c in blocked for c in path):
                continue
            # Prefer ones that look dispatchable right now.
            weight = 4 if not any(c in forbidden for c in path) else 1
            candidates.append((cells, facing, weight))
        if not candidates:
            break
        total = sum(c[2] for c in candidates)
        pick = rng.random() * total
        acc = 0.0
        for cells, facing, weight in candidates:
            acc += weight
            if acc >= pick:
                decoys.append((cells, facing, rng.choice(colors_in_play), SMALL_CAPACITY))
                forbidden.update(cells)
                break
    return decoys


def _assemble(
    rng: random.Random,
    dispatch: list[tuple[str, int]],
    placed: dict[int, tuple[tuple, str]],
    decoys: list[tuple[tuple, str, str, int]],
) -> list[Bus]:
    """Build the bus list with ids shuffled across intended buses AND decoys.

    Shuffling matters twice over: ids must not encode the dispatch order, and a
    decoy must not be identifiable as "the one with the high id".
    """
    entries = [
        (placed[i][0], placed[i][1], dispatch[i][0], dispatch[i][1])
        for i in range(len(dispatch))
    ]
    entries.extend(decoys)

    ids = list(range(1, len(entries) + 1))
    rng.shuffle(ids)
    buses = [
        Bus(id=ids[k], color=color, cells=cells, facing=facing, capacity=capacity)
        for k, (cells, facing, color, capacity) in enumerate(entries)
    ]
    return sorted(buses, key=lambda b: b.id)


def _blocked_cells(rng: random.Random, p: GenParams) -> frozenset[tuple[int, int]]:
    if p.blocked == 0:
        return frozenset()
    cells = [(x, y) for x in range(p.width) for y in range(p.height)]
    rng.shuffle(cells)
    return frozenset(cells[: p.blocked])


def generate_candidate(rng: random.Random, p: GenParams) -> Level:
    dispatch, queue = _build_schedule(rng, p)
    blocked = _blocked_cells(rng, p)
    placed, _occupied = _place_buses(rng, p, dispatch, blocked)
    decoys = _place_decoys(rng, p, dispatch, placed, blocked)
    buses = _assemble(rng, dispatch, placed, decoys)
    level = Level(
        id=p.level,
        width=p.width,
        height=p.height,
        bays=p.bays,
        buses=tuple(buses),
        queue=tuple(Passenger(color, luggage) for color, luggage in queue),
        blocked=blocked,
        chapter=p.chapter,
        move_limit=None,
    )
    problems = validate_level(level)
    if problems:
        raise GenerationFailure("; ".join(problems))
    return level


# A candidate that blows past this is one the on-device solver could not answer
# instantly either, so the cap is a playability gate rather than just a
# generation budget: it keeps the free-rewind check imperceptible.
STATE_CAP = 60_000


def generate_level(
    n: int,
    seed: int,
    attempts: Optional[int] = None,
    tolerance: float = 2.5,
) -> tuple[Level, Analysis]:
    """Generate candidates and keep the one closest to the target curve."""
    rng = random.Random(seed * 7919 + n)
    p = params_for(n)
    target = target_mds(n)

    if attempts is None:
        # Late boards cost orders of magnitude more to analyse, and they sit near
        # the ceiling anyway, so the first viable candidates are already close.
        attempts = 260 if n < 90 else (90 if n < 150 else 45)

    best: Optional[tuple[float, Level, Analysis]] = None
    for _ in range(attempts):
        try:
            level = generate_candidate(rng, p)
            analysis = analyse(level, state_cap=STATE_CAP)
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
