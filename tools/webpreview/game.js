// Sunny Stop — browser preview UI.
//
// Presentation only; every rule lives in engine.js. Mirrors what the Unity
// prototype does, including the parts that matter most to the design: the free
// rewind offered the instant a move makes a level unwinnable, hints that are
// re-solved from the current position, and a win screen with no timer, no
// forced dwell and nothing to sell.

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

  const MAX_QUEUE_SHOWN = 12;
  const STORE = 'sunnystop.preview';

  const $ = (sel) => document.querySelector(sel);
  const el = (tag, cls, text) => {
    const n = document.createElement(tag);
    if (cls) n.className = cls;
    if (text !== undefined) n.textContent = text;
    return n;
  };

  // ---- persistence ------------------------------------------------------- //

  function load() {
    try {
      return JSON.parse(localStorage.getItem(STORE)) || {};
    } catch (e) {
      return {};
    }
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

  // ---- game -------------------------------------------------------------- //

  const app = {
    level: null,
    state: null,
    history: [],
    busy: false,
    done: false,
    cell: 44,
  };

  function levelData(n) {
    return DATA.levels.find((l) => l.id === n);
  }

  function loadLevel(n) {
    const raw = levelData(n);
    if (!raw) return;

    app.level = E.inflateLevel(raw);
    app.state = E.initialState(app.level);
    app.history = [];
    app.busy = false;
    app.done = false;

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
    const lvl = app.level;
    const avail = Math.min(window.innerWidth - 28, 480);
    return Math.max(26, Math.min(52, Math.floor(avail / lvl.width)));
  }

  // ---- rendering --------------------------------------------------------- //

  function render() {
    const lvl = app.level;
    const st = app.state;
    app.cell = cellSize();
    document.documentElement.style.setProperty('--cell', app.cell + 'px');

    $('#blindNo').textContent = String(lvl.id);
    $('#blindSub').textContent =
      `Kapitel ${lvl.chapter} · ${lvl.queue.length - st.qi} von ${lvl.queue.length} warten`;

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

    for (let y = 0; y < lvl.height; y++) {
      for (let x = 0; x < lvl.width; x++) {
        const slot = el('div', 'slot');
        Object.assign(slot.style, {
          left: x * c + 2 + 'px', top: y * c + 2 + 'px',
          width: c - 4 + 'px', height: c - 4 + 'px',
        });
        lot.appendChild(slot);
      }
    }

    for (const key of lvl.blocked) {
      const [x, y] = key.split(',').map(Number);
      const cone = el('div', 'cone', '▲');
      Object.assign(cone.style, {
        left: x * c + 'px', top: y * c + 'px',
        width: c + 'px', height: c + 'px', color: '#E07B24',
      });
      lot.appendChild(cone);
    }

    for (const bus of lvl.buses) {
      if (!app.state.inLot.has(bus.id)) continue;
      lot.appendChild(busNode(bus));
    }
  }

  function busNode(bus) {
    const c = app.cell;
    const xs = bus.cells.map((p) => p[0]);
    const ys = bus.cells.map((p) => p[1]);
    const minX = Math.min(...xs), minY = Math.min(...ys);
    const horizontal = bus.facing === 'left' || bus.facing === 'right';
    const w = (horizontal ? bus.cells.length : 1) * c;
    const h = (horizontal ? 1 : bus.cells.length) * c;

    const node = el('button', 'bus');
    node.type = 'button';
    node.dataset.bus = String(bus.id);
    node.setAttribute('aria-label',
      `Bus ${GLYPHS[bus.color] || ''} ${bus.color}, ${bus.capacity} Plätze, Richtung ${bus.facing}`);
    Object.assign(node.style, {
      left: minX * c + 3 + 'px', top: minY * c + 3 + 'px',
      width: w - 6 + 'px', height: h - 6 + 'px',
      background: COLORS[bus.color] || '#888',
    });

    node.appendChild(el('span', 'glyph', GLYPHS[bus.color] || '?'));

    const nose = el('span', 'nose');
    const t = 5;
    const style = { left: '', top: '', right: '', bottom: '', width: '', height: '' };
    if (bus.facing === 'up') Object.assign(style, { left: '22%', right: '22%', top: '3px', height: t + 'px', width: '56%' });
    if (bus.facing === 'down') Object.assign(style, { left: '22%', bottom: '3px', height: t + 'px', width: '56%' });
    if (bus.facing === 'left') Object.assign(style, { top: '22%', left: '3px', width: t + 'px', height: '56%' });
    if (bus.facing === 'right') Object.assign(style, { top: '22%', right: '3px', width: t + 'px', height: '56%' });
    Object.assign(nose.style, style);
    node.appendChild(nose);

    node.addEventListener('click', () => onBusTap(bus.id, node));
    return node;
  }

  function renderBays() {
    const bays = $('#bays');
    bays.innerHTML = '';
    app.state.bays.forEach((bay) => {
      const node = el('div', 'bay' + (bay ? ' filled' : ''));
      if (bay) {
        node.style.background = COLORS[bay.color] || '#888';
        node.appendChild(el('span', 'glyph', GLYPHS[bay.color] || '?'));
        node.appendChild(el('span', 'seats', `noch ${bay.seats}`));
      } else {
        node.textContent = 'frei';
      }
      bays.appendChild(node);
    });
  }

  function renderQueue() {
    const lvl = app.level;
    const wrap = $('#queue');
    wrap.innerHTML = '';
    const remaining = lvl.queue.length - app.state.qi;
    for (let i = app.state.qi; i < Math.min(lvl.queue.length, app.state.qi + MAX_QUEUE_SHOWN); i++) {
      const p = lvl.queue[i];
      const r = el('div', 'rider' + (i === app.state.qi ? ' head' : '') + (p.luggage ? ' luggage' : ''),
        GLYPHS[p.color] || '');
      r.style.background = COLORS[p.color] || '#888';
      r.dataset.index = String(i);
      wrap.appendChild(r);
    }
    if (remaining > MAX_QUEUE_SHOWN) {
      wrap.appendChild(el('span', 'queue-more', `+${remaining - MAX_QUEUE_SHOWN}`));
    }
    if (remaining === 0) wrap.appendChild(el('span', 'queue-more', 'alle eingestiegen'));
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

    const events = [];
    const next = E.dispatch(app.level, app.state, busId, events);

    // Drive the bus off the board along its facing, then let it "arrive".
    const [dx, dy] = E.DIRECTIONS[app.level.byId.get(busId).facing];
    const travel = (app.level.byId.get(busId).exitPath.length + 1.4) * app.cell;
    node.style.transform = `translate(${dx * travel}px, ${dy * travel}px)`;
    node.classList.add('gone');
    await sleep(260);

    app.state = next;
    render();

    // Board the passengers one by one so the chain reaction is visible.
    const boardings = events.filter((e) => e.kind === 'board').length;
    for (let i = 0; i < boardings; i++) await sleep(90);

    app.busy = false;
    render();
    afterMove();
  }

  function afterMove() {
    if (E.isWon(app.level, app.state)) return completeLevel();

    if (E.isDead(app.level, app.state)) {
      return offerRewind('Hier geht es nicht mehr weiter.');
    }
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
      ? Math.max(0, Math.round((Date.now() - lastPlayed.getTime()) / 86400000))
      : 0;

    const cleared = Object.assign({}, s.attempts || {});
    delete cleared[app.level.id];
    save({ attempts: cleared, lastPlayed: new Date().toISOString() });

    const card = pickCard({ level: app.level.id, attempts, days });
    if (!card) return nextLevel();

    const recent = (load().recent || []).concat(card.id).slice(-120);
    save({ recent });
    setTimeout(() => showPostcard(card), 620);
  }

  function showPostcard(card) {
    const layer = el('div', 'postcard-layer');
    const cardNode = el('div', 'postcard');

    const quote = el('q');
    quote.textContent = card.text;
    cardNode.appendChild(quote);

    const meta = el('div', 'meta',
      `Level ${app.level.id} · ${new Date().toLocaleDateString('de-DE')}`);
    cardNode.appendChild(meta);

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

    // Tapping anywhere continues - no hunting for a tiny close button.
    layer.addEventListener('click', close);
    cardNode.addEventListener('click', (ev) => ev.stopPropagation());
    layer.appendChild(cardNode);
    document.body.appendChild(layer);

    function close() {
      layer.remove();
      nextLevel();
    }
  }

  function nextLevel() {
    const ids = DATA.levels.map((l) => l.id);
    const i = ids.indexOf(app.level.id);
    loadLevel(i >= 0 && i + 1 < ids.length ? ids[i + 1] : ids[0]);
  }

  // ---- banner & picker --------------------------------------------------- //

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

  function showPicker() {
    const layer = el('div', 'picker-layer');
    const head = el('div', 'picker-head');
    head.appendChild(el('h2', null, 'Level wählen'));
    const close = el('button', 'btn', 'Zurück');
    close.addEventListener('click', () => layer.remove());
    head.appendChild(close);
    layer.appendChild(head);

    const note = el('div', 'hint-note',
      'Alle 200 Level sind spielbar. Die Zahl darunter ist die gemessene Schwierigkeit.');
    layer.appendChild(note);

    const grid = el('div', 'picker-grid');
    DATA.levels.forEach((l) => {
      const cell = el('button', 'picker-cell' + (l.id % 25 === 0 ? ' milestone' : ''));
      cell.appendChild(el('b', null, String(l.id)));
      cell.appendChild(el('small', null, String(Math.round(l.mds))));
      cell.addEventListener('click', () => { layer.remove(); loadLevel(l.id); });
      grid.appendChild(cell);
    });
    layer.appendChild(grid);
    document.body.appendChild(layer);
  }

  const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

  // ---- boot -------------------------------------------------------------- //

  function boot() {
    $('#hint').addEventListener('click', doHint);
    $('#undo').addEventListener('click', undo);
    $('#restart').addEventListener('click', () => loadLevel(app.level.id));
    $('#pick').addEventListener('click', showPicker);
    window.addEventListener('resize', () => { if (app.level) render(); });

    const saved = load();
    loadLevel(saved.current || 1);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', boot);
  } else {
    boot();
  }
})();
