using System;
using System.Collections.Generic;
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
        private readonly List<Transform> _seats = new List<Transform>();
        private int _filled;

        public void Initialise(Bus bus, Renderer body)
        {
            Bus = bus;
            _body = body;
            if (_body != null) _baseColor = Palette.Of(bus.Color);
        }

        /// <summary>Registers a seat socket, in boarding order.</summary>
        public void AddSeat(Transform seat) => _seats.Add(seat);

        public int SeatCount => _seats.Count;

        /// <summary>World position of the next seat a passenger would take.</summary>
        public Vector3 NextSeatPosition()
        {
            if (_filled < _seats.Count && _seats[_filled] != null)
                return _seats[_filled].position;
            return transform.position;
        }

        /// <summary>
        /// Marks seats as taken. The ball the player watched fly in becomes the
        /// ball sitting in the seat, so capacity stays countable at a glance.
        /// </summary>
        public void FillSeats(int count, Color color)
        {
            for (int i = 0; i < count && _filled < _seats.Count; i++, _filled++)
            {
                Transform seat = _seats[_filled];
                if (seat == null) continue;
                var renderer = seat.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = true;
                    renderer.sharedMaterial = UiBuilder.CreateLitMaterial(
                        i == 0 ? color : new Color(0.42f, 0.33f, 0.25f));  // luggage
                }
            }
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
