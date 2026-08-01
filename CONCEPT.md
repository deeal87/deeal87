# SUNNY STOP — Game Concept

*A cosy bus-terminal puzzle game. 200+ hand-verified levels. No ads between levels — a kind word instead.*

**Status:** Concept v1.1 — key decisions taken (§13), M0 prototype built
**Platforms:** Android 8.0+ (API 26) and iOS 15+ — one codebase
**Engine / language:** Unity 6 LTS, **C#** (see §9 for the comparison and alternatives)
**Genre:** Casual logic puzzle (parking-jam × colour-sort), single player, offline-first

---

## 1. The pitch

You are the dispatcher of a small, sunny bus terminal. A queue of colourful passengers
waits on the platform. In front of you, the parking lot is a jam of buses — each one a
colour, each one facing a direction, each one blocking somebody.

Tap a bus, it drives out and pulls into a free bay. Passengers of the matching colour
board it. Fill a bus and it drives off waving. Clear the queue and the level is won.

Choose the wrong bus and it occupies a bay it cannot fill — do that three times and the
platform grinds to a halt.

And when you win: **no interstitial ad.** The screen turns warm and hands you one line
written for a human being who just did something well. You keep it. It goes in your
postcard album. There are more than 240 of them and you will not see the same one twice
for a very long time.

**One-line hook:** *Solve the jam, get a kind word — the only puzzle game that pays you in warmth.*

---

## 2. Why this works

The reference game (*Bus Traffic Fever!*, GOODROID — 10M+ downloads, 4.27★) proved the
mechanic's appeal. Its weakness is the same as the whole genre's: a forced 30-second
video ad after almost every level. Player reviews across this genre are dominated by ad
complaints, not gameplay complaints.

Our differentiator is not a mechanic. It is **the moment after the win**. That moment is
currently the most hated three seconds in casual gaming, and we turn it into the reason
people talk about the game. It is cheap to build, impossible to copy without giving up ad
revenue, and it is exactly the sort of thing that gets screenshotted and shared.

Target audience: 25–55, majority female, casual puzzle players, plays in 3–8 minute
sessions, often in bed or on a commute. Secondary: anyone burned out on aggressive
free-to-play.

---

## 3. Core gameplay

### 3.1 The board

Top to bottom, the screen is a tray of waiting passengers, a row of stands, and the lot.

```
┌─────────────────────────────────────┐
│ ⚙ ↺ ┃    LEVEL 137    ┃  ● 42       │   HUD floats over the tray rim.
│ ╭─────────────────────────────────╮ │
│ │ ● ● ● ● ● ● ● ● ● ● ● ●         │ │   THE TRAY. Every passenger still
│ │● ● ● ● ● ● ● ● ● ● ● ●          │ │   waiting, packed hex-fashion into
│ │ ● ● ● ● ● ● ● ● ● ● ● ●         │ │   one recessed board. It drains
│ │● ● ● ● ● ● ● ● ● ● ● ●          │ │   from the bottom row: the front of
│ │ ⊙ ● ● ● ● ● ● ● ● ● ● ●         │ │   the queue is always the floor,
│ ╰─────────────────────────────────╯ │   and ⊙ is the one boarding next.
├─────────────────────────────────────┤
│  ▁1  ▁2  ▁3  ▁4  ▨5  ▨6             │   Six painted stands, 3–4 in service.
├═════════════════════════════════════┤
│   ┌──┐  ┌──────┐   ┌──┐             │   THE LOT (grid, 5×5 → 9×10). Each
│   │ ↑│  │  ←   │   │ ↑│             │   vehicle carries one big white
│   └──┘  └──────┘   └──┘             │   arrow: the direction it leaves in.
│      ┌──────┐   ┌──┐                │   A vehicle occupies 1×2 (3 seats)
│      │  →   │   │ ↓│                │   or 1×3 cells (6 seats).
│      └──────┘   └──┘                │
├─────────────────────────────────────┤
│  TIPP   ZURÜCK   NEU   KARTE        │   Dock.
└─────────────────────────────────────┘
```

### 3.2 Rules

1. **Tap a bus** to dispatch it. It drives straight along its facing direction. If the
   path to the lot exit is blocked by another bus, it cannot move — it shakes and honks
   (clear feedback, no wasted move).
2. A dispatched bus **pulls into the leftmost free bay**. If no bay is free, the tap is
   refused. Bays are the scarce resource; that is where the game lives.
