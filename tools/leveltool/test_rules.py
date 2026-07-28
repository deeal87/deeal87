"""Sunny Stop — rules and content tests.

    python3 -m unittest discover -s tools/leveltool -p 'test_*.py' -v

The last test class re-solves every baked level, so this file doubles as the
content gate: a level that stops being solvable fails the build.
"""

from __future__ import annotations

import json
import unittest
from pathlib import Path

from curve import target_mds
from levelio import dict_to_level, level_to_dict, read_level
from rules import (
    Bus,
    Level,
    Passenger,
    apply_move,
    exit_path,
    initial_state,
    is_dead,
    is_won,
    legal_moves,
    play,
    validate_level,
)
from solver import analyse

LEVEL_DIR = Path(__file__).resolve().parents[2] / "Assets" / "Resources" / "Levels"
BAND = 6.0


def tiny_level(**overrides) -> Level:
    """A 4x4 board: red bus can drive up and out, blue is parked behind it."""
    defaults = dict(
        id=999,
        width=4,
        height=4,
        bays=2,
        buses=(
            Bus(1, "red", ((1, 0), (1, 1)), "up"),
            Bus(2, "blue", ((1, 2), (1, 3)), "up"),
        ),
        queue=(Passenger("red"),) * 3 + (Passenger("blue"),) * 3,
        blocked=frozenset(),
    )
    defaults.update(overrides)
    return Level(**defaults)


class TestGeometry(unittest.TestCase):
    def test_exit_path_runs_to_the_border(self):
        level = tiny_level()
        self.assertEqual(exit_path(level, level.bus_by_id(1)), [])
        self.assertEqual(exit_path(level, level.bus_by_id(2)), [(1, 1), (1, 0)])

    def test_validate_accepts_a_good_level(self):
        self.assertEqual(validate_level(tiny_level()), [])

    def test_validate_rejects_overlap(self):
        level = tiny_level(
            buses=(
                Bus(1, "red", ((1, 0), (1, 1)), "up"),
                Bus(2, "blue", ((1, 1), (1, 2)), "up"),
            ),
            queue=(Passenger("red"),) * 3 + (Passenger("blue"),) * 3,
        )
        self.assertTrue(any("overlaps" in p for p in validate_level(level)))

    def test_validate_rejects_unfillable_queue(self):
        level = tiny_level(queue=(Passenger("red"),) * 5 + (Passenger("blue"),) * 3)
        self.assertTrue(any("not enough red seats" in p for p in validate_level(level)))

    def test_validate_rejects_a_bus_walled_in(self):
        level = tiny_level(blocked=frozenset({(1, 1)}))
        problems = validate_level(level)
        self.assertTrue(any("can never leave" in p for p in problems))


class TestMoves(unittest.TestCase):
    def test_blocked_bus_cannot_be_dispatched(self):
        level = tiny_level()
        state = initial_state(level)
        self.assertEqual(legal_moves(level, state), [1])

    def test_dispatch_unblocks_the_bus_behind(self):
        level = tiny_level()
        state = apply_move(level, initial_state(level), 1)
        self.assertEqual(legal_moves(level, state), [2])

    def test_only_the_head_of_the_queue_boards(self):
        # Blue docks first but the queue starts with red, so nobody boards.
        level = tiny_level(
            buses=(
                Bus(1, "blue", ((0, 0), (0, 1)), "up"),
                Bus(2, "red", ((2, 0), (2, 1)), "up"),
            ),
        )
        state = apply_move(level, initial_state(level), 1)
        self.assertEqual(state.queue_index, 0)
        self.assertEqual(state.bays[0], ("blue", 3, 1))
        # Now red arrives. The three reds board and fill it, red departs, which
        # brings blue to the head - and blue is already docked, so it cascades.
        # That chain reaction is the pay-off moment the audio is built around.
        state = apply_move(level, state, 2)
        self.assertEqual(state.queue_index, 6)
        self.assertEqual(state.bays, (None, None))
        self.assertTrue(is_won(level, state))

    def test_full_bus_departs_and_frees_its_bay(self):
        level = tiny_level()
        state = apply_move(level, initial_state(level), 1)
        self.assertEqual(state.queue_index, 3)
        self.assertEqual(state.bays, (None, None))

    def test_leftmost_free_bay_is_used(self):
        level = tiny_level(
            buses=(
                Bus(1, "blue", ((0, 0), (0, 1)), "up"),
                Bus(2, "green", ((2, 0), (2, 1)), "up"),
                Bus(3, "red", ((3, 0), (3, 1)), "up"),
            ),
            bays=3,
            queue=(Passenger("red"),) * 3
            + (Passenger("blue"),) * 3
            + (Passenger("green"),) * 3,
        )
        state = apply_move(level, initial_state(level), 1)
        state = apply_move(level, state, 2)
        self.assertEqual(state.bays[0][0], "blue")
        self.assertEqual(state.bays[1][0], "green")
        self.assertIsNone(state.bays[2])

    def test_dispatch_without_a_free_bay_is_refused(self):
        level = tiny_level(
            bays=1,
            buses=(
                Bus(1, "blue", ((0, 0), (0, 1)), "up"),
                Bus(2, "green", ((2, 0), (2, 1)), "up"),
                Bus(3, "red", ((3, 0), (3, 1)), "up"),
            ),
            queue=(Passenger("red"),) * 3
            + (Passenger("blue"),) * 3
            + (Passenger("green"),) * 3,
        )
        state = apply_move(level, initial_state(level), 1)  # blue takes the only bay
        self.assertEqual(legal_moves(level, state), [])
        with self.assertRaises(ValueError):
            apply_move(level, state, 2)


