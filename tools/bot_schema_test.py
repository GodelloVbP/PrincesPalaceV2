#!/usr/bin/env python3
"""Doc-vs-emitter coverage for docs/BOT_SUMMARY_SCHEMA.md.

    python tools/bot_schema_test.py

The C# side of ContentSchemaTests already refuses a Raw*Entry field that
CONTENT_SCHEMA.md does not mention (Assets/_Project/Scripts/Tests/EditMode/
Content/ContentSchemaTests.cs) -- this is the same check for the OTHER
generated doc that calls itself a contract (BOT_SUMMARY_SCHEMA.md's own
header, lines 1-7). Two halves, one gap each:

- `CellKeysAreDocumentedTests`: every top-level key `bot_merge.cell_json`
  puts into one `summary.json` cell must be named, in backticks or in the
  worked JSON example, somewhere in the doc. spellAcquisition shipped
  computed and wired into every cell (BalanceBotRunner.cs writes the
  RoomTrace fields it is built from, bot_merge.py aggregates them) with
  nobody updating the doc that claims to be the reader/writer contract --
  this is the test that would have caught that, and the one that catches
  the next metric shaped the same way.
- `RoomTraceFieldsAreDocumentedTests`: the other side of the same trace --
  every RoomTrace field BalanceBotRunner.cs.RunRowJson actually writes into
  a `rooms[]` row must be named in the doc's own `RoomTrace` block. b03fa207
  added the cell-key test above and used it to backfill the three spell
  fields, but noted in its own commit message that RoomTrace's *shop*
  fields (GoldOnArrival, ShopChoices, and the rest) were ALREADY
  undocumented before that commit, and that the new test never looked at
  RoomTrace at all -- this closes that half.

Built via `bot_merge.cell_json` directly, on the smallest run row that does
not trip a KeyError in any of its sub-computations (death_causes,
build_diversity, shop_json, spell_acquisition_json, ...) -- one fight, one
room, no shop visit, no spell acquired. The point of this fixture is
breadth of KEYS, not correctness of the numbers under them; the numbers are
`bot_merge_test.py`'s job.
"""

import os
import re
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bot_merge

TOOLS_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.join(TOOLS_DIR, "..")
SCHEMA_PATH = os.path.join(REPO_ROOT, "docs", "BOT_SUMMARY_SCHEMA.md")
RUNTRACE_PATH = os.path.join(REPO_ROOT, "Assets", "_Project", "Scripts",
                              "Domain", "Bot", "RunTrace.cs")
RUNNER_PATH = os.path.join(REPO_ROOT, "Assets", "_Project", "Scripts",
                            "Editor", "Bot", "BalanceBotRunner.cs")


def a_minimal_run():
    """One run, just enough shape for every cell_json sub-computation to run."""
    return {
        "seed": 1,
        "archetype": "RandomLegal",
        "profile": "Fresh",
        "capped": False,
        "deathStep": 8,
        "consumablesLeft": 1,
        "replayed": True,
        "hashMatched": True,
        "swingTurns": 1,
        "swingDenomTurns": 2,
        "relicIds": ["bloodlust"],
        "talentIds": ["hardy"],
        "gearIds": ["iron_sword"],
        "levelAtDeath": 3,
        "fights": [
            {"step": 8, "floor": 1, "roomType": "Fight", "enemyIds": ["giant_rat"],
             "turns": 2, "damageTaken": 0, "partyHpOut": 0, "partyMaxHp": 100,
             "usedItem": False, "won": False, "payoutGold": 0},
        ],
        "rooms": [
            {"step": 4, "nodeId": 1, "roomType": "Fight",
             "offerItemIds": [], "pickedIndex": -1, "equippedItemIds": [],
             "goldOnArrival": 0, "goldSpent": 0, "goldOnLeave": 0,
             "purchasesBySection": [], "rerollsBySection": [],
             "shopOffers": [], "shopChoices": [],
             "learnedSpellCountAfterRoom": 0, "unassignedSpellBookCountAfterRoom": 0,
             "spellAssignments": []},
        ],
        "relicRounds": [],
        "skillsUsed": [],
        "bugs": [],
    }