3. **Boarding is automatic.** The passenger currently at the head boards any stand whose
   bus matches their colour, rolling out of the crowd and into a seat. The order only moves
   forward — a green passenger at the head blocks the blue behind them until a green bus
   is docked.

   Presentationally the crowd is **one packed tray, not a queue in a line**. A tidy
   left-to-right line let the player read the entire future at a glance, so the next move
   was never a judgement call; a mass of fifty balls has to be scanned. The pile drains
   from the bottom row, so the ordering information is still *there* — it just costs a
   look rather than being free.

   Two constraints on that tray, both load-bearing:

   - Every waiting passenger is drawn. No "+12 more" caption — if the queue order decides
     the level, the player must be able to see all of it.
   - The jitter that stops it looking like a spreadsheet is **seeded from the level id and
     the passenger's own queue index**, never random at runtime. It is identical for every
     player and on every replay, so the board stays reproducible and the solver still
     answers exactly — which is what the free rewind depends on (§5.1). It looks loose; it
     is not, and it must never become so.
4. A bus **departs when full** (capacity 3 standard, 6 double-decker), freeing its bay.
   A bus that is not full stays docked.
5. **Win:** the queue is empty.
6. **Lose:** every bay is occupied and no docked bus can accept the head of the queue and
   no further bus can be dispatched → *deadlock*. The game detects this the moment it
   becomes true and offers a rewind of the last move (see §7.3), not a punishment screen.

### 3.3 Why this ruleset is good

It layers two different kinds of thinking that interfere with each other:

- **Spatial** — which bus can physically leave the lot right now, and what does moving it
  unblock or seal off? (classic sliding/parking-jam extraction order)
- **Combinatorial** — with only 3 bays and a fixed queue order, which colours can I afford
  to commit a bay to? (a bounded-buffer sorting problem, provably deepening as colours grow)

Neither layer alone would carry 200 levels. Together they produce genuinely fresh
board states for hundreds of levels without ever adding twitch reflexes — the game stays
100% turn-based and pause-friendly, which suits the audience and the "cosy" promise.

### 3.4 Feel

Every action gets weight: buses lean into turns, the engine gives a soft *vrmm*, boarding
passengers hop with a squash-and-stretch and a rising pentatonic note (note index rises
with each boarding in a chain — filling a bus in one go plays a complete little melody).
A departing bus honks a friendly two-tone and confetti puffs from the exhaust. Target: the
board should be pleasant to touch even when you are losing.

---

## 4. Mechanics rollout across 200 levels

New mechanics are always introduced on a **deliberately easy level** so the player learns
the rule without also fighting the difficulty, then combined with everything before it.

| Levels | New mechanic | What it adds |
|---|---|---|
| 1–8 | Tutorial: tap, dock, board, fill | — |
| 9–20 | 4th colour, queue peek (see next 8) | Basic planning |
| 21–35 | **Cones & barriers**, and **surplus buses** — extra buses nobody needs | Spatial pressure, and the first mistakes that stay hidden |
| 36–50 | **Luggage passengers** — take 2 seats | Breaks the "capacity 3 = 3 people" arithmetic |
| 51–65 | **One-way arrows** on lanes | Variety and readability only — measured to add no difficulty, since buses drive straight and never have an alternative route |
| 66–80 | **Locked buses** — open with tickets earned by boarding | Resource sub-goal |
| 81–95 | **Garage buses** — colour hidden until adjacent bus leaves | Managed risk / probability |
| 96–110 | **Double-deckers** — capacity 6, occupy 1×3 | Bay commitment becomes expensive |
| 111–125 | **Traffic lights** — toggle a lane open/closed every N moves | Timing layer |
| 126–140 | **A fourth stand opens** | More open stands is *more forgiving*, not less (findings §5), so this arrives alongside much bigger boards. Four is the ceiling — at five the on-device solver can no longer answer instantly, and the free rewind depends on it (findings §16) |
| 141–155 | **VIP passengers** — must board within N moves | Soft urgency, no real-time timer |
| 156–170 | **Tunnels** — bus exits one side, re-enters another | Spatial re-think |
| 171–190 | Full combination, "expert dispatch" boards | Mastery |
| 191–200 | Finale arc — themed boards, story beat, 5 signature puzzles | Payoff |

