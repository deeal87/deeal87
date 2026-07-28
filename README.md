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

## Documents

| | |
|---|---|
| **[CONCEPT.md](CONCEPT.md)** | The full concept: gameplay, difficulty model, reward system, business model, plan, risks |
| **[docs/SETUP.md](docs/SETUP.md)** | How to run the game and the level tool |
| **[docs/PROTOTYPE_FINDINGS.md](docs/PROTOTYPE_FINDINGS.md)** | What building it actually taught us, including where the plan needs to change |

## Layout

```
Assets/Scripts/Core/     Rules, solver and content loading. No UnityEngine
                         references at all, so it runs headless in CI.
Assets/Scripts/Game/     Unity presentation. Builds itself at runtime, so the
                         repo carries no binary scene file.
Assets/Resources/        Baked levels and the 240-card German postcard book.
Assets/Tests/            NUnit tests, including a cross-language check that the
                         Python-verified solutions replay in the C# engine.
tools/leveltool/         Level generator, solver and difficulty scorer (Python).
```

## Status

Pre-production prototype (M0).

- Level generator, solver and difficulty scorer: **working**, 30 tests green
- 12 levels baked (1–10 plus previews of 50 and 120), all verified solvable and on-curve
- Unity prototype: rules, solver-backed hints, free rewind, postcard screen, colour-blind palettes
- 240 German postcards across six situational categories, all three tones covered

Not built yet: art, audio, the map, the album, the shop, and the remaining 188 levels.
