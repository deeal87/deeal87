# Running the prototype

## The game (Unity)

There is no `.unity` scene file in the repo on purpose — a binary scene is
something nobody can review or diff. The game builds itself at runtime instead,
so setup is three steps:

1. Create a new **Unity 6 LTS** project (3D template — URP or Built-in both work).
2. Copy this repo's `Assets/` folder into the project, merging with the existing one.
3. Press **Play**.

`Bootstrap.cs` hooks itself in via `RuntimeInitializeOnLoadMethod`, so it runs
from whatever scene is open, including the empty default one. It creates the
camera, lighting, board, HUD and postcard screen, then loads your saved level.

**Controls.** Tap a bus to send it out. Blocked buses shake instead of moving.
`Hinweis` highlights a bus that keeps the level winnable, `Zurück` undoes,
`Neu` restarts.

### Building for a phone

- **Android**: Build Settings → Android → Switch Platform → Build. Minimum API
  level 26, IL2CPP, ARM64.
- **iOS**: Build Settings → iOS → Switch Platform → Build, then open the
  generated Xcode project. Minimum iOS 15.

No plugins, packages or asset store dependencies are needed.

### Running the C# tests

Window → General → Test Runner → EditMode → Run All. The suite covers the rules,
the JSON reader, the palettes, the message book, and — most importantly — replays
every baked level's reference solution through the runtime engine to prove the
C# and Python rule sets agree.

## The level tool (Python)

Needs only Python 3.11+, no packages.

```bash
cd tools/leveltool

python3 main.py verify              # re-solve every shipped level (the CI gate)
python3 main.py report              # measured difficulty against the target curve
python3 main.py show 6              # print a level as ASCII art with its solution
python3 main.py generate --levels 1-200   # rebuild the whole ladder (~2 min)
python3 -m unittest discover -p "test_*.py"   # 35 rules + content tests
```

`generate` writes to `Assets/Resources/Levels/`. It generates many candidates per
level and keeps the one whose measured difficulty lands closest to the target
curve. The whole 200-level ladder rebuilds in under two minutes.

After changing the generator, the mechanic set, or the scoring weights, re-fit
the scoring sigmoid:

```bash
python3 calibrate.py --samples 24   # prints new SIGMOID_CENTER / SIGMOID_SLOPE
```

Paste the printed constants into `solver.py`. Do not hand-tune them to make the
numbers look better — see `docs/PROTOTYPE_FINDINGS.md` §2.

## Reading an ASCII board

```
  0 1 2 3 4
0 . . . < B      B/R/G/Y   bus, first letter of its colour
1 B > . . .      < > ^ v   the bus's front, pointing the way it drives
2 . . . . R      #         cone (permanently blocked)
3 . . . . v      .         empty
4 . B > . .
```

The queue is printed below the board, head first.
