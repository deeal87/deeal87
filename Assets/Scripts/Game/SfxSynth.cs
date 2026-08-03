using System;

namespace SunnyStop.Game
{
    /// <summary>
    /// Every sound effect in the game, generated as PCM rather than shipped as
    /// a file (CONCEPT.md §10).
    ///
    /// Why synthesis and not recordings:
    ///
    ///   * The boarding sound is a *rising pentatonic melody* — the note climbs
    ///     with each passenger in a chain, and a chain can be twelve long. As
    ///     files that is twelve perfectly-tuned samples per timbre that drift out
    ///     of tune the moment anybody re-exports one. As a formula it is
    ///     `base * ratio[i]`, in tune by construction.
    ///   * It costs nothing to license and nothing to download.
    ///   * It is code, so it is reviewable in a diff and identical on every
    ///     device — where a binary blob is neither.
    ///
    /// This class touches no Unity API at all, deliberately: the same code is
    /// rendered to .wav headlessly to audition the design without opening the
    /// editor, and mirrored in tools/webpreview/sound.js so the browser preview
    /// sounds like the game.
    ///
    /// The art pass may well replace some of these with recordings — a real
    /// marimba is warmer than an additive one. Keep the *timing* and the note
    /// choices if you do; those are the design, the waveform is the delivery.
    /// </summary>
    public static class SfxSynth
    {
        public const int SampleRate = 44100;

        /// <summary>
        /// C major pentatonic, as frequency ratios: C D E G A. Chosen because
        /// no two notes in it can clash, so a boarding chain of any length in
        /// any order is consonant — which matters, because the player decides
        /// the order, not us.
        /// </summary>
        private static readonly double[] Pentatonic = { 1.0, 9.0 / 8.0, 5.0 / 4.0, 3.0 / 2.0, 5.0 / 3.0 };

        private const double BaseHz = 523.25;   // C5

        /// <summary>
        /// The note a boarding passenger plays. <paramref name="indexInChain"/>
        /// is how many have already boarded on this move, so a bus filling in
        /// one go plays a complete little run upward.
        /// </summary>
        public static float[] Board(int indexInChain)
        {
            int i = Math.Max(0, indexInChain);
            // Wrap up the octaves rather than climbing forever: by the twelfth
            // passenger an un-wrapped run is a shriek.
            int degree = i % Pentatonic.Length;
            int octave = Math.Min(2, i / Pentatonic.Length);
            double hz = BaseHz * Pentatonic[degree] * (1 << octave);

            // Marimba-ish: fundamental plus a quiet fourth partial, struck.
            var voice = new Tone(hz, 0.26)
                .Partial(1.0, 1.0)
                .Partial(4.0, 0.16)
                .Partial(9.0, 0.05)
                .Attack(0.003)
                .Decay(14.0);
            return voice.Render(0.34);
        }

        /// <summary>Tapping a bus: a dry wooden click, low and short.</summary>
        public static float[] Dispatch()
        {
            var body = new Tone(196.0, 0.22).Partial(1.0, 1.0).Partial(2.0, 0.3)
                                            .Attack(0.002).Decay(38.0);
            float[] tone = body.Render(0.10);
            float[] click = Noise(0.10, 0.10, 0.30, seed: 7717, decay: 90.0);
            return Mix(tone, click);
        }

        /// <summary>A bus refused: it cannot move. Muted, never harsh.</summary>
        public static float[] Refused()
        {
            // Two soft low thuds. The board has to stay pleasant to touch even
            // when the player is losing (CONCEPT.md §3.4), so this is a shake of
            // the head, not a buzzer.
            var thud = new Tone(110.0, 0.20).Partial(1.0, 1.0).Partial(2.0, 0.22)
                                            .Attack(0.004).Decay(26.0);
            float[] a = thud.Render(0.19);
            float[] b = thud.Render(0.19);
            return Mix(a, Delay(b, 0.085));
        }