class CellKeysAreDocumentedTests(unittest.TestCase):
    def setUp(self):
        with open(SCHEMA_PATH, "r", encoding="utf-8") as f:
            self.schema_text = f.read()

    def key_is_documented(self, key):
        # Named either in backticks (the field-definitions prose) or as a
        # JSON key in the worked `summary.json` example -- either is a real
        # mention, not a coincidence: neither form is how the key would
        # appear if only mentioned in passing.
        return bool(re.search(r"`{}`".format(re.escape(key)), self.schema_text)) or \
            ('"{}":'.format(key)) in self.schema_text

    def test_every_cell_json_key_is_named_in_the_schema_doc(self):
        cell = bot_merge.cell_json([a_minimal_run()], cap=40)
        undocumented = sorted(k for k in cell if not self.key_is_documented(k))
        self.assertEqual(
            [], undocumented,
            "cells[] key(s) {} emitted by bot_merge.cell_json but not named "
            "in docs/BOT_SUMMARY_SCHEMA.md -- add a field-definition entry "
            "(or a mention in the worked example) for each.".format(undocumented))

    def test_spell_acquisition_sub_keys_are_named_in_the_schema_doc(self):
        # The metric this test file was added for: spell_acquisition_json's
        # own sub-keys, not just the top-level "spellAcquisition" wrapper.
        acquisition = bot_merge.spell_acquisition_json([a_minimal_run()], cap=40)
        undocumented = sorted(k for k in acquisition if not self.key_is_documented(k))
        self.assertEqual([], undocumented)


def _camel(pascal_name):
    return pascal_name[0].lower() + pascal_name[1:]


def room_trace_field_names():
    """Public field names declared directly on RoomTrace, in source order.

    Regex over the class body rather than a compiler -- RunTrace.cs is
    engine-free C#, one flat run of `public <type> Name;` /
    `public <type> Name = <init>;` lines and no nested class, so this does
    not need to be a real parser. Robust to the whitespace/init-expression
    variance actually present (`public int Step;`, `public int[] Foo = new
    int[0];`, `public List<Bar> Baz = new List<Bar>();`).
    """
    with open(RUNTRACE_PATH, "r", encoding="utf-8") as f:
        text = f.read()
    m = re.search(r"public sealed class RoomTrace\s*\{(.*?)\n    \}", text, re.DOTALL)
    if not m:
        raise AssertionError("RoomTrace class body not found in RunTrace.cs -- "
                              "did it move or get renamed?")
    body = m.group(1)
    return re.findall(r"public\s+[\w<>\[\],]+\s+(\w+)\s*(?:=[^;]*)?;", body)