Every 25th level is a **milestone**: a bigger board, a unique background (night terminal,
rain, festival, snow), a special postcard, and a permanent cosmetic reward.

Levels 201+ ship as free monthly "Season" packs of 25 so the game is not "finished".

---

## 5. Difficulty: rising, never impossible

This is the part of the design most likely to fail, so it is engineered rather than
eyeballed.

### 5.1 Guaranteed solvable, by construction

Levels are **never** generated by randomly filling a board and hoping. They are built so
that a solution exists by construction, in two stages:

1. **Build a legal schedule first.** Interleave "dispatch a bus" and "board a passenger"
   steps at random, respecting the bay count and the head-of-queue rule, until every bus
   has been dispatched and filled. This produces both the passenger queue and the order
   the buses must come out in. (One invariant keeps it unambiguous: never two docked buses
   of the same colour at once, so "leftmost matching bay" always has one answer.)
2. **Place the buses in reverse dispatch order.** When bus *i* is placed, the grid holds
   exactly the buses that get dispatched *after* it — which is precisely the situation bus
   *i* will face when its turn comes. Requiring a clear exit path at that moment makes the
   whole schedule replayable.

The result is guaranteed solvable, and "impossible level" is structurally impossible.

*Implementation note:* the first draft of this document proposed generating levels by
applying inverse moves from the solved state. That does not survive contact with automatic
boarding — un-boarding a passenger is ambiguous, because the rules decide on their own who
boards and when. The schedule-then-place method above is what is actually implemented in
`tools/leveltool/generator.py`, and it gives the same guarantee more cheaply.

### 5.2 Then measured, by solver

A generated candidate is fed to an offline solver (IDA* with a bay-commitment + queue
admissible heuristic, plus a deadlock detector). It reports:

| Symbol | Meaning |
|---|---|
| `L*` | length of the optimal solution (moves) |
| `S` | number of states expanded — proxy for search effort |
| `E` | *solution density*: average fraction of legal moves at each state that keep the level solvable. Low `E` = unforgiving |
| `T` | *trap depth*: minimum number of plausible-looking moves that lead to an unrecoverable state |
| `F` | forced-move ratio: fraction of states with exactly one sensible move (high = tedious, not hard) |

**Measured Difficulty Score** (fitted once against playtest data, then held stable):

```
MDS = 100 · σ( 0.9·ln(S)/ln(S_max) + 0.55·L*/L*_max + 1.30·(1 − E) + 0.45·(1 − 1/T) − 0.50·F )
```

Candidates are generated in batches; only those whose MDS lands inside the target
band for their slot are kept. Everything else is discarded. Levels are **baked as static
JSON** and shipped with the app — no runtime generation, so every player worldwide plays
the identical, verified level, and walkthrough sites/YouTube (a major organic traffic
source for this genre) actually work.

### 5.2b Why surplus buses exist

The single most important thing measurement changed about this design.

With seats exactly matching passengers, every bus eventually fills and every bay
eventually frees, so the only way to lose is instant gridlock — which the player
sees the moment it happens. That makes the game *punishing* but not *hard*: a
mistake can never stay hidden.

Levels therefore carry **surplus buses** the solution does not need, parked off
every intended route so solvability by construction is untouched. Send one into a
bay and it can sit there forever, costing a bay silently while play continues.
That is what lets a mistake take ten moves to surface, and it is the mechanic
that moved difficulty when luggage and double-deckers did not.

It also makes the instant rewind offer (§5.4) load-bearing rather than a nicety:
deep deception is only fair if the game tells you the moment it happens.

### 5.3 The target curve

```
Target(n) = 4 + 68 · (n / 200)^1.15          # gentle opening, steady rise
          × 1.18  if n mod 25 == 0           # milestone spike
          × 0.58  else if n mod 10 == 0      # breather - never on a milestone
          × (1 ± 0.07)                       # texture, so it never feels metronomic
Clamped to [3, 72].
```

Three details that only became obvious once it was measured:

- **The exponent is above 1.** The first draft used 0.80, which rises fastest at
  the very start and wanted MDS 13 by level 10 — from an opening chapter whose
  whole job is to be unfailable.
- **The breather never lands on a milestone.** Levels 50, 100, 150 and 200 are
  multiples of both 10 and 25; applying both modifiers made the flagship levels
  *easier* than their neighbours.
- **The ceiling is 72, not 100.** That is the hardest the current ruleset can
  actually produce. Aiming past it yields permanent drift on the last dozen
  levels rather than harder levels.

