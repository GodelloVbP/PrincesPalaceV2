#!/usr/bin/env python3
"""Doc-vs-emitter coverage for docs/BOT_SUMMARY_SCHEMA.md's `cells[]` shape.

    python tools/bot_schema_test.py

The C# side of ContentSchemaTests already refuses a Raw*Entry field that
CONTENT_SCHEMA.md does not mention (Assets/_Project/Scripts/Tests/EditMode/
Content/ContentSchemaTests.cs) -- this is the same check for the OTHER
generated doc that calls itself a contract (BOT_SUMMARY_SCHEMA.md's own
header, lines 1-7): every top-level key `bot_merge.cell_json` puts into one
`summary.json` cell must be named, in backticks or in the worked JSON
example, somewhere in the doc. spellAcquisition shipped computed and wired
into every cell (BalanceBotRunner.cs writes the RoomTrace fields it is built
from, bot_merge.py aggregates them) with nobody updating the doc that claims
to be the reader/writer contract -- this is the test that would have caught
that, and the one that catches the next metric shaped the same way.

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

SCHEMA_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..",
                           "docs", "BOT_SUMMARY_SCHEMA.md")


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


if __name__ == "__main__":
    unittest.main(verbosity=2)
