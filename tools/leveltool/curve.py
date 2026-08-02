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
# Floor of the curve, and it is a measured value, not a taste one. The opening
# used to target MDS 4, which a 3-bus board on a 5x5 lot hits exactly - and
# which playtest called empty and too easy. A board full enough to look like the
# rest of the game measures ~10 even when it is still thoroughly forgiving
# (solution density ~0.85). Aiming below what the smallest shipped board can
# produce just means the generator misses the target on every early level.
FLOOR = 10.0
PALETTE = ["red", "blue", "green", "yellow", "purple", "orange", "pink", "teal"]


def target_mds(n: int) -> float:
    """Target Measured Difficulty Score for level `n` (1-based).

    The exponent is above 1 on purpose: the curve must stay almost flat through
    the tutorial and rise steadily afterwards. An exponent below 1 (the first
    draft used 0.80) reaches MDS 13 by level 10, which contradicts an opening
    chapter whose job is to be forgiving.

    Note that "forgiving" is carried by SOLUTION DENSITY, not by this number.
    The opening now targets ~10 rather than ~4 because the boards are full, and
    a full board scores higher mostly through state count - which no player ever
    searches. The guarantee that matters, and the one under test, is that
    levels 1-8 keep a density of 0.75 or better: three moves in four still win.
    """
    # The span stops at what this ruleset can actually deliver, not at a round
    # 100. Measured ceiling with the current mechanic set is ~MDS 78; aiming the
    # curve past it just makes every level below it read as too easy, because
    # the fit stretches to chase a top it can never reach. Raise CEILING when a
    # new deception-class mechanic lands - see docs/PROTOTYPE_FINDINGS.md.
    base = FLOOR + (CEILING - FLOOR) * (n / TOTAL_LEVELS) ** 1.15

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
    # Every bus here is a bus the solution needs; there is no surplus any more,
    # so this is the whole vehicle count on the board. The top of the ramp is
    # set by what a 9x10 lot can physically hold: each bus needs a clear exit
    # path at the moment it is placed, and past ~22 the placer starts failing
    # more often than it succeeds.
    #
    # The BOTTOM of the ramp used to be 3 buses on a 5x5 board, which measured
    # as a perfect tutorial and played as an empty car park - the complaint from
    # playtest was that the opening looks and feels like nothing is happening.
    # It now starts at 9. Filling the board turns out to cost far less
    # forgiveness than it looks: at nine buses in two colours the solution
    # density is still ~0.85, so four moves in five keep the level winnable.
    # What rises is mostly the state count, and a player never searches that.
    buses = 9 + round(13 * t)
    colors = min(len(PALETTE), 2 + n // 28)

    # The lot widens as the game goes on. Cells are drawn smaller so a 9x10
    # board still fits a phone screen. The floor is 7x7: a fuller opening needs
    # somewhere to put the buses, and a 5x5 lot cannot hold nine of them with a
    # clear exit path each.
    width = min(9, 7 + (n > 70) + (n > 140))
    height = min(10, 7 + (n > 45) + (n > 105) + (n > 160))

    # Open stands. The terminal always shows six numbered stands; this is how
    # many are in service. Counter-intuitively, MORE open stands is more
    # forgiving, not less - a bigger buffer absorbs more mistakes (findings §5,
    # §15) - so this ramps up only alongside much bigger boards, and stops at
    # five: at six the reachable state space blows past the point where the
    # on-device solver can answer "is this still winnable?" instantly, and that
    # answer is what the free rewind depends on.
    # Four open stands is the ceiling, and it is a measured one. At five the
    # reachable state space runs past 100k, and the on-device solver answers
    # "is this still winnable?" after EVERY move - at that size the free rewind
    # visibly stalls, and that rewind is the whole anti-frustration promise.
    # Difficulty comes from board size, bus count, colours and seat capacity
    # instead, all of which are cheap to search - capacity especially, since
    # boarding is deterministic and so never branches (findings §19).
    bays = 4 if n >= 50 else 3

    blocked = 0 if n < 21 else min(6, 1 + (n - 21) // 26)

    # Mechanics from the rollout in CONCEPT.md §4. Each is introduced gently and
    # ramps up, so the level where a player first meets it is never also the
    # level where it is hardest.
    luggage = _ramp(n, 36, 120, 0.15, 0.40)   # passengers taking two seats
    decker = _ramp(n, 96, 180, 0.12, 0.35)    # capacity-12 buses, 3 cells long

    # A breather level has a much lower target, and past level ~100 the normal
    # parameter set simply cannot build a board that easy - the easiest of 300
    # candidates still came out 14 points too hard. So a breather gets a
    # genuinely smaller board, not just a smaller target.
    #
    # Cutting COLOURS instead was tried, to keep the board full: it does not
    # work. At level 190, dropping 8 colours to 5 moved the score by 1.8 points
    # (50.9 -> 49.1) while the state count went UP, because more docked buses
    # match the head and more orderings stay viable. At 3 colours the schedule
    # cannot even be built - only one bus per colour may be docked at a time.
    # So the bus count it is, but gently: 0.62 left the board looking deserted.
    if n % 10 == 0 and n % 25 != 0:
        buses = max(6, round(buses * 0.78))
        blocked = max(0, blocked - 2)

    chapter = min(8, (n - 1) // 25 + 1)
    return GenParams(n, width, height, bays, buses, colors, blocked, chapter,
                     luggage_chance=luggage, decker_chance=decker)
