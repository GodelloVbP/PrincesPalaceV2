using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // THE 5 -> 6 MIGRATION: progression v2 resets the ladder and keeps
    // everything that is not the ladder.
    //
    // docs/handoffs/progression_v2/PLAN_PROGRESSION_V2.md §6. The level cap
    // came down from 100 to 40, every level's cost changed, and the reward at
    // each level is reauthored in phase 4 -- so a carried `level` means
    // something different on each side of the bump, and a carried `exp` was
    // earned against income that paid nineteen times what it now pays.
    // Talents, Embers and gear were never priced in experience and survive.
    //
    // DIRECT FIELD ASSIGNMENT ON A CreateNew SAVE, the idiom EmberOwnershipTests
    // established and RewardTrackClaimTests reuses for the 4 -> 5 step: this
    // migration reinterprets values without changing the save's SHAPE, so
    // there is nothing a JSON round-trip would prove that this does not.
    //
    // THE TALENT AND THE WEAPON ARE READ OUT OF CONTENT rather than typed as
    // ids. Reconcile prunes an unlocked talent that is not on this character's
    // tree and an equipped id that is not equippable, both tolerantly and
    // silently -- so a hardcoded id that went stale would make the "kept"
    // assertions pass while proving nothing. Both lookups are asserted
    // non-empty before they are used, so the fixture cannot quietly become a
    // test of nothing either.
    //
    // PlayMode, not EditMode: SaveData and ContentDatabase are both Core types
    // and the EditMode assembly references only Domain.
    public class SaveVersionSixTests
    {
        // The plan's own worked example, and deliberately a save nothing could
        // reach after the bump: level 57 is past the new cap of 40, 300,000
        // experience is thirty deep runs at the new rate, and a watermark of
        // 40 is the whole new track already collected.
        private const int FixtureLevel = 57;
        private const int FixtureExp = 300_000;
        private const int FixtureClaimed = 40;
        private const int FixtureUnspent = 12;
        private const int FixtureEmbers = 3;

        private static (SaveData save, string talentId, string weaponId) VersionFiveSave()
        {
            var save = SaveData.CreateNew();
            save.version = 5;

            var character = save.roster[0];

            var talent = ContentDatabase.TalentsFor(character).FirstOrDefault();
            Assert.IsNotNull(talent, "fixture: the first roster character has no talent to unlock");

            var weapon = ContentDatabase.Weapons.FirstOrDefault();
            Assert.IsNotNull(weapon, "fixture: content holds no weapon to equip");

            character.level = FixtureLevel;
            character.exp = FixtureExp;
            character.claimedTrackLevel = FixtureClaimed;
            character.unspentStatPoints = FixtureUnspent;
            character.investedAbilityScores = new AbilityScoreBlock(0, 3, 5, 0, 2, 0);
            character.embers = FixtureEmbers;
            character.unlockedTalentIds.Add(talent.id);
            character.equipment.Set(EquipmentSlot.Weapon1, weapon.id);

            return (save, talent.id, weapon.id);
        }

        [Test]
        public void AVersionFiveSaveComesBackAtLevelOneWithTheTrackUncollected()
        {
            var (save, _, _) = VersionFiveSave();

            Assert.IsTrue(save.Migrate(), "a version-5 save was refused rather than migrated");

            var character = save.roster[0];
            Assert.AreEqual(1, character.level, "the ladder was not reset");
            Assert.AreEqual(0, character.exp, "experience earned against the old income survived");
            Assert.AreEqual(0, character.claimedTrackLevel, "the track's watermark survived");
            Assert.AreEqual(0, character.unspentStatPoints, "points the old table paid survived");
            Assert.AreEqual(SaveData.CurrentVersion, save.version);
        }

        [Test]
        public void EveryInvestedScoreGoesBackToBase()
        {
            var (save, _, _) = VersionFiveSave();

            save.Migrate();

            var character = save.roster[0];
            foreach (var score in AbilityScores.All)
            {
                Assert.AreEqual(0, character.investedAbilityScores[score],
                    $"{score} kept points paid by a table that no longer exists");
            }

            Assert.AreEqual(0, character.InvestedPointTotal);
        }

        // THE HALF THE MIGRATION MUST NOT TOUCH. None of these was ever priced
        // in experience, and a player who spent an evening on a talent tree
        // should not lose it to a curve retune.
        [Test]
        public void TalentsEmbersAndGearAreKept()
        {
            var (save, talentId, weaponId) = VersionFiveSave();

            save.Migrate();

            var character = save.roster[0];
            Assert.AreEqual(FixtureEmbers, character.embers, "embers were confiscated");
            CollectionAssert.Contains(character.unlockedTalentIds, talentId,
                "an unlocked talent was taken away");
            Assert.AreEqual(weaponId, character.equipment.Get(EquipmentSlot.Weapon1),
                "equipped gear was stripped");
        }

        // IDEMPOTENT, and by the version STAMP rather than by the arithmetic:
        // Migrate returns at its first line for a save already at
        // CurrentVersion. Asserted by playing a little AFTER the migration and
        // then migrating again -- a second reset would take that back, so this
        // is a claim with teeth rather than "resetting zeroes to zeroes
        // changes nothing".
        [Test]
        public void MigratingAVersionSixSaveAgainChangesNothing()
        {
            var (save, talentId, weaponId) = VersionFiveSave();
            save.Migrate();

            var character = save.roster[0];
            Assert.AreEqual(1, character.level, "fixture: the first migration should have reset to level 1");
            Assert.AreEqual(0, character.exp, "fixture: the first migration should have zeroed experience");

            character.AddExperience(50);
            int expAfterPlaying = character.exp;
            int levelAfterPlaying = character.level;
            Assert.AreEqual(50, expAfterPlaying, "fixture: 50 experience should bank, not level");

            Assert.IsTrue(save.Migrate(), "a current-version save was refused");

            var after = save.roster[0];
            Assert.AreEqual(SaveData.CurrentVersion, save.version);
            Assert.AreEqual(levelAfterPlaying, after.level, "a second load reset a level earned since");
            Assert.AreEqual(expAfterPlaying, after.exp, "a second load wiped experience earned since");
            Assert.AreEqual(0, after.claimedTrackLevel);
            Assert.AreEqual(0, after.unspentStatPoints);
            Assert.AreEqual(FixtureEmbers, after.embers);
            CollectionAssert.Contains(after.unlockedTalentIds, talentId);
            Assert.AreEqual(weaponId, after.equipment.Get(EquipmentSlot.Weapon1));
        }

        // WHAT THE RESET LOOKS LIKE ON THE SCREEN THAT DRAWS IT (phase 5).
        //
        // The fixture's version-5 save is a character with the whole track
        // collected: claimedTrackLevel 40 means every Identity node above 30
        // has been paid, so the plate would be wearing a gold rim, a frame and
        // the title "Legend" before the migration runs. Reading that back
        // through CharacterIdentity is what proves the reset reached the half
        // of the save that has no field of its own -- identity is not stored
        // anywhere, it is read live off the watermark, so a migration that
        // moved `level` and forgot `claimedTrackLevel` would leave a level-1
        // character still wearing MASTER with nothing in these asserts saying
        // so.
        //
        // AN EXTENSION, NOT A SECOND SUITE: everything above already covers
        // the load, the reset and the idempotence, and this is the one thing
        // phase 5 added a way to observe.
        [Test]
        public void TheResetTakesTheIdentityStretchWithIt()
        {
            var (save, _, _) = VersionFiveSave();
            var before = save.roster[0];

            // The track is fully collected in the fixture, so there is
            // something to lose. If this ever goes quiet, the assertions below
            // are asserting nothing.
            Assert.IsTrue(CharacterIdentity.LookFor(before).IsAnything,
                "fixture: a claimedTrackLevel of 40 no longer resolves any identity at all");

            save.Migrate();

            var after = save.roster[0];
            var look = CharacterIdentity.LookFor(after);

            Assert.IsFalse(look.IsAnything,
                "the reset left the character wearing rewards from a track they no longer have");
            Assert.IsEmpty(look.Line, "a title survived the reset");
            Assert.IsNull(look.RimMetal, "a plate rim survived the reset");
            Assert.IsNull(look.EmbossMetal, "a plate emboss survived the reset");
            Assert.IsFalse(look.HasPortraitFrame, "a portrait frame survived the reset");

            // AND LOADING AGAIN CHANGES NOTHING, which is the half this file
            // exists for, asked of the same surface.
            Assert.IsTrue(save.Migrate(), "a current-version save was refused on the second load");
            Assert.IsFalse(CharacterIdentity.LookFor(save.roster[0]).IsAnything);
        }

        // A save from a version this build does not know is refused rather
        // than read with fields it cannot interpret -- unchanged by this bump,
        // restated because the bump moves what "newer" means.
        [Test]
        public void ASaveFromANewerBuildIsStillRefused()
        {
            var save = SaveData.CreateNew();
            save.version = SaveData.CurrentVersion + 1;

            Assert.IsFalse(save.Migrate());
        }
    }
}
