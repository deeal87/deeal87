"""Sunny Stop — postcard content gate.

The postcards are the product, so they get the same treatment as the levels:
invariants that must hold, checked on every commit.

    python3 -m unittest test_messages -v
"""

from __future__ import annotations

import json
import re
import unittest
from collections import Counter
from pathlib import Path

MESSAGE_DIR = Path(__file__).resolve().parents[2] / "Assets" / "Resources" / "Messages"

# One card per level plus headroom: nobody finishing all 200 levels may see a
# repeat, and the picker needs slack within each category to avoid one.
REQUIRED_TOTAL = 240
SITUATIONAL = ["recognition", "playful", "gentle", "grounding", "return"]
MILESTONE_LEVELS = [25, 50, 75, 100, 125, 150, 175, 200]

# The card sits on a phone-sized postcard. Longer than this and it wraps to a
# wall of text, which is the opposite of the intended two-second read.
MAX_LENGTH = 110

# Words that would turn a friendly line into a health claim (CONCEPT.md §6.2).
FORBIDDEN = [
    "depress", "angst", "therapie", "therapeut", "krank", "heilung", "diagnose",
    "psych", "trauma", "medikament",
]


def books() -> list[tuple[str, dict]]:
    found = []
    for path in sorted(MESSAGE_DIR.glob("messages.*.json")):
        found.append((path.name, json.loads(path.read_text(encoding="utf-8"))))
    return found


class TestMessageBooks(unittest.TestCase):
    def setUp(self):
        self.books = books()
        self.assertTrue(self.books, f"no message books in {MESSAGE_DIR}")

    def test_full_set_is_present(self):
        for name, book in self.books:
            with self.subTest(book=name):
                self.assertEqual(len(book["messages"]), REQUIRED_TOTAL)

    def test_ids_and_texts_are_unique(self):
        for name, book in self.books:
            with self.subTest(book=name):
                ids = [m["id"] for m in book["messages"]]
                texts = [m["text"] for m in book["messages"]]
                self.assertEqual(
                    [i for i, c in Counter(ids).items() if c > 1], [],
                    "duplicate ids")
                self.assertEqual(
                    [t for t, c in Counter(texts).items() if c > 1], [],
                    "duplicate texts")

    def test_every_situational_category_covers_every_tone(self):
        # Without this, a player who picked "quiet" would silently be served
        # the playful register whenever the picker had to widen its search.
        for name, book in self.books:
            for category in SITUATIONAL:
                tones = Counter(
                    m["tone"] for m in book["messages"] if m["category"] == category)
                for tone in book["tones"]:
                    with self.subTest(book=name, category=category, tone=tone):
                        self.assertGreaterEqual(
                            tones[tone], 8,
                            f"{category}/{tone} has only {tones[tone]} cards")

    def test_milestones_are_pinned_to_their_levels(self):
        for name, book in self.books:
            with self.subTest(book=name):
                levels = sorted(
                    m["level"] for m in book["messages"]
                    if m["category"] == "milestone")
                self.assertEqual(levels, MILESTONE_LEVELS)

    def test_only_milestones_are_pinned(self):
        for name, book in self.books:
            for m in book["messages"]:
                if m["category"] != "milestone":
                    with self.subTest(book=name, card=m["id"]):
                        self.assertNotIn("level", m)

    def test_categories_and_tones_are_declared(self):
        for name, book in self.books:
            for m in book["messages"]:
                with self.subTest(book=name, card=m["id"]):
                    self.assertIn(m["category"], book["categories"])
                    self.assertIn(m["tone"], book["tones"])

    def test_cards_fit_on_a_postcard(self):
        for name, book in self.books:
            for m in book["messages"]:
                with self.subTest(book=name, card=m["id"]):
                    self.assertLessEqual(len(m["text"]), MAX_LENGTH)
                    self.assertTrue(m["text"].strip(), "empty card")

    def test_no_medical_or_therapeutic_register(self):
        for name, book in self.books:
            for m in book["messages"]:
                lowered = m["text"].lower()
                for word in FORBIDDEN:
                    with self.subTest(book=name, card=m["id"], word=word):
                        self.assertNotIn(word, lowered)

    def test_tone_stays_calm(self):
        # House style: at most one exclamation mark in the whole book. Warmth
        # comes from what the line says, not from shouting it.
        for name, book in self.books:
            with self.subTest(book=name):
                shouty = [m["id"] for m in book["messages"] if "!" in m["text"]]
                self.assertLessEqual(len(shouty), 1, f"exclamation marks in {shouty}")

    def test_german_book_uses_real_orthography(self):
        # Guards against transliterated umlauts (ue/oe/ae/ss) creeping back in.
        for name, book in self.books:
            if book.get("locale") != "de":
                continue
            suspicious = re.compile(
                r"\b\w*(?:ue|oe|ae)\w*\b", re.IGNORECASE)
            allowed = {
                "auseinandergenommen", "neue", "neuen", "neues", "aber", "auer",
                "eue", "wahrscheinlich",
            }
            for m in book["messages"]:
                for hit in suspicious.findall(m["text"]):
                    lowered = hit.lower()
                    if lowered in allowed:
                        continue
                    # Legitimate German words containing these pairs are common
                    # (neu, heute, Leute...); only flag ones that would become a
                    # real word by substituting an umlaut.
                    for pair, umlaut in (("ue", "ü"), ("oe", "ö"), ("ae", "ä")):
                        if pair in lowered:
                            with self.subTest(book=name, card=m["id"], word=hit):
                                self.assertNotIn(
                                    lowered.replace(pair, umlaut),
                                    TRANSLITERATION_TRAPS,
                                    f"'{hit}' looks like a transliterated umlaut")


# Words that would indicate the file was written without umlauts.
TRANSLITERATION_TRAPS = {
    "schön", "für", "über", "zurück", "können", "möglich", "gefühl", "läuft",
    "zäh", "hängengeblieben", "völlig", "nächste", "früh", "draußen", "spät",
    "verspätung", "unverschämt", "hättest", "wäre", "gäste", "fahrgäste",
    "größer", "später", "müde",
}


if __name__ == "__main__":
    unittest.main()
