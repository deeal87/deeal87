# Prototype findings

What building the level pipeline actually taught us, as opposed to what the
concept assumed. Several of these overturn things written in CONCEPT.md; where
they do, the concept has been corrected and the finding is recorded here.

---

## 1. Every winning solution dispatches every *needed* bus exactly once

Seats match passengers, so a win necessarily empties the lot of the buses the
solution uses. The optimal move count therefore equals the number of needed
buses and carries no information about difficulty.

*(Since §19 removed surplus buses, "needed" is now every bus on the board, and
the optimal move count equals the bus count outright.)*

**Consequences**

- Move limits are meaningless in this design. Levels ship with `moveLimit: null`
  and the game shows no move counter. That also removes any pressure mechanic
  from the board, which suits the cosy, pause-friendly positioning.
- All the difficulty lives in the *order*, which is why scoring leans on
  solution density and deception depth rather than path length.

## 2. The first mechanic rollout did nothing — and that was the useful result

Luggage passengers (two seats) and double-deckers (capacity 6) were implemented
first, because the concept's rollout schedule put them first. Measured effect on
the difficulty signal, sampled 20 boards per point:

| Level | raw before | raw after | change |
|---:|---:|---:|---:|
| 90 | 1.361 | 1.325 | −0.036 |
| 110 | 1.283 | 1.406 | +0.123 |
| 150 | 1.274 | 1.285 | +0.011 |
| 200 | 1.475 | 1.433 | −0.042 |

Essentially nothing. Both mechanics change *how fast a bus fills*, and neither
changes *what happens when you choose wrong* — so neither adds difficulty.

## 3. The real ceiling was the ruleset: mistakes announced themselves instantly

Measuring **deception depth** — after a losing move, how many more moves can you
still play before you actually get stuck — explained why:

| Level | wrong moves available | max deception depth | mean |
|---:|---:|---:|---:|
| 3–9 | 6 | 0 | 0.0 |
| 50 | 155 | 2 | 0.1 |
| 120 | 361 | 7 | 0.3 |

Almost every mistake stopped the player *immediately*. The game was punishing,
not hard: you could jam the buffer, but you could never quietly waste anything,
because a bus only departs when full, so every bus always eventually fills and
every bay always eventually frees.

**A puzzle is hard when a mistake stays hidden.** That needs a resource you can
waste at a cost that shows up later.

## 4. Surplus buses were the fix — later reversed, see §19

Adding buses the solution does not need — parked off every intended route, so
solvability by construction is untouched — creates exactly that. Send one into a
bay and it may sit there forever, costing a bay silently while play continues.

Effect at level 120: solution density 0.55 → 0.42, state space 901 → 4710, and
deep mistakes appeared at all. One surplus bus moved the signal further (+0.24
raw) than a whole extra bus (+0.20) or an extra colour. Returns flatten past
about four, and placement starts failing on crowded late boards, so that is the
cap.

Across the shipped ladder, max deception depth now runs 1 → 2 → 5 → 9 → 10 → 11
→ 11 → 13 by chapter. Mistakes take longer and longer to reveal themselves,
which is the actual experience of a puzzle getting harder.

## 5. Fewer bays is *easier*, not harder — the concept had this backwards

CONCEPT.md called "only 2 bays" the hardest single knob in the game. Measured on
a level-200 board:

| Bays | raw | solution density | reachable states |
|---:|---:|---:|---:|
| 2 | 1.522 | 0.35 | 817 |
| 3 | 1.554 | 0.40 | 2 711 |
| 4 | 1.674 | 0.42 | 16 307 |
| 5 | 1.729 | 0.46 | 77 261 |

Two bays is genuinely less forgiving (density 0.35 vs 0.46) but the decision
space collapses — there is very little to think about, so it plays tight and
shallow rather than hard. More bays means more simultaneous commitments to get
wrong.

It stays in the design as a **change of feel** for the late chapters, not as a
difficulty lever, and the surrounding parameters compensate for it.

## 6. The scorer was measuring board size

