"""Sunny Stop — solver and difficulty measurement.

Prototype boards have a small reachable state space (bus positions are fixed,
so a state is just "which buses are still parked" + bay contents + queue
index). That lets us do an exhaustive BFS and measure difficulty properties
that a sampling solver could only estimate:

    L*  optimal solution length (dispatches)
    S   reachable states explored
    E   solution density: at a solvable state, what share of the legal moves
        keep the level solvable. Low E = unforgiving.
    T   trap depth: fewest moves from the start that reach a dead end.
    F   forced ratio: share of solvable states with exactly one legal move
        (high F means tedious rather than hard).
    D   deception depth: after a losing move, how many moves you can still play
        before you actually get stuck. A mistake that stops you immediately is
        obvious; one you only discover six moves later is what makes a puzzle
        hard rather than merely punishing. This is the metric that separates
        difficulty from board size - see docs/PROTOTYPE_FINDINGS.md.

The move graph is a DAG layered by depth: every dispatch permanently removes
one bus from the lot, so all paths to a given state have dispatched the same
number of buses. That makes longest-path exact and cheap.

Larger production boards switch to IDA* with the same metric definitions
estimated over a sampled sub-graph; the scoring formula does not change.
"""

from __future__ import annotations

import math
from collections import deque
from dataclasses import dataclass
from typing import Optional

from rules import Level, State, apply_move, initial_state, is_won, legal_moves

S_MAX = 200_000.0
L_MAX = 60.0
# Beyond ~3 hidden moves a mistake is already thoroughly deceptive;
# more does not make it meaningfully harder, only longer to undo.
DECEPTION_CAP = 3.0

# Logistic centring, fitted by calibrate.py across the whole ladder with the
# current mechanic set (colours, bays, cones, luggage, double-deckers, surplus
# buses); rmse 11.0 MDS points. The slope is deliberately NOT the least-squares
# optimum (0.165, rmse 8.7): that value collapses every tutorial board to MDS
# 0.0, leaving the index unable to tell level 1 from level 21. A usable index
# across the whole ladder is worth more than two points of fit. The residual is concentrated at the very top:
# level 200 tops out near 70 against a target of 88, because the ruleset still
# cannot make a mistake stay hidden often enough. See
# docs/PROTOTYPE_FINDINGS.md. Re-fit after adding mechanics, then freeze
# against playtest data.
SIGMOID_CENTER = 1.525
SIGMOID_SLOPE = 0.350


@dataclass
class Analysis:
    solvable: bool
    optimal: int
    states: int
    solution_density: float
    trap_depth: int
    forced_ratio: float
    deception: float
    deception_max: int
    mds: float
    solution: list[int]
    dead_ends: int
    raw: float = 0.0

    def as_dict(self) -> dict:
        return {
            "optimal": self.optimal,
            "states": self.states,
            "solutionDensity": round(self.solution_density, 4),
            "trapDepth": self.trap_depth,
            "forcedRatio": round(self.forced_ratio, 4),
            "deception": round(self.deception, 3),
            "deceptionMax": self.deception_max,
            "deadEnds": self.dead_ends,
            "raw": round(self.raw, 4),
            "mds": round(self.mds, 1),
        }


def _sigmoid(x: float) -> float:
    return 1.0 / (1.0 + math.exp(-(x - SIGMOID_CENTER) / SIGMOID_SLOPE))


def raw_score(
    states: int, optimal: int, density: float, trap: int, forced: float,
    deception: float = 0.0,
) -> float:
    """Unbounded difficulty signal. See CONCEPT.md §5.2."""
    return (
        # Board size, deliberately a minor term. A bigger state graph is more
        # to read, but a human never searches it - weighting it heavily made
        # the scorer prefer wide, forgiving boards over tight, punishing ones.
        0.35 * math.log(max(states, 2)) / math.log(S_MAX)
        + 0.55 * min(optimal, L_MAX) / L_MAX
        # How unforgiving the level is, and how long a mistake hides: the two
        # things a player actually experiences. These dominate on purpose.
        + 1.60 * (1.0 - density)
        + 0.80 * min(deception, DECEPTION_CAP) / DECEPTION_CAP
        + 0.45 * (1.0 - 1.0 / max(trap, 1))
        - 0.50 * forced
    )


def score(states: int, optimal: int, density: float, trap: int, forced: float,
          deception: float = 0.0) -> float:
    """Measured Difficulty Score, 0-100. See CONCEPT.md §5.2."""
    return 100.0 * _sigmoid(
        raw_score(states, optimal, density, trap, forced, deception))


