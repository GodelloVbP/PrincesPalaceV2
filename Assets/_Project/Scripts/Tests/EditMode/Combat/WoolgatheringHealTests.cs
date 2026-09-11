using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // THE TWO ANCHORS Woolgathering was retuned against, pinned literally.
    //
    // What it used to be: flatAmount 40 + power 30 per wool spent, four wool,
    // no mana, no cooldown -- a flat 160 every single turn, forever, for a
    // resource that refills at one a turn on its own. The balance bot spent
    // 22% of a deep fight's turns on it and healed roughly three full health
    // bars per fight with it.
    //
    // What it is now: 40 flat, 2 a wool (so 8 at its four-wool cost), plus
    // half a percent of the caster's own max health per point of Wisdom above
    // neutral, on a one-turn cooldown. That last term is the whole point --
    // the heal is worth having on a Wisdom build and unremarkable on one that
    // never bought any, instead of being the same free 160 for everybody.
    //
    // THE COOLDOWN IS AUTHORED AS 2, not 1, and that is the schema's meaning
    // rather than a change of mind: SkillEntryResolver refuses a 1 outright
    // ("usable again next turn, which is no cooldown at all"). Two is the
    // shortest real wait -- cast on turn one, back on turn three -- which is
    // the one turn off the brief asked for.
    //
    // LITERALS, NOT A RECOMPUTED FORMULA (CLAUDE.md gotcha 5). The expected
    // numbers below are written out, so a change to WisdomHealBonus's rate
    // fails these rather than agreeing with itself.
    public class WoolgatheringHealTests
    {
        // Woolgathering as skills.json authors it.
        private const int WoolgatheringFlat = 40;
        private const int WoolgatheringPower = 2;
        private const int WoolgatheringWool = 4;

        private static CombatantState Caster(int maxHealth, int wisdom)
        {
            var caster = new CombatantState("Shawn", true, maxHealth, 30, 7, 10);
            var scores = ScalingProfile.NeutralScores;
            scores.wisdom = wisdom;
            caster.AbilityScores = scores;
            return caster;
        }

        private static int Woolgathering(CombatantState caster) =>
            SkillResolution.Amount(SkillEffect.HealSelf, caster, caster,
                WoolgatheringPower, WoolgatheringFlat, WoolgatheringWool, false);

        // ANCHOR ONE: a fresh Shawn. Wisdom 12 as characters.json authors him,
        // against a level-1 health pool the balance bot measures at 360-390.
        //
        // 40 flat + 2x4 wool + (400 x 2 x 5 / 1000) = 40 + 8 + 4 = 52.
        [Test]
        public void FreshShawn_HealsFiftyTwo()
        {
            Assert.AreEqual(52, Woolgathering(Caster(maxHealth: 400, wisdom: 12)));
        }

        // ANCHOR TWO: a level-60 build that actually spent its stat points on
        // Wisdom -- 14 authored plus roughly the 24 stat points the reward
        // track has paid out by level 60, plus what late gear carries. Against
        // the 980-1010 health pool the bot measures at that level.
        //
        // 40 flat + 2x4 wool + (1000 x 30 x 5 / 1000) = 40 + 8 + 150 = 198,
        // which is very close to a fifth of the bar -- the brief's "about a
        // 20% heal on a late-game build".
        [Test]
        public void LateWisdomBuild_HealsAboutAFifthOfTheBar()
        {
            var caster = Caster(maxHealth: 1000, wisdom: 40);

            Assert.AreEqual(198, Woolgathering(caster));
            Assert.That(198 * 100 / caster.MaxHealth, Is.EqualTo(19));
        }

        // THE BUILD THAT DID NOT BUY WISDOM is the other half of the change,
        // and the half the bot will feel: a level-60 defensive character puts
        // every point into Constitution (GearWeights.Defensive scores a
        // Constitution point at 46 against a Wisdom point's 16), so it arrives
        // at the same authored Wisdom 14 it started with. Its Woolgathering is
        // 40 + 8 + 20 = 68 against a thousand-point bar -- under 7%, where the
        // old flat 160 was 16% and free every turn.
        [Test]
        public void LateBuildThatSkippedWisdom_GetsLittleMoreThanTheFlat()
        {
            Assert.AreEqual(68, Woolgathering(Caster(maxHealth: 1000, wisdom: 14)));
        }

        // FLOORED AT NEUTRAL, not signed. Every monster in enemies.json is
        // authored with no ability scores at all, and the two enemy heals in
        // skills.json (shell_up, and whatever follows it) must keep healing
        // exactly what they authored rather than being scaled to nothing by a
        // Wisdom of zero.
        [Test]
        public void ACasterWithNoAuthoredWisdom_HealsExactlyWhatItAuthored()
        {
            var monster = Caster(maxHealth: 400, wisdom: 0);

            Assert.AreEqual(0, CombatMath.WisdomHealBonus(monster));
            Assert.AreEqual(2, SkillResolution.Amount(
                SkillEffect.HealSelf, monster, monster, 0, 2, 0, false));
        }

        // MANA IS NOT HEALTH. RestorePartyMana shares the flat-plus-resource
        // shape and must NOT pick up a term measured in the caster's health
        // bar -- the split in SkillResolution.Amount is what this pins.
        [Test]
        public void ManaRestore_DoesNotScaleWithWisdom()
        {
            var caster = Caster(maxHealth: 1000, wisdom: 40);

            Assert.AreEqual(48, SkillResolution.Amount(
                SkillEffect.RestorePartyMana, caster, caster, 2, 40, 4, false));
        }
    }
}