The bay experiment exposed a flaw in the formula: state count carried weight
0.90 and dominated, so the scorer preferred wide forgiving boards over tight
punishing ones. A human never searches a 77 000-state graph; they look a few
moves ahead and get caught or don't.

Re-weighted: state count 0.90 → **0.35**, solution density 1.30 → **1.60**,
deception depth added at **0.80**. The two things a player actually experiences
now dominate.

## 7. The target curve's own shape was wrong

The first curve, `5 + 85·(n/200)^0.8`, has an exponent below 1, so it rises
fastest at the very beginning — it wanted MDS 13 by level 10, from an opening
chapter whose entire job is to be unfailable. Replaced with an exponent above 1
(`4 + 68·(n/200)^1.15`), which stays nearly flat through the tutorial and rises
steadily after.

Two further bugs in the same function:

- **Milestones were being discounted as breathers.** Levels 50, 100, 150 and 200
  are multiples of both 25 and 10, so the milestone spike and the breather
  discount both applied — the flagship every-25th levels were coming out
  *easier* than their neighbours. Breather now only applies when the level is
  not a milestone.
- **The sawtooth was absolute, not proportional.** A flat −12 pushed every early
  breather through the floor, resetting levels 10, 20 and 30 to trivial. Now
  multiplicative.

## 8. The calibration was fitting against the wrong levels

`calibrate.py` samples the *hardest* board a level's parameters can produce
(p90) and fits the scoring sigmoid against that level's target. Half its sample
points were multiples of 10 or 25 — breathers and milestones, whose targets are
deliberately modulated down or up. Fitting the hardest achievable board against
a deliberately discounted breather target dragged the whole curve off. The
sample set now avoids modulated levels; rmse fell from 12.5 to 8.7.

## 9. The best-fitting scale was the wrong scale

The least-squares optimum (slope 0.165, rmse 8.7) maps every tutorial board to
MDS 0.0 — the index could not tell level 1 from level 21, which makes it useless
for curating the opening chapter. Slope 0.350 (rmse 11.0) was chosen instead. A
usable index across the whole ladder is worth more than two points of fit.

## 10. The curve now stops where the game does

Even with surplus buses, the measured ceiling is about **MDS 72**. Aiming the
curve past it does not produce harder levels, only permanent drift on the last
dozen, so `CEILING` is set to the measured maximum.

*(Re-measured after §19 removed surplus and doubled capacity: the ceiling is
unchanged at 72 — level 200 now lands at 70.5 and the best candidate seen in
trials was 71.7 — so `CEILING` still stands.)*

Raising it requires another mechanic of the same *class* as surplus buses —
something that lets a player waste a resource at a delayed cost. Candidates, in
rough order of promise:

1. **Locked buses opened with tickets earned by boarding** (CONCEPT.md §4,
   levels 66–80). Spend a ticket on the wrong lock and the shortage appears much
   later. Same shape as surplus, different flavour.
2. **Buses that depart before they are full** on some trigger. Directly creates
   wasted seats, the purest form of delayed cost. Large rule change.
3. **Passengers who leave the queue if not served within N moves.** Removes the
   exact-supply invariant entirely.

Mechanics that will *not* help, and why: anything that only changes how fast a
bus fills (luggage, double-deckers — finding 2), and anything that is a static
placement constraint. One-way lanes and tunnels fall in the second group: buses
drive straight, so there is never an alternative route, and a one-way cell is
just a cone that only some buses can cross. Both are worth having for *variety*
and readability; neither will move difficulty. They were deferred for that
reason.

## 15. More open stands is more forgiving, not harder

Asked to make the terminal busier with six bus lines, the obvious reading is that six
stands is harder than three. Measured, it is the opposite:

| Open stands | raw | solution density | reachable states |
|---:|---:|---:|---:|
| 3 | 1.257 | 0.48 | 1 343 |
| 4 | 1.275 | 0.52 | 5 851 |
| 5 | 1.487 | 0.52 | 64 565 |
| 6 | 1.468 | 0.57 | 142 154 |

A stand is buffer. More buffer absorbs more mistakes: at level 60 with six stands the
density reaches 0.91 and the board becomes almost unlosable. This is the same lesson as
finding 5 seen from the other side.

