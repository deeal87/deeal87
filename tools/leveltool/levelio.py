"""Sunny Stop — level (de)serialisation.

The JSON written here is exactly what the Unity client reads, so this module
and Assets/Scripts/Core/LevelData.cs must stay in step.
"""

from __future__ import annotations

import json
from pathlib import Path

from rules import Bus, Level, Passenger
from solver import Analysis

SCHEMA_VERSION = 1


def level_to_dict(level: Level, analysis: Analysis | None = None) -> dict:
    data = {
        "schema": SCHEMA_VERSION,
        "id": level.id,
        "chapter": level.chapter,
        "grid": {"w": level.width, "h": level.height},
        "bays": level.bays,
        "colors": level.colors,
        "buses": [
            {
                "id": b.id,
                "color": b.color,
                "cells": [[c[0], c[1]] for c in b.cells],
                "facing": b.facing,
                "capacity": b.capacity,
            }
            for b in level.buses
        ],
        "blocked": [[c[0], c[1]] for c in sorted(level.blocked)],
        "queue": [
            {"color": p.color, **({"luggage": True} if p.luggage else {})}
            for p in level.queue
        ],
    }
    if level.move_limit is not None:
        data["moveLimit"] = level.move_limit
    if analysis is not None:
        data["analysis"] = analysis.as_dict()
        data["solution"] = analysis.solution
    return data


def dict_to_level(data: dict) -> Level:
    buses = tuple(
        Bus(
            id=b["id"],
            color=b["color"],
            cells=tuple((c[0], c[1]) for c in b["cells"]),
            facing=b["facing"],
            capacity=b.get("capacity", 3),
        )
        for b in data["buses"]
    )
    queue = tuple(
        Passenger(color=p["color"], luggage=bool(p.get("luggage", False)))
        for p in data["queue"]
    )
    return Level(
        id=data["id"],
        width=data["grid"]["w"],
        height=data["grid"]["h"],
        bays=data["bays"],
        buses=buses,
        queue=queue,
        blocked=frozenset((c[0], c[1]) for c in data.get("blocked", [])),
        chapter=data.get("chapter", 1),
        move_limit=data.get("moveLimit"),
    )


def write_level(path: Path, level: Level, analysis: Analysis | None = None) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(level_to_dict(level, analysis), indent=2) + "\n", encoding="utf-8"
    )


def read_level(path: Path) -> Level:
    return dict_to_level(json.loads(path.read_text(encoding="utf-8")))
