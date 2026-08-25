using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    // A monster's abilities: the draw, and what the telegraph promises about it.
    //
    // Both halves matter and only one of them is about correctness. A weighted
    // pick that is subtly biased makes a boss feel wrong in a way nobody can
    // name; a telegraph that describes the wrong action makes the fight
    // unplayable, because the whole point of committing an intent a turn early
    // is that the player can act on it.
    public class EnemyAbilityTests
    {
        private static EnemyAbility Legacy(string label, float power, float weight) =>
            EnemyAbility.LegacyAttack(label, power, weight);

        // ---- the draw ------------------------------------------------------------

        [Test]
        public void APoolWithOneEntryAlwaysPicksIt()
        {
            var pool = new List<EnemyAbility> { Legacy("Swing", 1f, 1f) };

            Assert.AreEqual(0, EnemyAbilityDraw.Pick(pool, 0f));
            Assert.AreEqual(0, EnemyAbilityDraw.Pick(pool, 0.5f));
            Assert.AreEqual(0, EnemyAbilityDraw.Pick(pool, 1f));
        }

        // WEIGHTS ARE RELATIVE, so a pool of 3 and 1 splits the roll at 0.75 --
        // not at 0.5, and not at some normalised probability the author has to
        // compute. Pinned with literal boundaries rather than by re-deriving
        // the division here, which would make this a restatement of the code.
        [Test]
        public void TheRollSplitsAtTheCumulativeWeight()
        {
            var pool = new List<EnemyAbility>
            {
                Legacy("Swing", 1f, 3f),
                Legacy("Slam", 1.8f, 1f),
            };

            Assert.AreEqual(0, EnemyAbilityDraw.Pick(pool, 0f), "the bottom of the range is the first entry");
            Assert.AreEqual(0, EnemyAbilityDraw.Pick(pool, 0.74f), "still inside the 3-weight share");
            Assert.AreEqual(1, EnemyAbilityDraw.Pick(pool, 0.76f), "past 0.75 belongs to the 1-weight share");
            Assert.AreEqual(1, EnemyAbilityDraw.Pick(pool, 1f), "the very top must not fall off the end");
        }

        // A generator is allowed to return exactly 1, and an unclamped
        // implementation walks off the end of the list and returns nothing from
        // a pool that plainly has entries.
        [Test]
        public void ARollOfExactlyOnePicksTheLastEntryRatherThanNothing()
        {
            var pool = new List<EnemyAbility>
            {
                Legacy("A", 1f, 1f), Legacy("B", 1f, 1f), Legacy("C", 1f, 1f),
            };

            Assert.AreEqual(2, EnemyAbilityDraw.Pick(pool, 1f));
        }

        // A zero weight is a legitimate authoring state -- "written, but switched
        // off while I tune" -- and must never be chosen.
        [Test]
        public void AZeroWeightIsNeverChosen()
        {
            var pool = new List<EnemyAbility>
            {
                Legacy("Never", 1f, 0f),
                Legacy("Always", 1f, 1f),
            };

            for (int i = 0; i <= 100; i++)
            {
                Assert.AreEqual(1, EnemyAbilityDraw.Pick(pool, i / 100f),
                    $"roll {i / 100f} chose the zero-weight entry");
            }
        }

        [Test]
        public void AnEmptyOrFullyZeroedPoolPicksNothing()
        {
            Assert.AreEqual(-1, EnemyAbilityDraw.Pick(null, 0.5f));
            Assert.AreEqual(-1, EnemyAbilityDraw.Pick(new List<EnemyAbility>(), 0.5f));
            Assert.AreEqual(-1, EnemyAbilityDraw.Pick(
                new List<EnemyAbility> { Legacy("A", 1f, 0f) }, 0.5f));
        }

        // ---- the legacy pool -----------------------------------------------------
        //
        // A monster authored before abilities existed has to behave EXACTLY as
        // it did. Its trio is a two-entry pool: skillChance of the scaled
        // attack, the remainder of a plain one.
        [Test]
        public void ALegacyMonsterBecomesATwoEntryPoolSplitOnItsOwnChance()
        {
            var kit = new EnemyKit(new ResolvedEnemy("golem", "Golem", new StatBlockOf(), 5, 3, false,
                DamageType.Physical, DamageType.Physical, 0,
                skillName: "Boulder Slam", skillPower: 1.8f, skillChance: 0.25f), false);

            Assert.AreEqual(2, kit.Abilities.Count);

            Assert.IsTrue(kit.Abilities[0].IsPlainSwing, "the first entry is the plain swing");
            Assert.AreEqual(0.75f, kit.Abilities[0].Weight, 0.0001f,
                "the swing carries the REMAINDER of the skill chance");

            Assert.AreEqual("Boulder Slam", kit.Abilities[1].Label);
            Assert.AreEqual(1.8f, kit.Abilities[1].Power, 0.0001f);
            Assert.AreEqual(0.25f, kit.Abilities[1].Weight, 0.0001f);
        }

        [Test]
        public void AMonsterWithNoSkillAtAllStillHasItsSwing()
        {
            var kit = new EnemyKit(new ResolvedEnemy("rat", "Rat", new StatBlockOf(), 5, 3, false,
                DamageType.Physical, DamageType.Physical, 0), false);

            Assert.AreEqual(1, kit.Abilities.Count, "a monster with no skill has exactly one thing it does");
            Assert.IsTrue(kit.Abilities[0].IsPlainSwing);
        }

        // ---- the telegraph -------------------------------------------------------
        //
        // The scope is read off the effect rather than authored, because an
        // author who could disagree with it could make the telegraph lie.
        [Test]
        public void ScopeFollowsTheEffect()
        {
            Assert.AreEqual(EnemyIntentScope.One, EnemyIntentIcons.ScopeFor(SkillEffect.DamageSingle));
            Assert.AreEqual(EnemyIntentScope.AllOpponents, EnemyIntentIcons.ScopeFor(SkillEffect.DamageAll));
            Assert.AreEqual(EnemyIntentScope.Self, EnemyIntentIcons.ScopeFor(SkillEffect.HealSelf));
            Assert.AreEqual(EnemyIntentScope.AllAllies, EnemyIntentIcons.ScopeFor(SkillEffect.HealParty));
        }

        // A HEAL WITH NO STATUS still has to read as a heal. The old KindFor
        // knew only about statuses, so a straight mend telegraphed as the
        // generic "skill" icon -- "something is coming" on the one turn it is
        // not coming for you.
        [Test]
        public void AHealTelegraphsAsAHealEvenWithNoStatusAttached()
        {
            Assert.AreEqual(EnemyIntentKind.Heal,
                EnemyIntentIcons.KindFor(SkillEffect.HealSelf, null, false));
            Assert.AreEqual(EnemyIntentKind.Heal,
                EnemyIntentIcons.KindFor(SkillEffect.HealParty, null, false));
        }

        // THE SAME GAP ONE STEP FURTHER ON. A Summon telegraphed as the generic
        // "skill" badge, which is the icon that means "something is coming" --
        // useless on the one turn where what is coming is another monster, and
        // where knowing it changes whether the player spends their burst now or
        // banks it for the fight that is about to be one enemy larger.
        [Test]
        public void ASummonTelegraphsAsASummon_NotAsAGenericSkill()
        {
            Assert.AreEqual(EnemyIntentKind.Summon,
                EnemyIntentIcons.KindFor(SkillEffect.Summon, null, false));
        }

        // The effect outranks the status, same precedence heals already have.
        // A summon that also poisoned would otherwise announce the poison and
        // hide the extra body, which is the larger fact by a distance.
        [Test]
        public void ASummonOutranksAnyStatusItAlsoCarries()
        {
            Assert.AreEqual(EnemyIntentKind.Summon,
                EnemyIntentIcons.KindFor(SkillEffect.Summon, StatusEffectType.Poison, true));
        }

        // A badge kind with no artwork behind it renders as NOTHING, silently
        // -- the failure this whole icon set was rebuilt to stop. The PlayMode
        // suite proves the sprite actually loads; this proves the two lookup
        // tables were not left with a default-case hole, which is the half that
        // is cheap to check and easy to forget.
        [Test]
        public void SummonHasItsOwnIconAndTintRatherThanFallingThroughToSkill()
        {
            Assert.AreNotEqual(EnemyIntentIcons.ResourceFor(EnemyIntentKind.Skill),
                EnemyIntentIcons.ResourceFor(EnemyIntentKind.Summon));
            Assert.AreNotEqual(EnemyIntentIcons.TintFor(EnemyIntentKind.Skill),
                EnemyIntentIcons.TintFor(EnemyIntentKind.Summon));
            Assert.AreNotEqual(EnemyIntentIcons.For(EnemyIntentKind.Skill),
                EnemyIntentIcons.For(EnemyIntentKind.Summon));
        }

        // The sentence under the badge, which had the same bug the icon did.
        // A summon's committed target is whoever the draw happened to pick, and
        // with the default scope the tooltip announced an attack on them.
        [Test]
        public void TheSummonTooltipSaysWhatIsComing_NotWhoItIsComingFor()
        {
            var shawn = new CombatantState("Shawn", true, 100, 0, 10, 0, 5);
            var intent = new EnemyIntent("Roar", EnemyIntentKind.Summon, shawn, 0, 0,
                EnemyIntentIcons.ScopeFor(SkillEffect.Summon));

            string tooltip = FightHudModel.IntentTooltip("Forest Troll", intent);

            StringAssert.Contains("Roar", tooltip);
            StringAssert.Contains("another monster", tooltip);
            StringAssert.DoesNotContain("Shawn", tooltip,
                "the draw picked Shawn as a target, but a Roar never touches him");
        }

        [Test]
        public void ADamagingSkillStillTakesItsKindFromTheStatusItInflicts()
        {
            Assert.AreEqual(EnemyIntentKind.Weaken,
                EnemyIntentIcons.KindFor(SkillEffect.DamageSingle, StatusEffectType.Vulnerable, true));
            Assert.AreEqual(EnemyIntentKind.Skill,
                EnemyIntentIcons.KindFor(SkillEffect.DamageSingle, null, false));
        }

        // ---- what the player actually reads --------------------------------------

        [Test]
        public void TheTooltipNamesOneTargetForASingleTargetBlow()
        {
            var shawn = new CombatantState("Shawn", true, 100, 0, 10, 0, 5);
            var intent = new EnemyIntent("Mud Burst", EnemyIntentKind.Weaken, shawn, 42, 0);

            StringAssert.Contains("Shawn", FightHudModel.IntentTooltip("Bog Witch", intent));
            StringAssert.Contains("42 damage", FightHudModel.IntentTooltip("Bog Witch", intent));
        }

        // NAMING ONE TARGET FOR A SWEEP IS WORSE THAN SAYING NOTHING: it is a
        // specific claim, and it is false.
        [Test]
        public void TheTooltipSaysWholePartyForASweep()
        {
            var shawn = new CombatantState("Shawn", true, 100, 0, 10, 0, 5);
            var intent = new EnemyIntent("Wail", EnemyIntentKind.Skill, shawn, 18, 0,
                EnemyIntentScope.AllOpponents);

            string text = FightHudModel.IntentTooltip("Hollow Choir", intent);

            StringAssert.Contains("your whole party", text);
            StringAssert.Contains("each", text, "a per-target figure has to say it is per target");
            StringAssert.DoesNotContain("Shawn", text,
                "a sweep must not name one victim - that is the claim a telegraph exists to avoid");
        }

        // A MAGNITUDE WITH NO SIGN READS AS A THREAT. "for about 40" under a
        // heal icon is the one sentence that sends a player to kill the wrong
        // monster.
        [Test]
        public void TheTooltipSaysHealingRatherThanDamageForAMend()
        {
            var witch = new CombatantState("Witch", false, 100, 0, 10, 0, 5);
            var intent = new EnemyIntent("Mend", EnemyIntentKind.Heal, witch, 40, 0,
                EnemyIntentScope.AllAllies, heals: true);

            string text = FightHudModel.IntentTooltip("Bog Witch", intent);

            StringAssert.Contains("its allies", text);
            StringAssert.Contains("40 healing", text);
            StringAssert.DoesNotContain("damage", text);
        }

        // ---- end to end ----------------------------------------------------------
        //
        // The pieces above are each correct on their own. This is the one that
        // would have caught them being correct and not connected: a monster
        // whose pool holds a real skill has to TELEGRAPH that skill and then
        // RESOLVE that skill, and the failure worth fearing is the two
        // disagreeing.
        private static ResolvedSkill Hex() =>
            new ResolvedSkill("hex", "Hex", "", "", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, 0, 0, false, 100, 25, false,
                null, SpellPresentation.None, 0,
                appliesStatus: StatusEffectType.Vulnerable, statusMagnitude: 25, statusDuration: 2);

        private static (FightSession session, CombatantState hero, CombatantState monster) WithAbility(
            ResolvedSkill skill)
        {
            // TOUGH ENOUGH TO SURVIVE THE BLOW. Health lost is capped by health
            // held, so a hero who dies to the cast reports the damage as exactly
            // their remaining HP -- which reads as the preview being wrong when
            // it is the assertion that cannot see past zero.
            var hero = new CombatantState("Shawn", true, 5000, 30, 40, 0, 10);
            // SLOWER THAN THE HERO, and tough enough to survive being hit.
            //
            // The turn order is what drives this: ExecuteAttack's argument is
            // the TARGET and the actor is whoever is up, so the monster's reply
            // only resolves when the turn advances past the hero. A witch that
            // dies to Shawn's opening swing never gets to cast the thing this
            // test is about.
            var monster = new CombatantState("Witch", false, 5000, 0, 12, 2, 9);
            var source = new ResolvedEnemy("witch", "Witch", new StatBlockOf(), 5, 3, false,
                DamageType.Physical, DamageType.Physical, 0);

            // Weight 1 against nothing else: the draw cannot pick anything but
            // the ability, so this tests the wiring rather than the dice.
            var kit = new EnemyKit(source, false, new List<EnemyAbility> { EnemyAbility.Of(skill, 1f) });

            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { monster }),
                null, new List<EnemyKit> { kit }, new SeededRandom(7)) { DamageVarianceRange = 0f };
            session.Begin();
            return (session, hero, monster);
        }

        [Test]
        public void AMonsterTelegraphsTheSkillItIsAboutToCast()
        {
            var (session, hero, monster) = WithAbility(Hex());

            session.PrepareEnemyIntents();
            var intent = session.IntentDetailFor(monster);

            Assert.IsTrue(intent.HasValue, "nothing was telegraphed at all");
            Assert.AreEqual("Hex", intent.Value.Label, "the telegraph names the wrong action");
            Assert.AreEqual(EnemyIntentKind.Weaken, intent.Value.Kind,
                "a skill inflicting Vulnerable telegraphs as the weaken icon, not the generic one");
            Assert.AreEqual(EnemyIntentScope.One, intent.Value.Scope);
            Assert.AreSame(hero, intent.Value.Target, "the victim is committed with the action");
            Assert.Greater(intent.Value.ExpectedDamage, 0, "the player cannot plan against a blank number");
        }

        // THE TELEGRAPH AND THE BLOW HAVE TO BE THE SAME EVENT. A monster that
        // announces one thing and does another is worse than one that announces
        // nothing, and it is the specific failure that resolving by label rather
        // than by index would produce.
        [Test]
        public void AndThenActuallyCastsIt()
        {
            var (session, hero, monster) = WithAbility(Hex());

            session.PrepareEnemyIntents();
            int expected = session.IntentDetailFor(monster).Value.ExpectedDamage;
            int before = hero.CurrentHealth;

            // The hero swings; the witch's reply resolves as the turn advances.
            session.ExecuteAttack(monster);

            Assert.Less(hero.CurrentHealth, before, "the announced blow never landed");
            Assert.AreEqual(expected, before - hero.CurrentHealth,
                "the damage differs from what was telegraphed, so the preview is a second formula");

            Assert.IsTrue(hero.Statuses.Any(st => st.Type == StatusEffectType.Vulnerable),
                "the skill's own status never applied -- a monster casting a real skill has to get " +
                "the whole skill, not the damage half of it");
        }


        // ---- what the view is handed ---------------------------------------------

        // A MONSTER'S CAST HAS TO BE DRAWN, and nothing checked that it was.
        //
        // THE TEST THAT WOULD HAVE CAUGHT THE DROPPED BEAT, and the reason it
        // is worth having on top of AndThenActuallyCastsIt just above: that one
        // asserts the hero lost health, which is model state and landed
        // perfectly well while every visible part of the cast was being thrown
        // away. A monster casting a real skill returned out of ResolveEnemyAction
        // before the CommitBeat at the bottom of it, so the beat holding the
        // spell frames, the poses, the number and the log line was abandoned.
        // Reported from play as the Bog Witch not using her Mud Burst animation.
        //
        // Asserting on the BEAT rather than on the damage is the whole point:
        // the beat is the only thing the view is ever handed, so it is the only
        // place "will the player see this" can be asked.
        //
        // THE POSE IS A SECOND BUG in the same three lines. A damaging skill's
        // authored stance was dropped: the enemy path set it before the beat it
        // belonged to existed (a silent no-op against a null _recordingBeat)
        // and ResolveDamageSingle then posed the caster with a flat "cast".
        [Test]
        public void AMonstersCastCarriesItsSpellAnimationAndItsOwnPose()
        {
            var (session, hero, monster) = WithAbility(Conjuring());

            session.PrepareEnemyIntents();
            session.ExecuteAttack(monster);

            var cast = session.DrainBeats().LastOrDefault(b => ReferenceEquals(b.Actor, monster));

            Assert.IsNotNull(cast,
                "the monster's cast never reached the view at all -- the beat was opened and " +
                "abandoned rather than committed, so its spell, pose, number and log line are gone");
            Assert.IsTrue(cast.HasSpellAnimation,
                "the monster's cast reached the view with no spell frames, so nothing would be drawn");
            Assert.AreEqual("Spells/mud_burst", cast.Vfx.path);
            Assert.AreSame(hero, cast.Target, "the effect lands on whoever was cast at");

            Assert.AreEqual("attack_charge", cast.Stances[monster],
                "the skill named its own stance and was posed with the generic cast instead");
        }

        // A MELEE SKILL HAS TO REACH WHAT IT HITS, and until the approach
        // vocabulary existed it could not.
        //
        // Every cast opens its beat with BeginBeat(isCast: true), which roots
        // the actor -- correct for a spell and wrong the moment monsters got
        // melee skills. The Warden's Grapple and the Treant's Trunk Slam both
        // resolve through the cast path, so both inherited "hold" and both
        // connected from across the stage without leaving their mark. Reported
        // from play as the grapple keeping the troll at the same spot.
        //
        // Asserted on the BEAT, which is the only thing the view is handed:
        // the approach is a fact about the action, and a test that watched a
        // rect move would be testing the tween instead of the decision.
        [TestCase(StageApproach.Lunge)]
        [TestCase(StageApproach.Close)]
        public void AMeleeSkillTellsTheStageToCloseTheDistance(StageApproach approach)
        {
            var (session, _, monster) = WithAbility(Swing(approach));

            session.PrepareEnemyIntents();
            session.ExecuteAttack(monster);

            var cast = session.DrainBeats().LastOrDefault(b => ReferenceEquals(b.Actor, monster));

            Assert.IsNotNull(cast);
            Assert.AreEqual(approach, cast.Approach);
            Assert.IsFalse(cast.ActorHoldsPosition,
                "a swing that stays where it is connects from across the stage");
        }

        // The default has to stay put, or every spell in the game starts
        // walking over to its target.
        [Test]
        public void ASkillThatSaysNothingAboutMovingStandsStillAsItAlwaysHas()
        {
            var (session, _, monster) = WithAbility(Hex());

            session.PrepareEnemyIntents();
            session.ExecuteAttack(monster);

            var cast = session.DrainBeats().LastOrDefault(b => ReferenceEquals(b.Actor, monster));

            Assert.IsNotNull(cast);
            Assert.AreEqual(StageApproach.Hold, cast.Approach);
            Assert.IsTrue(cast.ActorHoldsPosition);
        }

        // A BLOW THE STAGE HAS TO FEEL WITHOUT LANDING.
        //
        // Everything else derives its shake from the damage, which leaves the
        // Warden's Roar -- the loudest moment in its fight -- as the only
        // silent one, because a Summon deals nothing. The floor is authored,
        // and it has to survive the trip to the beat or the skill is decorative.
        [Test]
        public void ASkillCanInsistTheStageShakesEvenWhenItLandsNothing()
        {
            var (session, _, monster) = WithAbility(Bellow());

            session.PrepareEnemyIntents();
            session.ExecuteAttack(monster);

            var cast = session.DrainBeats().LastOrDefault(b => ReferenceEquals(b.Actor, monster));

            Assert.IsNotNull(cast);
            Assert.AreEqual(0, cast.Amount, "fixture: this skill is supposed to land nothing");
            Assert.AreEqual(0.85f, cast.Shake, 0.001f,
                "the authored shake never reached the beat, so the roar is silent on the stage");
        }

        private static ResolvedSkill Swing(StageApproach approach) =>
            new ResolvedSkill("swing", "Swing", "", "", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, 0, 0, false, 0, 12, false,
                null, SpellPresentation.None, 0, approach: approach);

        // Damages nothing on purpose -- a heal on the caster, which is the
        // nearest thing to the Roar that does not need a summon pool behind it.
        private static ResolvedSkill Bellow() =>
            new ResolvedSkill("bellow", "Bellow", "", "", 1, SkillEffect.HealSelf,
                SkillTargeting.Self, 0, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0, shake: 0.85f);

        // A damaging skill that states both halves of its presentation: real
        // frames to play, and a pose of its own to play them from.
        private static ResolvedSkill Conjuring() =>
            new ResolvedSkill("conjuring", "Conjuring", "", "", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, 0, 0, false, 0, 12, false,
                null, new SpellPresentation { path = "Spells/mud_burst", seconds = 0.65f, impactFrame = 13 }, 0,
                stance: "attack_charge");

        // ---- the golem's slam, pinned -------------------------------------------
        //
        // Boulder Slam was a 1.8x multiplier on the golem's basic attack and is
        // a real skill now. The two forms do not produce the same curve and
        // cannot: the multiplier scaled a figure the target's armour had
        // ALREADY been subtracted from, so armour counted twice against it,
        // while a skill's flatAmount is added BEFORE defence.
        //
        // flatAmount 2 lands it within about a tenth of the old number across
        // the defence band Shawn actually occupies (4 base, plus gear). What
        // changes is the tail: heavily armoured, the old slam collapsed toward
        // the floor of 1 and the new one does not.
        //
        // LITERAL EXPECTED VALUES, not a recomputation of the formula -- see
        // CLAUDE.md. These are what the numbers ARE, so a retune has to come
        // here and say so.
        [Test]
        public void TheGolemsSlamLandsWhereItUsedTo()
        {
            var slam = new ResolvedSkill("boulder_slam", "Boulder Slam", "", "golem", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, 0, 0, false, 0, 2, false,
                null, SpellPresentation.None, 0);

            var golem = new CombatantState("Golem", false, 350, 0, 6, 8, 3);

            Assert.AreEqual(20, Landed(slam, golem, defence: 4),
                "against Shawn's base 4 defence the slam should land where the old 1.8x did");
            Assert.AreEqual(10, Landed(slam, golem, defence: 6),
                "and hold up through the gear band");
        }

        private static int Landed(ResolvedSkill skill, CombatantState attacker, int defence)
        {
            var victim = new CombatantState("Shawn", true, 5000, 30, 7, defence, 10);

            int raw = SkillResolution.Amount(skill.Effect, attacker, victim,
                skill.Power, skill.FlatAmount, 0, skill.IgnoresDefense);

            return DamagePipeline.AfterDefences(raw, attacker, victim,
                attackType: null, affinity: ElementalAffinity.Neutral,
                varianceRange: 0f, rng: null, resolveWard: null).Damage;
        }


        // A tiny stand-in so these tests do not depend on StatBlock's own
        // constructor shape, which is not what any of them are about.
        private struct StatBlockOf
        {
            public static implicit operator Stats.StatBlock(StatBlockOf _) => new Stats.StatBlock();
        }
    }
}
