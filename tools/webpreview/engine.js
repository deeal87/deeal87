// Sunny Stop — rules engine, JavaScript port.
//
// A third implementation of the same rules, mirroring tools/leveltool/rules.py
// and Assets/Scripts/Core. It exists so the game can be played in a browser
// before Unity is set up, which is the only way to answer the one question the
// whole project turns on: is it fun?
//
// Because a third copy of the rules is a third chance to diverge, validate.js
// replays every shipped level's reference solution through this engine, and
// that runs in the same gate as the Python and C# checks.
//
// No DOM, no globals: usable from Node and from the page alike.

'use strict';

const DIRECTIONS = {
  up: [0, -1],
  down: [0, 1],
  left: [-1, 0],
  right: [1, 0],
};

/** Expands the compact level format into something with named fields. */
function inflateLevel(raw) {
  const buses = raw.bs.map(([id, color, facing, capacity, flat]) => {
    const cells = [];
    for (let i = 0; i < flat.length; i += 2) cells.push([flat[i], flat[i + 1]]);
    return { id, color, facing, capacity, cells };
  });

  const level = {
    id: raw.id,
    chapter: raw.ch,
    width: raw.w,
    height: raw.h,
    bays: raw.bays,
    blocked: raw.bl.map(([x, y]) => `${x},${y}`),
    buses,
    queue: raw.q.map(([color, luggage]) => ({ color, luggage: !!luggage, seats: luggage ? 2 : 1 })),
    solution: raw.sol,
  };

  const blockedSet = new Set(level.blocked);
  level.isBlocked = (x, y) => blockedSet.has(`${x},${y}`);
  level.byId = new Map(buses.map((b) => [b.id, b]));

  // Exit path: the cells a bus crosses on its way off the grid, nearest first.
  for (const bus of buses) {
    const [dx, dy] = DIRECTIONS[bus.facing];
    let front = bus.cells[0];
    let best = -Infinity;
    for (const c of bus.cells) {
      const score = c[0] * dx + c[1] * dy;
      if (score > best) { best = score; front = c; }
    }
    const path = [];
    let x = front[0] + dx;
    let y = front[1] + dy;
    while (x >= 0 && x < level.width && y >= 0 && y < level.height) {
      path.push([x, y]);
      x += dx;
      y += dy;
    }
    bus.exitPath = path;
    bus.front = front;
  }

  return level;
}

function initialState(level) {
  const state = {
    inLot: new Set(level.buses.map((b) => b.id)),
    bays: new Array(level.bays).fill(null),
    qi: 0,
    moves: 0,
  };
  return resolveBoarding(level, state);
}

function cloneState(s) {
  return {
    inLot: new Set(s.inLot),
    bays: s.bays.map((b) => (b ? { ...b } : null)),
    qi: s.qi,
    moves: s.moves,
  };
}

/** Board the head of the queue repeatedly until nothing more can board. */
function resolveBoarding(level, state, events) {
  const next = cloneState(state);
  let changed = true;
  while (changed && next.qi < level.queue.length) {
    changed = false;
    const head = level.queue[next.qi];
    for (let i = 0; i < next.bays.length; i++) {
      const bay = next.bays[i];
      if (!bay) continue;
      if (bay.color !== head.color || bay.seats < head.seats) continue;

      bay.seats -= head.seats;
      const busId = bay.busId;
      if (events) events.push({ kind: 'board', bay: i, queueIndex: next.qi, busId, seatsLeft: bay.seats });
      if (bay.seats === 0) {
        next.bays[i] = null;
        if (events) events.push({ kind: 'depart', bay: i, busId });
      }
      next.qi++;
      changed = true;
      break;
    }
  }
  return next;
}

function isPathClear(level, state, busId) {
  if (!state.inLot.has(busId)) return false;
  const bus = level.byId.get(busId);
  const occupied = new Set();
  for (const id of state.inLot) {
    if (id === busId) continue;
    for (const [x, y] of level.byId.get(id).cells) occupied.add(`${x},${y}`);
  }
  for (const [x, y] of bus.exitPath) {
    if (level.isBlocked(x, y)) return false;
    if (occupied.has(`${x},${y}`)) return false;
  }
  return true;
}

function hasFreeBay(state) {
  return state.bays.some((b) => b === null);
}

/** Why a tap was refused — drives the feedback the player sees. */
function canDispatch(level, state, busId) {
  if (isWon(level, state)) return 'won';
  if (!state.inLot.has(busId)) return 'gone';
  if (!isPathClear(level, state, busId)) return 'blocked';
  if (!hasFreeBay(state)) return 'nobay';
  return null;
}

function legalMoves(level, state) {
  if (isWon(level, state) || !hasFreeBay(state)) return [];
  const moves = [];
  for (const id of state.inLot) {
    if (isPathClear(level, state, id)) moves.push(id);
  }
  return moves.sort((a, b) => a - b);
}

/** Dispatch a bus into the leftmost free bay, then resolve boarding. */
function dispatch(level, state, busId, events) {
  const refusal = canDispatch(level, state, busId);
  if (refusal) throw new Error(`cannot dispatch bus ${busId}: ${refusal}`);

  const bus = level.byId.get(busId);
  const next = cloneState(state);
  next.inLot.delete(busId);
  next.moves++;

  const bayIndex = next.bays.findIndex((b) => b === null);
  next.bays[bayIndex] = { busId, color: bus.color, seats: bus.capacity };
  if (events) events.push({ kind: 'dispatch', bay: bayIndex, busId });

  return resolveBoarding(level, next, events);
}

function isWon(level, state) {
  return state.qi >= level.queue.length;
}

function isDead(level, state) {
  return !isWon(level, state) && legalMoves(level, state).length === 0;
}

function stateKey(state) {
  const lot = [...state.inLot].sort((a, b) => a - b).join('.');
  const bays = state.bays.map((b) => (b ? `${b.busId}:${b.seats}` : '-')).join('|');
  return `${lot}/${bays}/${state.qi}`;
}

/**
 * Can the level still be won from here? Exhaustive with memoisation — the
 * boards are small enough (worst case under 9000 reachable states) for this to
 * be instant, which is what makes both the hint button and the "that move lost
 * it" rewind offer possible at all.
 */
function isSolvable(level, state, memo) {
  memo = memo || new Map();
  if (isWon(level, state)) return true;
  const key = stateKey(state);
  const cached = memo.get(key);
  if (cached !== undefined) return cached;

  memo.set(key, false); // guards against revisiting inside the current branch
  for (const busId of legalMoves(level, state)) {
    if (isSolvable(level, dispatch(level, state, busId), memo)) {
      memo.set(key, true);
      return true;
    }
  }
  return false;
}

/** One good next move, or null. Prefers a move that gets somebody aboard now. */
function hint(level, state) {
  const memo = new Map();
  let best = null;
  let bestProgress = -1;
  for (const busId of legalMoves(level, state)) {
    const next = dispatch(level, state, busId);
    if (!isSolvable(level, next, memo)) continue;
    const progress = next.qi - state.qi;
    if (progress > bestProgress) {
      bestProgress = progress;
      best = busId;
    }
  }
  return best;
}

const Engine = {
  DIRECTIONS,
  inflateLevel,
  initialState,
  resolveBoarding,
  dispatch,
  canDispatch,
  legalMoves,
  isPathClear,
  isWon,
  isDead,
  isSolvable,
  hint,
  stateKey,
};

if (typeof module !== 'undefined' && module.exports) module.exports = Engine;
if (typeof window !== 'undefined') window.Engine = Engine;