So the terminal now shows **six numbered stands, with three or four in service** — the
look of a real bus station, while difficulty comes from board size, bus count, colours
and surplus, which do scale the right way.

## 16. The solver budget is a design constraint, not an implementation detail

Five open stands reach ~100k reachable states and six exceed 140k. That matters twice:

- **Generation** slowed to roughly three minutes per level, making a 200-level rebuild a
  multi-hour job.
- **The game itself.** The on-device solver answers "is this still winnable?" after
  *every* move, and that answer is what the free rewind is built on. At 130k states the
  check visibly stalls; a full re-solve on the largest shipped board (9×10, 4 stands,
  ~24 buses) measures **204 ms in a browser**, which is already at the edge of
  imperceptible.

So the generator carries a hard `STATE_CAP`, and it is a playability gate rather than a
performance budget: any board the generator cannot analyse quickly is a board the game
could not have rescued the player on. Four open stands is the ceiling that keeps it.

If five or six stands are ever wanted, the rewind check has to stop being an exhaustive
search — a bounded-depth check with a "probably still fine" answer would be fast enough,
but it would weaken a promise the whole design rests on. That is a real trade, not a
tuning knob.

## 17. Breather levels need a smaller board, not just a lower target

Past level ~100 the parameter set could no longer build a board easy enough for a
breather: level 180 targeted 34.7 and the easiest of hundreds of candidates still scored
49.0. Lowering the target does nothing if every candidate is above it. Breathers now get
a genuinely smaller board — about 60% of the bus count, fewer cones and fewer surplus
buses — and land on target (level 180 now scores 36.2).

## 18. The presentation had to be rebuilt around a reference, and one measured
thing came out of it

The player supplied a screenshot of the look they wanted and asked to get as
close to it as possible. Structurally it mapped onto the existing rules with
nothing to change: a packed tray of balls at the top is the passenger queue, a
row of painted markings in the middle is the stands, and a lot of arrow-marked
vehicles at the bottom is the lot. No rule, level or difficulty number moved —
only `styles.css`, the render functions in `game.js`, and `Palette.cs`.

The one finding worth keeping is about colour, and it is counter-intuitive.
After the first pass the tray looked pastel and the identical hues on the buses
looked saturated. Sampling the rendered pixels showed the balls were *exactly*
their base colour — e.g. pink measured (232, 92, 168) against a specified
`#E85CA8`. The wash was not the fill at all; it was the light tray showing
through the gaps that the scatter-jitter opened between the balls. Cutting the
jitter from ±16% of a ball diameter to ±6% and darkening the tray floor fixed it
without touching a single colour value.

Two consequences worth carrying forward:

- Trust the measurement over the impression, but measure the *composition*, not
  just the element. The element was right and the picture was still wrong.
- Simultaneous contrast is real and asymmetric: on the dark theme the same
  verified-correct hues read duller again, which is why the balls get a thin rim
  light in dark mode only. Do not "fix" that by changing the palette per theme —
  the palette is shared with Unity and must stay one table.

## 19. Surplus buses were the right mechanic and the wrong feature — capacity
replaced them

Finding §4 recorded surplus buses as the fix for "mistakes announce themselves
instantly". Playtest killed it, and the reason is worth keeping.

The report was: *buses sometimes stay standing even though all the balls are
gone.* That is exactly what surplus buses do, and it was working as designed —
but nothing on screen ever told the player those buses were never meant to
leave. A win condition the board contradicts reads as a bug, not as a trap. The
mechanic was invisible, and an invisible mechanic is indistinguishable from a
defect. Measured across the shipped ladder before the change: **177 of 200
levels** left at least one bus standing, up to 3 buses and 33 empty seats at
level 200.

The removal cost difficulty, so it needed a replacement lever. Three were tested
at level 200:

| Lever | Queue | Solver states | Notes |
|---|---:|---:|---|
| more buses (20 → 25) | — | — | placement fails 6/10; a 9×10 lot cannot hold them |
| more buses (20 → 30) | — | — | placement fails 10/10 |
| more seats (3/6 → 5/10) | 101 | 30.7k | works |
| more seats (3/6 → 7/14) | 141 | 26.8k | works, and *fewer* states |

