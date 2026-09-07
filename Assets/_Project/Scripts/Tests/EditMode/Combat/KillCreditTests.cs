using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // THE PAIRING, per kill path.
    //
    // When a combatant dies two things have to happen together: the rider
    // block's flag goes up (AdvanceAfterAction reads it once, and it is what
    // makes Trample and Bloodlust eligible) and the ledger takes a kill row.
    // Until 2026-09-06 both were typed by hand after five separate DealDamage
    // calls -- a plain swing, Disgruntled Lackey's grudge, Inconspicuous Key's
    // last blow, Lucky Deck's splash and Shatter's chain -- and a sixth path,
    // Black Ram's transform splash, had already lost the flag half with
    // nothing to notice. The pair now lives inside DealDamage itself
    // (SettleDeath, FightSession.Ledger.cs), so there is one place to get it
    // wrong instead of six.
    //
    // ONE TEST PER FORMER SITE, deliberately not folded into a
    // [TestCase]-driven loop: the paths reach the funnel through five
    // different fixtures (a swing, two relic seams, a talent cast, a summon),
    // and sharing one parameterised body would mean the fixture, not the
    // invariant, doing most of the work. A regression in any one of them
    // should name that one.
    public class KillCreditTests
    {
        // Speed 100 against 1 keeps the turn with the hero throughout, so a
        // setup step never has the monsters replying in the middle of it. The
        // rider is therefore read off Trample's own MESSAGE rather than off
        // "the enemies never acted" -- with a hero this fast they would not
        // have acted either way, and TurnRiderTests already owns the
        // turn-order half of Trample.
        private const int HeroSpeed = 100;
        private const int FoeSpeed = 1;

        private const string TrampleLine = "tramples straight over the body";

        private static CombatantState Hero(string name = "Shawn", int health = 500, int attack = 20) =>
            new CombatantState(name, true, health, 50, attack, HeroSpeed);

        private static CombatantState Foe(string name, int health) =>
            new CombatantState(name, false, health, 0, 1, FoeSpeed);

        // Cap 3 rather than 1: several of these fixtures fell two bodies in a
        // round (Inconspicuous Key's blow lands on top of the kill that
        // summoned it), and a cap that runs out mid-test would look exactly
        // like a lost flag.
        private static void GiveTrample(CombatantState actor, params TalentEffect[] extra) =>
            actor.Talents = new TalentEffectSet(
                new[] { new TalentEffect(TalentEffectType.ExtraAttackOnKill, 3) }.Concat(extra).ToArray());

        private static PlayerKit Kit(IReadOnlyList<ResolvedSkill> skills = null, params RelicEffect[] relics) =>
            new PlayerKit("hero", CharacterRole.Tank, skills,
                relics.Select(r => new ResolvedRelic(r.ToString(), r.ToString(), "", r, 0)).ToList(), null);

        // No EnemyKits, so LedgerIdOf falls through to a combatant's own Name
        // and a ledger assertion can name the body it means.
        private static (FightSession session, CombatEncounter encounter) Fight(
            CombatantState hero, PlayerKit kit, params CombatantState[] foes)
        {
            var encounter = new CombatEncounter(new[] { hero }, foes);
            var session = new FightSession(
                encounter,
                kit == null ? null : new List<PlayerKit> { kit },
                null,
                new SeededRandom(1)) { DamageVarianceRange = 0f };
            return (session, encounter);
        }

        private static IEnumerable<string> Drain(FightSession session) =>
            session.DrainBeats().SelectMany(b => b.Messages).ToList();

        private static bool Trampled(IEnumerable<string> messages) =>
            messages.Any(m => m.Contains(TrampleLine));

        // ---- the control ------------------------------------------------------
        //
        // Every assertion below reads "Trample fired" as evidence the flag went
        // up. That only means something if an action which killed nothing
        // leaves it down.

        [Test]
        public void AnActionThatKillsNothingRaisesNoFlagAndScoresNoKill()
        {
            var hero = Hero();
            GiveTrample(hero);
            var (session, encounter) = Fight(hero, Kit(), Foe("Tank", 999999));

            session.ExecuteAttack(encounter.Enemies[0]);

            Assert.IsFalse(Trampled(Drain(session)), "nothing died; nothing to trample over");
            Assert.AreEqual(0, session.Ledger.For("hero").Kills);
            Assert.AreEqual(0, session.Ledger.For("Tank").TimesDowned);
        }

        // ---- site 1: the plain swing (was FightSession.cs:436) ----------------

        [Test]
        public void APlainSwingsKillRaisesTheFlagAndScoresTheKill()
        {
            var hero = Hero();
            GiveTrample(hero);
            var (session, encounter) = Fight(hero, Kit(), Foe("Weak", 1), Foe("Tank", 999999));

            session.ExecuteAttack(encounter.Enemies[0]);

            Assert.IsFalse(encounter.Enemies[0].IsAlive, "fixture: the swing really killed");
            Assert.IsTrue(Trampled(Drain(session)));
            Assert.AreEqual(1, session.Ledger.For("hero").Kills);
            Assert.AreEqual(1, session.Ledger.For("Weak").TimesDowned);
        }

        // ---- site 2: Disgruntled Lackey (was .RelicMechanics.cs:419) ---------

        [Test]
        public void TheLackeysGrudgeKillingASummonerRaisesTheFlagAndScoresTheKill()
        {
            // The relic answers an ENEMY summon by dealing the holder's whole
            // max health to the summoner, which is why 100 max HP against a
            // 100 HP boss is a kill and not a scratch.
            var hero = Hero(health: 100);
            GiveTrample(hero);
            var summoner = Foe("Boss", 100);

            bool SummonFactory(string id, out CombatantState state, out EnemyKit kit)
            {
                // Fat enough to survive the follow-up swing below, which has
                // to kill nothing for that swing to be a clean read of the
                // flag the SUMMON set. ResolveSummon refuses a factory that
                // hands back no kit, so this is not optional plumbing.
                state = new CombatantState("Add", false, 999999, 0, 1, FoeSpeed);
                kit = EnemyKitNamed("Add");
                return true;
            }

            var encounter = new CombatEncounter(new[] { hero }, new[] { summoner });
            var session = new FightSession(encounter,
                new List<PlayerKit> { Kit(null, RelicEffect.DisgruntledLackey) },
                new List<EnemyKit> { EnemyKitNamed("Boss") }, new SeededRandom(5),
                summonFactory: SummonFactory) { DamageVarianceRange = 0f };
            session.Begin();

            var roar = new ResolvedSkill("roar", "Roar", "", "boss", 1, SkillEffect.Summon,
                SkillTargeting.Self, 0, 0, false, 0, 0, false, null, SpellPresentation.None, 0,
                summonEnemyId: "add", summonCap: 1);

            session.ResolveSummonForTest(summoner, roar);
            session.DrainBeats();

            Assert.IsFalse(summoner.IsAlive, "fixture: the grudge really killed the summoner");
            Assert.AreEqual(1, session.Ledger.For("hero").Kills);
            Assert.AreEqual(1, session.Ledger.For("Boss").TimesDowned);

            // The flag is read by the next AdvanceAfterAction, which a seam
            // call does not reach -- so the hero's own next action is where it
            // becomes visible. That is exactly how it behaves in production
            // too: the summon resolves inside an enemy's turn and the flag is
            // still standing when the player acts.
            var add = encounter.Enemies.First(e => e.Name == "Add");
            session.ExecuteAttack(add);

            Assert.IsTrue(add.IsAlive, "fixture: the follow-up swing must kill nothing of its own");
            Assert.IsTrue(Trampled(Drain(session)));
        }

        // ---- site 3: Inconspicuous Key (was .RelicMechanics.cs:461) ----------

        [Test]
        public void TheKeysLastBlowKillingSomeoneScoresASecondKill()
        {
            // The key fires FROM a kill, so the flag is already up by the time
            // its own blow lands -- there is no arrangement in which this site
            // is the only thing that could have raised it. The ledger is what
            // isolates it: two bodies, two kill rows, one of them only
            // reachable through the key.
            var hero = Hero();
            GiveTrample(hero);
            var struck = Foe("Struck", 1);
            struck.Attack = 7;

            // EVERY remaining foe at 1 HP, because the key picks its target
            // with RandomLivingEnemy and a fixture that only dies on one of
            // three draws is a flaky test wearing a seed. Three of them, so
            // two are still standing afterwards and the fight is not over --
            // every rider bails out early on IsOver.
            var (session, encounter) = Fight(hero, Kit(null, RelicEffect.InconspicuousKey),
                struck, Foe("BystanderA", 1), Foe("BystanderB", 1), Foe("BystanderC", 1));

            session.ExecuteAttack(struck);

            Assert.IsFalse(struck.IsAlive, "fixture: the swing killed the first body");
            Assert.AreEqual(2, encounter.Enemies.Count(e => !e.IsAlive),
                "fixture: the fallen enemy's last blow killed exactly one more");
            Assert.AreEqual(2, session.Ledger.For("hero").Kills,
                "the swing and the key's own blow are two kills, not one");
            Assert.IsTrue(Trampled(Drain(session)));
        }

        // ---- site 4: Lucky Deck's splash (was .Relics.cs:322) ----------------

        [Test]
        public void TheLuckyDeckSplashKillingSomeoneRaisesTheFlagAndScoresTheKill()
        {
            // Through the seam rather than a real cast: the branch is chosen by
            // one RNG draw, and hunting a seed that lands in a given third
            // would make this file depend on SeededRandom's algorithm. See
            // FightSession.Relics' own header on the seams.
            var hero = Hero();
            GiveTrample(hero);
            var (session, encounter) = Fight(hero, Kit(), Foe("Primary", 999999), Foe("Weak", 1));

            session.LuckyDeckSplashForTest(hero, encounter.Enemies[0], damage: 1000);
            session.DrainBeats();

            Assert.IsFalse(encounter.Enemies[1].IsAlive, "fixture: the splash really killed");
            Assert.AreEqual(1, session.Ledger.For("hero").Kills);
            Assert.AreEqual(1, session.Ledger.For("Weak").TimesDowned);

            // Same reasoning as the Lackey's: a seam call reaches no
            // AdvanceAfterAction, so the standing flag shows up on the next
            // action instead.
            session.ExecuteAttack(encounter.Enemies[0]);

            Assert.IsTrue(encounter.Enemies[0].IsAlive, "fixture: the follow-up swing kills nothing");
            Assert.IsTrue(Trampled(Drain(session)));
        }

        // ---- site 5: Shatter's chain (was .Talents.cs:332) -------------------

        [Test]
        public void ShattersChainKillingSomeoneRaisesTheFlagAndScoresTheKill()
        {
            var hero = Hero(attack: 40);
            GiveTrample(hero,
                new TalentEffect(TalentEffectType.WardReductionPercent, 50),
                new TalentEffect(TalentEffectType.ShatterDamagePercentOfAttack, 100));

            // Both foes at 1 HP, so Shatter's own RandomLivingEnemy draw cannot
            // decide whether this test passes -- either body it picks dies, and
            // the fight is not over either way.
            var (session, encounter) = Fight(hero,
                Kit(new List<ResolvedSkill> { Skill(SkillEffect.Ward, "Fleece Ward"), Skill(SkillEffect.Shatter, "Shatter") }),
                Foe("WeakA", 1), Foe("WeakB", 1), Foe("Tank", 999999));

            session.CastSkill(0, null);
            session.DrainBeats();
            Assert.IsTrue(StatusEffects.IsWarded(hero), "fixture: there is a ward to shatter");

            session.CastSkill(1, null);

            Assert.AreEqual(1, session.Ledger.For("hero").Kills, "the detonation felled exactly one body");
            Assert.IsTrue(Trampled(Drain(session)));
        }

        // ---- site 6: the transform splash, which had ALREADY lost the flag --

        [Test]
        public void ATransformSplashKillingABystanderRaisesTheFlagToo()
        {
            // SplashOntoNeighbours called RecordKill and never set
            // _killedThisAction -- the exact half-typed pairing this change
            // exists to make impossible. Reachable only through Black Ram
            // Mode, because it is the one splash that fires on a landed hit
            // rather than on a kill: the primary survives here, so the flag
            // can have come from nowhere else.
            var hero = Hero(attack: 40);
            GiveTrample(hero);

            var transform = new TransformGrant
            {
                displayName = "Black Ram Mode",
                turns = 9,
                splashPercent = 100,
            };

            // The struck enemy sits at index 0 and the bystander at 1 --
            // "adjacent" is index adjacency in Enemies, which is what the
            // stage draws. THE PRIMARY IS IN FRONT, which it was not before
            // the front-rank rule bound the player's own swing: a blow aimed
            // past a living rank 0 is now refused outright, so a fixture that
            // struck index 1 would land nothing at all.
            var (session, encounter) = Fight(hero,
                Kit(new List<ResolvedSkill> { Skill(SkillEffect.Transform, "Ram", transform) }),
                Foe("Primary", 999999), Foe("Bystander", 1), Foe("Tank", 999999));

            session.CastSkill(0, null);
            session.DrainBeats();
            Assert.IsNotNull(hero.Transformation, "fixture: the transform is running");

            session.ExecuteAttack(encounter.Enemies[0]);

            Assert.IsTrue(encounter.Enemies[0].IsAlive, "fixture: the PRIMARY survived");
            Assert.IsFalse(encounter.Enemies[1].IsAlive, "fixture: the splash felled the bystander");
            Assert.AreEqual(1, session.Ledger.For("hero").Kills);
            Assert.AreEqual(1, session.Ledger.For("Bystander").TimesDowned);
            Assert.IsTrue(Trampled(Drain(session)),
                "a splash kill is a kill: before this change the flag stayed down here");
        }

        // ---- site 7: the poison detonation, which never reached the funnel ---

        [Test]
        public void APoisonDetonationKillingItsTargetRaisesTheFlagAndScoresTheKill()
        {
            // NOT ONE OF THE FIVE HAND-TYPED SITES -- a path that never went
            // through DealDamage at all. StatusCombos.DetonatePoisonIfMatched
            // calls CombatMath.ApplyDamage directly from inside
            // DamagePipeline.AfterDefences, so a detonation big enough to fell
            // its target killed it outside the one funnel that settles a
            // death: no kill row, no down-count, no rider eligibility, and the
            // damage missing from the ledger entirely.
            //
            // The swing that triggers it then lands on a corpse, and
            // DealDamage's own wasAlive guard correctly refuses to settle a
            // body that was already down when it was called -- so the kill
            // has nowhere left to be recorded.
            var hero = Hero();
            GiveTrample(hero);

            // 25 x 4 = 100 detonated onto 40 health. A second foe keeps the
            // fight running, so AdvanceAfterAction reaches the rider block
            // instead of returning early on IsOver.
            var poisoned = Foe("Poisoned", 40);
            StatusEffects.Apply(poisoned.Statuses, StatusEffectType.Poison, 25, 4);

            var natureSwinger = new PlayerKit("hero", CharacterRole.Tank, null, null, DamageType.Nature);
            var (session, _) = Fight(hero, natureSwinger, poisoned, Foe("Tank", 999999));

            session.ExecuteAttack(poisoned);

            Assert.IsFalse(poisoned.IsAlive, "fixture: the detonation really killed");
            Assert.AreEqual(1, session.Ledger.For("hero").Kills,
                "a detonation is the hero's Nature swing setting it off -- the kill is theirs");
            Assert.AreEqual(1, session.Ledger.For("Poisoned").TimesDowned);
            Assert.IsTrue(Trampled(Drain(session)),
                "and a kill is a kill for the rider block, however the last point of damage arrived");
        }

        [Test]
        public void ADetonationsDamageIsCountedLikeEveryOtherPointDealt()
        {
            // The ledger half on its own, on a target that SURVIVES -- so the
            // count cannot be confused with the swing's own kill bookkeeping.
            var hero = Hero();
            var poisoned = Foe("Poisoned", 999999);
            StatusEffects.Apply(poisoned.Statuses, StatusEffectType.Poison, 25, 4);

            var natureSwinger = new PlayerKit("hero", CharacterRole.Tank, null, null, DamageType.Nature);
            var (session, _) = Fight(hero, natureSwinger, poisoned);

            int before = poisoned.CurrentHealth;
            session.ExecuteAttack(poisoned);
            int lost = before - poisoned.CurrentHealth;

            Assert.AreEqual(lost, session.Ledger.For("hero").TotalDealt,
                "every point the target lost to this action is a point the hero dealt");
        }

        // ---- and the same mutation, fired from a TELEGRAPH ------------------

        [Test]
        public void PreparingATelegraphDoesNotSetOffThePartysPoison()
        {
            // PreviewDamage's own header: "What the blow would land for, with
            // NOTHING that mutates and NOTHING that draws from the run's
            // generator." It passes rng: null and resolveWard: null for
            // exactly that reason -- but the poison combo has no such
            // collaborator to leave out, so it fires from inside the preview:
            // the party member's Poison is consumed and its remaining ticks
            // are dealt as damage, before the monster has swung at all.
            //
            // An enemy authored attackType "Poison" is what routes the
            // preview into the typed overload where the combo lives.
            // enemies.json ships two of them.
            var hero = Hero(health: 500);
            StatusEffects.Apply(hero.Statuses, StatusEffectType.Poison, 25, 4);

            var venomous = new CombatantState("Venomous", false, 200, 0, 5, FoeSpeed);
            var encounter = new CombatEncounter(new[] { hero }, new[] { venomous });
            var session = new FightSession(encounter, null,
                new List<EnemyKit> { new EnemyKit(PoisonSwinger("venomous"), false) },
                new SeededRandom(1)) { DamageVarianceRange = 0f };

            int before = hero.CurrentHealth;
            session.PrepareEnemyIntents();

            Assert.AreEqual(before, hero.CurrentHealth,
                "a telegraph dealt real damage");
            Assert.IsTrue(hero.Statuses.Any(s => s.Type == StatusEffectType.Poison),
                "and spent the poison the player is still carrying");
        }

        private static ResolvedEnemy PoisonSwinger(string id) =>
            new ResolvedEnemy(id, id, new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0,
                attackType: DamageType.Poison);

        // ---- the exception: a poison tick credits nobody ---------------------

        [Test]
        public void APoisonTickKillsWithoutCreditingAnyone()
        {
            // KillCredit.Nobody, written down at the call site rather than
            // left as a missing pair. The poison was applied turns ago by
            // someone who may now be dead; back-crediting it would put points
            // in a column the player cannot account for against any blow they
            // watched land.
            var hero = Hero();
            GiveTrample(hero);
            var victim = Foe("Victim", 5);
            var (session, encounter) = Fight(hero, Kit(), victim, Foe("Tank", 999999));

            StatusEffects.Apply(victim.Statuses, StatusEffectType.Poison, 999, 5, hero);
            session.TickStatusesForTest(victim);
            session.DrainBeats();

            Assert.IsFalse(victim.IsAlive, "fixture: the tick really killed");
            Assert.AreEqual(0, session.Ledger.For("hero").Kills,
                "the applier is not credited for a tick that landed turns later");
            Assert.AreEqual(0, session.Ledger.For("Victim").TimesDowned,
                "and no kill row is written for the body either");

            // The other half of the exception, and the one a lost pairing
            // would break in the opposite direction: no rider eligibility. The
            // hero's next action swings at a survivor, so anything Trample
            // says here came from the poison.
            session.ExecuteAttack(encounter.Enemies[1]);

            Assert.IsFalse(Trampled(Drain(session)),
                "a death credited to nobody must not arm the rider block either");
        }

        // ---- fixture plumbing -------------------------------------------------

        private static EnemyKit EnemyKitNamed(string id) =>
            new EnemyKit(new ResolvedEnemy(id, id, new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0), false);

        private static ResolvedSkill Skill(SkillEffect effect, string name, TransformGrant transform = null) =>
            new ResolvedSkill("t_" + name, name, "", "hero", 1, effect, SkillTargeting.Self,
                0, 0, false, 100, 0, false, null, SpellPresentation.None, 0, transform: transform);
    }
}
