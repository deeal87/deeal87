using System.Collections.Generic;
using UnityEngine;

namespace SunnyStop.Game
{
    /// <summary>What just happened. The game asks for these, never for a file.</summary>
    public enum Sound
    {
        Dispatch,
        Dock,
        Depart,
        Refused,
        Rewind,
        Win,
        Postcard,
        UiTap
    }

    /// <summary>
    /// Decides when the game makes a noise, and how loud.
    ///
    /// The waveforms come from <see cref="SfxSynth"/>; everything interesting
    /// is here, because in a game like this *when* a sound plays is the design
    /// and the waveform is only the delivery. Three rules it enforces:
    ///
    ///   * **Boarding climbs.** Each passenger in a chain plays the next note up
    ///     a pentatonic scale, so filling a bus in one go plays a run and a
    ///     twelve-seat double-decker plays a long one. The chain resets on every
    ///     move, which is what makes a big cascade feel like an achievement.
    ///   * **The postcard is nearly silent.** One soft bell, at a third of the
    ///     level of everything else, and the board's own sounds are held off
    ///     while it is on screen. A fanfare there would turn a kind word into a
    ///     payout jingle, which is the register this whole game is defined
    ///     against (CONCEPT.md §6).
    ///   * **Silence is a first-class mode.** Off means off, immediately, and the
    ///     game is fully playable that way (CONCEPT.md §10). This never plays
    ///     anything the player has not opted into.
    ///
    /// Clips are generated once on first use and cached. Generating all nine
    /// costs a few milliseconds of maths and no download at all.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        /// <summary>The postcard sits well under the rest of the mix.</summary>
        private const float PostcardLevel = 0.34f;

        /// <summary>How many notes a single boarding run climbs before it wraps.</summary>
        private const int ChainCap = 12;

        private static AudioDirector _instance;

        /// <summary>
        /// May be null before Bootstrap runs. Callers use
        /// <see cref="Play(Sound)"/> rather than touching this.
        /// </summary>
        public static AudioDirector Instance => _instance;

        private AudioSource _source;
        private readonly Dictionary<Sound, AudioClip> _cache = new Dictionary<Sound, AudioClip>();
        private readonly List<AudioClip> _boardNotes = new List<AudioClip>();
        private int _chain;
        private bool _postcardShowing;

        public static bool Enabled
        {
            get => SaveGame.SoundOn;
            set
            {
                SaveGame.SoundOn = value;
                SaveGame.Flush();
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;

            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;      // UI-style: no 3D falloff on a board game
            _source.volume = 1f;
        }

        // ----- the interesting part -------------------------------------- //

        public static void Play(Sound sound)
        {
            if (_instance != null) _instance.PlayInternal(sound, 1f);
        }

        /// <summary>
        /// The next note in a boarding run. Call once per passenger, in order;
        /// call <see cref="ResetChain"/> when a move begins.
        /// </summary>
        public static void PlayBoarding()
        {
            if (_instance != null) _instance.PlayBoardingInternal();
        }

        public static void ResetChain()
        {
            if (_instance != null) _instance._chain = 0;
        }

        /// <summary>
        /// Holds the board's sounds while the win screen is up, so the bell is
        /// the only thing in the room.
        /// </summary>
        public static void SetPostcardShowing(bool showing)
        {
            if (_instance != null) _instance._postcardShowing = showing;
        }

        private void PlayBoardingInternal()
        {
            if (!Enabled || _source == null) return;

            int index = Mathf.Min(_chain, ChainCap - 1);
            while (_boardNotes.Count <= index)
            {
                _boardNotes.Add(ToClip($"board{_boardNotes.Count}",
                                       SfxSynth.Board(_boardNotes.Count)));
            }
            _chain++;
            _source.PlayOneShot(_boardNotes[index], 1f);
        }

        private void PlayInternal(Sound sound, float gain)
        {
            if (!Enabled || _source == null) return;

            // While the postcard is up, only the postcard speaks. A depart or a
            // win tail arriving underneath it undoes the quiet on purpose.
            if (_postcardShowing && sound != Sound.Postcard && sound != Sound.UiTap) return;

            AudioClip clip;
            if (!_cache.TryGetValue(sound, out clip))
            {
                clip = ToClip(sound.ToString(), Render(sound));
                _cache[sound] = clip;
            }
            _source.PlayOneShot(clip, sound == Sound.Postcard ? PostcardLevel * gain : gain);
        }

        private static float[] Render(Sound sound)
        {
            switch (sound)
            {
                case Sound.Dispatch: return SfxSynth.Dispatch();
                case Sound.Dock: return SfxSynth.Dock();
                case Sound.Depart: return SfxSynth.Depart();
                case Sound.Refused: return SfxSynth.Refused();
                case Sound.Rewind: return SfxSynth.Rewind();
                case Sound.Win: return SfxSynth.Win();
                case Sound.Postcard: return SfxSynth.Postcard();
                default: return SfxSynth.UiTap();
            }
        }

        private static AudioClip ToClip(string name, float[] pcm)
        {
            AudioClip clip = AudioClip.Create(name, pcm.Length, 1,
                                              SfxSynth.SampleRate, false);
            clip.SetData(pcm, 0);
            return clip;
        }
    }
}
