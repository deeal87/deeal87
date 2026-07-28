"""Sunny Stop — target difficulty curve and per-level generation parameters.

See CONCEPT.md §5.3. The curve is deliberately not monotonic: a breather every
tenth level and a spike every twenty-fifth is what stops 200 levels feeling
like a grind.
"""

from __future__ import annotations

import math
from dataclasses import dataclass

TOTAL_LEVELS = 200
PALETTE = ["red", "blue", "green", "yellow", "purple", "orange", "pink", "teal"]


def target_mds(n: int) -> float:
    """Target Measured Difficulty Score for level `n` (1-based)."""
    base = 5.0 + 85.0 * (n / TOTAL_LEVELS) ** 0.80
    if n % 10 == 0:
        base -= 12.0
    if n % 25 == 0:
        base += 8.0
    # Deterministic texture so the ladder never feels metronomic.
    base += 3.0 * math.sin(n * 2.399963)
    return max(4.0, min(92.0, base))


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


def params_for(n: int) -> GenParams:
    """Board size and mechanic budget for level `n`.

    Everything scales with a sub-linear curve so early levels stay tiny and
    late levels stay readable on a phone screen.
    """
    t = n / TOTAL_LEVELS
    buses = 3 + round(11 * t**0.70)
    colors = min(len(PALETTE), 2 + n // 25)

    # Grid grows just fast enough to keep the lot congested but placeable.
    width = min(7, 5 + (n > 30) + (n > 90))
    height = min(8, 5 + (n > 20) + (n > 60) + (n > 130))

    # Two bays instead of three is the single hardest knob in the game and is
    # held back until chapter 6 (CONCEPT.md §4).
    bays = 2 if n >= 126 else 3

    blocked = 0 if n < 21 else min(6, 1 + (n - 21) // 30)

    chapter = min(8, (n - 1) // 25 + 1)
    return GenParams(n, width, height, bays, buses, colors, blocked, chapter)
