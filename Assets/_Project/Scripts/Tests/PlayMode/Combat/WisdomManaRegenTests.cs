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
    // mechanism in isolation. "sheep" is Shawn -- WIS 12 as of the 2026-09-07
    // ability-score rewrite (characters.json; the exact-total budget that
    // used to constrain this number is gone, see CharacterEntryResolver's
    // header), no authored base manaRegen and no talents unlocked by
    // default, so his EffectiveStats.manaRegen is the WIS-derived figure
    // alone, nothing else summed in. floor(12 / 4) is still 3, same as the
    // old WIS 14 spread's floor(14 / 4) -- the regen number below did not
    // move, only the fixture check on WIS itself did.
    public class WisdomManaRegenTests
    {
        private static Character FreshShawn()
        {
            var definition = ContentDatabase.Characters.FirstOrDefault(c => c != null && c.id == "sheep");
            Assert.IsNotNull(definition, "fixture: content still authors Shawn under id \"sheep\"");
            // Was 14 pre-2026-09-07; the ability-score rewrite that day
            // (owner's call, exact-total budget dropped) moved Shawn to
            // WIS 12 -- updated here rather than recomputed, per this
            // project's rule against a test deriving its own expected value.
            Assert.AreEqual(12, definition.Data.AbilityScores.wisdom, "fixture check: Shawn's authored WIS as of the 2026-09-07 ability-score rewrite");
            return new Character(definition.id);
        }

        [Test]
        public void ShawnsEffectiveStats_CarryTheWisdomDerivedManaRegen()
        {
            var shawn = FreshShawn();

            var stats = ContentDatabase.EffectiveStats(shawn);

            // floor(12 / 4) = 3 (WIS 12 as of 2026-09-07) -- nothing else
            // (no gear, no talents) contributes to manaRegen for a fresh
            // character.
            Assert.AreEqual(3, stats.manaRegen, "WIS 12: floor(12 / 4) = 3");
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

            Assert.AreEqual(3, built.Party[0].PrimaryPool.GainPerTurn,
                "the WIS-derived baseline must reach the real combatant's PRIMARY POOL, which is " +
                "where per-turn income lives now -- built by ContentDatabase.BuildPrimaryPool at " +
                "the same FightEncounterAdapter seam every other derived stat already reaches");

            // mana's own row authors gainPerTurn 0, so this 3 is entirely the
            // derived term added on top of that base (PoolPrecedence.
            // GainPerTurn). The compatibility view reads the same number.
            Assert.AreEqual(3, built.Party[0].ManaRegen);
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

            hero.PrimaryPool.Current = 0;
            Assert.AreEqual(3, hero.PrimaryPool.GainPerTurn, "fixture: the pool carries the regen");
            // A LARGE speed gap and one granted extra turn, the same fixture
            // shape TurnRiderTests' own turn-start tests rely on -- hands the
            // very next turn straight back to the hero rather than letting a
            // real enemy's turn (or a second one of the hero's own) intervene
            // and regen twice before the read below.
            //
            // GrantExtraTurn rather than the banked action this used to set:
            // banking is gone with Hold Back, and the primitive underneath it
            // is what the fixture actually wanted.
            hero.Speed = 500;
            session.Encounter.GrantExtraTurn(hero);

            // NOT session.Begin() -- Begin() itself calls GrantTurnStart once
            // (FightSession.cs), which would regen BEFORE this action even
            // resolves and double the read below once the granted turn opens
            // its own. TurnRiderTests.TheTurnOpensWithManaRegenerated
            // exercises the identical shape (one granted extra turn, no
            // Begin()) for the same reason.
            session.DamageVarianceRange = 0f;
            session.ExecuteAttack(foe);

            Assert.AreEqual(3, hero.CurrentMana,
                "mana must rise by exactly the WIS-derived regen at the turn the hero's own action " +
                "opened -- through ResourcePool.TickTurnStart now, which is the ONE turn-start tick " +
                "for a primary pool (gain, then idle decay, which mana authors as 0)");
        }
    }
}
