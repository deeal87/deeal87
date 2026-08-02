"""Sunny Stop — pure rules engine (reference implementation).

This module is the single source of truth for the game rules. The C# `Core`
library under Assets/Scripts/Core mirrors it 1:1; both are engine-free and
side-effect free so that levels verified here behave identically in the game.

Board model
-----------
* The lot is a w x h grid. Origin is top-left, +x right, +y down.
* A bus occupies N collinear cells along its facing axis and drives straight
  out of the grid along `facing`. Its path must be free of buses and blocked
  cells all the way to the border.
* A dispatched bus docks in the LEFTMOST free bay. It stays until it is full.
* Only the passenger at the HEAD of the queue may board, and only into the
  leftmost docked bus of the matching colour that has enough free seats.
* Win: queue empty.  Dead: queue non-empty and no bus can be dispatched.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Iterable, Optional, Sequence

DIRECTIONS = {
    "up": (0, -1),
    "down": (0, 1),
    "left": (-1, 0),
    "right": (1, 0),
}


@dataclass(frozen=True)
class Bus:
    id: int
    color: str
    cells: tuple[tuple[int, int], ...]
    facing: str
    capacity: int = 3

    @property
    def length(self) -> int:
        return len(self.cells)

    def front_cell(self) -> tuple[int, int]:
        """The cell that leads the bus when it drives along `facing`."""
        dx, dy = DIRECTIONS[self.facing]
        return max(self.cells, key=lambda c: c[0] * dx + c[1] * dy)


@dataclass(frozen=True)
class Passenger:
    color: str
    luggage: bool = False

    @property
    def seats(self) -> int:
        return 2 if self.luggage else 1


@dataclass(frozen=True)
class Level:
    id: int
    width: int
    height: int
    bays: int
    buses: tuple[Bus, ...]
    queue: tuple[Passenger, ...]
    blocked: frozenset[tuple[int, int]] = frozenset()
    chapter: int = 1
    move_limit: Optional[int] = None

    @property
    def colors(self) -> list[str]:
        seen: list[str] = []
        for b in self.buses:
            if b.color not in seen:
                seen.append(b.color)
        return seen

    def bus_by_id(self, bus_id: int) -> Bus:
        for b in self.buses:
            if b.id == bus_id:
                return b
        raise KeyError(bus_id)


def exit_path(level: Level, bus: Bus) -> list[tuple[int, int]]:
    """Cells the bus must cross to leave the lot, excluding its own cells."""
    dx, dy = DIRECTIONS[bus.facing]
    x, y = bus.front_cell()
    path: list[tuple[int, int]] = []
    x, y = x + dx, y + dy
    while 0 <= x < level.width and 0 <= y < level.height:
        path.append((x, y))
        x, y = x + dx, y + dy
    return path


def is_straight(bus: Bus) -> bool:
    """A bus must be a straight run of cells aligned with its facing axis."""
    xs = {c[0] for c in bus.cells}
    ys = {c[1] for c in bus.cells}
    dx, _dy = DIRECTIONS[bus.facing]
    horizontal = len(ys) == 1
    if horizontal:
        if dx == 0:
            return False
        return sorted(xs) == list(range(min(xs), min(xs) + len(bus.cells)))
    if len(xs) != 1 or dx != 0:
        return False
    return sorted(ys) == list(range(min(ys), min(ys) + len(bus.cells)))


def validate_level(level: Level) -> list[str]:
    """Structural checks. Returns a list of problems; empty means well formed."""
    problems: list[str] = []
    occupied: dict[tuple[int, int], int] = {}
    for bus in level.buses:
        if not is_straight(bus):
            problems.append(f"bus {bus.id} is not a straight run along {bus.facing}")
        for cell in bus.cells:
            x, y = cell
            if not (0 <= x < level.width and 0 <= y < level.height):
                problems.append(f"bus {bus.id} cell {cell} is outside the grid")
            if cell in level.blocked:
                problems.append(f"bus {bus.id} cell {cell} sits on a blocked cell")
            if cell in occupied:
                problems.append(f"bus {bus.id} overlaps bus {occupied[cell]} at {cell}")
            occupied[cell] = bus.id
    for bus in level.buses:
        if any(c in level.blocked for c in exit_path(level, bus)):
            problems.append(f"bus {bus.id} can never leave: blocked cell on its path")

    seats = {}
    for bus in level.buses:
        seats[bus.color] = seats.get(bus.color, 0) + bus.capacity
    needed: dict[str, int] = {}
    for p in level.queue:
        needed[p.color] = needed.get(p.color, 0) + p.seats
    for color, n in needed.items():
        if seats.get(color, 0) < n:
            problems.append(f"not enough {color} seats: need {n}, have {seats.get(color, 0)}")
    for color in seats:
        if color not in needed:
            problems.append(f"colour {color} has buses but no passengers")

    # Every seat on the board is spoken for, per colour. Without this a level
    # can be won while buses are still parked in the lot, which reads to the
    # player as a bug rather than as a puzzle (see the generator docstring).
    # Checked per colour, not just in total: matching totals with mismatched
    # colours would leave a red bus stranded and a blue one over-supplied.
    for color, have in sorted(seats.items()):
        want = needed.get(color, 0)
        if have > want:
            problems.append(
                f"{have - want} {color} seat(s) can never be filled: "
                f"{have} on the board, {want} in the queue")
    if level.bays < 1:
        problems.append("a level needs at least one bay")
    return problems


# --------------------------------------------------------------------------- #
# Game state
# --------------------------------------------------------------------------- #

# A bay is either None (free) or (color, seats_remaining, bus_id).
Bay = Optional[tuple[str, int, int]]


@dataclass(frozen=True)
class State:
    """Immutable game state. `in_lot` is the set of bus ids still parked."""

    in_lot: frozenset[int]
    bays: tuple[Bay, ...]
    queue_index: int
    moves: int = 0

    def key(self) -> tuple:
        """Hashable identity used by the solver (move count excluded)."""
        return (self.in_lot, self.bays, self.queue_index)


def initial_state(level: Level) -> State:
    state = State(
        in_lot=frozenset(b.id for b in level.buses),
        bays=tuple([None] * level.bays),
        queue_index=0,
    )
    return resolve_boarding(level, state)


def resolve_boarding(level: Level, state: State) -> State:
    """Board the head of the queue repeatedly until nothing more can board."""
    bays = list(state.bays)
    idx = state.queue_index
    changed = True
    while changed and idx < len(level.queue):
        changed = False
        head = level.queue[idx]
        for i, bay in enumerate(bays):
            if bay is None:
                continue
            color, remaining, bus_id = bay
            if color == head.color and remaining >= head.seats:
                remaining -= head.seats
                bays[i] = None if remaining == 0 else (color, remaining, bus_id)
                idx += 1
                changed = True
                break
    return State(state.in_lot, tuple(bays), idx, state.moves)


def blocked_cells_for(level: Level, in_lot: Iterable[int]) -> set[tuple[int, int]]:
    cells = set(level.blocked)
    for bus_id in in_lot:
        cells.update(level.bus_by_id(bus_id).cells)
    return cells


def legal_moves(level: Level, state: State) -> list[int]:
    """Ids of buses that can be dispatched right now."""
    if None not in state.bays:
        return []
    if state.queue_index >= len(level.queue):
        return []
    occupied = blocked_cells_for(level, state.in_lot)
    moves = []
    for bus_id in state.in_lot:
        bus = level.bus_by_id(bus_id)
        if all(cell not in occupied for cell in exit_path(level, bus)):
            moves.append(bus_id)
    return sorted(moves)


def apply_move(level: Level, state: State, bus_id: int) -> State:
    """Dispatch a bus into the leftmost free bay, then resolve boarding."""
    if bus_id not in state.in_lot:
        raise ValueError(f"bus {bus_id} is not in the lot")
    bus = level.bus_by_id(bus_id)
    occupied = blocked_cells_for(level, state.in_lot)
    if any(cell in occupied for cell in exit_path(level, bus)):
        raise ValueError(f"bus {bus_id} is blocked")
    try:
        bay_index = state.bays.index(None)
    except ValueError:
        raise ValueError("no free bay") from None

    bays = list(state.bays)
    bays[bay_index] = (bus.color, bus.capacity, bus.id)
    nxt = State(
        in_lot=state.in_lot - {bus_id},
        bays=tuple(bays),
        queue_index=state.queue_index,
        moves=state.moves + 1,
    )
    return resolve_boarding(level, nxt)


def is_won(level: Level, state: State) -> bool:
    return state.queue_index >= len(level.queue)


def is_dead(level: Level, state: State) -> bool:
    """No win, and nothing left to do."""
    if is_won(level, state):
        return False
    if level.move_limit is not None and state.moves >= level.move_limit:
        return True
    return not legal_moves(level, state)


def play(level: Level, moves: Sequence[int]) -> State:
    """Replay a sequence of dispatches. Raises on an illegal move."""
    state = initial_state(level)
    for m in moves:
        state = apply_move(level, state, m)
    return state
