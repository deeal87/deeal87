// Sunny Stop — the browser half of the sound design.
//
// A direct port of Assets/Scripts/Game/SfxSynth.cs and AudioDirector.cs: same
// notes, same envelopes, same rules about when a thing is allowed to make a
// noise. It exists so the preview can be auditioned as sound as well as looked
// at, and so a change to the design can be heard before it is written twice.
//
// Everything is synthesised with WebAudio oscillators rather than loaded from
// files. The reason is in SfxSynth.cs and worth repeating: the boarding sound
// climbs a pentatonic scale once per passenger, a cascade can be twelve long,
// and as a formula that is always in tune where twelve samples are not.

'use strict';

(function () {
  // C major pentatonic — no two notes in it can clash, so a boarding chain in
  // any order is consonant. The player picks the order, not us.
  const PENTATONIC = [1, 9 / 8, 5 / 4, 3 / 2, 5 / 3];
  const BASE_HZ = 523.25;      // C5
  const CHAIN_CAP = 12;
  const POSTCARD_LEVEL = 0.34;

  let ctx = null;
  let master = null;
  let chain = 0;
  let postcardShowing = false;

  function ready() {
    if (ctx) return ctx.state !== 'closed';
    const Ctx = window.AudioContext || window.webkitAudioContext;
    if (!Ctx) return false;
    ctx = new Ctx();
    master = ctx.createGain();
    master.gain.value = 0.9;
    master.connect(ctx.destination);
    return true;
  }

  /** Browsers refuse to start audio until the user has interacted. */
  function resume() {
    if (ctx && ctx.state === 'suspended') ctx.resume();
  }

  /**
   * One struck partial. `mult` is the harmonic, `amp` its share, `decay` the
   * exponential rate — the same three numbers the C# Tone class takes.
   */
  function partial(t0, hz, mult, amp, attack, decay, dur, gain, type) {
    const osc = ctx.createOscillator();
    const env = ctx.createGain();
    osc.type = type || 'sine';
    osc.frequency.value = hz * mult;

    const peak = Math.max(0.0001, amp * gain);
    env.gain.setValueAtTime(0.0001, t0);
    env.gain.linearRampToValueAtTime(peak, t0 + attack);
    // setTargetAtTime decays exponentially with a time constant, which is the
    // same shape as exp(-t * rate) with rate = 1 / timeConstant.
    env.gain.setTargetAtTime(0.0001, t0 + attack, 1 / decay);

    osc.connect(env);
    env.connect(master);
    osc.start(t0);
    osc.stop(t0 + dur);
  }

  /** Lowpassed noise burst, for brakes and wooden clicks. */
  function noise(t0, dur, gain, cutoffHz, decay) {
    const frames = Math.max(1, Math.floor(ctx.sampleRate * dur));
    const buffer = ctx.createBuffer(1, frames, ctx.sampleRate);
    const data = buffer.getChannelData(0);
    // Seeded, like the C# side: the same tap must sound the same twice.
    let state = 4231 >>> 0;
    let previous = 0;
    for (let n = 0; n < frames; n++) {
      state = (state * 1664525 + 1013904223) >>> 0;
      const white = state / 2147483648 - 1;
      previous += 0.25 * (white - previous);
      data[n] = previous * Math.exp((-n / ctx.sampleRate) * decay);
    }

    const src = ctx.createBufferSource();
    src.buffer = buffer;
    const filter = ctx.createBiquadFilter();
    filter.type = 'lowpass';
    filter.frequency.value = cutoffHz;
    const env = ctx.createGain();
    env.gain.value = gain;

    src.connect(filter);
    filter.connect(env);
    env.connect(master);
    src.start(t0);
  }

  // ---- the sounds --------------------------------------------------------- //

  const VOICES = {
    dispatch(t) {
      partial(t, 196, 1, 1, 0.002, 38, 0.10, 0.22);
      partial(t, 196, 2, 0.3, 0.002, 38, 0.10, 0.22);
      noise(t, 0.10, 0.10, 1400, 90);
    },
    dock(t) {
      noise(t, 0.30, 0.055, 900, 9);
      partial(t + 0.075, 87, 1, 1, 0.010, 11, 0.28, 0.16);
      partial(t + 0.075, 87, 2, 0.2, 0.010, 11, 0.28, 0.16);
    },
    depart(t) {
      horn(t, 392.0);
      horn(t + 0.145, 493.88);
    },
    refused(t) {
      thud(t);
      thud(t + 0.085);
    },
    rewind(t) {
      partial(t, 440.0, 1, 1, 0.020, 6.5, 0.45, 0.15);
      partial(t + 0.16, 329.63, 1, 1, 0.020, 6.5, 0.45, 0.15);
    },
    win(t) {
      [1, 5 / 4, 3 / 2].forEach((ratio, i) => {
        partial(t + 0.085 * i, BASE_HZ * ratio, 1, 1, 0.006, 7, 0.60, 0.17);
        partial(t + 0.085 * i, BASE_HZ * ratio, 2, 0.14, 0.006, 7, 0.60, 0.17);
      });
    },
    postcard(t) {
      // Inharmonic partials read as a bell rather than an organ.
      partial(t, 659.25, 1, 1, 0.004, 2.4, 1.6, 0.11 * POSTCARD_LEVEL * 3);
      partial(t, 659.25, 2.76, 0.22, 0.004, 2.4, 1.6, 0.11 * POSTCARD_LEVEL * 3);
      partial(t, 659.25, 5.40, 0.07, 0.004, 2.4, 1.6, 0.11 * POSTCARD_LEVEL * 3);
    },
    uitap(t) {
      partial(t, 1046.5, 1, 1, 0.001, 60, 0.06, 0.10);
    },
  };

  function horn(t, hz) {
    [[1, 1], [2, 0.42], [3, 0.20], [4, 0.09]].forEach(([m, a]) => {
      partial(t, hz, m, a, 0.018, 6, 0.38, 0.17);
      partial(t, hz * 1.004, m, a * 0.5, 0.018, 6, 0.38, 0.17);   // beating
    });
  }

  function thud(t) {
    // A shake of the head, not a buzzer: the board has to stay pleasant to
    // touch even when the player is losing.
    partial(t, 110, 1, 1, 0.004, 26, 0.19, 0.20);
    partial(t, 110, 2, 0.22, 0.004, 26, 0.19, 0.20);
  }

  // ---- the director ------------------------------------------------------- //

  const api = {
    get enabled() {
      try {
        const s = JSON.parse(localStorage.getItem('sunnystop.preview')) || {};
        return s.sound !== false;
      } catch (e) { return true; }
    },
    set enabled(on) {
      try {
        const s = JSON.parse(localStorage.getItem('sunnystop.preview')) || {};
        s.sound = !!on;
        localStorage.setItem('sunnystop.preview', JSON.stringify(s));
      } catch (e) { /* private mode */ }
    },

    resetChain() { chain = 0; },

    setPostcardShowing(showing) { postcardShowing = !!showing; },

    play(name) {
      if (!api.enabled || !ready()) return;
      // While the postcard is up, only the postcard speaks. A depart tail
      // arriving underneath it undoes the quiet on purpose.
      if (postcardShowing && name !== 'postcard' && name !== 'uitap') return;
      const voice = VOICES[name];
      if (!voice) return;
      resume();
      voice(ctx.currentTime + 0.005);
    },

    /** The next note in a boarding run. Call once per passenger, in order. */
    board() {
      if (!api.enabled || !ready()) return;
      if (postcardShowing) return;
      resume();
      const i = Math.min(chain, CHAIN_CAP - 1);
      chain++;
      const degree = i % PENTATONIC.length;
      const octave = Math.min(2, Math.floor(i / PENTATONIC.length));
      const hz = BASE_HZ * PENTATONIC[degree] * (1 << octave);
      const t = ctx.currentTime + 0.005;
      partial(t, hz, 1, 1, 0.003, 14, 0.34, 0.26);
      partial(t, hz, 4, 0.16, 0.003, 14, 0.34, 0.26);
      partial(t, hz, 9, 0.05, 0.003, 14, 0.34, 0.26);
    },
  };

  window.Sfx = api;
})();
