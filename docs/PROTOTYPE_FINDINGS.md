# Prototype findings

What building the level pipeline actually taught us, as opposed to what the
concept assumed. Several of these overturn things written in CONCEPT.md; where
they do, the concept has been corrected and the finding is recorded here.

---

## 1. Every winning solution dispatches every *needed* bus exactly once

Seats match passengers, so a win necessarily empties the lot of the buses the
solution uses. The optimal move count therefore equals the number of needed
buses and carries no information about difficulty.

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

## 4. Surplus buses were the fix

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

## 11. Early levels are unlosable, and that is correct

Ten levels in chapter 1 have solution density 1.00 — every legal move keeps the
level winnable, so they cannot be failed. This looked like a generator bug and
is not: with three bays and two colours there is no way to commit the buffer
badly enough to matter.

It gives the opening exactly the shape onboarding wants, and the first real trap
appears at level 9, which is where the game starts asking something. Kept
deliberately, and asserted by test so it cannot drift.

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
provably solvable and actually is not. A C# test replays every baked level's
reference solution move-for-move through the runtime engine; it has to stay
green alongside the Python content gate.

---

## The shipped ladder

200 levels, all verified solvable, mean drift from target 1.5 MDS points,
197 of 200 within 6 points.

| Chapter | Levels | Mean MDS | Solution density | Max deception | Buses | Surplus | Queue |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 1–25 | 5.5 | 0.90 | 1 | 3.9 | 0.2 | 11 |
| 2 | 26–50 | 13.2 | 0.73 | 2 | 6.4 | 1.0 | 15 |
| 3 | 51–75 | 21.6 | 0.61 | 5 | 8.0 | 1.4 | 18 |
| 4 | 76–100 | 30.0 | 0.54 | 9 | 10.4 | 2.0 | 21 |
| 5 | 101–125 | 38.3 | 0.47 | 10 | 12.2 | 2.5 | 27 |
| 6 | 126–150 | 47.4 | 0.34 | 11 | 14.1 | 2.7 | 32 |
| 7 | 151–175 | 55.6 | 0.28 | 11 | 15.9 | 3.2 | 37 |
| 8 | 176–200 | 62.7 | 0.24 | 13 | 17.5 | 3.2 | 43 |

Difficulty rises monotonically on every measure that matters: levels get less
forgiving (density 0.90 → 0.24) and mistakes take longer to reveal themselves
(deception 1 → 13). Regenerating the whole ladder takes under two minutes.

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
