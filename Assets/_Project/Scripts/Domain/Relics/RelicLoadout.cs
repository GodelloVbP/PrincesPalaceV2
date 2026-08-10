using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Relics
{
    // One character's relic. A list of these rather than a Dictionary for the
    // same reason EquipmentLoadout is: JsonUtility cannot round-trip a
    // Dictionary, and this is small enough (one relic slot per character,
    // for now — see RelicLoadout's own header) that a linear scan is free.
    [Serializable]
    public class RelicAssignmentEntry
    {
        public string characterId;
        public string relicId;

        public RelicAssignmentEntry()
        {
        }

        public RelicAssignmentEntry(string characterId, string relicId)
        {
            this.characterId = characterId;
            this.relicId = relicId;
        }
    }

    // Which relic each character is carrying: characterId -> relicId.
    // Engine-free and content-free on purpose, same reasoning as
    // EquipmentLoadout — every rule here ("assigning a relic that is already
    // in use takes it away from whoever had it") is unit-testable without a
    // scene or a ScriptableObject.
    //
    // ONE slot per character right now, not eight named slots like
    // Equipment — "relic slot counts can be upgraded" is a stated future
    // extension, not built here. When it lands, this becomes "one entry per
    // (characterId, slotIndex)" rather than a redesign: the exclusivity rule
    // below (a relicId appears at most once across the whole list) does not
    // change shape either way.
    //
    // The exclusivity is the one thing Equipment's own Set does NOT need to
    // enforce (an item can only ever be in one slot because a slot is a
    // physical place; a relic has no such constraint on its own) — a relic
    // assigned to character B has to be actively taken away from character A
    // if A was already holding it, which is why Set here scans the WHOLE
    // list for the relic id, not just the one character's own entry.
    [Serializable]
    public class RelicLoadout
    {
        public List<RelicAssignmentEntry> entries = new List<RelicAssignmentEntry>();

        // The relic id `characterId` is carrying, or "" if none. Never
        // returns null, so callers can string-compare without guarding.
        public string Get(string characterId)
        {
            var entry = Find(characterId);
            return entry == null || string.IsNullOrEmpty(entry.relicId) ? "" : entry.relicId;
        }

        public bool IsEmpty(string characterId)
        {
            return Get(characterId).Length == 0;
        }

        // Assigns `relicId` to `characterId`, taking it away from whoever
        // else was holding it (a relic can occupy at most one character's
        // slot) and replacing whatever `characterId` was already carrying
        // (one slot, for now). Returns what `characterId` themself was
        // carrying before this call ("" if their slot was empty) — the same
        // "tell the caller what was displaced" contract EquipmentLoadout.Set
        // uses, though here there is nowhere to put it back: an unassigned
        // relic simply becomes available again rather than needing a bag.
        //
        // A null/empty relicId clears the slot, so Set and Clear are the
        // same operation and cannot disagree.
        public string Set(string characterId, string relicId)
        {
            string previousForThisCharacter = Get(characterId);

            if (string.IsNullOrEmpty(relicId))
            {
                var entry = Find(characterId);
                if (entry != null)
                {
                    entries.Remove(entry);
                }

                return previousForThisCharacter;
            }

            // Whoever else holds this relic loses it. Checked before this
            // character's own entry is touched, so assigning a character
            // their OWN current relic is a harmless no-op rather than a
            // self-displacement.
            entries.RemoveAll(e => e != null && e.relicId == relicId && e.characterId != characterId);

            var own = Find(characterId);
            if (own == null)
            {
                entries.Add(new RelicAssignmentEntry(characterId, relicId));
            }
            else
            {
                own.relicId = relicId;
            }

            return previousForThisCharacter;
        }

        public string Clear(string characterId)
        {
            return Set(characterId, "");
        }

        // Who currently carries `relicId`, or "" if nobody does. What the
        // assign screen reads to grey out (rather than hide) a relic another
        // character already has, and what a relic-effect check in combat
        // reads in the other direction (see RelicFor-style lookups).
        public string CharacterHolding(string relicId)
        {
            if (string.IsNullOrEmpty(relicId))
            {
                return "";
            }

            foreach (var entry in entries)
            {
                if (entry != null && entry.relicId == relicId)
                {
                    return entry.characterId;
                }
            }

            return "";
        }

        // Drops every entry the caller rejects (dangling character id,
        // dangling relic id, or both) — used by SaveData.Reconcile, same
        // tolerant posture as the rest of Reconcile. No "give it back"
        // destination is needed here, unlike EquipmentLoadout.RemoveWhere:
        // dropping a relic assignment just frees that relic up again.
        public void RemoveWhere(Func<string, string, bool> shouldRemove)
        {
            entries.RemoveAll(e => e == null
                || string.IsNullOrEmpty(e.characterId)
                || string.IsNullOrEmpty(e.relicId)
                || shouldRemove(e.characterId, e.relicId));
        }

        private RelicAssignmentEntry Find(string characterId)
        {
            foreach (var entry in entries)
            {
                if (entry != null && entry.characterId == characterId)
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
