using System;
using SunnyStop.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SunnyStop.Game
{
    /// <summary>
    /// One bus. Handles being tapped and the little bits of feedback that make the
    /// board pleasant to touch even when the player is losing (CONCEPT.md §3.4).
    ///
    /// Uses IPointerClickHandler rather than reading input directly, so the same code
    /// works with mouse, touch, the legacy input manager and the new Input System.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BusView : MonoBehaviour, IPointerClickHandler
    {
        public event Action<int> Tapped;

        public Bus Bus { get; private set; }
        public bool IsDocked { get; private set; }

        private Renderer _body;
        private Color _baseColor;
        private float _hintUntil;

        public void Initialise(Bus bus, Renderer body)
        {
            Bus = bus;
            _body = body;
            if (_body != null) _baseColor = Palette.Of(bus.Color);
        }

        public void SetDocked(bool docked) => IsDocked = docked;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (IsDocked) return;
            Tapped?.Invoke(Bus.Id);
        }

        /// <summary>Pulse this bus for a few seconds - used by the hint button.</summary>
        public void Highlight(float seconds = 2.5f) => _hintUntil = Time.time + seconds;

        private void Update()
        {
            if (_body == null) return;

            if (Time.time < _hintUntil)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 7f);
                SetColor(Color.Lerp(_baseColor, Color.white, pulse * 0.55f));
            }
            else if (_hintUntil != 0f)
            {
                SetColor(_baseColor);
                _hintUntil = 0f;
            }
        }

        private void SetColor(Color color)
        {
            Material material = _body.material;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }
    }
}
