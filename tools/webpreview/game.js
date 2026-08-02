// Sunny Stop — browser preview UI.
//
// Presentation only; every rule lives in engine.js. Mirrors what the Unity
// prototype does, including the parts that matter most to the design: the free
// rewind offered the instant a move makes a level unwinnable, hints re-solved
// from the current position, and a win screen with no timer and nothing to sell.
//
// Three presentation ideas carry most of the readability:
//   * Passengers are balls packed into one recessed tray, and a boarding ball
//     physically flies out of the tray into a seat inside the bus. Capacity
//     stops being a number and becomes a row of sockets you can count.
//   * A bus is drawn nose-right and the whole element is rotated to face, so
//     one piece of markup covers all four directions and driving off is just
//     `rotate(a) translateX(d)`. Each one carries one big white arrow, so the
//     direction it will leave in is legible without reading anything.
//   * The tray never resizes during a level. It is sized once from the full
//     queue, so the pile shrinks inside a fixed board instead of the whole
//     screen reflowing every time somebody gets on.

'use strict';

(function () {
  const E = window.Engine;
  const DATA = window.SUNNY_DATA;

  // Sampled off the reference screenshot: saturated, high-contrast plastics
  // rather than the muted illustration palette this used to carry.
  const COLORS = {
    red: '#E4433C', blue: '#3D74DE', green: '#3FC155', yellow: '#F0B428',
    purple: '#8B44C6', orange: '#F0842A', pink: '#E85CA8', teal: '#2FBFAE',
  };
  const GLYPHS = {
    red: '★', blue: '●', green: '▲', yellow: '◆',
    purple: '♥', orange: '■', pink: '✿', teal: '▼',
  };
  const ANGLE = { right: 0, down: 90, left: 180, up: 270 };

  // The eight chapters from CONCEPT.md §10, as a route through a day.
  const CHAPTERS = [
    { name: 'Morgen', tint: 'rgba(250, 214, 137, .34)' },
    { name: 'Mittag', tint: 'rgba(150, 199, 236, .34)' },
    { name: 'Abend', tint: 'rgba(243, 166, 118, .34)' },
    { name: 'Nacht', tint: 'rgba(120, 128, 186, .34)' },
    { name: 'Regen', tint: 'rgba(160, 178, 186, .34)' },
    { name: 'Schnee', tint: 'rgba(206, 224, 234, .40)' },
    { name: 'Fest', tint: 'rgba(240, 168, 196, .34)' },
    { name: 'Morgengrauen', tint: 'rgba(246, 197, 150, .34)' },
  ];

  const STORE = 'sunnystop.preview';

  const $ = (s) => document.querySelector(s);
  const el = (tag, cls, text) => {
    const n = document.createElement(tag);
    if (cls) n.className = cls;
    if (text !== undefined) n.textContent = text;
    return n;
  };
  const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

  // ---- persistence ------------------------------------------------------- //

  function load() {
    try { return JSON.parse(localStorage.getItem(STORE)) || {}; } catch (e) { return {}; }
  }
  function save(patch) {
    const s = Object.assign(load(), patch);
    try { localStorage.setItem(STORE, JSON.stringify(s)); } catch (e) { /* private mode */ }
    return s;
  }

  // ---- postcards --------------------------------------------------------- //

  const CARDS = DATA.messages.map(([id, category, tone, text, level]) => ({
    id, category, tone, text, level: level || 0,
  }));

  function categoryFor(ctx) {
    if (ctx.level % 25 === 0) return 'milestone';
    if (ctx.days >= 3) return 'return';
    if (ctx.attempts >= 3) return 'recognition';
    const hour = new Date().getHours();
    if (hour >= 22 || hour < 4) return 'grounding';
    return 'playful';
  }

  function pickCard(ctx) {
    const state = load();
    const tone = state.tone || 'warm';
    if (tone === 'off') return null;

    let category = categoryFor(ctx);
    if (category === 'milestone') {
      const pinned = CARDS.find((c) => c.level === ctx.level);
      if (pinned) return pinned;
      category = 'recognition';
    }
    const recent = new Set((state.recent || []).slice(-60));
    const tiers = [
      (c) => c.level === 0 && c.category === category && c.tone === tone,
      (c) => c.level === 0 && c.category === category,
      (c) => c.level === 0 && c.tone === tone,
      (c) => c.level === 0,
    ];
    for (const match of tiers) {
      const pool = CARDS.filter((c) => match(c) && !recent.has(c.id));
      if (pool.length) return pool[Math.floor(Math.random() * pool.length)];
    }
    const any = CARDS.filter((c) => c.level === 0);
    return any[Math.floor(Math.random() * any.length)];
  }

  // ---- state ------------------------------------------------------------- //

  const app = {
    level: null,
    state: null,
    history: [],
    busy: false,
    done: false,
    cell: 34,
    // Visual bays lag the rules by one animation: they show balls arriving one
    // at a time, while the engine has already resolved the whole cascade.
    bays: [],
    queueAt: 0,
    pending: null,
    tray: null,   // fixed for the whole level; see trayLayout()
  };

  const levelData = (n) => DATA.levels.find((l) => l.id === n);

  function syncVisuals() {
    app.bays = app.state.bays.map((bay) => {
      if (!bay) return null;
      const bus = app.level.byId.get(bay.busId);
      const seats = new Array(bus.capacity).fill(null);
      let taken = bus.capacity - bay.seats;
      for (let i = 0; i < taken; i++) seats[i] = { color: bus.color };
      return { bus, seats };
    });
    app.queueAt = app.state.qi;
  }

  function loadLevel(n) {
    const raw = levelData(n);
    if (!raw) return;

    app.level = E.inflateLevel(raw);
    app.state = E.initialState(app.level);
    app.history = [];
    app.busy = false;
    app.done = false;
    app.tray = null;
    syncVisuals();

    const s = load();
    const attempts = Object.assign({}, s.attempts || {});
    attempts[n] = (attempts[n] || 0) + 1;
    save({ current: n, attempts, highest: Math.max(s.highest || 1, n) });

    hideBanner();
    render();

    const fails = attempts[n] - 1;
    if (fails >= 10) {
      showBanner('Dieses Level darf auch mal liegen bleiben.', [
        { label: 'Überspringen', cls: 'primary', act: nextLevel },
        { label: 'Weiter versuchen', act: hideBanner },
      ]);
    } else if (fails >= 3) {
      showBanner('Der hier ist zäh. Soll ich dir einen Bus zeigen?', [
        { label: 'Ja, gern', cls: 'warm', act: doHint },
        { label: 'Nein danke', act: hideBanner },
      ]);
    }
  }

  /**
   * The lot has to fit whatever the tray, the stands and the dock leave over.
   * Late boards are 9x10, so this is nearly always height-bound, not width-
   * bound - which is exactly the "everything smaller" the reference shows.
   */
  function cellSize() {
    const availW = Math.min(window.innerWidth - 18, 414);
    const byW = Math.floor(availW / app.level.width);
    const chrome = 30                                  // top padding
      + (app.tray ? app.tray.h + 72 : 190)             // tray, plus its deep rim
      + 58 + 30 + 70 + 34;                             // stands, grip, dock, status
    const byH = Math.floor((window.innerHeight - chrome) / app.level.height);
    // Early boards are 5x5 with room to spare, so let the vehicles grow into
    // it rather than leaving a half-empty apron under the stands.
    return Math.max(15, Math.min(64, Math.min(byW, byH)));
  }

  /**
   * Pack the crowd into a hexagonal pile, biggest ball that still fits.
   *
   * Sized once per level from the FULL queue and then frozen: if the tray
   * shrank as the pile emptied, the lot underneath would jump on every board.
   */
  const TRAY_MIN_H = 84;    // so an 18-passenger level still reads as a board
  // Ceiling on ball size. Not a fitting constraint - a level with few
  // passengers could draw them much bigger - but a composition one: big balls
  // make the tray look sparse and steal the height the lot needs. Small balls
  // in a full tray read as MORE crowd, which is the honest impression.
  const TRAY_BALL_MAX = 26;

  function trayLayout(n, width, maxH) {
    for (let d = TRAY_BALL_MAX; d >= 11; d--) {
      // Leave a margin either side, or the ring on the next-to-board ball gets
      // clipped by the tray wall.
      const cols = Math.floor((width - 14) / d);
      if (cols < 4) continue;
      let count = 0, rows = 0;
      while (count < n) { count += (rows % 2 === 0) ? cols : cols - 1; rows++; }
      const h = Math.round(rows * d * 0.84 + d * 0.14 + 6);
      if (h <= maxH) return { d, cols, rows, h: Math.max(TRAY_MIN_H, h), width };
    }
    const d = 11;
    return { d, cols: Math.max(4, Math.floor((width - 6) / d)), rows: 0, h: maxH, width };
  }

  /** Where the k-th still-waiting passenger sits. k = 0 boards next. */
  function traySlot(k, t) {
    let row = 0, base = 0;
    for (;;) {
      const per = (row % 2 === 0) ? t.cols : t.cols - 1;
      if (k < base + per) {
        const off = (row % 2 === 0) ? 0 : t.d / 2;
        return {
          // Odd rows are inset by half a ball - that is what makes it a pile
          // rather than a spreadsheet.
          left: (t.width - t.cols * t.d) / 2 + off + (k - base) * t.d,
          // Rows stack up from the floor of the tray, so the pile drains
          // downwards and the front of the queue is always along the bottom.
          top: t.h - (row + 1) * t.d * 0.84 - 3,
          row,
        };
      }
      base += per; row++;
    }
  }

  // ---- pieces ------------------------------------------------------------ //

  function ballNode(color, bag) {
    const b = el('div', 'ball' + (bag ? ' bag' : ''), GLYPHS[color] || '');
    b.style.setProperty('--ball', COLORS[color] || '#888');
    return b;
  }

  /**
   * A vehicle, always drawn nose-right; the wrapper is rotated to face.
   *
   * In the lot it carries one big white arrow - the direction it will leave in
   * is the only thing you need to read there. Parked at a stand the arrow goes
   * away and the seats grow, because that is where the balls actually land.
   */
  function busBody(bus, seats, opts) {
    const o = opts || {};
    const body = el('div', 'body' + (o.parked ? ' parked' : ''));
    body.style.setProperty('--bus', COLORS[bus.color] || '#888');
    body.style.setProperty('--seat', (o.seat || Math.round(app.cell * 0.22)) + 'px');

    if (!o.parked) {
      body.appendChild(el('div', 'screen'));
      body.appendChild(el('div', 'arrow'));
      // The colour-blind glyph rides at the tail and counter-rotates so it
      // stays upright whichever way the bus faces.
      const sign = el('div', 'sign', GLYPHS[bus.color] || '?');
      if (o.rotate) sign.style.transform = `rotate(${-ANGLE[bus.facing]}deg)`;
      body.appendChild(sign);
    }

    // Six sockets fit across a body in one row. A twelve-seat double-decker
    // gets two rows - which is what a double-decker is, so the shape carries
    // the capacity instead of fighting it.
    const perRow = bus.capacity > 6 ? Math.ceil(bus.capacity / 2) : bus.capacity;
    const row = el('div', 'seats' + (bus.capacity > perRow ? ' twodeck' : ''));
    for (let i = 0; i < bus.capacity; i++) {
      const seat = el('div', 'seat');
      seat.dataset.seat = String(i);
      const filled = seats && seats[i];
      if (filled) {
        if (filled.bag) seat.classList.add('bag');
        else seat.appendChild(ballNode(filled.color));
      }
      row.appendChild(seat);
    }
    row.style.setProperty('--per-row', String(perRow));
    body.appendChild(row);
    return body;
  }

  function busNode(bus) {
    const c = app.cell;
    const xs = bus.cells.map((p) => p[0]);
    const ys = bus.cells.map((p) => p[1]);
    const cx = (Math.min(...xs) + Math.max(...xs) + 1) * c / 2;
    const cy = (Math.min(...ys) + Math.max(...ys) + 1) * c / 2;
    const w = bus.cells.length * c - Math.max(3, Math.round(c * 0.16));
    const h = c - Math.max(3, Math.round(c * 0.16));

    const node = el('button', 'bus');
    node.type = 'button';
    node.dataset.bus = String(bus.id);
    node.setAttribute('aria-label',
      `Bus ${bus.color}, ${bus.capacity} Plätze, Richtung ${bus.facing}`);
    Object.assign(node.style, {
      left: (cx - w / 2) + 'px', top: (cy - h / 2) + 'px',
      width: w + 'px', height: h + 'px',
      transform: `rotate(${ANGLE[bus.facing]}deg)`,
    });
    node.appendChild(busBody(bus, null, { rotate: true }));
    node.addEventListener('click', () => onBusTap(bus.id, node));
    return node;
  }

  // ---- rendering --------------------------------------------------------- //

  function render() {
    // The tray is laid out first: the lot only gets the height it leaves over.
    renderQueue();
    renderBays();
    app.cell = cellSize();
    document.documentElement.style.setProperty('--cell', app.cell + 'px');
    renderLot();

    const remaining = app.level.queue.length - app.queueAt;
    $('#levelChip').textContent = `LEVEL ${app.level.id}`;
    $('#status').textContent =
      `${CHAPTERS[app.level.chapter - 1].name} · ${remaining}/${app.level.queue.length} warten`
      + ` · ${app.level.bays} von ${STANDS} Buchten offen`;
    setDisabled('undo', app.history.length === 0 || app.busy);
  }

  function setDisabled(act, off) {
    document.querySelectorAll(`[data-act="${act}"]`)
      .forEach((n) => { n.disabled = off; });
  }

  function renderLot() {
    const lvl = app.level;
    const c = app.cell;
    const lot = $('#lot');
    lot.innerHTML = '';
    lot.style.width = lvl.width * c + 'px';
    lot.style.height = lvl.height * c + 'px';

    for (const key of lvl.blocked) {
      const [x, y] = key.split(',').map(Number);
      const cone = el('div', 'cone');
      Object.assign(cone.style, {
        left: x * c + 'px', top: y * c + 'px', width: c + 'px', height: c + 'px',
      });
      cone.appendChild(el('i'));
      cone.appendChild(el('b'));
      lot.appendChild(cone);
    }

    for (const bus of lvl.buses) {
      if (app.state.inLot.has(bus.id)) lot.appendChild(busNode(bus));
    }
  }

  const STANDS = 6;   // the terminal always shows six numbered stands

  function renderBays() {
    const wrap = $('#bays');
    wrap.innerHTML = '';
    for (let i = 0; i < STANDS; i++) {
      const inService = i < app.bays.length;
      const bay = inService ? app.bays[i] : null;
      const node = el('div', 'bay'
        + (!inService ? ' closed' : (bay ? '' : ' free')));
      node.dataset.bay = String(i);
      node.appendChild(el('span', 'no', String(i + 1)));
      if (bay) {
        const holder = el('div', 'docked');
        // Big enough sockets that a landing ball is unmistakable.
        holder.appendChild(busBody(bay.bus, bay.seats,
          { parked: true, seat: bay.bus.capacity > 4 ? 8 : 13 }));
        node.appendChild(holder);
      }
      wrap.appendChild(node);
    }
  }

  /**
   * The waiting crowd, as one packed board.
   *
   * Every passenger still waiting is on screen - no "12 more behind" caption to
   * take on trust. They sit in a hexagonal pile that drains from the bottom, so
   * the front of the queue is always the bottom row and the mass above it is
   * genuinely hard to read at a glance, which is the point: the pile used to be
   * a tidy line and the whole future came for free.
   *
   * The jitter that keeps it from looking like a spreadsheet is SEEDED from the
   * level id and the passenger's own queue index - never random at runtime.
   * Everyone playing level 137 sees the same pile, the board stays
   * reproducible, and the solver still answers exactly, which is what the free
   * rewind depends on. It looks loose; it is not.
   */
  function renderQueue() {
    const lvl = app.level;
    const wrap = $('#queue');
    wrap.innerHTML = '';
    const remaining = lvl.queue.length - app.queueAt;

    const width = wrap.clientWidth || (Math.min(window.innerWidth, 432) - 36);
    if (!app.tray || app.tray.width !== width) {
      // Under a quarter of the screen. The tray had a third and the lot came
      // out cramped - vehicles at 32px on the last boards, against a wall of
      // 30px balls above them. The lot is where the decisions happen, so it
      // gets the height.
      app.tray = trayLayout(lvl.queue.length, width,
                            Math.max(90, Math.min(196, window.innerHeight * 0.23)));
    }
    const t = app.tray;
    wrap.style.height = t.h + 'px';

    for (let k = 0; k < remaining; k++) {
      const i = app.queueAt + k;
      const p = lvl.queue[i];
      const rnd = seeded(lvl.id * 7919 + i * 131);
      const slot = traySlot(k, t);

      const size = Math.round(t.d * 1.04);
      const rider = el('div', 'rider' + (k === 0 ? ' next' : ''));
      rider.dataset.index = String(i);
      Object.assign(rider.style, {
        // Just enough jitter to break the grid. More than this and light gaps
        // open between the balls, and the whole tray reads pastel instead of
        // like a mass of coloured plastic.
        left: (slot.left + (t.d - size) / 2 + (rnd() - 0.5) * t.d * 0.06) + 'px',
        top: (slot.top + (rnd() - 0.5) * t.d * 0.06) + 'px',
        zIndex: String(200 - slot.row),
      });
      const ball = ballNode(p.color, p.luggage);
      ball.style.width = size + 'px';
      ball.style.height = size + 'px';
      ball.style.fontSize = Math.round(size * 0.44) + 'px';
      rider.appendChild(ball);
      wrap.appendChild(rider);
    }

    if (remaining === 0) wrap.appendChild(el('div', 'tray-empty', 'alle eingestiegen'));
    $('#queueCount').textContent = String(remaining);
  }

  /** Tiny deterministic PRNG, so the crowd looks scattered but never shifts. */
  function seeded(seed) {
    let s = seed >>> 0;
    return () => {
      s = (s * 1664525 + 1013904223) >>> 0;
      return s / 4294967296;
    };
  }

  // ---- the boarding flight ----------------------------------------------- //

  /**
   * How fast to fly each ball, given how many are boarding on this move.
   *
   * A twelve-seat double-decker fills in one cascade. At the pace that reads
   * beautifully for three balls that is three seconds of watching, which is
   * where a nice animation turns into a wait. Long cascades accelerate; short
   * ones keep the original timing, because they are the ones you actually see.
   */
  function boardingPace(count) {
    return Math.max(0.38, Math.min(1, 1 - (count - 3) * 0.055));
  }

  /** Flies the head ball from the platform into a seat, then fills the seat. */
  async function flyIntoSeat(bayIndex, seatIndex, passenger, pace) {
    const p = pace || 1;
    const rider = document.querySelector(`.rider[data-index="${app.queueAt}"] .ball`);
    const seat = document.querySelector(`.bay[data-bay="${bayIndex}"] .seat[data-seat="${seatIndex}"]`);
    if (!rider || !seat) return;

    const from = rider.getBoundingClientRect();
    const to = seat.getBoundingClientRect();

    const flyer = ballNode(passenger.color, false);
    flyer.classList.add('flyer');
    Object.assign(flyer.style, {
      left: from.left + 'px', top: from.top + 'px',
      width: from.width + 'px', height: from.height + 'px',
      transition: `transform ${Math.round(380 * p)}ms cubic-bezier(.34, .8, .4, 1)`,
    });
    document.body.appendChild(flyer);
    rider.style.opacity = '0';

    // A little hop on the way in - the arc is what makes it feel cheerful.
    const dx = to.left + (to.width - from.width) / 2 - from.left;
    const dy = to.top + (to.height - from.height) / 2 - from.top;
    await sleep(16);
    flyer.style.transform = `translate(${dx * 0.55}px, ${dy - 22}px) scale(1.08)`;
    await sleep(Math.round(110 * p));
    flyer.style.transform = `translate(${dx}px, ${dy}px) scale(${to.width / from.width})`;
    await sleep(Math.round(120 * p));

    flyer.remove();
    app.bays[bayIndex].seats[seatIndex] = { color: passenger.color };
    if (passenger.luggage) app.bays[bayIndex].seats[seatIndex + 1] = { bag: true };
    app.queueAt++;
    renderBays();
    renderQueue();
  }

  async function departBay(bayIndex) {
    const bay = document.querySelector(`.bay[data-bay="${bayIndex}"] .docked`);
    if (bay) {
      bay.style.transition = 'transform 380ms cubic-bezier(.4,0,.7,1), opacity 380ms ease';
      bay.style.transform = 'translateX(140%)';
      bay.style.opacity = '0';
      await sleep(360);
    }
    app.bays[bayIndex] = null;
    renderBays();
  }

  // ---- interaction ------------------------------------------------------- //

  function onBusTap(busId, node) {
    if (app.done) return;
    if (app.busy) {
      // Queue the tap rather than swallowing it: during a long boarding cascade
      // an ignored tap just feels like the game is broken.
      app.pending = busId;
      return;
    }
    const refusal = E.canDispatch(app.level, app.state, busId);
    if (refusal) {
      node.classList.remove('refused');
      void node.offsetWidth;
      node.classList.add('refused');
      if (refusal === 'nobay') {
        showBanner('Alle Buchten sind belegt.', [{ label: 'Verstanden', act: hideBanner }]);
      }
      return;
    }
    void doMove(busId, node);
  }

  async function doMove(busId, node) {
    app.busy = true;
    app.history.push(app.state);
    hideBanner();
    setDisabled('undo', true);

    const bus = app.level.byId.get(busId);
    const events = [];
    const next = E.dispatch(app.level, app.state, busId, events);

    // 1. Drive out of the lot, along the way the bus is already facing.
    const travel = (bus.exitPath.length + 1.5) * app.cell;
    node.style.transform = `rotate(${ANGLE[bus.facing]}deg) translateX(${travel}px)`;
    await sleep(300);

    app.state = next;
    renderLot();

    // 2. Arrive in the bay with every seat still empty.
    const dispatched = events.find((e) => e.kind === 'dispatch');
    app.bays[dispatched.bay] = { bus, seats: new Array(bus.capacity).fill(null) };
    renderBays();
    await sleep(140);

    // 3. Board the balls one at a time, so a cascade reads as a cascade.
    const pace = boardingPace(events.filter((e) => e.kind === 'board').length);
    for (const evt of events) {
      if (evt.kind === 'board') {
        const passenger = app.level.queue[evt.queueIndex];
        const bay = app.bays[evt.bay];
        const seatIndex = bay ? bay.seats.findIndex((s) => s === null) : -1;
        if (seatIndex >= 0) await flyIntoSeat(evt.bay, seatIndex, passenger, pace);
        else { app.queueAt++; renderQueue(); }
      } else if (evt.kind === 'depart') {
        await sleep(120);
        await departBay(evt.bay);
      }
    }

    syncVisuals();
    app.busy = false;
    render();
    afterMove();

    // Play back a tap made while the balls were still boarding.
    const queued = app.pending;
    app.pending = null;
    if (queued != null && !app.done) {
      const next = document.querySelector(`.bus[data-bus="${queued}"]`);
      if (next && !E.canDispatch(app.level, app.state, queued)) doMove(queued, next);
    }
  }

  function afterMove() {
    if (E.isWon(app.level, app.state)) return completeLevel();
    if (E.isDead(app.level, app.state)) return offerRewind('Hier geht es nicht mehr weiter.');
    // The valuable check: legal moves remain, but none of them can win any more.
    if (!E.isSolvable(app.level, app.state)) {
      offerRewind('Mit diesem Zug ist das Level nicht mehr zu schaffen.');
    }
  }

  function offerRewind(reason) {
    showBanner(reason, [
      { label: 'Zug zurücknehmen', cls: 'primary', act: undo },
      { label: 'Neu starten', act: () => loadLevel(app.level.id) },
    ]);
  }

  function undo() {
    if (!app.history.length || app.busy) return;
    app.state = app.history.pop();
    app.done = false;
    syncVisuals();
    hideBanner();
    render();
  }

  function doHint() {
    if (app.busy || app.done) return;
    hideBanner();
    const busId = E.hint(app.level, app.state);
    if (busId === null) return offerRewind('Von hier aus geht es leider nicht mehr.');
    const node = document.querySelector(`.bus[data-bus="${busId}"]`);
    if (node) {
      node.classList.add('hinted');
      setTimeout(() => node.classList.remove('hinted'), 2600);
    }
  }

  // ---- win --------------------------------------------------------------- //

  function completeLevel() {
    app.done = true;
    const s = load();
    const attempts = (s.attempts || {})[app.level.id] || 1;
    const lastPlayed = s.lastPlayed ? new Date(s.lastPlayed) : null;
    const days = lastPlayed
      ? Math.max(0, Math.round((Date.now() - lastPlayed.getTime()) / 86400000)) : 0;

    const cleared = Object.assign({}, s.attempts || {});
    delete cleared[app.level.id];
    const beaten = (s.beaten || []).concat(app.level.id);
    save({ attempts: cleared, beaten, lastPlayed: new Date().toISOString() });

    const card = pickCard({ level: app.level.id, attempts, days });
    if (!card) return nextLevel();
    save({ recent: (load().recent || []).concat(card.id).slice(-120) });
    setTimeout(() => showPostcard(card), 640);
  }

  function showPostcard(card) {
    const layer = el('div', 'postcard-layer');
    const cardNode = el('div', 'postcard');

    const quote = el('q');
    quote.textContent = card.text;
    cardNode.appendChild(quote);
    cardNode.appendChild(el('div', 'meta',
      `Level ${app.level.id} · ${new Date().toLocaleDateString('de-DE')}`));

    const row = el('div', 'row');
    const keep = el('button', 'btn warm', 'Behalten');
    keep.addEventListener('click', (ev) => {
      ev.stopPropagation();
      const album = load().album || [];
      // Store when and where it was earned, not just which card. "Level 12,
      // 2. August" is something the player recognises; the card's internal
      // category is not.
      if (!album.some((e) => (e && typeof e === 'object' ? e.id : e) === card.id)) {
        save({ album: album.concat({ id: card.id, level: app.level.id, at: Date.now() }) });
      }
      keep.textContent = 'Im Album ✓';
      keep.disabled = true;
    });
    const next = el('button', 'btn primary', 'Weiter');
    next.addEventListener('click', (ev) => { ev.stopPropagation(); close(); });
    row.append(keep, next);
    cardNode.appendChild(row);

    layer.addEventListener('click', close);   // tapping anywhere continues
    cardNode.addEventListener('click', (ev) => ev.stopPropagation());
    layer.appendChild(cardNode);
    document.body.appendChild(layer);

    function close() { layer.remove(); nextLevel(); }
  }

  function nextLevel() {
    const ids = DATA.levels.map((l) => l.id);
    const i = ids.indexOf(app.level.id);
    loadLevel(i >= 0 && i + 1 < ids.length ? ids[i + 1] : ids[0]);
  }

  // ---- banner ------------------------------------------------------------ //

  function showBanner(text, actions) {
    const b = $('#banner');
    b.innerHTML = '';
    b.hidden = false;
    b.appendChild(el('p', null, text));
    const row = el('div', 'row');
    actions.forEach((a) => {
      const btn = el('button', 'btn ' + (a.cls || ''), a.label);
      btn.addEventListener('click', a.act);
      row.appendChild(btn);
    });
    b.appendChild(row);
  }
  function hideBanner() { $('#banner').hidden = true; }

  // ---- the route map ----------------------------------------------------- //

  function showMap() {
    const saved = load();
    const beaten = new Set(saved.beaten || []);
    const current = app.level ? app.level.id : 1;

    const layer = el('div', 'map-layer');
    const head = el('div', 'map-head');
    head.appendChild(el('h2', null, 'Linie 200'));
    const close = el('button', 'btn', 'Zurück');
    close.addEventListener('click', () => layer.remove());
    head.appendChild(close);
    layer.appendChild(head);

    const width = Math.min(window.innerWidth, 520);
    const stopGap = 56;          // vertical distance between stops
    const amplitude = Math.min(width * 0.30, 132);

    // Deterministic scenery, so the route looks like a place rather than a
    // diagram - and looks the same every time you open it.
    const rng = (seed) => () => (seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff;

    CHAPTERS.forEach((chapter, ci) => {
      const levels = DATA.levels.filter(
        (l) => l.id > ci * 25 && l.id <= (ci + 1) * 25);
      if (!levels.length) return;

      const section = el('div', 'chapter');
      section.style.setProperty('--ch', chapter.tint);
      const band = el('div', 'chapter-band');
      band.appendChild(el('b', null, `${ci + 1}. ${chapter.name}`));
      band.appendChild(el('span', null,
        `Level ${ci * 25 + 1}–${(ci + 1) * 25}`));
      section.appendChild(band);

      const canvas = el('div', 'chapter-canvas');
      const height = levels.length * stopGap + 40;
      canvas.style.height = height + 'px';

      // The road: a smooth serpentine through every stop in the chapter.
      const pts = levels.map((l, i) => [
        width / 2 + Math.sin(i * 0.94) * amplitude,
        24 + i * stopGap,
      ]);
      const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
      svg.setAttribute('viewBox', `0 0 ${width} ${height}`);
      svg.setAttribute('preserveAspectRatio', 'none');
      const NS = 'http://www.w3.org/2000/svg';

      // Scenery first, so the road always draws on top of it.
      const rand = rng(ci * 7919 + 13);
      const scenery = document.createElementNS(NS, 'g');
      scenery.setAttribute('opacity', '0.5');
      for (let k = 0; k < Math.round(height / 46); k++) {
        const side = rand() < 0.5 ? -1 : 1;
        const x = width / 2 + side * (amplitude + 26 + rand() * (width * 0.22));
        const y = 30 + rand() * (height - 60);
        if (x < 14 || x > width - 14) continue;
        if (rand() < 0.55) {
          // a tree: trunk plus canopy
          const trunk = document.createElementNS(NS, 'rect');
          trunk.setAttribute('x', String(x - 1.5)); trunk.setAttribute('y', String(y));
          trunk.setAttribute('width', '3'); trunk.setAttribute('height', '9');
          trunk.setAttribute('fill', 'rgba(90,70,50,.55)');
          const top = document.createElementNS(NS, 'circle');
          top.setAttribute('cx', String(x)); top.setAttribute('cy', String(y - 3));
          top.setAttribute('r', String(7 + rand() * 5));
          top.setAttribute('fill', 'rgba(70,130,90,.45)');
          scenery.append(trunk, top);
        } else {
          // a little house with a roof
          const w = 16 + rand() * 14, h = 13 + rand() * 12;
          const house = document.createElementNS(NS, 'rect');
          house.setAttribute('x', String(x - w / 2)); house.setAttribute('y', String(y));
          house.setAttribute('width', String(w)); house.setAttribute('height', String(h));
          house.setAttribute('rx', '2');
          house.setAttribute('fill', 'rgba(150,140,125,.42)');
          const roof = document.createElementNS(NS, 'path');
          roof.setAttribute('d',
            `M ${x - w / 2 - 2} ${y} L ${x} ${y - 8} L ${x + w / 2 + 2} ${y} Z`);
          roof.setAttribute('fill', 'rgba(150,95,75,.5)');
          scenery.append(house, roof);
        }
      }
      svg.appendChild(scenery);

      let d = `M ${pts[0][0]} ${pts[0][1]}`;
      for (let i = 1; i < pts.length; i++) {
        const [px, py] = pts[i - 1];
        const [x, y] = pts[i];
        const my = (py + y) / 2;
        d += ` C ${px} ${my}, ${x} ${my}, ${x} ${y}`;
      }
      for (const [cls, w, dash] of [['road', 15, null], ['centre', 2, '9 11']]) {
        const p = document.createElementNS('http://www.w3.org/2000/svg', 'path');
        p.setAttribute('d', d);
        p.setAttribute('fill', 'none');
        p.setAttribute('stroke-linecap', 'round');
        p.setAttribute('stroke-width', String(w));
        p.setAttribute('stroke', cls === 'road' ? 'rgba(74,78,76,.55)' : 'rgba(255,236,178,.75)');
        if (dash) p.setAttribute('stroke-dasharray', dash);
        svg.appendChild(p);
      }
      canvas.appendChild(svg);

      levels.forEach((l, i) => {
        const [x, y] = pts[i];
        const milestone = l.id % 25 === 0;
        const stop = el('button', 'stop'
          + (milestone ? ' milestone' : '')
          + (beaten.has(l.id) ? ' done' : '')
          + (l.id === current ? ' current' : ''));
        stop.type = 'button';
        stop.textContent = String(l.id);
        stop.title = `Level ${l.id} · Schwierigkeit ${Math.round(l.mds)}`;
        stop.style.left = x + 'px';
        stop.style.top = y + 'px';
        stop.addEventListener('click', () => { layer.remove(); loadLevel(l.id); });
        canvas.appendChild(stop);

        if (l.id === current) {
          const token = el('div', 'stop-token');
          token.style.left = x + 'px';
          token.style.top = y + 'px';
          token.innerHTML =
            '<svg viewBox="0 0 30 20" width="30" height="20">' +
            '<rect x="1" y="3" width="28" height="13" rx="4" fill="#E9A33F"/>' +
            '<rect x="20" y="6" width="7" height="6" rx="2" fill="#DCEAF2"/>' +
            '<circle cx="8" cy="17" r="2.6" fill="#23262A"/>' +
            '<circle cx="22" cy="17" r="2.6" fill="#23262A"/></svg>';
          canvas.appendChild(token);
        }
      });

      section.appendChild(canvas);
      layer.appendChild(section);
    });

    layer.appendChild(el('div', 'map-legend',
      'Jede Haltestelle ist ein Level. Große Haltestellen sind Meilensteine — alle 25 Level.'));
    document.body.appendChild(layer);

    const marker = layer.querySelector('.stop.current');
    if (marker) marker.scrollIntoView({ block: 'center' });
  }

  // ---- the destination blind --------------------------------------------- //

  /**
   * Renders text as an LED dot matrix on a canvas.
   *
   * The title is not set in a typeface, it is *rastered* - which is what a real
   * departure board does, and what makes the menu read as a piece of terminal
   * equipment rather than a title screen with a glow filter. Drawn rather than
   * hand-authored as SVG so the text can be any string and any width.
   *
   * Works by rendering the text into a small offscreen canvas at 4x the dot
   * grid, then averaging each 4x4 block into one lamp. Averaging rather than
   * point-sampling is what keeps diagonals from breaking up.
   */
  function drawBlind(canvas, text, progress) {
    const pitch = 5;                       // CSS px between lamp centres
    const w = canvas.clientWidth || 300;
    const h = canvas.clientHeight || 88;
    if (!w || !h) return;

    const dpr = Math.min(2, window.devicePixelRatio || 1);
    canvas.width = Math.round(w * dpr);
    canvas.height = Math.round(h * dpr);
    const ctx = canvas.getContext('2d');
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, w, h);

    const cols = Math.floor((w - 10) / pitch);
    const rows = Math.floor((h - 10) / pitch);
    if (cols < 8 || rows < 5) return;

    const S = 4;
    const off = document.createElement('canvas');
    off.width = cols * S; off.height = rows * S;
    const oc = off.getContext('2d');
    oc.fillStyle = '#000';
    oc.fillRect(0, 0, off.width, off.height);
    oc.fillStyle = '#fff';
    oc.textAlign = 'center';
    oc.textBaseline = 'middle';

    const face = '900 SIZEpx ui-sans-serif, "SF Pro Display", "Segoe UI", Roboto, Arial, sans-serif';
    let size = rows * S;
    while (size > 6) {
      oc.font = face.replace('SIZE', String(size));
      const m = oc.measureText(text);
      if (m.width <= off.width - S * 2 && size <= rows * S * 0.92) break;
      size -= 1;
    }
    oc.font = face.replace('SIZE', String(size));
    oc.fillText(text, off.width / 2, off.height / 2 + S * 0.3);
    const px = oc.getImageData(0, 0, off.width, off.height).data;

    const x0 = (w - cols * pitch) / 2 + pitch / 2;
    const y0 = (h - rows * pitch) / 2 + pitch / 2;
    const lit = Math.max(0, Math.min(1, progress === undefined ? 1 : progress));

    for (let cy = 0; cy < rows; cy++) {
      for (let cx = 0; cx < cols; cx++) {
        let sum = 0;
        for (let sy = 0; sy < S; sy++) {
          const rowBase = ((cy * S + sy) * off.width + cx * S) * 4;
          for (let sx = 0; sx < S; sx++) sum += px[rowBase + sx * 4];
        }
        const on = sum / (S * S) > 118 && cx < cols * lit;
        const x = x0 + cx * pitch;
        const y = y0 + cy * pitch;

        // Unlit lamps are drawn too: the dark grid is what says "this is a
        // board with lamps on it" rather than "this is glowing text".
        ctx.beginPath();
        ctx.arc(x, y, on ? pitch * 0.38 : pitch * 0.16, 0, Math.PI * 2);
        ctx.shadowBlur = on ? 7 : 0;
        ctx.shadowColor = 'rgba(255,179,0,.85)';
        ctx.fillStyle = on ? '#FFC22E' : 'rgba(255,200,120,.075)';
        ctx.fill();
      }
    }
    ctx.shadowBlur = 0;
  }

  /** Wipes the board on from the left, once, like a sign powering up. */
  function powerOnBlind(canvas, text) {
    const reduce = window.matchMedia
      && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    if (reduce) { drawBlind(canvas, text, 1); return; }
    const started = performance.now();
    const step = (now) => {
      const t = Math.min(1, (now - started) / 620);
      drawBlind(canvas, text, t);
      if (t < 1) requestAnimationFrame(step);
    };
    requestAnimationFrame(step);
  }

  // ---- start menu -------------------------------------------------------- //

  const TONES = [
    ['warm', 'WARM'],
    ['playful', 'VERSPIELT'],
    ['off', 'AUS'],
  ];

  function showMenu() {
    const saved = load();
    const beaten = new Set(saved.beaten || []);
    const album = saved.album || [];
    const resume = Math.min(200, Math.max(1, saved.current || 1));
    // Not `!saved.current`: boot() builds the board behind the menu, and that
    // writes `current` before this ever runs. "Never won anything, still on
    // the first level" is the honest test for a first visit.
    const isNew = beaten.size === 0 && resume === 1;

    const layer = el('div', 'menu-layer');
    layer.appendChild(el('div', 'menu-horizon'));

    // Three groups, so the layer's space-between has something to distribute:
    // the sign up top, the controls in the middle, progress on the floor.
    const top = el('div', 'menu-top');
    const mid = el('div', 'menu-mid');

    // --- the blind ---
    const box = el('div', 'blind-box');
    const screen = el('canvas', 'blind-screen');
    screen.setAttribute('role', 'img');
    screen.setAttribute('aria-label', 'Sunny Stop');
    box.appendChild(screen);
    top.appendChild(box);

    const strap = el('div', 'blind-strap');
    strap.appendChild(el('span', null, 'LINIE 200'));
    strap.appendChild(el('b', null, 'TERMINAL · BETRIEB'));
    top.appendChild(strap);

    // Where you are on the route, from the player's own save.
    const chapter = Math.min(8, Math.floor((resume - 1) / 25) + 1);
    const milestone = Math.min(200, Math.ceil(resume / 25) * 25);
    const brief = el('div', 'menu-brief');
    for (const [label, value] of [
      ['ABSCHNITT', `${chapter} · ${CHAPTERS[chapter - 1].name}`],
      ['NÄCHSTER MEILENSTEIN', `Level ${milestone}`],
    ]) {
      const cell = el('div');
      cell.appendChild(el('small', null, label));
      cell.appendChild(el('b', null, value));
      brief.appendChild(cell);
    }
    top.appendChild(brief);
    layer.appendChild(top);

    // --- the one obvious thing to do ---
    const go = el('button', 'menu-go');
    go.type = 'button';
    const mark = el('div', 'go-mark');
    mark.innerHTML = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M8 5v14l11-7z"/></svg>';
    go.appendChild(mark);
    const goText = el('div', 'go-text');
    goText.appendChild(el('span', 'go-kicker', isNew ? 'ERSTE FAHRT' : 'ABFAHRT'));
    goText.appendChild(el('span', 'go-title',
      isNew ? 'Losfahren' : `Weiter · Level ${resume}`));
    go.appendChild(goText);
    go.addEventListener('click', () => { layer.remove(); loadLevel(resume); });
    mid.appendChild(go);

    // --- secondary destinations ---
    const rows = el('div', 'menu-rows');

    rows.appendChild(menuRow('200', 'Alle Haltestellen',
      `${beaten.size} GELÖST`, () => { layer.remove(); showMap(); }));

    rows.appendChild(menuRow(String(album.length).padStart(3, '0'), 'Postkarten-Album',
      album.length ? 'ÖFFNEN' : 'NOCH LEER',
      () => showAlbum(), album.length === 0));

    const toneRow = menuRow('TON', 'Worte nach dem Sieg', toneLabel(saved.tone), null);
    toneRow.addEventListener('click', () => {
      const i = TONES.findIndex((t) => t[0] === (load().tone || 'warm'));
      const next = TONES[(i + 1) % TONES.length];
      save({ tone: next[0] });
      toneRow.querySelector('.row-note').textContent = next[1];
    });
    rows.appendChild(toneRow);
    mid.appendChild(rows);
    layer.appendChild(mid);

    // --- progress ---
    const status = el('div', 'menu-status');
    const meter = el('div', 'menu-meter');
    const fill = el('i');
    fill.style.width = '0%';
    meter.appendChild(fill);
    status.appendChild(meter);
    const legend = el('div', 'menu-legend');
    legend.appendChild(el('span', null, `${beaten.size} / 200 GELÖST`));
    legend.appendChild(el('span', null, `${album.length} POSTKARTEN`));
    status.appendChild(legend);
    layer.appendChild(status);

    document.body.appendChild(layer);
    powerOnBlind(screen, 'SUNNY STOP');
    requestAnimationFrame(() => {
      fill.style.width = Math.max(1.5, (beaten.size / 200) * 100) + '%';
    });

    const onResize = () => drawBlind(screen, 'SUNNY STOP', 1);
    window.addEventListener('resize', onResize);
    const observer = new MutationObserver(() => {
      if (!document.body.contains(layer)) {
        window.removeEventListener('resize', onResize);
        observer.disconnect();
      }
    });
    observer.observe(document.body, { childList: true });
  }

  function toneLabel(tone) {
    const found = TONES.find((t) => t[0] === (tone || 'warm'));
    return found ? found[1] : 'WARM';
  }

  function menuRow(no, name, note, act, muted) {
    const row = el('button', 'menu-row' + (muted ? ' is-off' : ''));
    row.type = 'button';
    row.appendChild(el('span', 'row-no', no));
    row.appendChild(el('span', 'row-name', name));
    row.appendChild(el('span', 'row-note', note));
    if (act) row.addEventListener('click', act);
    return row;
  }

  // ---- album ------------------------------------------------------------- //

  function showAlbum() {
    const kept = load().album || [];
    const layer = el('div', 'album-layer');

    const head = el('div', 'album-head');
    head.appendChild(el('h2', null, 'POSTKARTEN'));
    const back = el('button', 'album-back', 'Zurück');
    back.type = 'button';
    back.addEventListener('click', () => layer.remove());
    head.appendChild(back);
    layer.appendChild(head);

    const grid = el('div', 'album-grid');
    if (!kept.length) {
      grid.appendChild(el('div', 'album-empty',
        'Noch keine Karte behalten. Nach einem gewonnenen Level auf „Behalten“ tippen — dann liegt sie hier.'));
    }
    // Newest first: the one you just kept is the one you want to re-read.
    for (const entry of kept.slice().reverse()) {
      // Older saves stored a bare id; both shapes have to keep working or a
      // returning player loses their album. The type test is explicit on
      // purpose: `someString.at` is String.prototype.at, which is truthy, so a
      // duck-typed check reads every legacy entry as a dated record and prints
      // "Invalid Date".
      const record = entry && typeof entry === 'object' ? entry : null;
      const id = record ? record.id : entry;
      const card = CARDS.find((c) => c.id === id);
      if (!card) continue;
      const node = el('div', 'album-card');
      const quote = el('q');
      quote.textContent = card.text;
      node.appendChild(quote);

      const parts = [];
      if (record && record.level) parts.push(`Level ${record.level}`);
      if (record && record.at) {
        parts.push(new Date(record.at).toLocaleDateString('de-DE',
          { day: 'numeric', month: 'long', year: 'numeric' }));
      }
      node.appendChild(el('span', 'who', parts.join(' · ') || 'Behalten'));
      grid.appendChild(node);
    }
    layer.appendChild(grid);
    document.body.appendChild(layer);
  }

  // ---- boot -------------------------------------------------------------- //

  // Two controls appear twice - undo in the floating HUD and again in the dock,
  // as in the reference - so they are wired by intent rather than by id.
  const ACTIONS = {
    hint: doHint,
    undo: undo,
    restart: () => loadLevel(app.level.id),
    map: showMap,
    menu: showMenu,
  };

  function boot() {
    document.querySelectorAll('[data-act]').forEach((node) => {
      const act = ACTIONS[node.dataset.act];
      if (act) node.addEventListener('click', act);
    });
    window.addEventListener('resize', () => {
      if (!app.level || app.busy) return;
      app.tray = null;                   // re-pack the pile for the new width
      render();
    });
    // The board is built behind the menu, so "Weiter" is instant rather than a
    // second load screen.
    loadLevel(load().current || 1);
    showMenu();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', boot);
  } else {
    boot();
  }
})();