Measured on the shipped ladder (mean per chapter):

| Chapter | Levels | MDS | Solution density | Deepest hidden mistake |
|---|---|---|---|---|
| 1 | 1–25 | 5.5 | 0.90 | 1 move |
| 2 | 26–50 | 13.2 | 0.73 | 2 moves |
| 3 | 51–75 | 21.6 | 0.61 | 5 moves |
| 4 | 76–100 | 30.0 | 0.54 | 9 moves |
| 5 | 101–125 | 38.3 | 0.47 | 10 moves |
| 6 | 126–150 | 47.4 | 0.34 | 11 moves |
| 7 | 151–175 | 55.6 | 0.28 | 11 moves |
| 8 | 176–200 | 62.7 | 0.24 | 13 moves |

Difficulty rises on both measures that matter to a player: levels get steadily
less forgiving, and a wrong move takes steadily longer to reveal itself.

Deliberate design choices in that curve:
- **The sawtooth is the point.** A pure monotonic ramp reads as a grind. The every-10th
  breather gives a guaranteed win right after a hard level; that rhythm is what makes a
  200-level run feel good.
- **Cap at 92, not 100.** The top 8 points are reserved for optional bonus/expert levels
  that never block progression.
- **Move budgets are generous:** where a level has a move limit, it is `ceil(1.35 · L*)`,
  never tighter. Players are never asked to find *the* optimal solution, only *a* good one.

### 5.4 Anti-frustration (the "never impossible" guarantee in practice)

Being solvable is not the same as being beatable by a real person on a bus. So:

- **Fail-aware assistance.** After 3 failures on a level, the game quietly offers a free
  hint (highlights one good next move). After 5, it offers a free extra bay for that
  attempt. It is offered as *"Want a hand?"*, is always declinable, is never sold, and
  never appears in stats.
- **One-tap undo, always free.** Deadlock is detected instantly and offered as
  *"Rewind that one?"* — no life lost, no timer, no "watch a video to continue".
- **No lives, no energy, no timers.** You can fail forever at no cost. This removes the
  single biggest reason people quit games in this genre, and it costs us nothing because
  we are not monetising frustration.
- **Skip after 10 fails.** A level can be skipped for free, marked with a small grey dot,
  and re-tried any time. Nobody is ever hard-walled.
- **Live telemetry gate:** any level whose measured live fail-rate exceeds 55% or whose
  quit-rate exceeds 12% is flagged and re-tuned in the next content patch. Level data is
  loaded from a versioned bundle so tuning ships without a store review.

---

## 6. The reward moment (the heart of the product)

### 6.1 What happens after a win

1. Bus drives off, confetti, ~1.2 s of celebration.
2. Screen washes to a soft gradient (per chapter). Ambient loop dips, one warm piano chord.
3. A **postcard** slides in, hand-lettered type, one illustration, one line of text:

   > *"You just untangled something that looked hopeless. Remember that you're good at that."*

4. Two buttons: **Keep** (saves to album) and **Next level**. No timer, no forced dwell,
   no X-button-that-is-actually-an-ad. Tapping anywhere continues.

Total added time: about 2 seconds if you tap through, as long as you like if you don't.

### 6.2 The message system

- **240+ unique lines** at launch, so a player finishing all 200 levels never repeats one.
- Written and reviewed by a human writer — this is the product, not filler. No LLM-
  generated-at-runtime text (unpredictable, unlocalisable, off-brand).
- **Categories**, selected by context, not at random:
  - *Recognition* — after a hard-won level (≥3 attempts)
  - *Gentle* — after a level the player struggled with and skipped
  - *Playful* — after a fast, clean solve
  - *Grounding* — late night sessions (device clock 22:00–04:00)
  - *Milestone* — every 25 levels, longer, illustrated, collectible
  - *Return* — first win after ≥3 days away
- **No repeat** within the last 60 shown (persisted ring buffer).
- **Tone selector** in settings: ***Warm* (the default, decided)** · *Playful* · *Quiet*
  (short, plain, no exclamation marks) · *Off* (just the confetti). Kindness that cannot be
  turned off is not kindness. Every situational category carries cards in all three tones,
  so choosing a register never silently drops you into another one — enforced by test.
