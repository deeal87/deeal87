using System;
using System.Collections;
using System.Collections.Generic;
using SunnyStop.Core;
using UnityEngine;

namespace SunnyStop.Game
{
    /// <summary>
    /// Renders the terminal and animates transitions.
    ///
    /// Everything is built from primitives - this is the M0 prototype whose job is to
    /// answer "is the mechanic fun?", not "is it pretty". The layout, timings and feel
    /// notes here are what the art pass in M1 replaces with real models.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        public const float CellSize = 1f;
        private const float BayRowOffset = 1.9f;
        private const float QueueRowOffset = 3.4f;
        private const float PassengerSpacing = 0.52f;
        private const int MaxVisibleQueue = 14;

        public float DriveOutDuration = 0.34f;
        public float DockDuration = 0.30f;
        public float BoardDuration = 0.16f;
        public float DepartDuration = 0.42f;

        /// <summary>Raised when the player taps a bus that is still in the lot.</summary>
        public event Action<int> BusTapped;

        private LevelDefinition _level;
        private readonly Dictionary<int, BusView> _buses = new();
        private readonly List<GameObject> _passengers = new();
        private readonly List<Transform> _bayMarkers = new();
        private Transform _root;

        public bool TryGetBus(int busId, out BusView view) => _buses.TryGetValue(busId, out view);

        public void HighlightBus(int busId)
        {
            if (_buses.TryGetValue(busId, out BusView view) && view != null) view.Highlight();
        }

        // ----- layout ---------------------------------------------------------- //

        public Vector3 CellToWorld(Cell cell) => new Vector3(
            (cell.X - (_level.Width - 1) * 0.5f) * CellSize,
            0f,
            ((_level.Height - 1) * 0.5f - cell.Y) * CellSize);

        private float FrontRowZ => -(_level.Height - 1) * 0.5f * CellSize;

        public Vector3 BayPosition(int bayIndex)
        {
            float span = (_level.Bays - 1) * 1.6f;
            return new Vector3(bayIndex * 1.6f - span * 0.5f, 0f, FrontRowZ - BayRowOffset);
        }

        private Vector3 QueuePosition(int slot) => new Vector3(
            -2.4f + slot * PassengerSpacing, 0.25f, FrontRowZ - QueueRowOffset);

        // ----- construction ---------------------------------------------------- //

        public void Build(LevelDefinition level)
        {
            Clear();
            _level = level;

            _root = new GameObject("Board").transform;
            _root.SetParent(transform, false);

            BuildGround();
            BuildBlockedCells();
            BuildBays();
            foreach (Bus bus in level.Buses) BuildBus(bus);
        }

        public void Clear()
        {
            if (_root != null) Destroy(_root.gameObject);
            _buses.Clear();
            _passengers.Clear();
            _bayMarkers.Clear();
        }

        private void BuildGround()
        {
            var lot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lot.name = "Lot";
            lot.transform.SetParent(_root, false);
            lot.transform.localScale = new Vector3(
                _level.Width * CellSize + 0.5f, 0.2f, _level.Height * CellSize + 0.5f);
            lot.transform.localPosition = new Vector3(0f, -0.12f, 0f);
            Paint(lot, new Color(0.86f, 0.85f, 0.80f));
            Destroy(lot.GetComponent<Collider>());

            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Platform";
            platform.transform.SetParent(_root, false);
            platform.transform.localScale = new Vector3(
                _level.Width * CellSize + 2.5f, 0.2f, 2.9f);
            platform.transform.localPosition =
                new Vector3(0f, -0.12f, FrontRowZ - (BayRowOffset + QueueRowOffset) * 0.5f);
            Paint(platform, new Color(0.78f, 0.77f, 0.73f));
            Destroy(platform.GetComponent<Collider>());
        }

        private void BuildBlockedCells()
        {
            foreach (Cell cell in _level.Blocked)
            {
                var cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cone.name = $"Cone {cell}";
                cone.transform.SetParent(_root, false);
                cone.transform.localScale = new Vector3(0.32f, 0.26f, 0.32f);
                cone.transform.localPosition = CellToWorld(cell) + Vector3.up * 0.26f;
                Paint(cone, new Color(0.95f, 0.45f, 0.15f));
                Destroy(cone.GetComponent<Collider>());
            }
        }

