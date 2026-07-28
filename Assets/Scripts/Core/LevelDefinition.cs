using System;
using System.Collections.Generic;
using System.Linq;

namespace SunnyStop.Core
{
    /// <summary>Grid direction a bus drives in.</summary>
    public enum Facing
    {
        Up,
        Down,
        Left,
        Right
    }

    public static class FacingExtensions
    {
        public static Cell Step(this Facing facing)
        {
            switch (facing)
            {
                case Facing.Up: return new Cell(0, -1);
                case Facing.Down: return new Cell(0, 1);
                case Facing.Left: return new Cell(-1, 0);
                case Facing.Right: return new Cell(1, 0);
                default: throw new ArgumentOutOfRangeException(nameof(facing));
            }
        }

        public static Facing Parse(string value)
        {
            switch (value)
            {
                case "up": return Facing.Up;
                case "down": return Facing.Down;
                case "left": return Facing.Left;
                case "right": return Facing.Right;
                default: throw new FormatException($"unknown facing '{value}'");
            }
        }
    }

    /// <summary>Integer grid coordinate. Origin top-left, +x right, +y down.</summary>
    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int X;
        public readonly int Y;

        public Cell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static Cell operator +(Cell a, Cell b) => new Cell(a.X + b.X, a.Y + b.Y);

        public bool Equals(Cell other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Cell other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X},{Y})";
    }

    public sealed class Bus
    {
        public int Id { get; }
        public string Color { get; }
        public IReadOnlyList<Cell> Cells { get; }
        public Facing Facing { get; }
        public int Capacity { get; }

        /// <summary>Cells the bus must cross to leave the lot, nearest first.</summary>
        public IReadOnlyList<Cell> ExitPath { get; }

        public Bus(int id, string color, IReadOnlyList<Cell> cells, Facing facing,
                   int capacity, int gridWidth, int gridHeight)
        {
            Id = id;
            Color = color;
            Cells = cells;
            Facing = facing;
            Capacity = capacity;
            ExitPath = BuildExitPath(gridWidth, gridHeight);
        }

        /// <summary>The leading cell when the bus drives along <see cref="Facing"/>.</summary>
        public Cell FrontCell()
        {
            Cell step = Facing.Step();
            Cell best = Cells[0];
            int bestScore = int.MinValue;
            foreach (Cell c in Cells)
            {
                int s = c.X * step.X + c.Y * step.Y;
                if (s > bestScore)
                {
                    bestScore = s;
                    best = c;
                }
            }
            return best;
        }

        private List<Cell> BuildExitPath(int width, int height)
        {
            var path = new List<Cell>();
            Cell step = Facing.Step();
            Cell cursor = FrontCell() + step;
            while (cursor.X >= 0 && cursor.X < width && cursor.Y >= 0 && cursor.Y < height)
            {
                path.Add(cursor);
                cursor += step;
            }
            return path;
        }
    }

    public sealed class Passenger
    {
        public string Color { get; }
        public bool Luggage { get; }
        public int Seats => Luggage ? 2 : 1;

        public Passenger(string color, bool luggage = false)
        {
            Color = color;
            Luggage = luggage;
        }
    }

    /// <summary>An immutable, fully validated level. Mirrors tools/leveltool/rules.py.</summary>
    public sealed class LevelDefinition
    {
        public int Id { get; }
        public int Chapter { get; }
        public int Width { get; }
        public int Height { get; }
        public int Bays { get; }
        public IReadOnlyList<Bus> Buses { get; }
        public IReadOnlyList<Passenger> Queue { get; }
        public IReadOnlyList<Cell> Blocked { get; }
        public int? MoveLimit { get; }
        /// <summary>Reference solution from the bake step. Metadata only - the runtime
        /// hint system re-solves from the live state instead (see <see cref="Solver"/>).</summary>
        public IReadOnlyList<int> ReferenceSolution { get; }

        private readonly Dictionary<int, Bus> _byId;
        private readonly HashSet<Cell> _blockedSet;

        public LevelDefinition(int id, int chapter, int width, int height, int bays,
                               IReadOnlyList<Bus> buses, IReadOnlyList<Passenger> queue,
                               IReadOnlyList<Cell> blocked, int? moveLimit,
                               IReadOnlyList<int> referenceSolution)
        {
            Id = id;
            Chapter = chapter;
            Width = width;
            Height = height;
            Bays = bays;
            Buses = buses;
            Queue = queue;
            Blocked = blocked;
            MoveLimit = moveLimit;
            ReferenceSolution = referenceSolution ?? Array.Empty<int>();
            _byId = buses.ToDictionary(b => b.Id);
            _blockedSet = new HashSet<Cell>(blocked);
        }

        public Bus BusById(int id) => _byId[id];
        public bool IsBlocked(Cell cell) => _blockedSet.Contains(cell);

        public IReadOnlyList<string> Colors
        {
            get
            {
                var seen = new List<string>();
                foreach (Bus b in Buses)
                {
                    if (!seen.Contains(b.Color)) seen.Add(b.Color);
                }
                return seen;
            }
        }

        /// <summary>Structural checks. Empty result means the level is well formed.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var occupied = new Dictionary<Cell, int>();
            foreach (Bus bus in Buses)
            {
                foreach (Cell cell in bus.Cells)
                {
                    if (cell.X < 0 || cell.X >= Width || cell.Y < 0 || cell.Y >= Height)
                        problems.Add($"bus {bus.Id} cell {cell} is outside the grid");
                    if (IsBlocked(cell))
                        problems.Add($"bus {bus.Id} cell {cell} sits on a blocked cell");
                    if (occupied.TryGetValue(cell, out int other))
                        problems.Add($"bus {bus.Id} overlaps bus {other} at {cell}");
                    occupied[cell] = bus.Id;
                }
                foreach (Cell cell in bus.ExitPath)
                {
                    if (IsBlocked(cell))
                    {
                        problems.Add($"bus {bus.Id} can never leave: blocked cell on its path");
                        break;
                    }
                }
            }

            var seats = new Dictionary<string, int>();
            foreach (Bus b in Buses)
                seats[b.Color] = seats.TryGetValue(b.Color, out int s) ? s + b.Capacity : b.Capacity;
            var needed = new Dictionary<string, int>();
            foreach (Passenger p in Queue)
                needed[p.Color] = needed.TryGetValue(p.Color, out int n) ? n + p.Seats : p.Seats;

            foreach (var kv in needed)
            {
                seats.TryGetValue(kv.Key, out int have);
                if (have < kv.Value)
                    problems.Add($"not enough {kv.Key} seats: need {kv.Value}, have {have}");
            }
            foreach (var kv in seats)
            {
                if (!needed.ContainsKey(kv.Key))
                    problems.Add($"colour {kv.Key} has buses but no passengers");
            }
            if (Bays < 1) problems.Add("a level needs at least one bay");
            return problems;
        }
    }
}
