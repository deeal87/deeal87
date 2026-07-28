# Sunny Stop

A cosy bus-terminal puzzle game for Android and iOS, built in Unity 6 with C#.
200 levels of rising difficulty, and **no ads between levels** — after every level
you win, you get a kind word on a postcard instead, and you keep it.

```
┌─────────────────────────────────────┐
│   PARKING LOT                       │   Tap a bus. It drives out and docks.
│   ┌──┐ ┌────┐ ┌──┐                  │   Matching passengers board it.
│   │▲R│ │ ◀B │ │▲G│                  │   Fill it and it leaves, freeing a bay.
│   └──┘ └────┘ └──┘                  │
├─────────────────────────────────────┤
│   ▣ BAY 1   ▣ BAY 2   ▣ BAY 3       │   Only 2-3 bays. The bottleneck.
├─────────────────────────────────────┤
│  ● ● ● ● ● ● ● ● ● ● …              │   Only the head of the queue boards.
└─────────────────────────────────────┘   Clear the queue to win.
```

## Opening it

Clone the repo and add **the repository folder itself** as a project in Unity
Hub (*Add* → *Add project from disk*), then press Play. There is no scene to
open — the game builds itself at runtime. Then run **Sunny Stop → Apply Project
Settings** once from the menu bar.

Requires Unity 6 LTS. Full instructions, including what to do if the Hub does
not see the project, are in [docs/SETUP.md](docs/SETUP.md).

## Documents

| | |
|---|---|
| **[CONCEPT.md](CONCEPT.md)** | The full concept: gameplay, difficulty model, reward system, business model, plan, risks |
| **[docs/SETUP.md](docs/SETUP.md)** | How to run the game and the level tool |
| **[docs/PROTOTYPE_FINDINGS.md](docs/PROTOTYPE_FINDINGS.md)** | What building it actually taught us, including where the plan needs to change |

## Layout

```
Assets/                  the Unity project
Assets/Scripts/Core/     Rules, solver and content loading. No UnityEngine
                         references at all, so it runs headless in CI.
Assets/Scripts/Game/     Unity presentation. Builds itself at runtime, so the
                         repo carries no binary scene file.
Assets/Resources/        Baked levels and the 240-card German postcard book.
Assets/Tests/            NUnit tests, including a cross-language check that the
                         Python-verified solutions replay in the C# engine.
tools/leveltool/         Level generator, solver and difficulty scorer (Python).
tools/crosscheck/        Headless harness replaying every level through the C#
                         engine, proving it agrees with the Python one.
tools/unitystub/         Stand-in for the Unity API so the game layer can be
                         compile-checked without the editor.
tools/check.sh           The whole gate in one command. No Unity needed.
ProjectSettings/         Editor version pin; Unity generates the rest on open.
Packages/manifest.json   Package list.
```

## Checks

```bash
tools/check.sh     # python tests, all 200 levels re-solved, both C# assemblies
                   # compiled, cross-language replay. ~40 seconds.
```

Runs on every push (`.github/workflows/ci.yml`).

## Status

Pre-production prototype (M0).

- **All 200 levels built and verified** — every one proven solvable, mean drift
  from the target curve 1.5 points, 197 of 200 within 6. Regenerates in under
  two minutes.
- Level generator, solver and difficulty scorer: **working**, 35 tests green
- Both C# assemblies compile, and the C# engine replays all 200 Python-verified
  solutions move-for-move — checked headlessly, no Unity licence needed
- Unity prototype: rules, solver-backed hints, free rewind, postcard screen, colour-blind palettes
- 240 German postcards across six situational categories, all three tones covered

Difficulty rises on both measures that matter: levels get steadily less forgiving
(solution density 0.90 → 0.24 across the eight chapters) and a wrong move takes
steadily longer to reveal itself (1 move → 13).

Not built yet: art, audio, the map, the album and the shop.