        private void BuildBays()
        {
            for (int i = 0; i < _level.Bays; i++)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = $"Bay {i}";
                marker.transform.SetParent(_root, false);
                marker.transform.localScale = new Vector3(1.35f, 0.06f, 1.0f);
                marker.transform.localPosition = BayPosition(i) + Vector3.up * 0.01f;
                Paint(marker, new Color(0.70f, 0.69f, 0.65f));
                Destroy(marker.GetComponent<Collider>());
                _bayMarkers.Add(marker.transform);
            }
        }

        private void BuildBus(Bus bus)
        {
            var go = new GameObject($"Bus {bus.Id} ({bus.Color})");
            go.transform.SetParent(_root, false);

            Vector3 centre = Vector3.zero;
            foreach (Cell cell in bus.Cells) centre += CellToWorld(cell);
            centre /= bus.Cells.Count;
            go.transform.localPosition = centre + Vector3.up * 0.3f;

            bool horizontal = bus.Facing == Facing.Left || bus.Facing == Facing.Right;
            float length = bus.Cells.Count * CellSize * 0.92f;

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = horizontal
                ? new Vector3(length, 0.52f, 0.76f)
                : new Vector3(0.76f, 0.52f, length);
            Paint(body, Palette.Of(bus.Color));
            Destroy(body.GetComponent<Collider>());

            // A pale nose so the direction of travel is readable at a glance.
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Nose";
            nose.transform.SetParent(go.transform, false);
            Vector3 dir = FacingToWorld(bus.Facing);
            nose.transform.localScale = horizontal
                ? new Vector3(0.16f, 0.40f, 0.62f)
                : new Vector3(0.62f, 0.40f, 0.16f);
            nose.transform.localPosition = dir * (length * 0.5f - 0.06f) + Vector3.up * 0.02f;
            Paint(nose, new Color(0.97f, 0.97f, 0.95f));
            Destroy(nose.GetComponent<Collider>());

            // Colour-blind glyph, lying flat on the roof.
            var glyph = new GameObject("Glyph");
            glyph.transform.SetParent(go.transform, false);
            glyph.transform.localPosition = new Vector3(0f, 0.28f, 0f);
            glyph.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var textMesh = glyph.AddComponent<TextMesh>();
            textMesh.text = Palette.GlyphOf(bus.Color);
            textMesh.font = UiBuilder.Font;
            textMesh.characterSize = 0.08f;
            textMesh.fontSize = 72;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.color = new Color(1f, 1f, 1f, 0.92f);
            var glyphRenderer = glyph.GetComponent<MeshRenderer>();
            if (glyphRenderer != null && textMesh.font != null)
                glyphRenderer.material = textMesh.font.material;

            var collider = go.AddComponent<BoxCollider>();
            collider.size = horizontal
                ? new Vector3(length, 0.7f, 0.9f)
                : new Vector3(0.9f, 0.7f, length);

            var view = go.AddComponent<BusView>();
            view.Initialise(bus, body.GetComponent<Renderer>());
            view.Tapped += id => BusTapped?.Invoke(id);
            _buses[bus.Id] = view;
        }

        /// <summary>
        /// Makes the visuals match a game state exactly: buses that have already left
        /// are removed, docked buses are parked in their bay, everything else stays in
        /// the lot. Undo and level load both go through here, so there is exactly one
        /// piece of code that turns state into a picture.
        /// </summary>
        public void ApplyState(GameState state)
        {
            var docked = new Dictionary<int, int>();
            for (int i = 0; i < state.Bays.Count; i++)
            {
                Bay bay = state.Bays[i];
                if (!bay.IsFree) docked[bay.BusId] = i;
            }

            foreach (int busId in new List<int>(_buses.Keys))
            {
                BusView view = _buses[busId];
                if (view == null)
                {
                    _buses.Remove(busId);
                    continue;
                }

                if (docked.TryGetValue(busId, out int bayIndex))
                {
                    view.transform.localPosition = BayPosition(bayIndex) + Vector3.up * 0.3f;
                    view.transform.localRotation =
                        Quaternion.LookRotation(Vector3.back, Vector3.up);
                    view.SetDocked(true);
                }
                else if (!state.IsBusInLot(busId))
                {
                    // Already full and gone.
                    Destroy(view.gameObject);
                    _buses.Remove(busId);
                }
            }

            RefreshQueue(state.QueueIndex);
        }

        public static Vector3 FacingToWorld(Facing facing) => facing switch
        {
            Facing.Up => Vector3.forward,
            Facing.Down => Vector3.back,
            Facing.Left => Vector3.left,
            _ => Vector3.right
        };

        // ----- queue ----------------------------------------------------------- //

        /// <summary>Rebuilds the visible queue from the given index onwards.</summary>
        public void RefreshQueue(int fromIndex)
        {
            foreach (GameObject p in _passengers)
            {
                if (p != null) Destroy(p);
            }
            _passengers.Clear();

            int shown = 0;
            for (int i = fromIndex; i < _level.Queue.Count && shown < MaxVisibleQueue; i++, shown++)
            {
                Passenger passenger = _level.Queue[i];
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = $"Passenger {i} ({passenger.Color})";
                go.transform.SetParent(_root, false);
                go.transform.localScale = new Vector3(0.30f, 0.22f, 0.30f);
                go.transform.localPosition = QueuePosition(shown);
                Paint(go, Palette.Of(passenger.Color));
                Destroy(go.GetComponent<Collider>());

                // The head of the queue is the only one who can board - say so.
                if (shown == 0) go.transform.localScale *= 1.25f;

                _passengers.Add(go);
            }

            if (_level.Queue.Count - fromIndex > MaxVisibleQueue)
            {
                // Deliberately no "+n more" label in the prototype: the peek limit is
                // part of the puzzle (CONCEPT.md §4, "queue peek").
            }
        }

        // ----- animation ------------------------------------------------------- //

        public IEnumerator PlayDispatch(Bus bus, int bayIndex)
        {
            if (!_buses.TryGetValue(bus.Id, out BusView view) || view == null) yield break;

            Vector3 start = view.transform.localPosition;
            Vector3 dir = FacingToWorld(bus.Facing);
            float runway = bus.ExitPath.Count + 1.5f;
            Vector3 offBoard = start + dir * runway;

            yield return Tween(view.transform, start, offBoard, DriveOutDuration,
                               EaseInQuad);

            Vector3 bay = BayPosition(bayIndex) + Vector3.up * 0.3f;
            Quaternion faceQueue = Quaternion.LookRotation(Vector3.back, Vector3.up);
            yield return TweenWithRotation(view.transform, offBoard, bay,
                                           view.transform.localRotation, faceQueue,
                                           DockDuration, EaseOutQuad);
            view.SetDocked(true);
        }

        public IEnumerator PlayBoarding(int queueSlot, int bayIndex, int seatsLeft)
        {
            if (queueSlot >= _passengers.Count) yield break;
            GameObject passenger = _passengers[queueSlot];
            if (passenger == null) yield break;

            Vector3 from = passenger.transform.localPosition;
            Vector3 to = BayPosition(bayIndex) + Vector3.up * 0.3f;

            float elapsed = 0f;
            while (elapsed < BoardDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / BoardDuration);
                // A small hop: the arc is what makes boarding feel cheerful.
                Vector3 pos = Vector3.Lerp(from, to, t);
                pos.y += Mathf.Sin(t * Mathf.PI) * 0.45f;
                passenger.transform.localPosition = pos;
                passenger.transform.localScale *= 1f - 0.015f;
                yield return null;
            }
            Destroy(passenger);
        }

        public IEnumerator PlayDeparture(int busId)
        {
            if (!_buses.TryGetValue(busId, out BusView view) || view == null) yield break;
            _buses.Remove(busId);

            Vector3 start = view.transform.localPosition;
            Vector3 end = start + Vector3.right * 12f;
            yield return Tween(view.transform, start, end, DepartDuration, EaseInQuad);
            if (view != null) Destroy(view.gameObject);
        }

        /// <summary>Shake and honk: the tap was understood, the move was not possible.</summary>
        public IEnumerator PlayRefusal(int busId)
        {
            if (!_buses.TryGetValue(busId, out BusView view) || view == null) yield break;
            Vector3 origin = view.transform.localPosition;
            const float duration = 0.22f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float offset = Mathf.Sin(t * Mathf.PI * 6f) * 0.07f * (1f - t);
                view.transform.localPosition = origin + Vector3.right * offset;
                yield return null;
            }
            view.transform.localPosition = origin;
        }

        private static IEnumerator Tween(Transform target, Vector3 from, Vector3 to,
                                         float duration, System.Func<float, float> ease)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = ease(Mathf.Clamp01(elapsed / duration));
                if (target == null) yield break;
                target.localPosition = Vector3.Lerp(from, to, t);
                yield return null;
            }
            if (target != null) target.localPosition = to;
        }

        private static IEnumerator TweenWithRotation(Transform target, Vector3 from,
                                                     Vector3 to, Quaternion fromRot,
                                                     Quaternion toRot, float duration,
                                                     System.Func<float, float> ease)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = ease(Mathf.Clamp01(elapsed / duration));
                if (target == null) yield break;
                target.localPosition = Vector3.Lerp(from, to, t);
                target.localRotation = Quaternion.Slerp(fromRot, toRot, t);
                yield return null;
            }
            if (target == null) yield break;
            target.localPosition = to;
            target.localRotation = toRot;
        }

        private static float EaseInQuad(float t) => t * t;
        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

        private static void Paint(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = UiBuilder.CreateLitMaterial(color);
        }
    }
}