def room_trace_serialized_keys():
    """{field name: JSON key} for every RoomTrace field BalanceBotRunner.cs's
    RunRowJson actually writes into a `rooms[]` row (runs.jsonl), plus the
    list of field names it never references there at all.

    Two ways a field reaches a key, both read off the writer rather than
    guessed:

    1. Straight camelCase -- `GoldOnArrival` written under literal key
       `"goldOnArrival"`. This is nearly every field, and is checked by
       literally looking for that escaped-quote key text in the room block
       (the writer builds JSON by hand, so the key text is a literal C#
       string constant, not something a formatter could rename quietly).
    2. Explicit/semantic key -- a field the straight rule does not find
       (today, only `Equipped`: the room block does not emit an
       `"equipped"` key at all, it projects `r.Equipped` down to item ids
       under `"equippedItemIds"`). For these, the key is read from the
       writer by finding the nearest `"key":` literal that precedes the
       first `r.<Field>` reference in the block -- still the writer's own
       text, not a hand-written mapping.

    A field matching neither -- never referenced as `r.<Field>` anywhere in
    the room block -- is not serialized at all (a deliberate runtime-only
    field) and is returned separately so the doc-coverage test does not
    demand a row for it.
    """
    with open(RUNNER_PATH, "r", encoding="utf-8") as f:
        runner_text = f.read()

    # Isolate the one room-object loop inside RunRowJson (runs.jsonl), the
    # block the task/commit both point at (BalanceBotRunner.cs ~640-720).
    # Plain substring search rather than a regex across the escaped quotes:
    # the C# source spells the JSON delimiters as literal `\"`, and a regex
    # written against that is harder to read than the marker strings are.
    start_marker = 'sb.Append("\\"rooms\\":[")'
    end_marker = 'sb.Append("],")'
    start = runner_text.find(start_marker)
    if start < 0:
        raise AssertionError("Could not find the rooms[] serialization start "
                              "marker in BalanceBotRunner.cs -- RunRowJson's "
                              "shape changed under this test.")
    end = runner_text.find(end_marker, start)
    if end < 0:
        raise AssertionError("Could not find the rooms[] serialization end "
                              "marker in BalanceBotRunner.cs after its start.")
    block = runner_text[start + len(start_marker):end]

    key_positions = [(mm.start(), mm.group(1))
                      for mm in re.finditer(r'\\"(\w+)\\":', block)]

    serialized = {}
    unserialized = []
    for field in room_trace_field_names():
        key = _camel(field)
        if '\\"{}\\":'.format(key) in block:
            serialized[field] = key
            continue
        ref = re.search(r"\br\." + re.escape(field) + r"\b", block)
        if ref:
            preceding = [k for pos, k in key_positions if pos < ref.start()]
            if preceding:
                serialized[field] = preceding[-1]
                continue
        unserialized.append(field)
    return serialized, unserialized


def room_trace_doc_section(schema_text):
    """The text of docs/BOT_SUMMARY_SCHEMA.md's `RoomTrace` type block --
    from the `RoomTrace` header line up to the next line that starts a new
    type block (column 0, no indent) -- so a field name mentioned only in
    some OTHER type's block (e.g. `ShopOffers` used as a type name) does not
    count as RoomTrace documenting itself.
    """
    m = re.search(r"^RoomTrace\n(.*?)(?=^\S)", schema_text, re.DOTALL | re.MULTILINE)
    if not m:
        raise AssertionError("No `RoomTrace` type block found in "
                              "docs/BOT_SUMMARY_SCHEMA.md -- section renamed "
                              "or restructured under this test.")
    return m.group(1)


class RoomTraceFieldsAreDocumentedTests(unittest.TestCase):
    """The room-row half of the doc-vs-emitter check above: RoomTrace ships
    fields BalanceBotRunner.cs writes into every `rooms[]` row that
    docs/BOT_SUMMARY_SCHEMA.md's `RoomTrace` block never mentioned
    (`GoldOnArrival`, `ShopChoices`, and the rest of the shop group) --
    exactly the gap b03fa207 (the spellAcquisition commit) named but did not
    close, because its own new test only ever looked at `cells[]` keys.
    """

    def setUp(self):
        with open(SCHEMA_PATH, "r", encoding="utf-8") as f:
            self.schema_text = f.read()
        self.room_section = room_trace_doc_section(self.schema_text)

    def test_every_serialized_room_trace_field_is_named_in_the_doc(self):
        serialized, _unserialized = room_trace_serialized_keys()
        undocumented = sorted(
            field for field in serialized
            if not re.search(r"\b{}\b".format(re.escape(field)), self.room_section)
        )
        self.assertEqual(
            [], undocumented,
            "RoomTrace field(s) {} are written into every rooms[] row by "
            "BalanceBotRunner.cs.RunRowJson but not named in "
            "docs/BOT_SUMMARY_SCHEMA.md's RoomTrace block -- add a "
            "field-definition row for each.".format(undocumented))

    def test_room_trace_has_fields_left_to_check(self):
        # A regex that silently stopped matching (RoomTrace renamed, moved,
        # reshaped) would make the test above vacuously pass on an empty
        # dict. Pin a floor so that failure mode is loud instead of quiet.
        serialized, _unserialized = room_trace_serialized_keys()
        self.assertGreaterEqual(len(serialized), 15)


if __name__ == "__main__":
    unittest.main(verbosity=2)
