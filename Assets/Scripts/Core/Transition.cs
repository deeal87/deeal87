using System.Collections.Generic;

namespace SunnyStop.Core
{
    public enum TransitionEventKind
    {
        BusDispatched,
        PassengerBoarded,
        BusDeparted
    }

    public readonly struct TransitionEvent
    {
        public readonly TransitionEventKind Kind;
        public readonly int BusId;
        public readonly int BayIndex;
        /// <summary>Index into the level queue, for PassengerBoarded.</summary>
        public readonly int QueueIndex;
        /// <summary>Seats left after the event, for PassengerBoarded.</summary>
        public readonly int SeatsLeft;

        public TransitionEvent(TransitionEventKind kind, int busId, int bayIndex,
                               int queueIndex, int seatsLeft)
        {
            Kind = kind;
            BusId = busId;
            BayIndex = bayIndex;
            QueueIndex = queueIndex;
            SeatsLeft = seatsLeft;
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case TransitionEventKind.BusDispatched:
                    return $"bus {BusId} -> bay {BayIndex}";
                case TransitionEventKind.PassengerBoarded:
                    return $"passenger {QueueIndex} -> bay {BayIndex} ({SeatsLeft} left)";
                default:
                    return $"bus {BusId} departs bay {BayIndex}";
            }
        }
    }

    /// <summary>
    /// Replays one dispatch as an ordered list of things the player should SEE.
    ///
    /// The view needs to know which passenger walked into which bay and when a bus
    /// filled up - but re-deriving that in the presentation layer would mean a second
    /// copy of the boarding rules that can silently drift from the real ones. So the
    /// ordering is produced here, next to the rules, and a unit test asserts the
    /// resulting state matches <see cref="GameState.Dispatch"/> exactly.
    /// </summary>
    public static class Transition
    {
        public static List<TransitionEvent> Compute(LevelDefinition level, GameState state,
                                                    int busId, out GameState result)
        {
            var events = new List<TransitionEvent>();

            Bus bus = level.BusById(busId);
            int bayIndex = -1;
            for (int i = 0; i < state.Bays.Count; i++)
            {
                if (state.Bays[i].IsFree) { bayIndex = i; break; }
            }

            result = state.Dispatch(level, busId);
            events.Add(new TransitionEvent(TransitionEventKind.BusDispatched, busId,
                                           bayIndex, 0, bus.Capacity));

            // Mirror the boarding loop, recording each step.
            var bays = new Bay[state.Bays.Count];
            for (int i = 0; i < bays.Length; i++) bays[i] = state.Bays[i];
            bays[bayIndex] = new Bay(bus.Id, bus.Color, bus.Capacity);

            int queueIndex = state.QueueIndex;
            bool changed = true;
            while (changed && queueIndex < level.Queue.Count)
            {
                changed = false;
                Passenger head = level.Queue[queueIndex];
                for (int i = 0; i < bays.Length; i++)
                {
                    Bay bay = bays[i];
                    if (bay.IsFree) continue;
                    if (bay.Color != head.Color || bay.SeatsLeft < head.Seats) continue;

                    int seatsLeft = bay.SeatsLeft - head.Seats;
                    int boardingBusId = bay.BusId;
                    bays[i] = bay.WithSeats(seatsLeft);

                    events.Add(new TransitionEvent(TransitionEventKind.PassengerBoarded,
                                                   boardingBusId, i, queueIndex, seatsLeft));
                    if (seatsLeft == 0)
                    {
                        events.Add(new TransitionEvent(TransitionEventKind.BusDeparted,
                                                       boardingBusId, i, queueIndex, 0));
                    }
                    queueIndex++;
                    changed = true;
                    break;
                }
            }
            return events;
        }
    }
}
