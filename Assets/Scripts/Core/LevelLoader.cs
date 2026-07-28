using System;
using System.Collections.Generic;

namespace SunnyStop.Core
{
    /// <summary>
    /// Reads the baked level JSON produced by tools/leveltool.
    /// Keep in step with tools/leveltool/levelio.py - the schema version guards it.
    /// </summary>
    public static class LevelLoader
    {
        public const int SupportedSchema = 1;

        public static LevelDefinition FromJson(string json)
        {
            JsonValue root = JsonValue.Parse(json);
            int schema = root.IntOr("schema", 1);
            if (schema > SupportedSchema)
            {
                throw new FormatException(
                    $"level schema {schema} is newer than this build supports " +
                    $"({SupportedSchema}) - update the app or re-bake the levels");
            }

            int id = root["id"].AsInt();
            int chapter = root.IntOr("chapter", 1);
            JsonValue grid = root["grid"];
            int width = grid["w"].AsInt();
            int height = grid["h"].AsInt();
            int bays = root["bays"].AsInt();

            var buses = new List<Bus>();
            foreach (JsonValue b in root["buses"].Items())
            {
                var cells = new List<Cell>();
                foreach (JsonValue c in b["cells"].Items())
                {
                    cells.Add(new Cell(c[0].AsInt(), c[1].AsInt()));
                }
                buses.Add(new Bus(
                    b["id"].AsInt(),
                    b["color"].AsString(),
                    cells,
                    FacingExtensions.Parse(b["facing"].AsString()),
                    b.IntOr("capacity", 3),
                    width,
                    height));
            }

            var blocked = new List<Cell>();
            if (root.Has("blocked"))
            {
                foreach (JsonValue c in root["blocked"].Items())
                {
                    blocked.Add(new Cell(c[0].AsInt(), c[1].AsInt()));
                }
            }

            var queue = new List<Passenger>();
            foreach (JsonValue p in root["queue"].Items())
            {
                queue.Add(new Passenger(p["color"].AsString(), p.BoolOr("luggage", false)));
            }

            int? moveLimit = root.Has("moveLimit") ? root["moveLimit"].AsInt() : (int?)null;

            var solution = new List<int>();
            if (root.Has("solution"))
            {
                foreach (JsonValue m in root["solution"].Items()) solution.Add(m.AsInt());
            }

            var level = new LevelDefinition(id, chapter, width, height, bays, buses,
                                            queue, blocked, moveLimit, solution);

            List<string> problems = level.Validate();
            if (problems.Count > 0)
            {
                throw new FormatException(
                    $"level {id} is malformed: {string.Join("; ", problems)}");
            }
            return level;
        }
    }
}