- **Nothing therapeutic or medical.** No mental-health claims, no "you are not alone in
  your depression" register. The line is a friendly colleague, not a counsellor. This is
  both an editorial and a store-compliance decision.
- **Optional first name**, asked once, skippable, stored locally only.
- Fully localised (§11) — translated, not machine-translated, because a clumsy affirmation
  is worse than none.

### 6.3 The Postcard Album

Kept postcards land in an album — a grid of collectible cards with the illustration, the
line, and the date and level you earned it on. Some are rare (milestones, seasonal, "first
win of the year"). Any card can be exported as a clean image and shared.

This turns the anti-ad gimmick into a **collection meta-game**: a second reason to keep
playing that costs nothing to maintain and generates organic social sharing. It is also
the natural home for the paid cosmetic packs (§8).

---

## 7. Structure, UX, progression

### 7.1 Screens

`Splash → Map → Level → Win (postcard) → Map` — that is the whole loop. Everything else
(Album, Settings, Shop, Daily) hangs off the Map.

- **Map** — a vertical scrolling bus route: an actual winding road with 200 stops along it,
  8 chapters of 25, each tinted with its own time of day and dotted with roadside scenery
  (houses, trees). Beaten stops fill in, milestones every 25 are larger stations, and your
  bus token sits on the current stop. It is a place you travel through, not a grid of
  numbered buttons.
- **Level** — board fills the screen, minimal chrome: back, restart, hint, undo, move
  counter. No timers on screen. No pop-ups mid-level, ever.
- **Album** — the postcard collection.
- **Daily Stop** — one bonus puzzle per day (from the same generator, MDS pinned to your
  current skill estimate), gives a rare postcard. Purely optional, no streak-shaming: a
  broken streak shows "welcome back", not a lost counter.

### 7.2 Onboarding

Levels 1–5 teach by constrained boards, not by text overlays: level 1 has exactly one
legal move, so the player cannot fail and learns the tap. Text is limited to 4 short hints
across the first 8 levels. Time to first win: under 20 seconds.

### 7.3 Session shape

Median session ≈ 6 minutes, 4–7 levels early on, 1–3 levels late. Every level is
interruptible: full board state is serialised on pause, so a phone call never costs
progress.

### 7.4 Accessibility (not optional — colour matching *is* the game)

- **Colour-blind mode**: every bus colour also carries a distinct icon (star, moon, leaf,
  drop…) shown on the bus and on the passenger. Three palettes: default, deuteranopia,
  tritanopia. Tested with a simulator in CI.
- Dynamic type support, minimum 44 pt tap targets, haptics-optional, reduced-motion mode
  that removes confetti and camera shake, full VoiceOver/TalkBack labels on board state
  ("blue bus, row 3, blocked").
- Runs at 60 fps on a 2019 mid-range Android (target: Galaxy A20 / iPhone 8).

---

## 8. Business model (honest version)

The core promise — *no ads between levels* — must be absolute. If it is broken once, the
entire differentiator is gone and the reviews will say so.

**Recommended: free to play, no interstitial ads at all, revenue from:**

| Source | Price | Notes |
|---|---|---|
| **Sunny Pass** (one-time) | €4.99 | Unlocks all cosmetic bus/terminal skins, 3 exclusive postcard sets, "Keep All" auto-save, supporter badge. Zero gameplay advantage. |
| Postcard packs | €1.99 | Seasonal illustrated sets (12 cards) |
| Bus & terminal skins | €0.99–2.99 | Cosmetic only |
| Hint bundles | €0.99 | 20 hints — but hints are also earned free and given free after failures, so this is a convenience purchase, not a wall |
| "Buy me a coffee" tip | €2.99 | Surprisingly effective for games with this positioning |

**Explicitly rejected:** interstitials between levels, lives/energy, timers, ads disguised
as rewards, "watch to continue", any pop-up during the postcard moment.

**Decided:** *player-initiated* rewarded video is allowed — a button inside the shop
reading "Watch a short video for 3 hints?". Never automatic, never after a level, never on
the postcard screen. It roughly doubles realistic revenue for this genre without touching
the core promise. The ad SDK is therefore in the build, which makes one rule absolute: it
must be impossible for that SDK to be invoked from anywhere except that one shop button.
Enforce it with a single wrapper class and a test that no other caller exists.

Realistic expectation: this positioning trades ARPDAU for retention, word-of-mouth and
review score. It is a brand play. Comparable "premium-feel casual" titles convert 1.5–3%
of players; the marketing value of the no-ads promise is the real asset.

---

## 9. Technology

### 9.1 Recommendation: Unity 6 LTS + C#

One C# codebase → Android (.aab, IL2CPP/ARM64) and iOS (.ipa). This is the industry
default for exactly this kind of game and it is not close.

| Option | Language | Verdict |
|---|---|---|
| **Unity 6 LTS** | **C#** | ✅ **Recommended.** Best 2.5D/3D toolchain, mature mobile pipeline, huge hiring pool, every needed plugin (IAP, analytics, notifications) is first-party or well-maintained. Free until $200k revenue. |
| Godot 4.4 | C# or GDScript | Good, fully open-source, no fees, smaller build size. Weaker iOS C# tooling and a thinner mobile plugin ecosystem. Sensible fallback if Unity licensing is a concern. |
| Flutter + Flame | Dart | Fine for flat 2D UI-heavy games; the bus/board presentation we want (perspective lot, camera moves, particles) fights the framework. |
| React Native / web wrapper | JS/TS | Not recommended — animation performance on low-end Android is the exact thing we cannot compromise. |
| Native (Kotlin + Swift) | Two languages | Two codebases, two bug sets, no upside here. |

### 9.2 Architecture

Strict separation between **rules** and **presentation** — this is what makes the level
tooling and the test suite possible:

```
SunnyStop/
├─ Core/                     ← pure C#, zero UnityEngine references
│  ├─ Model/                 GameState, Bus, Passenger, Bay, Grid
│  ├─ Rules/                 MoveValidator, BoardingResolver, DeadlockDetector
│  ├─ Solver/                IDAStarSolver, Heuristics, DifficultyScorer
│  └─ Generation/            ReverseGenerator, LevelBaker
├─ Game/                     ← Unity layer
│  ├─ View/                  BoardView, BusView, QueueView, CameraRig
│  ├─ Input/                 TapRouter, UndoStack
│  ├─ Flow/                  LevelFlow, WinSequence, PostcardPresenter
│  └─ Meta/                  MapScreen, Album, Shop, Settings, Save
├─ Content/
│  ├─ levels/                level_001.json … level_200.json (baked)
│  ├─ postcards/             messages.<locale>.json + art
│  └─ art/, audio/
├─ Tools/                    ← standalone .NET console app
│  ├─ generate                batch-generate + score + bake levels
│  ├─ verify                  re-solve every shipped level in CI
│  └─ balance                 plot MDS vs target curve, flag outliers
└─ Tests/                    NUnit — rules, solver, and all 200 levels
```

`Core` being engine-free means the solver and generator run as a fast headless .NET tool,
and **CI re-verifies all 200 shipped levels on every commit** (solvable + MDS within band
+ move budget sufficient). A level that regresses fails the build. This is the single most
valuable piece of infrastructure in the project.

### 9.3 Data

- Levels: static JSON, versioned bundle, hot-updatable without a store release.
- Save: local JSON (progress, album, settings) + optional cloud save (Google Play Games /
  iCloud) so a lost phone does not lose 200 levels of postcards.
- Analytics: minimal and privacy-first — level attempts, fails, quits, time-per-level. No
  ad IDs, no third-party data brokers, GDPR-clean, ATT prompt not even needed. That is
  itself a marketing line.

### 9.4 Level JSON (draft)

```jsonc
{
  "id": 137,
  "chapter": 6,
  "grid": { "w": 7, "h": 8 },
  "bays": 2,
  "colors": ["red", "blue", "green", "yellow", "purple"],
  "buses": [
    { "id": 1, "color": "red",  "cells": [[2,3],[2,4]], "facing": "up",   "capacity": 3 },
    { "id": 2, "color": "blue", "cells": [[4,1],[5,1],[6,1]], "facing": "right",
      "capacity": 6, "type": "doubledecker" },
    { "id": 3, "color": "hidden", "cells": [[0,6],[0,7]], "facing": "down",
      "reveal": "onAdjacentClear" }
  ],
  "blocked": [[3,3],[3,4]],
  "oneWay":  [{ "cell": [5,5], "dir": "left" }],
  "queue": [
    { "color": "red" }, { "color": "red" }, { "color": "blue", "luggage": true },
    { "color": "green", "vip": 12 }
  ],
  "moveLimit": 41,
  "solution": { "optimal": 30, "mds": 68.4, "solverVersion": 7 }
}
```

---

## 10. Art & audio direction

**Look:** bright, glossy, high-contrast plastic — the shelf-appeal register of the genre
rather than an illustration style. Three horizontal bands fill the screen (§3.1):

- **The tray.** A recessed board with a heavy rim, packed edge to edge with glossy
  spheres. The rim is deep at the top because that band is where the HUD floats, so the
  controls never sit on a ball the player is trying to read.
- **The stands.** Painted parking markings on the apron — white, skewed a few degrees so
  they read as ground rather than as UI. Stands not in service this level are hatched out.
- **The lot.** A warm beige apron of chunky vehicles. Each carries **one big white arrow**
  giving the direction it will leave in, which is the only thing that has to be legible
  from across the board. Length carries capacity: 1×2 is three seats, 1×3 is six.

For 1.0 this is built from a single free low-poly kit rather than commissioned models
(§13) — the coherence comes from lighting, palette and camera, which cost nothing and
carry most of the look anyway. The chapter palette still shifts the backdrop: morning
yellow → midday blue → sunset orange → night indigo → rain → snow → festival → dawn.

Two earlier directions were tried and dropped, and it is worth recording why:

- The **motorway board** (asphalt, lane markings, crash barriers, traffic down the verges)
  was handsome but fought the pieces: painted lines and vehicle bodies compete at the same
  scale and the arrows stopped popping. The lot is now plain.
- **Destination signs, headlights and visible wheels** on every bus were detail nobody
  could resolve at 26 px once the board reached 9×10. All that survives is a small dark
  cab window, plus a tiny chip carrying the colour-blind glyph at the tail.

**Passengers are balls.** Glossy coloured spheres that physically *fly out of the tray and
land in a seat inside the bus*. This replaced the earlier "little characters" idea and is
a much better call:

- **Capacity becomes countable.** Every vehicle shows its seat sockets along its lower
  edge, so "this one holds three" is something you see rather than read. Parked at a stand
  the arrow disappears and the sockets grow, because that is where you actually watch.
- **Boarding becomes visible.** A ball leaves the tray, arcs across and lands in a
  specific seat. The chain reaction when a bus fills reads as a chain reaction.
- **It stays legible at 24 px** on a phone, which detailed characters do not.
- Luggage passengers take two sockets: the ball plus a small case beside it.

The colour-blind glyph rides on the ball at low contrast — findable when you look for it,
invisible at a glance, so a full tray still reads as a mass of colour rather than a sheet
of symbols. Colours are a single saturated table shared by the Unity build and the browser
preview (`Palette.DefaultColors` ↔ `COLORS` in `game.js`) so the two cannot drift.

**Postcards:** a distinctly different, hand-made illustration style — gouache/riso texture,
hand-lettered headline. The contrast with the clean 3D board is what makes the moment land.

**Audio:** warm acoustic ambience per chapter, no loop fatigue (layered stems). SFX are
tuned like an instrument, boarding notes form a rising pentatonic melody. Everything
duckable; the game is fully playable silent, with haptics carrying the feedback.

---

## 11. Localisation

Launch: **English, German, French, Spanish, Portuguese-BR, Japanese, Korean, Turkish**.
Board UI is nearly text-free, so 95% of localisation cost is the 240 postcards — and those
need real translators with a brief on tone, because a badly translated affirmation is
actively embarrassing. Budget for a native-speaker tone review per language.

---

## 12. Plan, scope and risks

### 12.1 Milestones

| Phase | Duration | Output |
|---|---|---|
| **M0 Prototype** | 3 weeks | `Core` rules + ugly board, 10 hand-made levels. Decision gate: *is it actually fun?* |
| **M1 Vertical slice** | 5 weeks | Final art on 1 chapter, win-postcard moment complete, 25 levels, 20-person playtest |
| **M2 Tooling** | 4 weeks | Generator + solver + difficulty scorer + CI verification |
| **M3 Content** | 6 weeks | All 200 levels generated, scored, curated, hand-polished; 240 postcards written & illustrated |
| **M4 Meta** | 4 weeks | Map, album, shop, IAP, cloud save, settings, accessibility |
| **M5 Polish & soft launch** | 4 weeks | Localisation, store assets, soft launch (CA/NL/PH), tune from telemetry |
| **M6 Launch** | 2 weeks | Global release |

≈ **6–7 months** with a small team. With the free-asset decision from §13 and the level
tooling and postcards already done, a solo developer can realistically hit **~4 months**
for 1.0: the remaining irreducible work is the mechanic rollout (§4), the 188 outstanding
levels, and the meta screens. Budget a contract illustrator only for the ~30 milestone and
seasonal postcards.

### 12.2 Risks

| Risk | Mitigation |
|---|---|
| Generated levels feel samey | Generator produces *candidates*; a human curates every one of the 200 and hand-edits milestones. Mechanic rollout (§4) forces variety. |
| The kind messages read as cheesy | Hire a real writer; tone selector; ship "Quiet" register; test the postcards with 20 players before writing 240 of them. This is the #1 product risk. |
| Difficulty curve wrong for real players | MDS is fitted to playtest data, not assumed; live fail-rate gate re-tunes levels via content patch without a store release. |
| Revenue below ad-model | Accepted trade. Sunny Pass + cosmetics + optional shop-only rewarded video (Q3) as the lever if needed. |
| Genre is crowded | We are not competing on mechanic. Store listing, screenshots and the first review line all lead with "no ads — a kind word instead". |
| Store rejection over wellbeing wording | Editorial rule: no medical/therapeutic claims, no mental-health framing (§6.2), enforced by a content test. |
| Free assets make it look generic | One kit only, never mixed; identity carried by lighting, palette, camera and the hand-made postcards. Licences tracked from day one. |

---

## 13. Decisions taken

| Question | Decision |
|---|---|
| What to build first | **Playable prototype**, then the tooling. Both are now done — see §14. |
| Engine and language | **Unity 6 LTS with C#**, one codebase for Android and iOS. |
| Advertising | **No interstitials, ever.** One *player-initiated* rewarded-video button inside the shop only (§8). |
| Postcard language | **German first**, drafted in-house. All 240 cards written. |
| Default tone | **Warm and personal.** *Playful*, *Quiet* and *Off* remain selectable. |
| Name | **Sunny Stop.** |
| Art | **Free and asset-store sources for 1.0** rather than a commissioned artist. |

### What the art decision means in practice

Going with free sources is the right call to reach a playable, shippable 1.0 — but it
changes three things, and they are worth being deliberate about:

- **Pick one source and stay in it.** Mixing packs is what makes a game look assembled
  rather than designed. A single low-poly kit (Kenney's assets are CC0 and cover vehicles,
  characters and props; Quaternius is similar) gives a coherent look for free.
- **Spend the saved money on the one thing assets cannot supply.** The postcards are the
  product's differentiator and there is no free asset for them. Budget a real illustrator
  for the ~30 milestone and seasonal cards even if everything else is off the shelf; the
  contrast between a clean 3D board and hand-made cards is exactly what makes the moment
  land (§10).
- **Check the licence before shipping, not after.** CC0 needs no attribution; CC-BY does,
  and a missing attribution file is a takedown risk. Keep `Assets/ThirdParty/LICENSES.md`
  current from the first imported file.

This shortens the plan in §12 by roughly four weeks and removes the single largest cost.

---

## 14. Prototype status

Built and verified:

- **Level generator, solver and difficulty scorer** (`tools/leveltool/`) — 30 tests green
- **12 levels baked** (1–10 plus previews of 50 and 120), every one proven solvable and
  within 6 MDS points of the target curve
- **Unity/C# prototype** — rules, on-device solver powering hints and the instant rewind
  offer, postcard screen, colour-blind palettes, runtime-built scene
- **240 German postcards** across six situational categories, all three tones covered

Not built: art, audio, the map, the album, the shop, and the remaining 188 levels.

One finding materially affects the plan: with only the three mechanics implemented so far,
measured difficulty plateaus from about level 90 and level 200 reaches roughly half its
target. The mechanic rollout in §4 is therefore the difficulty engine, not decoration.
Full write-up in `docs/PROTOTYPE_FINDINGS.md`.

---

## 15. Still open

- **Is it fun?** The prototype exists to answer this and it needs hands on a phone.
- **Do the postcards land, or read as cheesy?** The biggest product risk. Test the 240
  cards with ~20 people before committing to illustration.
- **Store identity** — developer name, account, and whether *Sunny Stop* is free of
  trademark conflicts in the target markets.
- **Localisation order** after German and English.

---

*Concept v1.1 — decisions in §13 are settled; everything else remains open to change.*
