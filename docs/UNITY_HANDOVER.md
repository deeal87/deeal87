# Finishing Sunny Stop in Unity

Everything below is written for the person who opens this repository in Unity and
takes it to a store build. It says what is **done**, what is **deliberately a
stand-in**, and what is **not started** — with the exact file and line to change
in each case.

Nothing here is aspirational. If a thing is listed as done, it compiles and is
covered by `tools/check.sh`.

---

## 1. Five minutes to a running game

```bash
git clone <repo> && cd deeal87
tools/check.sh          # optional, ~12 min: proves the content before you open it
```

Then Unity Hub → **Add** → **Add project from disk** → pick the repository root →
**Play**. There is no scene to open: `Bootstrap.cs` hooks itself in with
`[RuntimeInitializeOnLoadMethod]` and builds camera, lighting, board, HUD, menu,
map, album and postcard screen from whatever scene is active, including the empty
default one. That is why the repo carries no binary `.unity` file.

Run **Sunny Stop → Apply Project Settings** once from the menu bar. It sets
portrait, linear colour, Android API 26+, iOS 15+.

Editor version is pinned to **6000.0.32f1** (Unity 6 LTS) in
`ProjectSettings/ProjectVersion.txt`.

---

## 2. What is finished and should not need touching

The **rules and the content** are the part that is genuinely done, and they are
the part that would be most expensive to get wrong.

| Thing | Where | Proof it works |
|---|---|---|
| Rules engine | `Assets/Scripts/Core/` | 36 Python tests + C# cross-check |
| On-device solver | `Core/Solver.cs` | powers hints and the free rewind, ≤60k states |
| 200 levels | `Assets/Resources/Levels/` | every one re-solved in Python, JS **and** C# |
| 240 postcards | `Assets/Resources/Messages/messages.de.json` | invariants asserted by test |
| Save data | `Game/SaveGame.cs` | — |

`Assets/Scripts/Core/` has an asmdef with `noEngineReferences: true`. It does not
reference UnityEngine at all, which is what lets the whole rules layer be
compiled and tested without the editor. **Keep it that way** — the moment
something in `Core` needs `UnityEngine`, the cross-language gate stops being
able to run in CI.

### The invariant that must survive

Every bus on every board is part of the solution: total seats on the board equal
total seats the queue consumes, **per colour**. A level that could be won with a
bus still parked in the lot cannot be generated — `validate_level` rejects it and
two tests assert it. If you ever hand-author a level, run
`python3 tools/leveltool/main.py verify` before shipping it.

---

## 3. What is a deliberate stand-in

This is the real work list. Everything here **functions correctly** and is drawn
with runtime primitives, because a prototype whose scene is a binary file is a
prototype nobody can review. Each item is a seam where art drops in.

### 3.1 The board is primitives — replace with prefabs

`Assets/Scripts/Game/BoardView.cs` builds every vehicle from a stretched Cube,
every passenger from a Sphere, every cone from a Cylinder.

**What to do:** author prefabs and swap the four construction sites:

| Method | Builds | Replace with |
|---|---|---|
| `BuildBus` (line 218) | body cube, cab, glyph, seat sockets | one bus prefab per length (1×2, 1×3) with named seat sockets |
| `RefreshQueue` (line 368) | passenger spheres | a ball prefab, or a GPU-instanced mesh — see the note below |
| `BuildBlockedCells` (line 189) | cone cylinders | a cone prefab |
| `BuildGround` (line 140) | lot / apron / tray cubes | one terminal environment prefab |

Keep `BusView.AddSeat(Transform)` being called in boarding order. `PlayBoarding`
flies a ball to `BusView.NextSeatPosition()`, so as long as the prefab registers
its sockets nose-to-tail, the animation keeps working untouched.

**Watch the passenger count.** Late levels draw up to **150** passengers at once
and the whole ladder holds 16 887. At 150 separate GameObjects with a Renderer
each, this is the first thing that will cost you frames on a low-end Android
device. The tray is static between moves, so the cheap fix is one mesh with
`Graphics.DrawMeshInstanced`, or a single mesh rebuilt on each board event. Do
not discover this at submission time — profile a level-200 board early.

### 3.2 The LED destination blind is text, not a lamp grid

`MenuView.BuildUi` sets the title as wide amber capitals. The browser build
rasters it properly: text rendered into an offscreen buffer at 4× a lamp grid,
each 4×4 block averaged into one lamp, unlit lamps drawn faintly so the dark grid
reads as a board rather than as glowing text. See `drawBlind` in
`tools/webpreview/game.js` — the algorithm is ~40 lines and ports directly to a
`Texture2D` written once at load.

That is the single highest-value visual upgrade on this list. It is what makes
the menu look like terminal equipment.

### 3.3 The album card height is estimated

`AlbumView.BuildCard` guesses line count from `card.Text.Length / 42`. Correct
for a prototype, wrong for a shipping build with a real font. Put the quote on a
prefab with `ContentSizeFitter` + `VerticalLayoutGroup` and delete the estimate.

### 3.4 UI is built from code, not prefabs

`UiBuilder.cs` constructs every panel, label and button at runtime with
hard-coded pixel offsets against a 1080×1920 reference. It works and it scales,
but it is not how you want to iterate on layout. Rebuilding menu, map, album and
HUD as prefabs is a mechanical job; the view classes already separate *build*
from *refresh*, so only the build half changes.

