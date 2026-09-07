using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace
{
    // The per-save, mutable half of a character. Everything authored and
    // unchanging (display name, role, base stats) lives on the matching
    // CharacterDefinition and is resolved through ContentDatabase, so this
    // stays small and save files stay readable.
    [Serializable]
    public class Character
    {
        // Matches CharacterDefinition.id. Doubles as this character's
        // identity within a save, since the roster holds one of each.
        public string definitionId;

        // Orbs this character has awakened. Bought with EMBERS from the
        // save's own wallet (see TalentController), not with anything stored
        // here -- so this is a list of what was chosen, with no matching
        // per-character currency alongside it.
        public List<string> unlockedTalentIds = new List<string>();

        // Skills gated behind something OTHER than level — right now, only
        // an Event room's mage encounter grants one (see GameplayManager's
        // RoomType.Event case). Kept separate from unlockedTalentIds rather
        // than reusing it: a talent point is spent on a choice the player
        // made in the tree, this is handed over unconditionally by the
        // event, and conflating the two would make a respec (which only
        // ever refunds unlockedTalentIds) either strip an event-taught
        // spell it never granted or silently fail to strip a talent.
        public List<string> unlockedSkillIds = new List<string>();

        // LEGACY, migration source only. This was the whole equipment
        // system when there was a single weapon slot per character.
        // SaveData.Reconcile() moves a non-empty value into
        // equipment's Weapon1 slot and clears this, so a save written
        // before the paperdoll existed keeps its sword. Nothing should read
        // or write it — ask `equipment` (or ContentDatabase.EquippedWeapon)
        // instead. Kept as a field rather than deleted because deleting it
        // would make JsonUtility drop the value on load, silently
        // unequipping every existing save.
        public string equippedItemId = "";

        // The eight-slot paperdoll: head, necklace, torso, legs, shoes,
        // gloves and two weapon hands. THE source of truth for what this
        // character is wearing.
        //
        // Worn items are moved OUT of the player's item list and held here,
        // rather than being referenced in place — so the inventory grid
        // shows exactly what is spare, and unequipping is a real move back
        // rather than a flag flip. EquipmentService owns both halves of that
        // move so the two lists can never both hold, or both lose, the same
        // item.
        public EquipmentLoadout equipment = new EquipmentLoadout();

        // META progression: earned from combat and KEPT ACROSS RUNS.
        //
        // This comment used to say the opposite -- "PER-RUN progression,
        // earned from combat and reset by StartRun", closing on "one rule that
        // has to hold: StartRun resets all four. There is a test whose whole
        // job is to fail if it ever stops." None of that was true.
        // RunManager.StartRun replaces activeRun and never touches
        // save.roster; nothing anywhere assigns level = 1 or exp = 0 outside
        // this field initialiser; and there was no such test. RewardApplier's
        // own header states the real rule and always has -- "GOLD belongs to
        // the run and is lost with it, EXPERIENCE belongs to the characters
        // and survives".
        //
        // Worth knowing WHY the wrong version was dangerous rather than merely
        // wrong: it read as a load-bearing invariant with a named guard, so
        // anyone planning against it would price levels as a within-run curve
        // and design rewards that cannot exist. See AUDIT.md #49.
        //
        // The consequence, stated plainly because it is a balance fact and not
        // an implementation detail: level gates spell tiers
        // (ContentDatabase.GetSpellTierForLevel) and level-locked skills, so a
        // returning character starts their next descent at whatever tier they
        // had reached, not at tier 1.
        //
        // Living on Character rather than RunState is still the deliberate
        // choice it was, and now a cheaper one: every consumer picks these up
        // for free, and there is no snapshot work to get wrong at either end
        // of a run.
        public int level = 1;
        public int exp;

        // THIS CHARACTER'S embers, not the profile's.
        //
        // They were a single wallet figure, which meant investing in whoever
        // you actually play starved everyone else -- and worse, the reverse:
        // a character you have never fielded could be kindled to the top of
        // their tree out of a pool someone else earned. Progression is supposed
        // to reflect who you played.
        //
        // Purely additive, so CurrentVersion does not move: an older save has
        // no such field, JsonUtility leaves it at zero, and SaveData.Migrate
        // moves whatever was in the shared wallet onto the roster.
        public int embers;

        // Unspent points from levelling, one per level. Spent into
        // investedAbilityScores below, which is what turns a level into a
        // build decision rather than an automatic stat bump.
        public int unspentStatPoints;

        // Where those points went. Summed by
        // ContentDatabase.EffectiveAbilityScores alongside base scores and
        // talent bonuses, so it flows into AbilityDerivation and out into
        // attack/speed/health with no separate stat pathway of its own.
        public AbilityScoreBlock investedAbilityScores;

        // Max health granted by the reward track, on top of everything the
        // character's definition, talents, gear and ability scores already give
        // them.
        //
        // A FLAT ADDITION rather than invested Constitution, and the difference
        // matters. AbilityDerivation pays a flat rate for the first ten points
        // into a score and then switches to the square of the excess, which
        // starts far lower -- so folding these into CON would make each node
        // worth 20 health to a character who had spent nothing there and 1 to
        // one who had already filled the band. A reward whose value depends on
        // where the player happened to put unrelated points is not a reward
        // anyone can plan around.
        //
        // A GRANT, so it goes through the claim watermark and is paid exactly
        // once -- and unlike stat points it is NOT refunded by a respec. There
        // is nothing to take back: the player never chose where it went.
        //
        // Purely additive, so SaveData.CurrentVersion does not move.
        public int bonusMaxHealth;

        // How far up the reward track this character has been PAID.
        //
        // A watermark rather than a list of claimed ids, because the track is
        // ordered and dense: "everything up to 37" is the same statement as a
        // list of 37 entries and cannot disagree with itself about level 12.
        //
        // Only GRANTS need this -- the quantities, like a stat point or two
        // Favor, which have to be handed over exactly once. Unlocks (respec, a
        // wider offer, a second life) are pure functions of `level` and are
        // stored nowhere, so they cannot be missed by a character who passed
        // the level before the feature was built. RewardTrack's header has the
        // full reasoning.
        //
        // Zero on an older save, which is BELOW StartingLevel and therefore
        // reads as "has claimed nothing" -- RewardTrack.GrantedBetween floors
        // it. An existing character is paid everything the track owes them for
        // the levels they already have, the next time they gain any exp.
        public int claimedTrackLevel;

        public Character()
        {
        }

        public Character(string definitionId)
        {
            this.definitionId = definitionId;
        }

        // Places one unspent point into `score`. Returns false, changing
        // nothing, when there is nothing to spend -- callers can drive a
        // button off this without checking first.
        //
        // ONE point at a time on purpose: the UI offers a "+" per score, and
        // a bulk-invest API would need its own partial-success story (what
        // happens when you ask for 5 and can afford 3) for no gain.
        //
        // There is deliberately no matching Refund, and the reason given for
        // that has to change: it was "a placed point is placed for the rest of
        // the run ... and the run ending is already a full reset, so nobody is
        // stuck with a build forever". The second half was never true. Nothing
        // resets these (see `level` above), so a placed point is placed
        // FOREVER, and the escape hatch this argument leaned on does not
        // exist.
        //
        // Left without a Refund anyway, for the half of the argument that
        // survives: a point you can take back is a slider, and levelling
        // should be a decision. The way out is a deliberate, earned respec
        // rather than an always-available undo.
        public bool Invest(AbilityScore score)
        {
            if (unspentStatPoints <= 0)
            {
                return false;
            }

            unspentStatPoints--;
            investedAbilityScores = investedAbilityScores.With(score, investedAbilityScores[score] + 1);
            return true;
        }

        // What the next level costs. The curve itself is
        // Domain.Progression.LevelCurve; this stays as the name every caller
        // already asks, which is what the old comment here promised -- "tunable
        // later without touching callers, since they only ever ask how much to
        // next". Tuned, and no caller moved.
        public static int ExpToNextLevel(int level)
        {
            return LevelCurve.ExpToNextLevel(level);
        }

        // Adds exp and applies every level-up it earns (a big enough gain
        // can cross more than one threshold at once). Returns how many
        // levels were gained, purely so callers can show a "Level Up!".
        //
        // THIS NO LONGER GRANTS ANYTHING. It used to hand out one stat point
        // per level right here; the reward track owns every grant now, and
        // ClaimTrackRewards below is what pays them. Levelling and being paid
        // for levelling are two steps on purpose -- a level can be reached in
        // more than one way (a debug grant, a migration), and a grant that
        // rode inside the increment would fire for all of them or none.
        public int AddExperience(int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            exp += amount;
            int levelsGained = 0;

            while (exp >= ExpToNextLevel(level))
            {
                exp -= ExpToNextLevel(level);
                level++;
                levelsGained++;
            }

            return levelsGained;
        }

        // How many stat points are currently placed into ability scores.
        //
        // Floored at zero rather than returned raw: a hand-edited save carrying
        // a negative invested score would otherwise make a respec DEDUCT
        // points, and a refund that takes something away is the worst possible
        // reading of this button.
        public int InvestedPointTotal
        {
            get
            {
                int total = 0;
                foreach (var score in AbilityScores.All)
                {
                    total += investedAbilityScores[score];
                }

                return total < 0 ? 0 : total;
            }
        }

        // Whether a respec would give anything back. What the button reads, so
        // that "is this worth pressing" and "what does pressing it do" cannot
        // disagree -- the caller supplies the ember figure because that is a
        // content lookup, but the two halves are the same two halves.
        public bool HasAnythingToRespec(int embersSpent) =>
            embersSpent > 0 || unlockedTalentIds.Count > 0 || InvestedPointTotal > 0;

        // Takes back every choice this character has spent, and hands the
        // currency back to spend again. Level 20 of the reward track.
        //
        // BOTH KINDS OF SPEND, because there are two and a player asking to
        // rebuild means both: embers committed to talent orbs, and stat points
        // placed into ability scores.
        //
        // WHAT IT MUST NOT TOUCH is `unlockedSkillIds`. Those are handed over
        // by an Event room's mage, not bought, and this is exactly why they are
        // a separate list from `unlockedTalentIds` -- see that field's comment,
        // which named this hazard before a respec existed. Stripping them would
        // take away something the player never spent anything on and cannot get
        // back.
        //
        // Returns what came back, so a caller can say so rather than diffing
        // the character. Refund order does not matter: the two currencies are
        // independent and neither total is computed from the other.
        //
        // NOT gated here. Whether this character has EARNED a respec is
        // RewardTrack.HasUnlocked(Respec, level), and it is the caller's job --
        // a model method that silently refused would be indistinguishable from
        // one that worked and found nothing to give back.
        public RespecRefund Respec(int embersSpent)
        {
            if (embersSpent < 0) embersSpent = 0;

            int points = InvestedPointTotal;

            embers += embersSpent;
            unlockedTalentIds.Clear();

            unspentStatPoints += points;
            investedAbilityScores = default;

            return new RespecRefund(embersSpent, points);
        }

        // Pays out everything the reward track owes for levels reached since
        // the last time this was called, and moves the watermark.
        //
        // IDEMPOTENT, which is the property worth having: calling it twice
        // pays once, because the second call finds the watermark already at
        // `level` and has nothing between. Callers therefore do not have to
        // know whether anybody else has already claimed.
        //
        // Only GRANTS are paid here. Unlocks are answered from `level`
        // directly, wherever the capability is used -- see RewardTrack.
        //
        // Returns whether anything was actually handed over, so a caller can
        // drive a "reward earned" flourish without diffing the character.
        // THROUGH A LEVEL, not simply up to the character's own.
        //
        // It used to take no argument and always settle the entire gap, and
        // the reward track's node press called it -- so pressing level 13 with
        // a watermark at 12 and a character at 47 collected all thirty-five
        // levels. Every node on the rail was a collect-everything button
        // wearing a different number, which is what "it still auto claims"
        // describes from the outside: rewards arriving that nobody asked for.
        //
        // The comment that defended it argued that stopping short would need
        // "a second number on the save -- paid to here, but the player only
        // asked for that far". That is not so, and it is worth saying why: a
        // claim always begins at the watermark and always moves it, so
        // stopping at 13 leaves the watermark at 13 and 14 upward still owed.
        // One number, no hole, exactly as before. The design says the same
        // thing in section 4 -- "claims everything from claimedTrackLevel + 1
        // up to and including it" -- and `it` is the node, not the character.
        public bool ClaimTrackRewards(int throughLevel)
        {
            // NEVER PAST THE CHARACTER'S OWN LEVEL, whatever the caller asks
            // for. This is the one guard that makes an arbitrary argument safe:
            // a caller cannot collect a reward that has not been earned.
            if (throughLevel > level) throughLevel = level;

            if (claimedTrackLevel >= level)
            {
                // Also repairs a watermark that has somehow run ahead of the
                // level -- a hand-edited save, or a future respec that moves
                // levels. Clamping here means the character is not silently
                // owed nothing forever.
                claimedTrackLevel = level;
                return false;
            }

            if (throughLevel <= claimedTrackLevel) return false;

            int points = RewardTrack.GrantedBetween(TrackReward.StatPoint, claimedTrackLevel, throughLevel);
            int health = RewardTrack.GrantedBetween(TrackReward.MaxHealth, claimedTrackLevel, throughLevel);

            unspentStatPoints += points;
            bonusMaxHealth += health;
            claimedTrackLevel = throughLevel;

            return points > 0 || health > 0;
        }

        // Everything owed, which is what a collect-all button asks for.
        public bool ClaimTrackRewards() => ClaimTrackRewards(level);
    }
}