        /// <summary>A bus settling into a stand: brakes, then weight.</summary>
        public static float[] Dock()
        {
            float[] hiss = Noise(0.30, 0.055, 0.18, seed: 4231, decay: 9.0);
            var settle = new Tone(87.0, 0.16).Partial(1.0, 1.0).Partial(2.0, 0.2)
                                             .Attack(0.010).Decay(11.0);
            return Mix(hiss, Delay(settle.Render(0.28), 0.075));
        }

        /// <summary>A full bus driving off: a friendly two-tone horn.</summary>
        public static float[] Depart()
        {
            var low = Horn(392.00);    // G4
            var high = Horn(493.88);   // B4 - a major third up, warm not urgent
            return Mix(low, Delay(high, 0.145));
        }

        private static float[] Horn(double hz)
        {
            return new Tone(hz, 0.17)
                .Partial(1.0, 1.0)
                .Partial(2.0, 0.42)
                .Partial(3.0, 0.20)
                .Partial(4.0, 0.09)
                .Detune(0.004)
                .Attack(0.018)
                .Decay(6.0)
                .Render(0.38);
        }

        /// <summary>
        /// The free-rewind offer. This appears at the player's worst moment, so
        /// it is a hand on the shoulder: two soft notes falling, no sting.
        /// </summary>
        public static float[] Rewind()
        {
            var a = new Tone(440.00, 0.15).Partial(1.0, 1.0).Partial(3.0, 0.07)
                                          .Attack(0.020).Decay(6.5);
            var b = new Tone(329.63, 0.15).Partial(1.0, 1.0).Partial(3.0, 0.07)
                                          .Attack(0.020).Decay(6.5);
            return Mix(a.Render(0.45), Delay(b.Render(0.45), 0.16));
        }

        /// <summary>Level cleared. Restrained on purpose — see Postcard().</summary>
        public static float[] Win()
        {
            float[] mix = new float[(int)(SampleRate * 0.85)];
            double[] run = { 1.0, 5.0 / 4.0, 3.0 / 2.0 };
            for (int i = 0; i < run.Length; i++)
            {
                var note = new Tone(BaseHz * run[i], 0.17)
                    .Partial(1.0, 1.0).Partial(2.0, 0.14)
                    .Attack(0.006).Decay(7.0);
                mix = Mix(mix, Delay(note.Render(0.60), 0.085 * i));
            }
            return mix;
        }

        /// <summary>
        /// The postcard. One soft bell, and that is all.
        ///
        /// This is the moment the whole product rests on, and it is the easiest
        /// one to ruin: a triumphant fanfare here turns a kind word into a slot
        /// machine payout, which is the exact register the game is defined
        /// against. Quiet, long, and then nothing.
        /// </summary>
        public static float[] Postcard()
        {
            return new Tone(659.25, 0.11)          // E5
                .Partial(1.0, 1.0)
                .Partial(2.76, 0.22)               // inharmonic - reads as a bell
                .Partial(5.40, 0.07)
                .Attack(0.004)
                .Decay(2.4)
                .Render(1.60);
        }

        /// <summary>A menu button. Tiny, dry, out of the way.</summary>
        public static float[] UiTap()
        {
            var t = new Tone(1046.5, 0.10).Partial(1.0, 1.0).Partial(2.0, 0.1)
                                          .Attack(0.001).Decay(60.0);
            return t.Render(0.06);
        }

        // ----- synthesis primitives -------------------------------------- //

        /// <summary>
        /// An additive voice: partials over a fundamental, struck with an
        /// attack ramp and an exponential decay.
        /// </summary>
        private sealed class Tone
        {
            private readonly double _hz;
            private readonly double _gain;
            private readonly double[] _mult = new double[8];
            private readonly double[] _amp = new double[8];
            private int _count;
            private double _attack = 0.005;
            private double _decay = 10.0;
            private double _detune;

