using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Bot;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // VELVET SHACKLES (plan 2.10) AND THE SHARED LEGALITY CHECK (plan 1.10),
    // milestone D.
    //
    // THE SPELL IS FOUR LINES OF RESOLUTION; THE MILESTONE IS THE PREDICATE.
    // Velvet Shackles applies Rooted for two effective turns and nothing else,
    // so almost every test here is really about CombatActions.IsLegalFor being
    // consulted at all four sites that have to agree about it -- the player's
    // menu, the enemy's draw, the committed telegraph and execution. A spell
    // that lands a status nobody reads is the failure this file is for.
    //
    // ROOTED ITSELF GAINS THE RESTRICTION, rather than a second status beside
    // it (plan 2.10). The consequence is deliberate and is pinned here: the
    // one existing Rooted source in content (Sylvan's RootChancePercent, a
    // player weapon modifier) now denies a monster its whole physical kit for
    // the turn rather than only its bare swing.
    //
    // DRIVEN THROUGH THE DISPATCHER, per the plan's Appendix B. "Refused and
    // spends nothing" is a claim about CastSkill's refusal ORDER, which
    // calling a resolution directly would skip entirely.
    public class VelvetShacklesTests
    {
        // Same gap RootedStatusTests uses, and for the same reason: slow
        // enough that the hero always opens, fast enough that one hero action
        // is followed by exactly one monster reply.
        private static CombatantState Hero(string name = "Hero", int health = 500, int speed = 10) =>
            new CombatantState(name, true, health, 60, 20, speed);

        private static CombatantState Monster(string name = "Monster", int health = 1000, int speed = 9) =>
            new CombatantState(name, false, health, 10, 15, speed);

        private static ResolvedEnemy Source(string id) =>
            new ResolvedEnemy(id, id, new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0);

        // THE SPELL AS AUTHORED (skills.json, velvet_shackles): Afflict,
        // SingleEnemy, 9 mana, Rooted for 2. Written out rather than loaded so
        // this file runs in the dotnet host; the shipped row's own numbers are
        // pinned against the table in PhysicalMoveAuditTests and by the
        // content build.
        private static ResolvedSkill Shackles(int mana = 9, int turns = 2, bool freeAction = false) =>
            new ResolvedSkill("velvet_shackles", "Velvet Shackles", "", "sheep", 1,
                SkillEffect.Afflict, SkillTargeting.SingleEnemy, mana, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0,
                appliesStatus: StatusEffectType.Rooted, statusMagnitude: 0, statusDuration: turns,
                freeAction: freeAction, physicalMove: false);

        private static ResolvedSkill PhysicalStrike(int mana = 0) =>
            new ResolvedSkill("brawler_slam", "Slam", "", "bear", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, mana, 0, false, 0, 30, false,
                null, SpellPresentation.None, 0, physicalMove: true);

        private static ResolvedSkill Cast(int mana = 0) =>
            new ResolvedSkill("frost_flare", "Frost Flare", "", "sheep", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, mana, 0, false, 0, 30, false,
                null, SpellPresentation.None, 0, physicalMove: false);

        private static IReadOnlyList<CombatBeat> EnemyBeats(IReadOnlyList<CombatBeat> beats) =>
            beats.Where(b => b.Actor != null && !b.Actor.IsPlayerSide).ToList();

        private static IEnumerable<string> MessagesOf(IReadOnlyList<CombatBeat> beats) =>
            beats.SelectMany(b => b.Messages);

        // ---- the spell itself --------------------------------------------------

        [Test]
        public void ACastOfVelvetShackles_RootsOneEnemyForTwoTurns_AndDealsNoDamage()
        {
            var hero = Hero();
            var foe = Monster();
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                // A FREE ACTION FOR THE FIXTURE ONLY, the same device
                // GaleScytheTests uses and for the same reason: an ordinary
                // cast hands the turn on, the monster's own (forfeited) reply
                // resolves inside this very call, and the clock has already
                // spent one of the two turns before the assertion can read it.
                // The shipped row authors no freeAction; what the two turns
                // BUY is pinned by TheSecondOfTwoShackledTurns below.
                new List<PlayerKit> { new PlayerKit("sheep", CharacterRole.Support, new[] { Shackles(freeAction: true) }, null, DamageType.Physical) },
                new List<EnemyKit> { new EnemyKit(Source("monster"), false) },
                new SeededRandom(3)) { DamageVarianceRange = 0f };
            session.Begin();

            int healthBefore = foe.CurrentHealth;
            Assert.IsTrue(session.CastSkill(0, foe), "the cast must be accepted");

            Assert.AreEqual(healthBefore, foe.CurrentHealth,
                "an Afflict deals no damage -- it has no damage instance and never enters AfterDefences");
            Assert.AreEqual(51, hero.CurrentMana, "9 of 60 mana paid");

            var rooted = foe.Statuses.SingleOrDefault(s => s.Type == StatusEffectType.Rooted);
            Assert.IsNotNull(rooted, "Velvet Shackles must land Rooted");
            Assert.AreEqual(2, rooted.TurnsRemaining, "two effective turns, as authored");
        }

        // ---- the enemy's draw (site 2) ------------------------------------------

        [Test]
        public void ARootedEnemy_CannotDrawAPhysicalAbility_ButStillDrawsACast()
        {
            // TWO ABILITIES, ONE OF EACH KIND, and the physical one weighted
            // so heavily that every roll in [0,1) takes it at baseline (see
            // EnemyAbilityDraw.Pick: the roll is clamped to 0.9999999f before
            // it is multiplied by the total weight). The baseline assertion is
            // therefore arithmetic rather than a lucky seed.
            var pool = new List<EnemyAbility>
            {
                EnemyAbility.Of(PhysicalStrike(), 1_000_000f),
                EnemyAbility.Of(Cast(), 0.01f),
            };

            var baselineHero = Hero();
            var baselineFoe = Monster();
            var baseline = new FightSession(new CombatEncounter(new[] { baselineHero }, new[] { baselineFoe }),
                new List<PlayerKit> { null },
                new List<EnemyKit> { new EnemyKit(Source("monster"), false, pool) },
                new SeededRandom(5)) { DamageVarianceRange = 0f };
            baseline.Begin();
            baseline.ExecuteAttack(baselineFoe);
            var baselineLines = MessagesOf(EnemyBeats(baseline.DrainBeats())).ToList();
            Assert.IsTrue(baselineLines.Any(m => m.Contains("Slam")),
                "fixture check: the overwhelming weight must win the draw when nothing is rooted");

            var hero = Hero();
            var foe = Monster();
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { null },
                new List<EnemyKit> { new EnemyKit(Source("monster"), false, pool) },
                new SeededRandom(5)) { DamageVarianceRange = 0f };
            StatusEffects.Apply(foe.Statuses, StatusEffectType.Rooted, 0, 5);
            session.Begin();

            session.ExecuteAttack(foe);
            var lines = MessagesOf(EnemyBeats(session.DrainBeats())).ToList();

            Assert.IsFalse(lines.Any(m => m.Contains("Slam")),
                "a shackled monster may not take a physical ability however heavily it is weighted");
            Assert.IsTrue(lines.Any(m => m.Contains("Frost Flare")),
                "the cast is untouched -- a root stops the body, not the spell");
        }

        // ---- the committed telegraph (site 3) -----------------------------------

        [Test]
        public void ACommittedPhysicalSkill_IsVoidedWhenShacklesLandAfterItWasDrawn()
        {
            // THE ORDINARY CASE FOR THIS SPELL, not a corner one: Velvet
            // Shackles is cast on the player's turn, and the enemy's intent
            // for the reply that follows was drawn before the cast existed.
            // An intent that resolves anyway is the spell visibly not working.
            var hero = Hero();
            var foe = Monster();
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { new PlayerKit("sheep", CharacterRole.Support, new[] { Shackles() }, null, DamageType.Physical) },
                new List<EnemyKit> { new EnemyKit(Source("monster"), false,
                    new List<EnemyAbility> { EnemyAbility.Of(PhysicalStrike(), 1f) }) },
                new SeededRandom(7)) { DamageVarianceRange = 0f };
            session.Begin();

            Assert.AreEqual("Slam", session.IntentFor(foe),
                "fixture check: the physical ability must be the committed telegraph before the cast");

            int before = hero.CurrentHealth;
            session.CastSkill(0, foe);

            var lines = MessagesOf(EnemyBeats(session.DrainBeats())).ToList();
            Assert.AreEqual(before, hero.CurrentHealth,
                "a commitment made before the root landed must be voided at resolution, not honoured");
            Assert.IsTrue(lines.Any(m => m.Contains("rooted")),
                "and the player must be told why the telegraphed blow did not arrive");
        }

        // ---- the player's menu and the dispatcher (sites 1 and 4) ---------------

        [Test]
        public void ARootedPlayer_IsRefusedAPhysicalSkill_AndSpendsNothing()
        {
            var hero = Hero();
            var foe = Monster();
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { new PlayerKit("bear", CharacterRole.Tank, new[] { PhysicalStrike(mana: 5) }, null, DamageType.Physical) },
                new List<EnemyKit> { new EnemyKit(Source("monster"), false) },
                new SeededRandom(9)) { DamageVarianceRange = 0f };
            session.Begin();
            StatusEffects.Apply(hero.Statuses, StatusEffectType.Rooted, 0, 3);

            int mana = hero.CurrentMana;
            int foeHealth = foe.CurrentHealth;

            Assert.IsFalse(session.CastSkill(0, foe), "a rooted character may not slam");
            Assert.AreEqual(mana, hero.CurrentMana, "a refusal spends no mana");
            Assert.AreEqual(foeHealth, foe.CurrentHealth, "and lands nothing");
            Assert.IsTrue(session.Current == hero, "and costs no turn");
        }

        [Test]
        public void ARootedPlayer_IsRefusedThePlainAttack_AndSpendsNoTurn()
        {
            var hero = Hero();
            var foe = Monster();
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { new PlayerKit("sheep", CharacterRole.Support, new[] { Cast() }, null, DamageType.Physical) },
                new List<EnemyKit> { new EnemyKit(Source("monster"), false) },
                new SeededRandom(11)) { DamageVarianceRange = 0f };
            session.Begin();
            StatusEffects.Apply(hero.Statuses, StatusEffectType.Rooted, 0, 3);

            int foeHealth = foe.CurrentHealth;
            Assert.IsFalse(session.ExecuteAttack(foe),
                "the plain attack is a physical move by construction and is refused with everything else");
            Assert.AreEqual(foeHealth, foe.CurrentHealth, "no swing landed");
            Assert.IsTrue(session.Current == hero, "and the turn did not pass");
        }

        [Test]
        public void ARootedPlayersNonPhysicalCast_IsUntouched()
        {
            var hero = Hero();
            var foe = Monster();
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { new PlayerKit("sheep", CharacterRole.Support, new[] { Cast(mana: 5) }, null, DamageType.Physical) },
                new List<EnemyKit> { new EnemyKit(Source("monster"), false) },
                new SeededRandom(13)) { DamageVarianceRange = 0f };
            session.Begin();
            StatusEffects.Apply(hero.Statuses, StatusEffectType.Rooted, 0, 3);

            int foeHealth = foe.CurrentHealth;
            Assert.IsTrue(session.CastSkill(0, foe), "a root forbids physical moves, not casting");
            Assert.Less(foe.CurrentHealth, foeHealth, "and the cast lands in full");
        }

        [Test]
        public void TheMenuGreysAPhysicalRow_AndSaysRootedRatherThanItsCost()
        {
            var hero = Hero();
            var foe = Monster();
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { new PlayerKit("bear", CharacterRole.Tank,
                    new[] { PhysicalStrike(mana: 5), Cast(mana: 5) }, null, DamageType.Physical) },
                new List<EnemyKit> { new EnemyKit(Source("monster"), false) },
                new SeededRandom(15)) { DamageVarianceRange = 0f };
            session.Begin();
            StatusEffects.Apply(hero.Statuses, StatusEffectType.Rooted, 0, 3);

            var options = session.SkillOptionsFor(hero);
            Assert.AreEqual(2, options.Count, "both rows stay on the menu -- greyed, never dropped");
            Assert.IsTrue(options[0].Restricted, "the slam is refused");
            Assert.IsFalse(options[0].Ready, "so it is not pressable");
            Assert.IsTrue(options[0].Affordable,
                "and the reason is NOT affordability -- a row that said 'no mana' would send the player " +
                "to look at the wrong bar");
            Assert.IsFalse(options[1].Restricted, "the cast is untouched");
            Assert.IsTrue(options[1].Ready);

            var rows = FightHudModel.SkillRows(options, hero);
            Assert.AreEqual("Rooted", rows[0].Cost, "the cost column names what is actually stopping them");
            Assert.IsFalse(rows[0].Affordable, "and the row draws dimmed");
            Assert.IsTrue(rows[1].Affordable);
        }

        // ---- the bot's legal menu ------------------------------------------------

        [Test]
        public void ARootedActorsLegalMenu_OffersNeitherTheSwingNorThePhysicalCast()
        {
            var hero = Hero();
            var foe = Monster();
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { new PlayerKit("bear", CharacterRole.Tank,
                    new[] { PhysicalStrike(mana: 5), Cast(mana: 5) }, null, DamageType.Physical) },
                new List<EnemyKit> { new EnemyKit(Source("monster"), false) },
                new SeededRandom(17)) { DamageVarianceRange = 0f };
            session.Begin();

            var before = FightAction.LegalActions(session, hero, new List<SatchelStack>());
            Assert.IsTrue(before.Any(a => a.Kind == FightActionKind.Attack),
                "fixture check: the swing is on the menu to begin with");
            Assert.AreEqual(2, before.Count(a => a.Kind == FightActionKind.Skill),
                "fixture check: and both casts are");

            StatusEffects.Apply(hero.Statuses, StatusEffectType.Rooted, 0, 3);
            var after = FightAction.LegalActions(session, hero, new List<SatchelStack>());

            Assert.IsFalse(after.Any(a => a.Kind == FightActionKind.Attack),
                "a policy handed the swing would stall on a command the dispatcher refuses");
            Assert.AreEqual(1, after.Count(a => a.Kind == FightActionKind.Skill),
                "only the non-physical cast survives");
            Assert.IsNotEmpty(after, "and the list is not empty, so no policy can stall on it");
        }

        // ---- the duration: the SECOND affected turn ------------------------------

        [Test]
        public void TheSecondOfTwoShackledTurns_IsStillRestricted()
        {
            // THE CLOCK IS THE POINT. Rooted is an AtTurnEnd status (plan D1,
            // milestone A), so "2" means two of the bearer's turns fully
            // covered -- the restriction intact through the second turn's own
            // ACTION, not lifted by the tick that opens it. Under the old
            // turn-start countdown this test's third assertion would have been
            // the second one's.
            var hero = Hero();
            var foe = Monster();
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { null },
                new List<EnemyKit> { new EnemyKit(Source("monster"), false) },
                new SeededRandom(19)) { DamageVarianceRange = 0f };
            session.Begin();
            StatusEffects.Apply(foe.Statuses, StatusEffectType.Rooted, 0, 2);

            var actedOn = new List<bool>();
            for (int turn = 0; turn < 3; turn++)
            {
                int before = hero.CurrentHealth;
                session.ExecuteAttack(foe);
                session.DrainBeats();
                actedOn.Add(hero.CurrentHealth < before);
            }

            Assert.IsFalse(actedOn[0], "the first shackled turn is forfeited");
            Assert.IsFalse(actedOn[1], "and so is the second -- the restriction covers its action");
            Assert.IsTrue(actedOn[2], "the third turn is free, so the root lasted exactly two");
        }

        // ---- the fallback, and its limits ----------------------------------------

        [Test]
        public void AShackledPlayerWithAReadyCast_IsNotForfeited()
        {
            // THE FALLBACK MUST NOT OVERREACH. A rooted character with
            // anything legal left takes their turn as normal; only one with
            // nothing at all is skipped. Every character in live content
            // carries a non-physical skill, so this is the arm that actually
            // fires in play.
            var hero = Hero();
            var foe = Monster();
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { new PlayerKit("sheep", CharacterRole.Support, new[] { Cast(mana: 5) }, null, DamageType.Physical) },
                new List<EnemyKit> { new EnemyKit(Source("monster"), false) },
                new SeededRandom(21)) { DamageVarianceRange = 0f };
            StatusEffects.Apply(hero.Statuses, StatusEffectType.Rooted, 0, 3);
            session.Begin();

            Assert.IsTrue(session.IsPlayerTurn, "the turn must be handed to the player, not skipped past");
            Assert.IsTrue(session.Current == hero);
            Assert.IsFalse(MessagesOf(session.DrainBeats()).Any(m => m.Contains("rooted fast")),
                "and nothing may announce a forfeit that did not happen");
        }

        [Test]
        public void AShackledPlayerWithNothingLegalLeft_ForfeitsTheTurn_RatherThanStalling()
        {
            // A kit of one physical skill and nothing else, which no shipped
            // character has -- the guarantee that a turn can always END, held
            // by Attack until milestone D made Attack refusable too.
            var hero = Hero();
            var foe = Monster();
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { new PlayerKit("bear", CharacterRole.Tank, new[] { PhysicalStrike() }, null, DamageType.Physical) },
                new List<EnemyKit> { new EnemyKit(Source("monster"), false) },
                new SeededRandom(23)) { DamageVarianceRange = 0f };
            StatusEffects.Apply(hero.Statuses, StatusEffectType.Rooted, 0, 3);
            session.Begin();

            var lines = MessagesOf(session.DrainBeats()).ToList();
            Assert.IsTrue(lines.Any(m => m.Contains("rooted fast")),
                "the turn is forfeited through ResolveSkippedTurn, and the HUD says why");
            Assert.IsFalse(lines.Any(m => m.Contains("Guard")),
                "no Guard mechanic was introduced -- forfeiture stays the answer (plan 1.10)");
        }

        // ---- RNG ------------------------------------------------------------------

        [Test]
        public void AskingWhetherAnActionIsLegal_ConsumesNoDrawFromTheSeededStream()
        {
            // EVERY SITE THAT ASKS IS A QUERY, NOT A COMMITMENT. The
            // resolution-time recheck runs on a branch whose frequency depends
            // on how the player is playing, so a draw spent here would make a
            // seeded run's whole shape depend on how often something got
            // rooted -- the one dependency a reproducible run cannot have.
            var rng = new SeededRandom(29);
            var hero = Hero();
            var foe = Monster();
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { new PlayerKit("bear", CharacterRole.Tank,
                    new[] { PhysicalStrike(mana: 5), Cast(mana: 5) }, null, DamageType.Physical) },
                new List<EnemyKit> { new EnemyKit(Source("monster"), false,
                    new List<EnemyAbility> { EnemyAbility.Of(PhysicalStrike(), 1f) }) },
                rng) { DamageVarianceRange = 0f };
            session.Begin();
            StatusEffects.Apply(hero.Statuses, StatusEffectType.Rooted, 0, 3);
            StatusEffects.Apply(foe.Statuses, StatusEffectType.Rooted, 0, 3);

            ulong before = rng.State;

            session.SkillOptionsFor(hero);
            FightAction.LegalActions(session, hero, new List<SatchelStack>());
            session.CastSkill(0, foe);
            session.ExecuteAttack(foe);
            CombatActions.PlainAttackIsLegalFor(hero, out _);

            Assert.AreEqual(before, rng.State,
                "a menu repaint, a bot's legal menu and two refusals must all spend nothing");
        }
    }
}
