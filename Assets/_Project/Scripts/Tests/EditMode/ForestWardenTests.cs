using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // The forest's own boss: three abilities, none of them a plain swing.
    // Overhead Slam is the familiar shape (a flat-amount DamageSingle that
    // ignores Defense, same lineage as the golem's Boulder Slam) — the two
    // worth real coverage are the two mechanics nothing else in the game
    // does yet. Grapple is the first authored use of Stun. Roar is the
    // first thing in this codebase that adds a combatant to a fight already
    // in progress, and the cap that stops it summoning a third rat is the
    // one piece of this boss with no precedent anywhere to copy from.
    public class ForestWardenTests
    {
        private static CombatantState Fighter(string name, bool isPlayerSide, int maxHealth = 100,
            int attack = 7, int defense = 0, int speed = 5) =>
            new CombatantState(name, isPlayerSide, maxHealth, 0, attack, defense, speed);

        private static ResolvedEnemy Source(string id, IReadOnlyList<EnemyAbilityRef> abilities = null) =>
            new ResolvedEnemy(id, id, new StatBlock(), 0, 0, isBoss: true,
                DamageType.Fire, DamageType.Nature, 0,
                abilities: abilities ?? System.Array.Empty<EnemyAbilityRef>());

        private static FightSession.SummonFactory FactoryFor(string enemyId, CombatantState state) =>
            (string id, out CombatantState outState, out EnemyKit outKit) =>
            {
                if (id == enemyId)
                {
                    outState = state;
                    outKit = new EnemyKit(Source(id), false);
                    return true;
                }

                outState = null;
                outKit = null;
                return false;
            };

        // ---- who else a floor-1 boss room could field --------------------------

        // THE ANSWER TO "MAKE IT THE ONLY BOSS", pinned against the real
        // enemies.json rather than against a fixture.
        //
        // There is no per-biome boss table in this project and this test is not
        // asking for one. A boss room draws from every boss whose minFloor it
        // has reached (EncounterRoll.Roll), so "the forest's boss is the only
        // one the forest can field" is not a feature -- it is an arithmetic
        // fact about the roster, and the way to make it true is for every other
        // boss to be either benched or deeper. The Hollow Choir was the other
        // floor-1 boss and is benched (active: false) for exactly this reason.
        //
        // The value of pinning it here is that it fails the moment someone
        // authors a second floor-1 boss, which is the only way this quietly
        // stops being true.
        [Test]
        public void TheForestTrollIsTheOnlyBossAFloorOneRoomCanDraw()
        {
            var resolved = ResolveEnemiesJson();

            var floorOneBosses = resolved
                .Where(e => e.Active && e.IsBoss && e.MinFloor <= 1)
                .Select(e => e.Id)
                .OrderBy(id => id)
                .ToList();

            CollectionAssert.AreEqual(new[] { "forest_warden" }, floorOneBosses,
                "a floor-1 boss room draws from every active boss whose minFloor it has reached, " +
                "so this list IS which bosses the first floor can field");
        }

        // The other half of the same fact, said the other way round: benching
        // an entry is what removes it from play, and a benched entry is still
        // validated. If the Hollow Choir were deleted outright instead, this
        // would fail on the missing id and say why.
        [Test]
        public void TheHollowChoirIsBenchedRatherThanDeleted()
        {
            var choir = ResolveEnemiesJson().SingleOrDefault(e => e.Id == "hollow_choir");

            Assert.IsNotNull(choir.Id,
                "hollow_choir is gone from enemies.json entirely -- if that was deliberate, delete " +
                "this test with it; if not, the entry was lost rather than benched");
            Assert.IsFalse(choir.Active,
                "the Hollow Choir is active again, which puts a second boss in the floor-1 pool");
        }

        // Reads and resolves the real catalogue, so these pins go through the
        // same validation the content build does. Walks up for the folder the
        // way FightCapacityPinTests does -- the test runner's working directory
        // is not the project root.
        private static IReadOnlyList<ResolvedEnemy> ResolveEnemiesJson()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Assets", "_Project", "ContentData", "enemies.json")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "Could not locate Assets/_Project/ContentData/enemies.json from the working directory.");

            string json = File.ReadAllText(Path.Combine(dir.FullName, "Assets", "_Project", "ContentData", "enemies.json"));
            var file = UnityEngine.JsonUtility.FromJson<RawEnemyFile>(json);

            Assert.IsTrue(EnemyEntryResolver.TryResolveAll(file.enemies, out var resolved, out var errors),
                "enemies.json does not resolve: " + string.Join("; ", errors ?? new List<string>()));

            return resolved;
        }

        // ---- Overhead Slam: same lineage as the golem's slam ------------------

        [Test]
        public void OverheadSlam_IgnoresDefenseEntirely()
        {
            var slam = new ResolvedSkill("overhead_slam", "Overhead Slam", "", "forest_warden", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, 0, 0, false, 0, 4, ignoresDefense: true,
                damageInstances: null, presentation: SpellPresentation.None, sortOrder: 0);

            var warden = Fighter("Forest Troll", false, attack: 7);

            int atLowDefense = Landed(slam, warden, defence: 2);
            int atHighDefense = Landed(slam, warden, defence: 50);

            Assert.AreEqual(atLowDefense, atHighDefense,
                "an armor-penetrating slam must land the same whatever the target is wearing");
            Assert.Greater(atLowDefense, 0, "fixture: the slam should still deal something");
        }

        private static int Landed(ResolvedSkill skill, CombatantState attacker, int defence)
        {
            var victim = Fighter("Shawn", true, maxHealth: 5000, attack: 7, defense: defence, speed: 10);

            int raw = SkillResolution.Amount(skill.Effect, attacker, victim,
                skill.Power, skill.FlatAmount, 0, skill.IgnoresDefense);

            return DamagePipeline.AfterDefences(raw, attacker, victim,
                attackType: null, affinity: ElementalAffinity.Neutral,
                varianceRange: 0f, rng: null, resolveWard: null).Damage;
        }

        // ---- Grapple: the first authored use of Stun ---------------------------

        [Test]
        public void Grapple_StunsTheTargetThroughItsNextTurnThenClearsIt()
        {
            var grapple = new ResolvedSkill("grapple", "Grapple", "", "forest_warden", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, 0, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0,
                appliesStatus: StatusEffectType.Stun, statusMagnitude: 1, statusDuration: 1);

            // High HP and defense so a big warden/hero speed gap can't turn
            // this into a curb-stomp before the assertion ever runs -- the
            // point of this test is the stun bookkeeping, not survival.
            var hero = Fighter("Hero", true, maxHealth: 5000, defense: 50, speed: 1);
            var warden = Fighter("Forest Troll", false, speed: 20);
            var encounter = new CombatEncounter(new[] { hero }, new[] { warden });
            var session = new FightSession(encounter,
                new List<PlayerKit> { null }, new List<EnemyKit> { new EnemyKit(Source("forest_warden"), false) },
                new SeededRandom(1)) { DamageVarianceRange = 0f };

            session.CastSkill(grapple, hero);

            // By the time CastSkill returns, its own AdvanceAfterAction has
            // already auto-resolved every turn between the cast and the next
            // one a player can genuinely act on -- and a stunned player's
            // turn is now one of the turns that loop skips (see
            // AutoResolveEnemyTurns), same as an enemy's always was. So the
            // stun must already be spent, not still sitting on the hero
            // waiting for a skip that never happens.
            Assert.IsFalse(StatusEffects.HasStun(hero.Statuses),
                "the stun should have been spent skipping the hero's next turn, not left sitting unconsumed");
            Assert.IsTrue(encounter.IsOver || encounter.IsPlayerTurn,
                "control must land back on a real player turn (or the fight ending), never stuck mid-skip");
        }

        // ---- Roar: summoning mid-fight, and the cap that limits it ------------

        [Test]
        public void Roar_CallsInTheNamedEnemy()
        {
            var roar = new ResolvedSkill("roar", "Roar", "", "forest_warden", 1,
                SkillEffect.Summon, SkillTargeting.Self, 0, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0,
                summonEnemyId: "rat", summonCap: 2);

            var hero = Fighter("Hero", true, speed: 1);
            var warden = Fighter("Forest Troll", false, speed: 20);
            var encounter = new CombatEncounter(new[] { hero }, new[] { warden });
            var rat = Fighter("Giant Rat", false, speed: 5);

            var session = new FightSession(encounter,
                new List<PlayerKit> { null }, new List<EnemyKit> { new EnemyKit(Source("forest_warden"), false) },
                new SeededRandom(1), summonFactory: FactoryFor("rat", rat)) { DamageVarianceRange = 0f };

            Assert.AreEqual(1, encounter.Enemies.Count, "fixture: the warden starts the fight alone");

            session.CastSkill(roar, null);

            CollectionAssert.Contains(encounter.Enemies, rat, "Roar resolved but no rat ever joined the fight");
        }

        [Test]
        public void Roar_WontSummonPastItsCap()
        {
            var roar = new ResolvedSkill("roar", "Roar", "", "forest_warden", 1,
                SkillEffect.Summon, SkillTargeting.Self, 0, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0,
                summonEnemyId: "rat", summonCap: 2);

            var hero = Fighter("Hero", true, speed: 1);
            var warden = Fighter("Forest Troll", false, speed: 20);
            var ratA = Fighter("Rat A", false, speed: 5);
            var ratB = Fighter("Rat B", false, speed: 5);
            var encounter = new CombatEncounter(new[] { hero }, new[] { warden, ratA, ratB });

            var thirdRat = Fighter("Rat C", false, speed: 5);
            var session = new FightSession(encounter,
                new List<PlayerKit> { null },
                new List<EnemyKit>
                {
                    new EnemyKit(Source("forest_warden"), false),
                    new EnemyKit(Source("rat"), false),
                    new EnemyKit(Source("rat"), false),
                },
                new SeededRandom(1), summonFactory: FactoryFor("rat", thirdRat)) { DamageVarianceRange = 0f };

            session.CastSkill(roar, null);

            CollectionAssert.DoesNotContain(encounter.Enemies, thirdRat,
                "two Giant Rats were already on the field, so Roar should not have called a third");
            Assert.AreEqual(3, encounter.Enemies.Count);
        }

        [Test]
        public void Roar_IsNeverDrawnOnceTheCapIsAlreadyMet()
        {
            var roar = new ResolvedSkill("roar", "Roar", "", "forest_warden", 1,
                SkillEffect.Summon, SkillTargeting.Self, 0, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0,
                summonEnemyId: "rat", summonCap: 2);

            var hero = Fighter("Hero", true, speed: 1);
            var warden = Fighter("Forest Troll", false, speed: 1);
            var ratA = Fighter("Rat A", false, speed: 5);
            var ratB = Fighter("Rat B", false, speed: 5);
            var encounter = new CombatEncounter(new[] { hero }, new[] { warden, ratA, ratB });

            // Roar is the ONLY entry in the pool. If the cap gates it out of
            // the draw entirely, PrepareEnemyIntents must still telegraph a
            // plain attack rather than leaving the warden with nothing to do.
            var pool = new List<EnemyAbility> { EnemyAbility.Of(roar, 1f) };
            var session = new FightSession(encounter,
                new List<PlayerKit> { null },
                new List<EnemyKit>
                {
                    new EnemyKit(Source("forest_warden"), false, pool),
                    new EnemyKit(Source("rat"), false),
                    new EnemyKit(Source("rat"), false),
                },
                new SeededRandom(1), summonFactory: FactoryFor("rat", Fighter("Rat D", false))) { DamageVarianceRange = 0f };

            session.Begin();

            Assert.AreNotEqual("Roar", session.IntentFor(warden),
                "Roar was drawn even though the field already holds its cap of rats");
        }
    }
}
