// Replays every shipped level's reference solution through the JavaScript
// engine. A third implementation of the rules is a third chance to diverge, so
// this runs in the same gate as the Python and C# checks.
//
//   node tools/webpreview/validate.js

'use strict';

const fs = require('fs');
const path = require('path');
const Engine = require('./engine.js');

const root = path.resolve(__dirname, '../..');
const levelDir = path.join(root, 'Assets/Resources/Levels');

function compact(file) {
  const d = JSON.parse(fs.readFileSync(file, 'utf8'));
  return {
    id: d.id, ch: d.chapter, w: d.grid.w, h: d.grid.h, bays: d.bays,
    bl: d.blocked || [],
    bs: d.buses.map((b) => [b.id, b.color, b.facing, b.capacity || 3,
      b.cells.reduce((a, c) => a.concat(c), [])]),
    q: d.queue.map((p) => [p.color, p.luggage ? 1 : 0]),
    sol: d.solution,
  };
}

const files = fs.readdirSync(levelDir).filter((f) => /^level_\d+\.json$/.test(f)).sort();
if (files.length === 0) {
  console.error('no levels found');
  process.exit(1);
}

let failures = 0;
let hintSteps = 0;

for (const file of files) {
  const raw = compact(path.join(levelDir, file));
  const level = Engine.inflateLevel(raw);
  const name = `level ${String(level.id).padStart(3, '0')}`;

  // 1. The Python-verified solution must replay exactly.
  let state = Engine.initialState(level);
  let ok = true;
  for (const busId of level.solution) {
    const refusal = Engine.canDispatch(level, state, busId);
    if (refusal) {
      console.log(`FAIL ${name}: bus ${busId} not dispatchable (${refusal})`);
      failures++; ok = false; break;
    }
    state = Engine.dispatch(level, state, busId);
  }
  if (!ok) continue;
  if (!Engine.isWon(level, state)) {
    console.log(`FAIL ${name}: reference solution did not win`);
    failures++; continue;
  }

  // 2. The solver must agree the level is winnable from the start.
  const start = Engine.initialState(level);
  if (!Engine.isSolvable(level, start)) {
    console.log(`FAIL ${name}: solver says unsolvable`);
    failures++; continue;
  }

  // 3. Hints must never walk the player into a dead end.
  let walk = start;
  let guard = 0;
  while (!Engine.isWon(level, walk) && guard++ < 200) {
    const busId = Engine.hint(level, walk);
    if (busId === null) {
      console.log(`FAIL ${name}: hint gave up on a winnable position`);
      failures++; break;
    }
    walk = Engine.dispatch(level, walk, busId);
    hintSteps++;
    if (!Engine.isSolvable(level, walk)) {
      console.log(`FAIL ${name}: hint led into a dead end`);
      failures++; break;
    }
  }
}

if (failures) {
  console.log(`\nFAILED: ${failures} problem(s)`);
  process.exit(1);
}
console.log(`all ${files.length} levels pass the JavaScript engine ` +
            `(${hintSteps} hint moves walked)`);
