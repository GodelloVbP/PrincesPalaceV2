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
    // PALACE PASSAGE (plan 2.9, 1.12): two allies trade field places, as the
    // caster's one free action.
    //
    // THE SPELL IS MOSTLY A SELECTION, which is why most of this file is
    // about FightMenuState rather than about FightSession. The swap itself
    // reuses Move's whole mechanism -- SwapPartySlots, the Rooted rule read
    // off both sides, the NoteDeliberateMove pair -- and MoveCommandTests
    // already pins that. What is new is that a cast can now ask for TWO
    // presses, and that everything up to the second one is free to abandon.
    //
    // ATOMIC CANCELLATION IS PROVED BY CONSTRUCTION AND PINNED ANYWAY. The
    // menu holds the picks and the session is handed the finished list, so
    // there is no half-cast state anywhere to unwind -- but "there is nothing
    // to clean up" is exactly the kind of claim that stops being true the
    // first time somebody adds a field, so the board is compared before and
    // after a cancelled pick.
    public class PalacePassageTests
    {
        private const int PassageMana = 7;

        private static ResolvedSkill Passage() =>
            new ResolvedSkill("palace_passage", "Palace Passage", "", "sheep", 1,
                SkillEffect.SwapAllies, SkillTargeting.SingleAlly, PassageMana, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0, cooldownTurns: 3, freeAction: true);

        private static CombatantState Member(string name, int speed, int health = 300) =>
            new CombatantState(name, true, health, 40, 20, speed);

        // The caster is fastest so it holds the turn; the foe is slow enough
        // that it never replies between the two halves of a test.
        private static (FightSession session, CombatantState caster, CombatantState mid, CombatantState rear)
            Fight(ResolvedSkill skill = null)
        {
            var caster = Member("Caster", 30);
            var mid = Member("Mid", 12);
            var rear = Member("Rear", 6);
            var foe = new CombatantState("Foe", false, 100000, 10, 1, 1);

            var kits = new List<PlayerKit>
            {
                new PlayerKit("sheep", CharacterRole.Support, new[] { skill ?? Passage() }, null,
                    DamageType.Physical),
            };

            var session = new FightSession(new CombatEncounter(new[] { caster, mid, rear }, new[] { foe }),
                kits, null, new SeededRandom(4))
            {
                DamageVarianceRange = 0f,
            };

            Assert.AreSame(caster, session.Current, "fixture: the caster has to be the one acting");
            return (session, caster, mid, rear);
        }

        private static void Root(CombatantState combatant) =>
            combatant.Statuses.Add(new ActiveStatus(StatusEffectType.Rooted, 0, 2));

        private static IEnumerable<string> Messages(FightSession session) =>
            session.DrainBeats().SelectMany(b => b.Messages)
                .Concat(session.DrainImmediateMessages());

        // ---- the selection state table (plan 1.12) ------------------------------

        [Test]
        public void TheSwapPickerFollowsItsStateTable()
        {
            // ONE ASSERTION PER CELL of 1.12's table, in the table's own row
            // order, because the cells are a sequence: the state a row starts
            // in is the state the row above leaves behind, and testing them
            // apart would test four states that never occur together.
            var (_, caster, mid, rear) = Fight();
            var menu = new FightMenuState();

            // Row 1: Sub, the Passage row selected -> Target, side Allies,
            // picks 0/2.
            menu.OpenBranch(MenuBranch.Skill);
            menu.Select(0, PassageMana);
            menu.EnterTargeting(TargetSide.Allies, SkillEffects.PicksRequired(SkillEffect.SwapAllies));

            Assert.IsTrue(menu.IsPickingAlly);
            Assert.AreEqual(2, menu.RequiredPicks);
            Assert.AreEqual(0, menu.PickCount);
            Assert.IsTrue(menu.NeedsAnotherPick);

            // Row 2, submit on a legal ally: record pick 1, STAY in Target.
            Assert.IsTrue(menu.RecordPick(caster, out bool complete));
            Assert.IsFalse(complete, "one pick of two must not commit");
            Assert.IsTrue(menu.IsPickingAlly, "the first pick left Target depth");
            Assert.AreEqual(1, menu.PickCount);

            // Row 2, submit on an ILLEGAL ally -- "already picked" is the one
            // illegal case the menu owns; dead and wrong-side are the
            // session's. Refused without recording and without moving.
            Assert.IsFalse(menu.RecordPick(caster, out _), "the same ally was accepted twice");
            Assert.AreEqual(1, menu.PickCount);
            Assert.IsTrue(menu.HasPicked(caster));

            // Row 3, cancel at picks 1/2: drop pick 1, STAY in Target.
            Assert.IsTrue(menu.Back(), "cancel at a held pick must be consumed, not passed up");
            Assert.IsTrue(menu.IsPickingAlly, "cancel at picks 1/2 left Target depth entirely");
            Assert.AreEqual(0, menu.PickCount);
            Assert.IsFalse(menu.HasPicked(caster));

            // Row 2's cancel arm, now that nothing is held: back to Sub, with
            // the picks and the count forgotten.
            Assert.IsTrue(menu.Back());
            Assert.AreEqual(MenuDepth.Sub, menu.Depth);
            Assert.AreEqual(1, menu.RequiredPicks, "a count left behind would stall the next command");

            // Row 3, submit on the second legal ally: COMMIT.
            menu.EnterTargeting(TargetSide.Allies, 2);
            Assert.IsTrue(menu.RecordPick(caster, out _));
            Assert.IsTrue(menu.RecordPick(mid, out bool committed));
            Assert.IsTrue(committed, "the second pick did not complete the cast");
            CollectionAssert.AreEqual(new[] { caster, mid }, menu.Picks.ToList());

            // Committed: nothing more is accepted.
            Assert.IsFalse(menu.RecordPick(rear, out _), "a full cast took a third pick");
        }

        [Test]
        public void EverySinglePickCommandStillCommitsOnItsFirstPress()
        {
            // THE OTHER HALF of the count-of-one claim, and the one that
            // matters more: a bug here would break every ward, gift and heal
            // in the game, not one new spell.
            var (_, caster, _, _) = Fight();
            var menu = new FightMenuState();
            menu.OpenBranch(MenuBranch.Skill);
            menu.Select(0, 0);
            menu.EnterTargeting(TargetSide.Allies);

            Assert.AreEqual(1, menu.RequiredPicks);
            Assert.IsTrue(menu.RecordPick(caster, out bool complete));
            Assert.IsTrue(complete, "a one-pick command did not commit on its only press");
        }

        [Test]
        public void CancellingAfterTheFirstPick_LeavesTheBoardUnchanged()
        {
            // "No mana, no cooldown, no free-action lock, no swap" -- 1.12's
            // own list, asserted item by item.
            var (session, caster, mid, _) = Fight();
            var partyBefore = session.Encounter.PlayerParty.ToList();
            int manaBefore = caster.PrimaryPool.Current;

            var menu = new FightMenuState();
            menu.OpenBranch(MenuBranch.Skill);
            menu.Select(0, PassageMana);
            menu.EnterTargeting(TargetSide.Allies, 2);
            menu.RecordPick(mid, out _);

            Assert.IsTrue(menu.Back(), "the cancel was not consumed");

            Assert.AreEqual(manaBefore, caster.PrimaryPool.Current, "a cancelled pick spent mana");
            Assert.AreEqual(0, session.CooldownRemaining(caster, "palace_passage"),
                "a cancelled pick started a cooldown");
            CollectionAssert.AreEqual(partyBefore, session.Encounter.PlayerParty.ToList(),
                "a cancelled pick moved somebody");
            Assert.AreSame(caster, session.Current, "a cancelled pick spent the turn");

            // AND THE FREE ACTION IS STILL THERE. The lock is what a second
            // Passage this turn would be refused by, so a cancel that took it
            // would cost the player their whole free action for nothing --
            // proved by casting for real afterwards and having it accepted.
            Assert.IsTrue(session.CastSkillOnPicks(0, new[] { caster, mid }),
                "the free action was consumed by a cancelled pick");
        }

        // ---- the refusals ---------------------------------------------------------

        [Test]
        public void ARootedPartner_RefusesTheSwap_TheSameWayMoveDoes()
        {
            // Rooted means "cannot change field position" and a swap changes
            // two, so EITHER end refuses -- the identical rule CanMove/Move
            // already read off both sides, and the identical sentence.
            var (session, caster, mid, _) = Fight();
            Root(mid);
            int manaBefore = caster.PrimaryPool.Current;

            Assert.IsFalse(session.CastSkillOnPicks(0, new[] { caster, mid }));

            Assert.AreEqual(manaBefore, caster.PrimaryPool.Current, "a refused swap spent mana");
            Assert.AreSame(caster, session.Encounter.PlayerParty[0], "a refused swap moved somebody");
            Assert.IsTrue(Messages(session).Any(m => m.Contains("Mid is rooted")),
                "the refusal has to name which of the two is rooted");
        }

        [Test]
        public void ARootedCaster_RefusesTheSwapToo()
        {
            var (session, caster, mid, _) = Fight();
            Root(caster);

            Assert.IsFalse(session.CastSkillOnPicks(0, new[] { caster, mid }));
            Assert.AreSame(caster, session.Encounter.PlayerParty[0]);
        }

        [Test]
        public void APartnerRootedBetweenThePickAndTheCommit_StillRefusesTheCast()
        {
            // A PICK AND A COMMIT ARE DIFFERENT MOMENTS (1.12). Between them
            // an enemy can act -- the fight does not stop for the picker on a
            // board with a summon or a rider resolving -- so the state that
            // was legal when the plate was pressed may not be legal when the
            // cast is made. Checked at BOTH, and this is the second.
            var (session, caster, mid, _) = Fight();

            var menu = new FightMenuState();
            menu.OpenBranch(MenuBranch.Skill);
            menu.Select(0, PassageMana);
            menu.EnterTargeting(TargetSide.Allies, 2);
            menu.RecordPick(caster, out _);

            // ...and now the partner is rooted, after the first plate was
            // pressed and before the second.
            Root(mid);
            menu.RecordPick(mid, out bool complete);
            Assert.IsTrue(complete, "fixture: the picker still finished");

            int manaBefore = caster.PrimaryPool.Current;
            Assert.IsFalse(session.CastSkillOnPicks(0, menu.Picks),
                "a partner rooted after the pick still has to refuse the cast");
            Assert.AreEqual(manaBefore, caster.PrimaryPool.Current);
            Assert.AreSame(caster, session.Encounter.PlayerParty[0]);
        }

        [Test]
        public void TheSameAllyTwiceIsRefused()
        {
            var (session, caster, _, _) = Fight();

            Assert.IsFalse(session.CastSkillOnPicks(0, new[] { caster, caster }),
                "an ally cannot trade places with themselves");
            Assert.AreSame(caster, session.Current, "the refusal spent the turn");
        }

        [Test]
        public void ASoloPartyIsRefusedTheCast()
        {
            // 1.12's empty case: a party of one has no legal second pick.
            var lone = Member("Lone", 30);
            var kits = new List<PlayerKit>
            {
                new PlayerKit("sheep", CharacterRole.Support, new[] { Passage() }, null, DamageType.Physical),
            };
            var session = new FightSession(
                new CombatEncounter(new[] { lone }, new[] { new CombatantState("Foe", false, 1000, 10, 1, 1) }),
                kits, null, new SeededRandom(4));

            Assert.IsFalse(session.CastSkillOnPicks(0, new[] { lone }));
            Assert.AreSame(lone, session.Current);
        }

        // ---- what the swap actually does ------------------------------------------

        [Test]
        public void ASwapMovesSlotsNotStatuses()
        {
            // HEALTH AND STATUSES BELONG TO THE ACTOR. Trivially true while
            // the party list holds references -- and that is exactly why it
            // is worth a pin: the obvious wrong implementation of "swap two
            // allies" copies fields between two slots, and it would pass
            // every ordering test in this file.
            var (session, caster, mid, _) = Fight();
            mid.CurrentHealth = 91;
            mid.Statuses.Add(new ActiveStatus(StatusEffectType.Protect, 50, 2));
            caster.CurrentHealth = 222;

            Assert.IsTrue(session.CastSkillOnPicks(0, new[] { caster, mid }), "the cast was refused");

            Assert.AreSame(mid, session.Encounter.PlayerParty[0], "the two did not trade slots");
            Assert.AreSame(caster, session.Encounter.PlayerParty[1]);
            Assert.AreEqual(91, mid.CurrentHealth, "health followed the slot instead of the actor");
            Assert.AreEqual(222, caster.CurrentHealth);
            Assert.IsTrue(mid.Statuses.Any(s => s.Type == StatusEffectType.Protect),
                "a status followed the slot instead of the actor");
            Assert.IsFalse(caster.Statuses.Any(s => s.Type == StatusEffectType.Protect));
        }

        [Test]
        public void ASwapRecordsTheSkillsPresentationOnTheBeat()
        {
            // THE AUTHORED LAYERS, OR NOTHING -- palace_passage's real
            // skills.json entry has a two-layer "target-centre" VFX
            // (ritual sprite + settle emitter, both keyed to
            // "Spells/palace_passage"/"..._particles"), but ResolveSwapAllies
            // used to open the beat and pose the caster without ever calling
            // RecordSpellPresentation, so that authored VFX never reached the
            // beat and the cast played with nothing on screen. Every other
            // resolver (HealSingle, BuffParty, HealParty, ...) calls
            // RecordSpellPresentation right after BeginBeat; this pins the
            // swap resolver to the same contract with a literal path, the way
            // EnemyAiTests.cs pins a beat's Vfx.path/sfxPath elsewhere.
            var skill = new ResolvedSkill("palace_passage", "Palace Passage", "", "sheep", 1,
                SkillEffect.SwapAllies, SkillTargeting.SingleAlly, PassageMana, 0, false, 0, 0, false,
                null, new SpellPresentation
                {
                    layerFormat = SpellLayerRules.CurrentLayerFormat,
                    layers = new[]
                    {
                        new SpellLayer
                        {
                            id = "ritual", render = "sprite", place = "target-centre",
                            at = "release", path = "Spells/palace_passage", seconds = 0.66f,
                        },
                    },
                }, 0, cooldownTurns: 3, freeAction: true);

            var (session, caster, mid, rear) = Fight(skill);

            // Picks deliberately EXCLUDE the caster, so the beat's target and
            // the acting caster are two different bodies and the assertion
            // below cannot pass by accident.
            Assert.IsTrue(session.CastSkillOnPicks(0, new[] { mid, rear }), "the cast was refused");

            var beat = session.DrainBeats().FirstOrDefault(b => b.Vfx != null && b.Vfx.HasLayers);
            Assert.IsNotNull(beat, "the swap's beat carries no spell presentation at all");
            Assert.AreEqual("Spells/palace_passage", beat.Vfx.layers[0].path,
                "the beat's presentation is not the skill's authored VFX");

            // THE BEAT'S TARGET IS targets[0] -- set by BeginBeat(actor,
            // targets[0]) before the swap moves anyone -- so "target-centre"
            // in both authored layers resolves against Mid (the first pick),
            // not the Caster and not Rear.
            Assert.AreSame(mid, beat.Target, "target-centre VFX would resolve to the wrong body");
        }

        [Test]
        public void ThePassageDoesNotEndTheTurn_ButOnlyOncePerTurn()
        {
            // THE FREE ACTION, and exactly what it locks. The first Passage
            // leaves the turn where it found it; the second is refused BEFORE
            // payment by the once-per-turn lock, which is what stops a free
            // action from being an unbounded turn.
            var (session, caster, mid, rear) = Fight();
            int manaBefore = caster.PrimaryPool.Current;

            Assert.IsTrue(session.CastSkillOnPicks(0, new[] { caster, mid }), "the cast was refused");
            Assert.AreSame(caster, session.Current, "a free action ended the turn");

            // AND IT WAS PAID FOR ONCE. The free action is about the TURN,
            // not about the cost -- a Passage still spends its mana and
            // starts its cooldown, which is what stops "it does not end the
            // turn" from reading as "it is free".
            int manaAfterFirst = caster.PrimaryPool.Current;
            Assert.AreEqual(PassageMana, manaBefore - manaAfterFirst, "the Passage did not charge its mana");
            Assert.Greater(session.CooldownRemaining(caster, "palace_passage"), 0,
                "the Passage started no cooldown");

            Assert.IsFalse(session.CastSkillOnPicks(0, new[] { mid, rear }),
                "a second free action in one turn must be refused");
            Assert.AreEqual(manaAfterFirst, caster.PrimaryPool.Current,
                "the refused second Passage spent mana");
        }

        [Test]
        public void AnActorTargetedIntent_FollowsTheSwappedActor()
        {
            // THE INTENT FOLLOWS THE ACTOR, NOT THE SEAT (1.12, 2.9). Proved
            // by comparison rather than by reading a private field: the same
            // seeded board is played twice, once with a Passage in the middle
            // and once without, and the enemy's telegraphed blow has to land
            // on the same NAME both times. If the intent followed the slot,
            // the swap would change who it hits, which is the whole bug this
            // pins against.
            //
            // The re-validation itself draws nothing (FightSession.Enemies'
            // own header), so the two runs stay on the same stream and the
            // comparison is honest.
            string victimWithoutSwap = VictimOfTheEnemysTurn(swap: false);
            string victimWithSwap = VictimOfTheEnemysTurn(swap: true);

            Assert.IsNotNull(victimWithoutSwap, "fixture: the enemy never landed a blow to compare");
            Assert.AreEqual(victimWithoutSwap, victimWithSwap,
                "the telegraphed blow followed the field slot instead of the actor it was aimed at");
        }

        // The monster's one ability: a single-enemy blow that can be aimed
        // anywhere in the line. Named for what it does to this test rather
        // than after any shipped content, because no shipped enemy ability
        // has to have this shape for the contract to hold.
        private static ResolvedSkill ReachingBlow() =>
            new ResolvedSkill("reaching_blow", "Reaching Blow", "", "golem", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, 0, 0, false, 0, 30, false,
                null, SpellPresentation.None, 0, reach: Reach.Any);

        // Plays one board to the end of the enemy's turn and reports who lost
        // health, optionally trading two allies' places first.
        private static string VictimOfTheEnemysTurn(bool swap)
        {
            var caster = Member("Caster", 30);
            var mid = Member("Mid", 12);
            var rear = Member("Rear", 6);
            // FAST ENOUGH TO ACT IMMEDIATELY after the caster's swing, and
            // still slower than the caster so the caster opens. Anything
            // slower than Mid would let an ally act first and the telegraphed
            // blow would land a turn later, against a board two more actions
            // have changed.
            var monster = new CombatantState("Golem", false, 100000, 10, 30, 25);

            var encounter = new CombatEncounter(new[] { caster, mid, rear }, new[] { monster });
            var kits = new List<PlayerKit>
            {
                new PlayerKit("sheep", CharacterRole.Support, new[] { Passage() }, null, DamageType.Physical),
            };
            var enemyKits = new List<EnemyKit>
            {
                // AN AUTHORED ABILITY WITH Reach.Any, and both halves are
                // load-bearing.
                //
                // A PLAIN SWING WOULD PROVE NOTHING: it is aimed at the front
                // RANK, which IS the seat, so it cannot tell "follows the
                // actor" from "follows the slot". Neither would the legacy
                // skillName/skillChance pair -- SingleOpponentReachOf answers
                // Reach.Melee for any entry with no real ResolvedSkill behind
                // it (`!value.HasSkill`), so a legacy skill is front-rank too.
                //
                // Reach.Any IS THE WHOLE FIXTURE. With a mask that still
                // covers the promised actor after the swap, the resolution-
                // time re-validation has no reason to fire and the promise is
                // honoured -- which is the claim. A narrower mask would
                // legitimately re-pick, and that is the OTHER contract
                // (FightSession.Enemies' re-validation block), not this one.
                new EnemyKit(new ResolvedEnemy("golem", "Golem", new StatBlock(), 5, 3, false,
                        DamageType.Physical, DamageType.Physical, 0), false,
                    new[] { EnemyAbility.Of(ReachingBlow(), 1f) }),
            };

            var session = new FightSession(encounter, kits, enemyKits, new SeededRandom(6))
            {
                DamageVarianceRange = 0f,
            };
            session.PrepareEnemyIntents();

            if (swap)
            {
                Assert.IsTrue(session.CastSkillOnPicks(0, new[] { caster, mid }), "fixture: the swap was refused");
            }

            var before = encounter.PlayerParty.ToDictionary(p => p.Name, p => p.CurrentHealth);

            // ENDING THE TURN is what lets the monster act. A Passage is a
            // free action, so something else has to spend the turn -- an
            // ordinary swing, which both runs make identically.
            // ExecuteAttack ends the turn, and AdvanceAfterAction runs the
            // monsters itself (AutoResolveEnemyTurns) -- there is no separate
            // "now let the enemy go" call to make.
            session.ExecuteAttack(monster);
            session.DrainBeats();
            return encounter.PlayerParty
                .FirstOrDefault(p => p.CurrentHealth < before[p.Name])?.Name;
        }
    }
}
