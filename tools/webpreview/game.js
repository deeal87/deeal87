// Sunny Stop — browser preview UI.
//
// Presentation only; every rule lives in engine.js. Mirrors what the Unity
// prototype does, including the parts that matter most to the design: the free
// rewind offered the instant a move makes a level unwinnable, hints re-solved
// from the current position, and a win screen with no timer and nothing to sell.
//
// Two presentation ideas carry most of the readability:
//   * Passengers are balls, and a boarding ball physically flies from the
//     platform into a seat inside the bus. Capacity stops being a number and
//     becomes a row of sockets you can count at a glance.
//   * A bus is drawn nose-right and the whole element is rotated to face, so
//     one piece of markup covers all four directions and driving off is just
//     `rotate(a) translateX(d)`.

'use strict';

(function () {
  const E = window.Engine;
  const DATA = window.SUNNY_DATA;

  const COLORS = {
    red: '#D9534F', blue: '#4A7FBF', green: '#5AA75A', yellow: '#D9A404',
    purple: '#8E6FC4', orange: '#DE8843', pink: '#DB8FA8', teal: '#3FA6A0',
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

  const MAX_QUEUE_SHOWN = 12;
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
    cell: 44,
    // Visual bays lag the rules by one animation: they show balls arriving one
    // at a time, while the engine has already resolved the whole cascade.
    bays: [],
    queueAt: 0,
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

  function cellSize() {
    const avail = Math.min(window.innerWidth - 14, 520);
    // Late boards are 9x10, so cells have to be small enough for the widest one
    // to fit a phone without scrolling.
    return Math.max(20, Math.min(46, Math.floor(avail / app.level.width)));
  }

  // ---- pieces ------------------------------------------------------------ //

  function ballNode(color, bag) {
    const b = el('div', 'ball' + (bag ? ' bag' : ''), GLYPHS[color] || '');
    b.style.setProperty('--ball', COLORS[color] || '#888');
    return b;
  }

  /**
   * A bus, drawn nose-right. `rotate` orients it; everything inside stays
   * upright because the sign counter-rotates.
   */
  function busBody(bus, seats, rotate) {
    const body = el('div', 'body');
    body.style.setProperty('--bus', COLORS[bus.color] || '#888');
    body.style.setProperty('--seat', Math.round(app.cell * 0.30) + 'px');

    body.appendChild(el('div', 'screen'));
    body.appendChild(el('div', 'lamp top'));
    body.appendChild(el('div', 'lamp bottom'));
    body.appendChild(el('div', 'wheel a'));
    body.appendChild(el('div', 'wheel b'));

    const sign = el('div', 'sign', GLYPHS[bus.color] || '?');
    if (rotate) sign.style.transform = `rotate(${-ANGLE[bus.facing]}deg)`;
    body.appendChild(sign);

    const row = el('div', 'seats');
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
    body.appendChild(row);
    return body;
  }

  function busNode(bus) {
    const c = app.cell;
    const xs = bus.cells.map((p) => p[0]);
    const ys = bus.cells.map((p) => p[1]);
    const cx = (Math.min(...xs) + Math.max(...xs) + 1) * c / 2;
    const cy = (Math.min(...ys) + Math.max(...ys) + 1) * c / 2;
    const w = bus.cells.length * c - 7;
    const h = c - 7;

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
    node.appendChild(busBody(bus, null, true));
    node.addEventListener('click', () => onBusTap(bus.id, node));
    return node;
  }

  // ---- rendering --------------------------------------------------------- //

  function render() {
    app.cell = cellSize();
    document.documentElement.style.setProperty('--cell', app.cell + 'px');
    const remaining = app.level.queue.length - app.queueAt;

    $('#blindNo').textContent = String(app.level.id);
    $('#blindSub').textContent =
      `${CHAPTERS[app.level.chapter - 1].name} · ${remaining} von ${app.level.queue.length} warten`;

    renderLot();
    renderBays();
    renderQueue();
    $('#undo').disabled = app.history.length === 0 || app.busy;
  }

  function renderLot() {
    const lvl = app.level;
    const c = app.cell;
    const lot = $('#lot');
    lot.innerHTML = '';
    lot.style.width = lvl.width * c + 'px';
    lot.style.height = lvl.height * c + 'px';

    // Lane markings: a solid edge line against each hard shoulder, broken white
    // lines between lanes, and painted distance markers on the tarmac.
    for (const [cls, x] of [['edge', 6], ['edge', lvl.width * c - 9]]) {
      const line = el('div', cls);
      line.style.left = x + 'px';
      lot.appendChild(line);
    }
    for (let x = 1; x < lvl.width; x++) {
      const lane = el('div', 'lane');
      lane.style.left = (x * c - 1) + 'px';
      lot.appendChild(lane);
    }
    lot.appendChild(el('div', 'barrier l'));
    lot.appendChild(el('div', 'barrier r'));

    for (let k = 1; k * 3 < lvl.height; k++) {
      const marker = el('div', 'marker', `${k * 100}`);
      marker.style.fontSize = Math.round(c * 0.44) + 'px';
      marker.style.left = (lvl.width * c - 14) + 'px';
      marker.style.top = (k * 3 * c) + 'px';
      lot.appendChild(marker);
    }

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
        holder.appendChild(busBody(bay.bus, bay.seats, false));
        node.appendChild(holder);
      }
      wrap.appendChild(node);
    }
  }

  function renderQueue() {
    const lvl = app.level;
    const wrap = $('#queue');
    wrap.innerHTML = '';
    const remaining = lvl.queue.length - app.queueAt;

    for (let i = app.queueAt; i < Math.min(lvl.queue.length, app.queueAt + MAX_QUEUE_SHOWN); i++) {
      const p = lvl.queue[i];
      const rider = el('div', 'rider' + (i === app.queueAt ? ' head' : ''));
      rider.dataset.index = String(i);
      rider.appendChild(ballNode(p.color, p.luggage));
      wrap.appendChild(rider);
    }
    if (remaining > MAX_QUEUE_SHOWN) {
      wrap.appendChild(el('span', 'queue-more', `+${remaining - MAX_QUEUE_SHOWN}`));
    }
    if (remaining === 0) wrap.appendChild(el('span', 'queue-more', 'alle eingestiegen'));
    $('#queueCount').textContent = remaining > 0 ? `${remaining} wartend` : 'leer';
    // Keep the blind in step during boarding, not just at the end of the move.
    $('#blindSub').textContent =
      `${CHAPTERS[lvl.chapter - 1].name} · ${remaining}/${lvl.queue.length}`
      + ` · ${lvl.bays}/${STANDS}`;
  }

  // ---- the boarding flight ----------------------------------------------- //

  /** Flies the head ball from the platform into a seat, then fills the seat. */
  async function flyIntoSeat(bayIndex, seatIndex, passenger) {
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
    });
    document.body.appendChild(flyer);
    rider.style.opacity = '0';

    // A little hop on the way in - the arc is what makes it feel cheerful.
    const dx = to.left + (to.width - from.width) / 2 - from.left;
    const dy = to.top + (to.height - from.height) / 2 - from.top;
    await sleep(20);
    flyer.style.transform = `translate(${dx * 0.55}px, ${dy - 26}px) scale(1.1)`;
    await sleep(190);
    flyer.style.transform = `translate(${dx}px, ${dy}px) scale(${to.width / from.width})`;
    await sleep(200);

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
    if (app.busy || app.done) return;
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
    $('#undo').disabled = true;

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
    for (const evt of events) {
      if (evt.kind === 'board') {
        const passenger = app.level.queue[evt.queueIndex];
        const bay = app.bays[evt.bay];
        const seatIndex = bay ? bay.seats.findIndex((s) => s === null) : -1;
        if (seatIndex >= 0) await flyIntoSeat(evt.bay, seatIndex, passenger);
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
      if (!album.includes(card.id)) save({ album: album.concat(card.id) });
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

  // ---- boot -------------------------------------------------------------- //

  /** Verges either side of the app: the motorway continues past the board. */
  function buildBackdrop() {
    const back = el('div', 'backdrop');
    for (const side of ['l', 'r']) {
      const verge = el('div', 'verge ' + side);
      for (let i = 0; i < 5; i++) {
        const car = el('div', 'traffic');
        Object.assign(car.style, {
          left: (10 + (i % 2) * 24) + 'px',
          background: ['#8A8F86', '#6E7A86', '#7D7169', '#93887A'][i % 4],
          animationDuration: (9 + i * 2.4) + 's',
          animationDelay: (-i * 3.1) + 's',
          animationDirection: side === 'l' ? 'normal' : 'reverse',
        });
        verge.appendChild(car);
      }
      back.appendChild(verge);
    }
    document.body.insertBefore(back, document.body.firstChild);
  }

  function boot() {
    buildBackdrop();
    $('#hint').addEventListener('click', doHint);
    $('#undo').addEventListener('click', undo);
    $('#restart').addEventListener('click', () => loadLevel(app.level.id));
    $('#pick').addEventListener('click', showMap);
    window.addEventListener('resize', () => { if (app.level && !app.busy) render(); });
    loadLevel(load().current || 1);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', boot);
  } else {
    boot();
  }
})();