**Capacity is nearly free and bus count is not.** Board area is the binding
constraint on bus count — each bus needs a clear exit path at the moment it is
placed, so past ~22 buses on 9×10 the placer fails more often than it succeeds.
Capacity has no such limit: boarding is deterministic, so a bigger bus lengthens
the cascade after a dispatch but never branches. Seats 3/6 → 6/12 roughly
doubled the passenger count while state counts stayed flat or fell.

Shipped: seats 6/12, bus ramp raised 17 → 19 (level 200 now carries 22 buses,
which is what the lot holds), no surplus. Level 200 went from a 57-ball queue to
122. Deception depth fell from 17 to 14, which is the honest cost.

Two things to carry forward:

- A difficulty mechanic the player cannot see is a bug report waiting to happen.
  If surplus ever comes back it needs a visual — an "out of service" blind, a
  dark windscreen, something.
- Solver cost tracks *branching*, not board content. Anything deterministic is
  cheap. That is a general lever, not a one-off.

## 20. An empty board is a worse tutorial than a full one

Playtest verdict on the opening: *the early levels just feel too easy and
empty.* Both halves were true and they had the same cause — 3 buses on a 5×5
lot, which is what a target of MDS 4 buys you.

The instinct is that filling the board makes the tutorial hard. Measured at
level 1, two colours, three bays:

| Buses | Board | Queue | States | MDS | Solution density |
|---:|---|---:|---:|---:|---:|
| 3 | 5×5 | 18 | 8 | 1.7 | 1.00 |
| 6 | 5×5 | 36 | 125 | 4.2 | 0.95 |
| 8 | 6×6 | 48 | 541 | 6.3 | 0.92 |
| 10 | 7×7 | 60 | 2 542 | 12.1 | 0.85 |
| 12 | 7×7 | 72 | 8 644 | 16.8 | 0.80 |
| 14 | 8×8 | 84 | 32 400 | 22.9 | 0.76 |

MDS triples from 3 buses to 10, but **solution density only falls from 1.00 to
0.85** — four moves in five still win. Almost all of the MDS rise is state
count, and a player never searches a 2 500-state graph; they look two moves
ahead. This is finding §6 resurfacing at the bottom of the ladder: the scorer
still partly measures board size, and at the opening that reads as difficulty
when it is really just fullness.

So the curve floor moved from 4 to 10 and the bus ramp from 3→22 to 9→22, with
the board starting at 7×7. Level 1 now carries 9 buses and 54 passengers
against 3 and 18. **The guarantee that actually matters is density, not MDS**,
and it is the one under test: levels 1–8 must stay at 0.75 or better.

A failed idea worth recording, because it looks obviously right: making a
breather forgiving by cutting its COLOURS instead of its buses, so the board
stays full. At level 190, dropping 8 colours to 5 moved the score by 1.8 points
(50.9 → 49.1) and the state count went *up* — with fewer colours more docked
buses match the head, so more orderings stay viable. At 3 colours the schedule
cannot be built at all, since only one bus per colour may be docked at a time.
Breathers still cut the bus count, just more gently (×0.78, was ×0.62).

## 11. Early levels are unlosable, and that is correct

Ten levels in chapter 1 have solution density 1.00 — every legal move keeps the
level winnable, so they cannot be failed. This looked like a generator bug and
is not: with three bays and two colours there is no way to commit the buffer
badly enough to matter.

It gives the opening exactly the shape onboarding wants, and the first real trap
appears at level 9, which is where the game starts asking something. Kept
deliberately, and asserted by test so it cannot drift.

*(Superseded in part by §20. Playtest found that opening too empty, so the
boards were filled out and density in chapter 1 now sits around 0.85 rather
than 1.00 — forgiving, but no longer literally unfailable. The test threshold
of 0.75 is unchanged and still the real guarantee.)*

## 12. Chain boarding is the best feel moment in the game

