using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // Standard Mana Regen from Wisdom: AbilityDerivation.ManaRegenBonus
    // (floor(WIS / 4), see that method's own header) reaching a REAL,
    // content-authored character's combat stats and an actual fight's
    // turn-start mana tick -- not a hand-built CombatantState fixture the
    // way TurnRiderTests.TheTurnOpensWithManaRegenerated already covers the
    // mechanism in isolation. "sheep" is Shawn -- the only real authored
    // character (see characters.json), WIS 14, no authored base manaRegen
    // and no talents unlocked by default, so his EffectiveStats.manaRegen is
    // the WIS-derived figure alone, nothing else summed in.
    public class WisdomManaRegenTests
    {
        private static Character FreshShawn()
        {
            var definition = ContentDatabase.Characters.FirstOrDefault(c => c != null && c.id == "sheep");
            Assert.IsNotNull(definition, "fixture: content still authors Shawn under id \"sheep\"");
            Assert.AreEqual(14, definition.baseAbilityScores.wisdom, "fixture check: Shawn's authored WIS, the AbilityDerivationTests spread");
            return new Character(definition.id);
        }

        [Test]
        public void ShawnsEffectiveStats_CarryTheWisdomDerivedManaRegen()
        {
            var shawn = FreshShawn();

            var stats = ContentDatabase.EffectiveStats(shawn);

            // floor(14 / 4) = 3, matching AbilityDerivationTests.ShawnsSpread
            // and the plan's own worked example -- nothing else (no gear, no
            // talents) contributes to manaRegen for a fresh character.
            Assert.AreEqual(3, stats.manaRegen, "WIS 14: floor(14 / 4) = 3");
        }

        [Test]
        public void ARealFightBuiltCharacter_CarriesTheSameWisdomDerivedManaRegen()
        {
            var shawn = FreshShawn();
            var enemyId = ContentDatabase.Enemies.Select(e => e.id).FirstOrDefault();
            Assert.IsNotNull(enemyId, "fixture: content has at least one enemy");

            var built = FightEncounterAdapter.Build(
                new List<string> { shawn.definitionId },
                new List<string> { enemyId },
                new SeededRandom(1),
                partyCharacters: new List<Character> { shawn });
            Assert.IsNotNull(built, "fixture: a real fight must build");

            Assert.AreEqual(3, built.Party[0].ManaRegen,
                "the WIS-derived baseline must reach the real combatant a fight actually runs on, " +
                "the same FightEncounterAdapter seam every other derived stat already reaches");
        }

        // THE MECHANISM: mana actually rises by that amount at turn start,
        // through the SAME rider TurnRiderTests.TheTurnOpensWithManaRegenerated
        // already pins with a hand-built fixture -- this proves the real
        // content-authored figure is what lands there, not merely that
        // EffectiveStats/CombatantState.ManaRegen carries the right number.
        [Test]
        public void AtTurnStart_ShawnsManaRisesByHisWisdomDerivedRegen()
        {
            var shawn = FreshShawn();
            var enemyId = ContentDatabase.Enemies.Select(e => e.id).FirstOrDefault();
            Assert.IsNotNull(enemyId, "fixture: content has at least one enemy");

            var built = FightEncounterAdapter.Build(
                new List<string> { shawn.definitionId },
                new List<string> { enemyId },
                new SeededRandom(1),
                partyCharacters: new List<Character> { shawn });
            Assert.IsNotNull(built, "fixture: a real fight must build");

            var session = built.Session;
            var hero = built.Party[0];
            var foe = session.Encounter.Enemies.FirstOrDefault();
            Assert.IsNotNull(foe, "fixture: the built fight has at least one enemy");

            hero.CurrentMana = 0;
            // A LARGE speed gap and one banked action, the same fixture shape
            // TurnRiderTests' own turn-start tests rely on -- hands the very
            // next turn straight back to the hero rather than letting a real
            // enemy's turn (or a second one of the hero's own) intervene and
            // regen twice before the read below.
            hero.Speed = 500;
            hero.BankedActions = 1;

            // NOT session.Begin() -- Begin() itself calls GrantTurnStart once
            // (FightSession.cs), which would regen BEFORE this action even
            // resolves and double the read below once the banked action
            // opens its own turn. TurnRiderTests.TheTurnOpensWithManaRegenerated
            // exercises the identical shape (BankedActions=1, no Begin()) for
            // the same reason.
            session.DamageVarianceRange = 0f;
            session.ExecuteAttack(foe);

            Assert.AreEqual(3, hero.CurrentMana,
                "mana must rise by exactly the WIS-derived regen at the turn the hero's own action opened");
        }
    }
}
