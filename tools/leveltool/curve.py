"""Sunny Stop — target difficulty curve and per-level generation parameters.

See CONCEPT.md §5.3. The curve is deliberately not monotonic: a breather every
tenth level and a spike every twenty-fifth is what stops 200 levels feeling
like a grind.
"""

from __future__ import annotations

import math
from dataclasses import dataclass

TOTAL_LEVELS = 200
# Hardest the current ruleset can actually reach, measured across the whole
# generated ladder (docs/PROTOTYPE_FINDINGS.md). Set it above this and the last
# dozen levels all target a difficulty no board can hit, which shows up as
# permanent drift rather than as harder levels.
CEILING = 72.0
PALETTE = ["red", "blue", "green", "yellow", "purple", "orange", "pink", "teal"]


def target_mds(n: int) -> float:
    """Target Measured Difficulty Score for level `n` (1-based).

    The exponent is above 1 on purpose: the curve must stay almost flat through
    the tutorial and rise steadily afterwards. An exponent below 1 (the first
    draft used 0.80) reaches MDS 13 by level 10, which contradicts an opening
    chapter whose whole job is to be unfailable.
    """
    # The span stops at what this ruleset can actually deliver, not at a round
    # 100. Measured ceiling with the current mechanic set is ~MDS 78; aiming the
    # curve past it just makes every level below it read as too easy, because
    # the fit stretches to chase a top it can never reach. Raise CEILING when a
    # new deception-class mechanic lands - see docs/PROTOTYPE_FINDINGS.md.
    base = 4.0 + (CEILING - 4.0) * (n / TOTAL_LEVELS) ** 1.15

    # Sawtooth: a guaranteed breather right after a hard level, a spike on the
    # chapter boundary. Proportional rather than absolute - a flat -12 drops the
    # early levels straight through the floor and resets the ladder to trivial.
    if n % 25 == 0:
        base *= 1.18          # milestone spike
    elif n % 10 == 0:
        base *= 0.58          # breather - never on a milestone level
    # Deterministic texture so the ladder never feels metronomic.
    base *= 1.0 + 0.07 * math.sin(n * 2.399963)
    return max(3.0, min(CEILING, base))


@dataclass
class GenParams:
    level: int
    width: int
    height: int
    bays: int
    buses: int
    colors: int
    blocked: int
    chapter: int
    luggage_chance: float = 0.0
    decker_chance: float = 0.0
    decoys: int = 0


def _ramp(n: int, start: int, full: int, low: float, high: float) -> float:
    """0 before `start`, then eases from `low` to `high` by level `full`."""
    if n < start:
        return 0.0
    t = min(1.0, (n - start) / max(1, full - start))
    return low + (high - low) * t


def params_for(n: int) -> GenParams:
    """Board size and mechanic budget for level `n`.

    Everything scales with a sub-linear curve so early levels stay tiny and
    late levels stay readable on a phone screen.
    """
    t = n / TOTAL_LEVELS
    # Both ramps are deliberately slow early and steep late: the generator's
    # difficulty responds sharply to the first few extra buses and colours, so
    # a linear ramp overshoots the gentle opening of the target curve.
    buses = 3 + round(12 * t)
    colors = min(len(PALETTE), 2 + n // 30)

    # Grid grows just fast enough to keep the lot congested but placeable.
    width = min(7, 5 + (n > 30) + (n > 90))
    height = min(8, 5 + (n > 20) + (n > 60) + (n > 130))

    # Two bays instead of three is the single hardest knob in the game and is
    # held back until chapter 6 (CONCEPT.md §4).
    bays = 2 if n >= 126 else 3

    blocked = 0 if n < 21 else min(6, 1 + (n - 21) // 26)

    # Mechanics from the rollout in CONCEPT.md §4. Each is introduced gently and
    # ramps up, so the level where a player first meets it is never also the
    # level where it is hardest.
    luggage = _ramp(n, 36, 120, 0.15, 0.40)   # passengers taking two seats
    decker = _ramp(n, 96, 180, 0.12, 0.35)    # capacity-6 buses, 3 cells long
    # Surplus buses: the mechanic that lets a mistake stay hidden (see
    # docs/PROTOTYPE_FINDINGS.md). Introduced early because without it the game
    # can only punish instantly, never deceive.
    decoys = 0 if n < 22 else min(4, 1 + (n - 22) // 45)

    chapter = min(8, (n - 1) // 25 + 1)
    return GenParams(n, width, height, bays, buses, colors, blocked, chapter,
                     luggage_chance=luggage, decker_chance=decker, decoys=decoys)