### 3.5 Sound effects are synthesised, not recorded

`SfxSynth.cs` generates all nine effects as PCM at runtime — no files, no
licensing, nothing to download. That is not a placeholder choice, it is the
right one for this game: the boarding sound climbs a pentatonic scale once per
passenger and a cascade can be twelve long, so as samples it is twelve
perfectly-tuned files that drift the moment anybody re-exports one. As
`base * ratio[i]` it is in tune by construction.

`AudioDirector.cs` is where the design actually lives — which sound on which
event, the climbing chain, the near-silent postcard. **Keep that file even if
you replace every waveform.** A recorded marimba would be warmer than an
additive one, and swapping `SfxSynth.Board(i)` for twelve clips is a small
change; the timing and note choices are the part that took thought.

To audition without opening Unity, the same code renders to .wav:

```bash
mcs -out:/tmp/render.exe tools/audio/Render.cs Assets/Scripts/Game/SfxSynth.cs
mono /tmp/render.exe /tmp/sfx      # writes nine wavs plus a full boarding chain
```

### 3.6 Legacy `UnityEngine.UI.Text`

Everything uses the built-in `Text` component, not TextMeshPro, so the project
has no package dependency beyond ugui. For a store build you almost certainly
want TMP: better kerning, proper SDF scaling, and it is what the wide-tracking
uppercase labels in the menu need to stop looking loose.

---

## 4. What is not started

Honest list. None of this is stubbed out anywhere — it simply does not exist.

- **Music and ambience.** The *effects* are done (§3.6); the warm acoustic bed
  per chapter is not. This is the part synthesis would do badly — a generated
  pad sounds like a generated pad — so it wants a composer or licensed loops.
  Budget one loop per chapter, layered stems so it does not fatigue.
- **Haptics.** Nothing. The design leans on them for the silent-play case.
- **Art.** No models, textures or icons. `Assets/ThirdParty/LICENSES.md` is the
  file to record asset provenance in *before* you import anything.
- **The shop.** The business model is in CONCEPT.md §8 — one non-consumable
  "Sunny Pass", shop-only player-initiated rewarded video, no interstitials ever.
  Nothing is built, and nothing about the current build assumes it.
- **English postcards.** German only. 240 cards. The pipeline is
  locale-agnostic: `ContentLoader.Messages(locale)` already takes a locale and
  looks for `messages.<locale>.json`.
- **Cloud save.** `SaveGame` is PlayerPrefs. CONCEPT.md §9.3 wants JSON + cloud
  save so nobody loses 200 levels of postcards with a phone.
- **Store presence.** Name check, developer account, screenshots, ratings
  questionnaires, privacy declarations.

---

## 5. Things that will bite you if nobody tells you

**The solver runs after every move.** `GameFlow` asks
`Solver.IsSolvable` after each dispatch, so it can offer the free rewind the
instant a move makes the level unwinnable. That check is the whole
anti-frustration promise, and it is why `STATE_CAP` is 60 000 rather than
"as high as generation can stand". The worst shipped level measures 59 686
states. **If you add a mechanic that widens the state space, the rewind stalls
before anything else breaks.** Measure it on level 200 before you commit to it.

**Four open stands is a measured ceiling, not a round number.** More open stands
makes the game *easier* (bigger buffer absorbs more mistakes) and blows past the
solver budget. `docs/PROTOTYPE_FINDINGS.md` §5, §15, §16.

**The colour table is shared.** `Palette.DefaultColors` and `COLORS` in
`tools/webpreview/game.js` are the same eight values by hand. If you change one,
change the other, or the preview stops being a preview.

**The scatter in the tray is seeded, never random.** `BoardView.Jitter` is an LCG
keyed on level id + queue index. If somebody "improves" it to `Random.value`, the
board differs between players and the solver behind the rewind is answering about
a different board than the one on screen.

**Do not put `tools/unitystub/` under `Assets/`.** It is a build-time stand-in
for the slice of the Unity API the game uses, so the ~2 100 lines of Unity-layer
code can be compiled in CI without a licence. Under `Assets/` Unity would find
two definitions of every type.

---

## 6. Suggested order

1. **Profile a level-200 board on a real low-end Android device.** Everything
   else is easier to decide once you know whether 150 passenger GameObjects are
   a problem. (They probably are.)
2. Passenger and bus prefabs, and the instancing decision that comes out of (1).
3. TextMeshPro pass, then the LED blind as a real lamp texture.
4. Audio: boarding notes first — it is the moment the whole game feels good, and
   sound doubles it.
5. Postcard illustration. CONCEPT.md §10 wants a deliberately different register
   from the board — gouache/riso, hand-lettered. Test the 240 cards on ~20 people
   **before** commissioning any of it; whether they land or read as cheesy is the
   single biggest product risk and no amount of art fixes the wrong words.
6. Shop, cloud save, store.

---

## 7. The browser preview is the spec

`tools/webpreview/` builds the entire game into one self-contained HTML file:

```bash
python3 tools/webpreview/build.py
```

Menu, board, route map, album, all 200 levels, all 240 cards, ~420 KB, no network
access. When you are unsure what a screen is meant to do, open it — it is the
reference the Unity build mirrors, and `tools/check.sh` replays all 200 levels
through its JavaScript engine on every commit to keep the two honest.