def analyse(level: Level, state_cap: int = 400_000) -> Analysis:
    """Exhaustive analysis of the reachable state graph."""
    start = initial_state(level)

    # 1. Forward BFS over every reachable state.
    order: list[State] = []
    index: dict[tuple, int] = {}
    edges: list[list[int]] = []
    depth: list[int] = []

    index[start.key()] = 0
    order.append(start)
    edges.append([])
    depth.append(0)

    queue: deque[int] = deque([0])
    while queue:
        i = queue.popleft()
        st = order[i]
        if is_won(level, st):
            continue
        for bus_id in legal_moves(level, st):
            nxt = apply_move(level, st, bus_id)
            k = nxt.key()
            j = index.get(k)
            if j is None:
                if len(order) >= state_cap:
                    raise RuntimeError("state cap exceeded")
                j = len(order)
                index[k] = j
                order.append(nxt)
                edges.append([])
                depth.append(depth[i] + 1)
                queue.append(j)
            edges[i].append(j)

    # 2. Which states can still reach a win?
    solvable = [False] * len(order)
    goals = [i for i, st in enumerate(order) if is_won(level, st)]
    reverse: list[list[int]] = [[] for _ in order]
    for i, outs in enumerate(edges):
        for j in outs:
            reverse[j].append(i)
    stack = list(goals)
    for g in goals:
        solvable[g] = True
    while stack:
        j = stack.pop()
        for i in reverse[j]:
            if not solvable[i]:
                solvable[i] = True
                stack.append(i)

    if not solvable[0]:
        return Analysis(False, -1, len(order), 0.0, 0, 0.0, 0.0, 0, 100.0, [], 0, 0.0)

    # 3. Optimal solution (BFS depth to the nearest goal) and one witness path.
    optimal = min(depth[g] for g in goals)
    witness_goal = min(goals, key=lambda g: depth[g])
    path: list[int] = []
    cur = witness_goal
    while cur != 0:
        for i in reverse[cur]:
            if depth[i] == depth[cur] - 1:
                st = order[i]
                for bus_id in legal_moves(level, st):
                    if apply_move(level, st, bus_id).key() == order[cur].key():
                        path.append(bus_id)
                        break
                cur = i
                break
        else:  # pragma: no cover - depth graph is always connected backwards
            break
    path.reverse()

    # 4. Metrics over the solvable, non-terminal part of the graph.
    densities: list[float] = []
    forced = 0
    considered = 0
    dead_ends = 0
    for i, st in enumerate(order):
        if is_won(level, st):
            continue
        outs = edges[i]
        if not outs:
            dead_ends += 1
            continue
        if not solvable[i]:
            continue
        considered += 1
        good = sum(1 for j in outs if solvable[j])
        densities.append(good / len(outs))
        if len(outs) == 1:
            forced += 1

    density = sum(densities) / len(densities) if densities else 1.0
    forced_ratio = forced / considered if considered else 0.0

    # 5. Trap depth: shortest distance from the start to an unsolvable state.
    trap = _trap_depth(order, edges, solvable, depth)

    # 6. Deception: how long a losing move stays hidden.
    deception, deception_max = _deception(order, edges, solvable, depth)

    raw = raw_score(len(order), optimal, density, trap, forced_ratio, deception)
    mds = 100.0 * _sigmoid(raw)
    return Analysis(
        solvable=True,
        optimal=optimal,
        states=len(order),
        solution_density=density,
        trap_depth=trap,
        forced_ratio=forced_ratio,
        deception=deception,
        deception_max=deception_max,
        mds=mds,
        solution=path,
        dead_ends=dead_ends,
        raw=raw,
    )


def _deception(order, edges, solvable, depth) -> tuple[float, int]:
    """Mean and worst "moves still playable after a losing move".

    Edges always run from depth d to d+1 (one bus leaves the lot per move), so
    processing states in decreasing depth order gives exact longest paths in a
    single pass.
    """
    by_depth = sorted(range(len(order)), key=lambda i: depth[i], reverse=True)
    longest = [0] * len(order)
    for i in by_depth:
        best = 0
        for j in edges[i]:
            best = max(best, 1 + longest[j])
        longest[i] = best

    depths = [
        longest[j]
        for i, outs in enumerate(edges) if solvable[i]
        for j in outs if not solvable[j]
    ]
    if not depths:
        # Nothing can go wrong here at all - an unlosable level.
        return 0.0, 0
    return sum(depths) / len(depths), max(depths)


def _trap_depth(order, edges, solvable, depth) -> int:
    best: Optional[int] = None
    for i, outs in enumerate(edges):
        if not solvable[i]:
            continue
        for j in outs:
            if not solvable[j]:
                d = depth[i] + 1
                if best is None or d < best:
                    best = d
    # No reachable trap at all: the level cannot be ruined.
    return best if best is not None else max(depth) + 1


def verify(level: Level) -> tuple[bool, str]:
    """Cheap gate used by CI: solvable, and the move limit leaves headroom."""
    try:
        a = analyse(level)
    except RuntimeError as exc:
        return False, f"level {level.id}: {exc}"
    if not a.solvable:
        return False, f"level {level.id}: NOT SOLVABLE"
    if level.move_limit is not None and level.move_limit < a.optimal:
        return False, (
            f"level {level.id}: move limit {level.move_limit} below optimal {a.optimal}"
        )
    return True, f"level {level.id}: ok (optimal {a.optimal}, MDS {a.mds:.1f})"
