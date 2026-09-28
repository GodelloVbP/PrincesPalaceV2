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

        // Meta progression: earned from combat and kept across runs.
        // RunManager.StartRun replaces activeRun and never touches
        // save.roster; nothing anywhere assigns level = 1 or exp = 0 outside
        // this field initialiser. RewardApplier's own header states the rule:
        // "GOLD belongs to the run and is lost with it, EXPERIENCE belongs to
        // the characters and survives".
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

        // How far up the reward track this character has been PAID.
        //
        // A watermark rather than a list of claimed ids, because the track is
        // ordered and dense: "everything up to 37" is the same statement as a
        // list of 37 entries and cannot disagree with itself about level 12.
        //
        // THE ONE NUMBER THE WHOLE TRACK IS READ AGAINST (docs/PLAN_
        // REWARD_TRACKS.md §2): a reward is COLLECTED or it is not, and
        // every read site (max health, wool capacity, elemental damage, the
        // respec, the second life, an unlocked spell) sums or tests the
        // track's entries at levels <= this. Reaching a level and being paid
        // for it stay two steps, which is what makes the collect button on
        // the track screen mean something.
        //
        // Zero on an older save, which is BELOW StartingLevel and therefore
        // reads as "has claimed nothing" -- every read floors it there. An
        // existing character is owed everything the track holds for the levels
        // they already have, and collects it the next time they open the
        // track.
        public int claimedTrackLevel;

        // PHASE 3: which collected Identity Title node (by the level it was
        // collected at) is shown on the plate/roster/victory screen. 0, the
        // default, means "never chosen" -- Core.CharacterIdentity.
        // SelectedTitleFor then falls back to the newest collected title, so
        // a character who has never opened the picker still shows the most
        // recent one rather than nothing (docs/handoffs/progression_v2/
        // PLAN_PROGRESSION_V2.md §4: "the newest is shown, earlier ones
        // selectable in the hub roster").
        //
        // PURELY ADDITIVE, same as `embers` above: an older save has no such
        // field, JsonUtility leaves it at zero, and zero already reads as
        // "default" -- no SaveData version bump needed for this field.
        //
        // Every OTHER Identity item (plate rim, portrait frame, plate
        // emboss, victory pose, mastery) has nothing to select between --
        // there is exactly one of each per character, so nothing but Title
        // needs a stored choice at all.
        public int selectedTitleTrackLevel;

        // Chooses which collected title shows. No validation here that
        // `level` actually names a collected Title node -- Character has no
        // ContentDatabase/track to check against (docs/CODE_STANDARDS.md "Layering": content
        // concepts do not belong in the save-shaped Data layer), so the
        // caller (Core.CharacterIdentity) is expected to have already
        // confirmed `level` is one of CollectedIdentity's own Title entries
        // before calling this, the same trust ClaimTrackRewards' own callers
        // are given.
        public void SelectTitle(int level) => selectedTitleTrackLevel = level;

        // HOW DEEP THIS CHARACTER'S LAST RUN REACHED, in dungeon steps.
        // Written once per settled run (RunSettlement.Settle, every fielded
        // squad member, win or wipe or abandon alike -- whichever step the
        // run's own RunSnapshot.deepestStep had reached when it closed) and
        // read by the hub's reward-track card so "about N fights to go" can
        // price a fight at the depth this character actually plays, instead
        // of the room-0 rate every character shared before this field
        // existed. THE LAST RUN, not the deepest ever: a character who has
        // fallen back to shallow retries after a deep failed one should see
        // the hub estimate get cheaper again, which lifetimeDeepestStep
        // (SaveData, a save-wide high-water mark used for the ember gate)
        // cannot do and was never meant to.
        //
        // PURELY ADDITIVE, same as `selectedTitleTrackLevel` above: an older
        // save has no such field, JsonUtility leaves it at zero, and zero
        // already reads correctly -- Character.FightsToNextLevel's caller
        // falls back to 0 for a character who has never run, which is
        // exactly what an absent field means. No SaveData version bump.
        public int lastRunDeepestStep;

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
        // A point you can take back is a slider, and levelling should be a
        // decision -- Refund below exists, but only where the dossier's hub
        // copy puts its minus. That is what keeps the decision a decision:
        // revisable between descents, not undoable inside one.
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

        // Takes one point back out of `score` and returns it to
        // unspentStatPoints. Returns false, changing nothing, when nothing is
        // invested in that score -- same "safe to drive a button off it"
        // contract as Invest above.
        //
        // Can flip a worn item inert, and that is not a bug: invested points
        // are part of the equipment requirement floor
        // (ContentDatabase.Effective.cs), so a sword lifted by levelling into
        // Strength goes back down when the point does. Not silent -- the
        // caller repaints the paperdoll in the same frame.
        public bool Refund(AbilityScore score)
        {
            if (investedAbilityScores[score] <= 0)
            {
                return false;
            }

            unspentStatPoints++;
            investedAbilityScores = investedAbilityScores.With(score, investedAbilityScores[score] - 1);
            return true;
        }

        // What the next level costs. The arithmetic is
        // Domain.Progression.LevelCurve; this stays as the name every caller
        // asks, so it is tunable without touching callers -- tuned twice now
        // (a formula retune, then a whole authored table), and no caller has
        // moved either time.
        //
        // THIS IS THE ONE PLACE THE TABLE IS FETCHED. LevelCurve is engine-
        // free and cannot reach ContentDatabase, so somebody in Core has to
        // hand it the costs, and this is the seam every caller already goes
        // through -- three call sites outside this file, all asking exactly
        // this question.
        public static int ExpToNextLevel(int level)
        {
            return LevelCurve.ExpToNextLevel(Content.ContentDatabase.LevelCosts, level);
        }

        // The same question in fights rather than in experience, which is
        // what the reward track's focus card says out loud. Same seam as
        // ExpToNextLevel above and for the same reason: LevelCurve is
        // engine-free and cannot fetch the table itself.
        //
        // DEPTH IS PASSED IN rather than read off RunManager here. This is
        // the save-shaped Data layer; RunManager is Core, and a save object
        // reaching for the live run to answer a question about itself is the
        // dependency docs/CODE_STANDARDS.md "Layering" exists to keep out. The caller holds
        // the answer already -- in a run it is the run's step, in the hub it
        // is 0.
        public int FightsToNextLevel(int depthStep)
        {
            return LevelCurve.FightsToNextLevel(Content.ContentDatabase.LevelCosts, level, exp, depthStep);
        }

        // Adds exp and applies every level-up it earns (a big enough gain
        // can cross more than one threshold at once). Returns how many
        // levels were gained, purely so callers can show a "Level Up!".
        //
        // This grants nothing: the reward track owns every stat-point grant,
        // and ClaimTrackRewards below is what pays them. Levelling and being
        // paid for levelling are two steps on purpose -- a level can be
        // reached in more than one way (a debug grant, a migration), and a
        // grant that rode inside the increment would fire for all of them or
        // none.
        //
        // The loop itself is LevelCurve.AddExperience, in Domain. What is
        // left here is the save-side wrapper: fetch the table, apply,
        // copy the three fields back. The loop moved so a career's worth of
        // level-ups can be pinned under `dotnet test` without a save, a scene
        // or Unity -- docs/CODE_STANDARDS.md "Layering"'s "arithmetic to Domain, wrapper
        // stays" -- and so that the cap at RewardTrack.MaxLevel is enforced
        // in exactly one place rather than wherever experience happens to be
        // added.
        public int AddExperience(int amount)
        {
            var after = LevelCurve.AddExperience(Content.ContentDatabase.LevelCosts, level, exp, amount);

            level = after.Level;
            exp = after.Exp;

            return after.LevelsGained;
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
        // RewardTracks.For(this).HasUnlocked(Respec, claimedTrackLevel) --
        // their own track, read against what they have collected rather than
        // what they have reached -- and it is the caller's job --
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
        // Only THE GRANT is paid here. Everything else the track carries is
        // read live off `claimedTrackLevel` at its own site -- see RewardTrack.
        //
        // Returns whether the watermark actually moved, so a caller can drive
        // a "reward earned" flourish without diffing the character. NOT
        // "whether stat points arrived": a level whose entry is a wool node or
        // a spell hands over something real and pays no points, and a claim
        // that reported false there would leave the screen unrefreshed and the
        // save unwritten with the watermark already moved.
        //
        // Through a level, not simply up to the character's own: pressing
        // node 13 with a watermark at 12 must collect only up to 13, not
        // settle the entire gap up to the character's own level. Stopping
        // short needs no second number on the save -- a claim always begins
        // at the watermark and always moves it, so stopping at 13 leaves the
        // watermark at 13 and 14 upward still owed. One number, no hole. The
        // design says the same thing in section 4 -- "claims everything from
        // claimedTrackLevel + 1 up to and including it" -- and `it` is the
        // node, not the character.
        //
        // Takes the track rather than finding it. The definition is content
        // (Core.RewardTracks reads it off ContentDatabase) and this type is
        // save state; a convenience overload that resolved its own track
        // would make this the one method on Character reaching into a content
        // lookup on its own initiative, which is the same discipline
        // ActiveLoadout's "an inert item is inert everywhere at once" keeps.
        // Every caller has the character in hand already, so
        // RewardTracks.For(character) is one expression at the call site.
        public bool ClaimTrackRewards(RewardTrackDefinition track, int throughLevel)
        {
            if (track == null) return false;

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

            // ONE GRANT, and moving the watermark is the rest of the payment.
            //
            // Everything the track pays except stat points is summed live off
            // `claimedTrackLevel` at its own read site (max health in
            // ContentDatabase.EffectiveStats, wool in
            // BuildSignatureResource, and so on). Stat points are the exception because
            // the player SPENDS them, so the balance has to be storable; a
            // stored copy of anything else could only disagree with the
            // definition after a retune.
            int points = track.GrantedBetween(TrackReward.StatPoint, claimedTrackLevel, throughLevel);

            unspentStatPoints += points;

            // MOVED WHATEVER THE GRANT PAID, because the watermark is what
            // every other reward is read against -- a level whose entry is a
            // wool node pays nothing here and must still be collected.
            claimedTrackLevel = throughLevel;

            return true;
        }
    }
}