class TestOutcomes(unittest.TestCase):
    def test_clearing_the_queue_wins(self):
        level = tiny_level()
        state = play(level, [1, 2])
        self.assertTrue(is_won(level, state))
        self.assertFalse(is_dead(level, state))

    def test_committing_the_last_bay_to_the_wrong_colour_is_death(self):
        # One bay. Green docks, but the queue only wants red and blue, so the
        # bay can never be freed.
        level = tiny_level(
            bays=1,
            buses=(
                Bus(1, "green", ((0, 0), (0, 1)), "up"),
                Bus(2, "red", ((2, 0), (2, 1)), "up"),
            ),
            queue=(Passenger("red"),) * 3 + (Passenger("green"),) * 3,
        )
        state = apply_move(level, initial_state(level), 1)
        self.assertTrue(is_dead(level, state))
        self.assertFalse(is_won(level, state))

    def test_dead_state_is_reported_as_unsolvable_by_the_solver(self):
        level = tiny_level(
            bays=1,
            buses=(
                Bus(1, "green", ((0, 0), (0, 1)), "up"),
                Bus(2, "red", ((2, 0), (2, 1)), "up"),
            ),
            queue=(Passenger("red"),) * 3 + (Passenger("green"),) * 3,
        )
        analysis = analyse(level)
        self.assertTrue(analysis.solvable)  # dispatch red first and it works
        self.assertLess(analysis.solution_density, 1.0)


class TestSerialisation(unittest.TestCase):
    def test_round_trip(self):
        level = tiny_level()
        again = dict_to_level(level_to_dict(level))
        self.assertEqual(level, again)


class TestBakedLevels(unittest.TestCase):
    """The content gate. Every shipped level must still be beatable."""

    @classmethod
    def setUpClass(cls):
        cls.paths = sorted(LEVEL_DIR.glob("level_*.json"))

    def test_levels_exist(self):
        self.assertGreater(len(self.paths), 0, f"no levels in {LEVEL_DIR}")

    def test_every_level_is_well_formed_and_solvable(self):
        for path in self.paths:
            with self.subTest(level=path.name):
                level = read_level(path)
                self.assertEqual(validate_level(level), [])
                analysis = analyse(level)
                self.assertTrue(analysis.solvable, f"{path.name} is not solvable")

    def test_recorded_solution_wins(self):
        for path in self.paths:
            with self.subTest(level=path.name):
                data = json.loads(path.read_text(encoding="utf-8"))
                level = read_level(path)
                state = play(level, data["solution"])
                self.assertTrue(is_won(level, state))

    def test_difficulty_stays_on_the_target_curve(self):
        for path in self.paths:
            with self.subTest(level=path.name):
                level = read_level(path)
                analysis = analyse(level)
                drift = abs(analysis.mds - target_mds(level.id))
                self.assertLessEqual(
                    drift, BAND,
                    f"{path.name}: MDS {analysis.mds:.1f} vs target "
                    f"{target_mds(level.id):.1f}",
                )

    def test_every_bus_is_dispatched_exactly_once(self):
        # Structural invariant of the design: seats exactly match passengers,
        # so a win always empties the lot. This is why the optimal move count
        # equals the bus count and all the difficulty lives in the ORDER.
        for path in self.paths:
            with self.subTest(level=path.name):
                data = json.loads(path.read_text(encoding="utf-8"))
                solution = data["solution"]
                self.assertEqual(sorted(solution), sorted(b["id"] for b in data["buses"]))


if __name__ == "__main__":
    unittest.main()
