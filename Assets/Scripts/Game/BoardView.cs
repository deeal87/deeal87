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

        // The waiting crowd is one packed tray rather than a line (CONCEPT.md
        // §3.1). Row 0 is nearest the stands and is the front of the queue; the
        // pile drains towards the board, so the ordering is still readable but
        // costs a look instead of being free.
        private const int TrayColumns = 12;
        private const float TrayWidth = 6.4f;
        private const float TraySpacing = TrayWidth / TrayColumns;
        private const float TrayRowPitch = TraySpacing * 0.86f;

        public float DriveOutDuration = 0.34f;
        public float DockDuration = 0.30f;
        public float BoardDuration = 0.16f;
        public float DepartDuration = 0.42f;

        /// <summary>Raised when the player taps a bus that is still in the lot.</summary>
        public event Action<int> BusTapped;

        private LevelDefinition _level;
        private readonly Dictionary<int, BusView> _buses = new Dictionary<int, BusView>();
        private readonly List<GameObject> _passengers = new List<GameObject>();
        private readonly List<Transform> _bayMarkers = new List<Transform>();
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

        /// <summary>
        /// Where the k-th still-waiting passenger stands. k = 0 boards next.
        /// Hexagonal packing: odd rows hold one fewer and are inset by half a
        /// place, which is what makes it read as a pile rather than a grid.
        /// </summary>
        private Vector3 QueuePosition(int k)
        {
            int row = 0;
            int consumed = 0;
            while (true)
            {
                int perRow = (row % 2 == 0) ? TrayColumns : TrayColumns - 1;
                if (k < consumed + perRow) break;
                consumed += perRow;
                row++;
            }

            int column = k - consumed;
            float inset = (row % 2 == 0) ? 0f : TraySpacing * 0.5f;
            float x = -TrayWidth * 0.5f + TraySpacing * 0.5f + inset + column * TraySpacing;
            float z = FrontRowZ - QueueRowOffset - row * TrayRowPitch;
            return new Vector3(x, 0.25f, z);
        }

        /// <summary>
        /// Deterministic scatter in the range -0.5..0.5.
        ///
        /// Seeded from the level and the passenger's own queue index, never from
        /// a runtime RNG: the board has to be identical for every player and on
        /// every replay, or the solver behind the free rewind is answering about
        /// a different board than the one on screen (CONCEPT.md §3.2, §5.1).
        /// This must match the browser preview's `seeded()`.
        /// </summary>
        private static float Jitter(int levelId, int queueIndex, int salt)
        {
            unchecked
            {
                uint s = (uint)(levelId * 7919 + queueIndex * 131 + salt * 17);
                s = s * 1664525u + 1013904223u;
                return s / 4294967296f - 0.5f;
            }
        }

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

            // The apron the stands are painted on.
            var apron = GameObject.CreatePrimitive(PrimitiveType.Cube);
            apron.name = "Apron";
            apron.transform.SetParent(_root, false);
            apron.transform.localScale = new Vector3(
                _level.Width * CellSize + 2.5f, 0.2f, QueueRowOffset - 0.4f);
            apron.transform.localPosition = new Vector3(
                0f, -0.12f, FrontRowZ - (QueueRowOffset + 0.4f) * 0.5f);
            Paint(apron, new Color(0.78f, 0.77f, 0.73f));
            Destroy(apron.GetComponent<Collider>());

            // The tray the crowd stands in: a recessed board sized to hold the
            // whole queue, so it never resizes as the pile drains.
            int rows = TrayRowCount(_level.Queue.Count);
            var tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tray.name = "Tray";
            tray.transform.SetParent(_root, false);
            tray.transform.localScale = new Vector3(
                TrayWidth + 0.5f, 0.18f, (rows - 1) * TrayRowPitch + TraySpacing + 0.5f);
            tray.transform.localPosition = new Vector3(
                0f, -0.16f, FrontRowZ - QueueRowOffset - (rows - 1) * TrayRowPitch * 0.5f);
            Paint(tray, new Color(0.70f, 0.70f, 0.69f));
            Destroy(tray.GetComponent<Collider>());
        }

        /// <summary>How many hex rows it takes to hold <paramref name="count"/> passengers.</summary>
        private static int TrayRowCount(int count)
        {
            int rows = 0;
            int held = 0;
            while (held < count)
            {
                held += (rows % 2 == 0) ? TrayColumns : TrayColumns - 1;
                rows++;
            }
            return Mathf.Max(1, rows);
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

            // Seat sockets, in boarding order. They start hidden and light up as
            // balls arrive, so capacity is countable without reading a number.
            //
            // Six fit along a body in one row. A twelve-seat double-decker gets
            // two - which is what a double-decker is, so the shape carries the
            // capacity rather than fighting it. Mirrors the browser preview.
            var view0 = go.AddComponent<BusView>();
            int perRow = bus.Capacity > 6 ? (bus.Capacity + 1) / 2 : bus.Capacity;
            Vector3 alongAxis = horizontal ? Vector3.right : Vector3.forward;
            Vector3 acrossAxis = horizontal ? Vector3.forward : Vector3.right;
            for (int i = 0; i < bus.Capacity; i++)
            {
                var seat = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                seat.name = $"Seat {i}";
                seat.transform.SetParent(go.transform, false);
                seat.transform.localScale = Vector3.one * 0.19f;

                int column = i % perRow;
                int deck = i / perRow;
                float t = perRow == 1 ? 0.5f : column / (float)(perRow - 1);
                float along = (t - 0.5f) * (length - 0.34f);
                // One row stays on the centre line; two straddle it.
                float across = bus.Capacity > perRow ? (deck == 0 ? -0.2f : 0.2f) : 0f;
                seat.transform.localPosition =
                    alongAxis * along + acrossAxis * across + Vector3.up * 0.3f;

                Paint(seat, new Color(0.16f, 0.16f, 0.18f));
                Destroy(seat.GetComponent<Collider>());
                var seatRenderer = seat.GetComponent<Renderer>();
                if (seatRenderer != null) seatRenderer.enabled = false;
                view0.AddSeat(seat.transform);
            }

            var collider = go.AddComponent<BoxCollider>();
            collider.size = horizontal
                ? new Vector3(length, 0.7f, 0.9f)
                : new Vector3(0.9f, 0.7f, length);

            BusView view = view0;
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

        public static Vector3 FacingToWorld(Facing facing)
        {
            switch (facing)
            {
                case Facing.Up: return Vector3.forward;
                case Facing.Down: return Vector3.back;
                case Facing.Left: return Vector3.left;
                default: return Vector3.right;
            }
        }

        // ----- queue ----------------------------------------------------------- //

        /// <summary>Rebuilds the visible queue from the given index onwards.</summary>
        public void RefreshQueue(int fromIndex)
        {
            foreach (GameObject p in _passengers)
            {
                if (p != null) Destroy(p);
            }
            _passengers.Clear();

            // Every waiting passenger is drawn. The queue order decides the level,
            // so hiding the tail behind a "+n more" would be hiding the puzzle.
            int shown = 0;
            for (int i = fromIndex; i < _level.Queue.Count; i++, shown++)
            {
                Passenger passenger = _level.Queue[i];
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"Passenger {i} ({passenger.Color})";
                go.transform.SetParent(_root, false);
                go.transform.localScale = Vector3.one * (passenger.Luggage ? 0.34f : 0.28f);

                Vector3 slot = QueuePosition(shown);
                slot.x += Jitter(_level.Id, i, 1) * TraySpacing * 0.12f;
                slot.z += Jitter(_level.Id, i, 2) * TraySpacing * 0.12f;
                go.transform.localPosition = slot;

                Paint(go, Palette.Of(passenger.Color));
                Destroy(go.GetComponent<Collider>());

                // The head of the queue is the only one who can board - say so.
                if (shown == 0) go.transform.localScale *= 1.25f;

                _passengers.Add(go);
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

        public IEnumerator PlayBoarding(int queueSlot, int bayIndex, int seatsLeft,
                                        int busId, int seats, string color)
        {
            if (queueSlot >= _passengers.Count) yield break;
            GameObject passenger = _passengers[queueSlot];
            if (passenger == null) yield break;

            // Fly to the seat the passenger will actually occupy.
            _buses.TryGetValue(busId, out BusView bus);
            Vector3 from = passenger.transform.localPosition;
            Vector3 to = bus != null
                ? _root.InverseTransformPoint(bus.NextSeatPosition())
                : BayPosition(bayIndex) + Vector3.up * 0.3f;

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
            if (bus != null) bus.FillSeats(seats, Palette.Of(color));
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
