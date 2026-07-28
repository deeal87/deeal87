# Prototype findings (M0)

What building the level pipeline actually taught us, as opposed to what the
concept assumed. These are the things that should change the plan.

---

## 1. The optimal move count is always the bus count

Seats exactly match passengers, so a win necessarily empties the lot: every bus
is dispatched exactly once. `L*` therefore equals the number of buses and
carries no information about how hard a level is.

**Consequences**

- Move limits are meaningless in this design. Levels ship with `moveLimit: null`
  and the game shows no move counter. That also removes any pressure mechanic
  from the board, which suits the "cosy, pause-friendly" positioning.
- All the difficulty lives in the *order*, which is why the scoring formula
  leans on solution density and trap depth rather than path length.

## 2. The difficulty ceiling of the prototype mechanic set is about half the curve

With the three mechanics implemented so far (colour count, bay count, cones),
the raw difficulty signal saturates around 1.4 and stops responding to bigger
boards. Sampling 24 generated candidates at 13 points on the ladder:

| Level | raw p90 | MDS after calibration | target |
|---:|---:|---:|---:|
| 1 | 0.28 | 3.9 | 8.3 |
| 35 | 1.08 | 23.6 | 28.3 |
| 90 | 1.36 | 38.3 | 40.0 |
| 130 | 1.28 | 33.7 | 50.7 |
| 200 | 1.48 | 45.3 | 87.9 |

The fit is good up to level ~90 (rmse 4.4 MDS points) and then flattens: level
200 measures 45 against a target of 88.

**Why.** From level 90 on, the only knobs still turning are bus count and colour
count, and both stop biting once the board is congested. Adding a fourth bus to
a board that is already jammed changes the search space but not the number of
ways to ruin it.

**Consequence.** The mechanic rollout in CONCEPT.md §4 is not decoration, it is
the difficulty engine. Each new mechanic adds an axis the generator can push on:
luggage breaks seat arithmetic, one-way lanes constrain routing, hidden colours
add risk, two bays cut the buffer, tunnels rewire the geometry. Without them the
back half of the game cannot reach its target difficulty at all.

**Action.** Implement mechanics in the order given, and re-run `calibrate.py`
after each one. Do not tune the sigmoid to force level 200 up to 88 — that would
hide the gap rather than close it.

## 3. Early levels are unlosable, and that is correct

Levels 1, 2, 4, 5, 7 and 10 have a solution density of 1.00: every legal move
keeps the level winnable. They cannot be failed.

This looked like a generator bug and is not. With three bays and two colours
there is no way to commit the buffer badly enough to matter. It gives the first
chapter exactly the shape onboarding wants — the player learns the tap, the
dock and the cascade without ever being punished — and the first real trap
appears at level 3 (density 0.85), which is where the game starts asking
something.

Worth keeping deliberately rather than generating it away.

## 4. Chain boarding is the best feel moment in the game

When a bus fills and departs, the next passenger becomes the head — and if a
matching bus is already docked, the queue keeps emptying on its own. A single
tap can clear six passengers and two buses.

This fell out of the rules rather than being designed, and it is the most
satisfying thing in the prototype. The audio design should be built around it:
the rising pentatonic boarding notes exist to make a long chain sound like a
finished melody.

## 5. Shipping the solver on device is cheap and changes the tone of the game

The reachable state space is small — 9 states at level 1, 1368 at level 120 —
so the game can answer "is this still winnable?" exactly, after every move, in
well under a millisecond.

That turns two concept promises into working features:

- **Hints work from anywhere.** A precomputed solution path is useless the
  moment the player deviates from it. Re-solving live means the hint is always
  valid from the actual position.
- **Losing is caught the instant it happens**, not two minutes later. The game
  offers a free rewind at the moment the mistake is made.

Production boards with more mechanics will grow the state space; the plan is to
keep the exhaustive check while it fits a budget (a few hundred thousand states)
and fall back to depth-limited search with the same interface beyond that.

## 6. Levels must not be verified in only one language

The level data is produced and proven correct by the Python tool, then played by
the C# engine. A rules mismatch between them would mean shipping a level that is
provably solvable and actually is not.

The guard is a C# test that replays every baked level's reference solution
move-for-move through the runtime engine and asserts it wins. It has to stay
green in CI alongside the Python content gate.

---

## Open risks not yet tested

- **Is it fun?** Unanswered. The prototype exists to answer it, and that needs
  hands on a phone, not more analysis.
- **Do the postcards land or do they read as cheesy?** The single biggest
  product risk. Test the 66 existing German cards with ~20 people before writing
  the remaining 174.
- **Does the measured score match perceived difficulty?** MDS is currently
  fitted to what the generator can produce, not to how hard players find it.
  It needs re-fitting against real attempt and quit data during the vertical
  slice.