            public Tone(double hz, double gain) { _hz = hz; _gain = gain; }

            public Tone Partial(double multiple, double amplitude)
            {
                if (_count < _mult.Length)
                {
                    _mult[_count] = multiple;
                    _amp[_count] = amplitude;
                    _count++;
                }
                return this;
            }

            public Tone Attack(double seconds) { _attack = Math.Max(1e-4, seconds); return this; }
            public Tone Decay(double rate) { _decay = rate; return this; }

            /// <summary>A second voice a hair off pitch, for a horn's beating.</summary>
            public Tone Detune(double fraction) { _detune = fraction; return this; }

            public float[] Render(double seconds)
            {
                int length = (int)(SampleRate * seconds);
                var buffer = new float[length];
                double norm = 0.0;
                for (int p = 0; p < _count; p++) norm += _amp[p];
                if (norm <= 0.0) return buffer;

                for (int n = 0; n < length; n++)
                {
                    double t = n / (double)SampleRate;
                    double env = t < _attack
                        ? t / _attack
                        : Math.Exp(-(t - _attack) * _decay);

                    double sample = 0.0;
                    for (int p = 0; p < _count; p++)
                    {
                        double w = 2.0 * Math.PI * _hz * _mult[p] * t;
                        sample += _amp[p] * Math.Sin(w);
                        if (_detune > 0.0)
                        {
                            sample += _amp[p] * Math.Sin(w * (1.0 + _detune)) * 0.5;
                        }
                    }
                    buffer[n] = (float)(sample / norm * env * _gain);
                }
                return Taper(buffer);
            }
        }

        /// <summary>
        /// Lowpassed noise. The PRNG is seeded, not `Random`: two players must
        /// hear the same game, and a diff of this file must describe a sound
        /// that is actually reproducible.
        /// </summary>
        private static float[] Noise(double seconds, double gain, double cutoff,
                                     int seed, double decay)
        {
            int length = (int)(SampleRate * seconds);
            var buffer = new float[length];
            uint state = (uint)seed;
            double previous = 0.0;

            for (int n = 0; n < length; n++)
            {
                state = state * 1664525u + 1013904223u;
                double white = state / 2147483648.0 - 1.0;   // -1 .. 1
                previous += cutoff * (white - previous);     // one-pole lowpass
                double env = Math.Exp(-(n / (double)SampleRate) * decay);
                buffer[n] = (float)(previous * env * gain);
            }
            return Taper(buffer);
        }

        /// <summary>Sums two buffers, extending to the longer one.</summary>
        private static float[] Mix(float[] a, float[] b)
        {
            int length = Math.Max(a.Length, b.Length);
            var mixed = new float[length];
            for (int n = 0; n < length; n++)
            {
                double sum = 0.0;
                if (n < a.Length) sum += a[n];
                if (n < b.Length) sum += b[n];
                // Soft clip rather than wrap: a wrapped sample is a loud tick,
                // and one tick is all it takes to make a game sound broken.
                mixed[n] = (float)Math.Tanh(sum);
            }
            return mixed;
        }

        private static float[] Delay(float[] source, double seconds)
        {
            int offset = (int)(SampleRate * seconds);
            var shifted = new float[source.Length + offset];
            Array.Copy(source, 0, shifted, offset, source.Length);
            return shifted;
        }

        /// <summary>
        /// Forces the buffer to start and end at silence. Without this the
        /// exponential decay is still audible at the last sample and the clip
        /// ends on a step, which every device renders as a click.
        /// </summary>
        private static float[] Taper(float[] buffer)
        {
            int fade = Math.Min(220, buffer.Length / 4);   // ~5 ms
            for (int n = 0; n < fade; n++)
            {
                double k = n / (double)fade;
                buffer[n] *= (float)k;
                buffer[buffer.Length - 1 - n] *= (float)k;
            }
            return buffer;
        }
    }
}