When a bus fills and departs, the next passenger becomes the head — and if a
matching bus is already docked, the queue keeps emptying on its own. A single
tap can clear six passengers and two buses.

This fell out of the rules rather than being designed, and it is the most
satisfying thing in the prototype. The audio should be built around it: the
rising pentatonic boarding notes exist to make a long chain sound like a
finished melody.

## 13. Shipping the solver on device is cheap and changes the tone of the game

The largest reachable state space across all 200 shipped levels is 8 722 states,
so the game can answer "is this still winnable?" exactly, after every move, in
well under a millisecond. That turns two concept promises into working features:

- **Hints work from anywhere.** A precomputed solution path is useless the moment
  the player deviates. Re-solving live means the hint is always valid from the
  actual position.
- **Losing is caught the instant it happens.** With deception depth now reaching
  13, a player could otherwise spend a long time on an already-lost board. The
  game offers a free rewind at the moment the mistake is made — which is what
  makes deep deception fair rather than cruel.

Those two features are load-bearing now, not nice-to-haves.

## 14. Levels must not be verified in only one language

Level data is produced and proven correct by the Python tool, then played by the
C# engine. A rules mismatch between them would mean shipping a level that is
provably solvable and actually is not.

This is now checked rather than assumed. `tools/crosscheck` compiles the
engine-free `Core` and replays every baked level's reference solution
move-for-move, re-solves each level from the start, and walks each one entirely
by hint to prove hints never dead-end. All 200 pass in about 3 seconds,
252 918 states explored, with no Unity involved.

The gate was verified to actually catch regressions rather than pass
vacuously — a reversed solution, a bus shifted one cell, and a missing postcard
were each introduced deliberately and each was caught. Worth noting that
reversing the solution of an *early* level does not fail, and should not: those
levels are unlosable by design, so every order genuinely wins.

---

## The shipped ladder

200 levels, all verified solvable, mean drift from target **1.3** MDS points,
**200 of 200** within 6 points. Every bus on every board is part of the
solution: no level can be won with a bus still standing in the lot.

| Chapter | Levels | Mean MDS | Solution density | Max deception | Buses | Queue |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | 1–25 | 12.4 | 0.83 | 4 | 9.6 | 58 |
| 2 | 26–50 | 19.1 | 0.64 | 7 | 11.3 | 62 |
| 3 | 51–75 | 25.4 | 0.60 | 8 | 12.9 | 65 |
| 4 | 76–100 | 33.9 | 0.51 | 9 | 14.4 | 70 |
| 5 | 101–125 | 41.1 | 0.43 | 11 | 16.1 | 86 |
| 6 | 126–150 | 49.4 | 0.38 | 11 | 17.6 | 96 |
| 7 | 151–175 | 57.2 | 0.33 | 14 | 19.2 | 113 |
| 8 | 176–200 | 64.8 | 0.28 | 14 | 20.9 | 125 |

Difficulty rises monotonically on every measure that matters: levels get less
forgiving (density 0.83 → 0.28) and mistakes take longer to reveal themselves
(deception 4 → 14). 16,887 passengers across the ladder, and no board is
thinner than 43 — the tray reads as a full board from level 1 rather than from
level 100. Opening density runs 0.80–0.89, comfortably inside the 0.75 floor
that is the real forgiveness guarantee (§20). Peak solver cost is 59,686
states, just inside the 60,000 playability gate. Regenerating the whole ladder
takes about twenty minutes.

---

## Open risks not yet tested

- **Is it fun?** Unanswered, and no amount of further analysis will answer it.
  It needs hands on a phone.
- **Do the postcards land or read as cheesy?** The single biggest product risk.
  Test the 240 German cards with ~20 people before commissioning illustration.
- **Does MDS match perceived difficulty?** It is fitted to what the generator
  can produce, not to how hard players find it. Re-fit against real attempt and
  quit data during the vertical slice — that is what the whole metric is for.
- **Is deception depth 13 fair, or infuriating?** It is only tolerable because
  the rewind offer is instant and free. If playtests show frustration anyway,
  cap deception rather than difficulty.
