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
    // PALACE PASSAGE ONTO ANY SEAT (PLAN_BELLWETHER_KIT 1.2/3.6, M3): the
    // second pick is a seat, occupied or empty. PalacePassageTests pins the
    // two-ally form, which is the occupied-seat case and must not move; this
    // file pins what the seat adds -- the empty step, its refusals, the menu's
    // seat pick and the bot's seat destination -- with literal seats, mana and
    // cooldowns.
    public class PalacePassageSeatTests
    {
        private const int PassageMana = 7;
        private const int PassageCooldown = 3;

        private static ResolvedSkill Passage() =>
            new ResolvedSkill("palace_passage", "Palace Passage", "", "sheep", 1,
                SkillEffect.SwapAllies, SkillTargeting.SingleAlly, PassageMana, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0, cooldownTurns: PassageCooldown, freeAction: true);

        private static CombatantState Member(string name, int speed) =>
            new CombatantState(name, true, 300, 40, 20, speed);

        // The first member is the caster and the fastest, so he holds the
        // turn; the foe never replies inside a test.
        private static FightSession Fight(params CombatantState[] party)
        {
            var kits = new List<PlayerKit>
            {
                new PlayerKit("sheep", CharacterRole.Support, new[] { Passage() }, null, DamageType.Physical),
            };
            var session = new FightSession(
                new CombatEncounter(party, new[] { new CombatantState("Foe", false, 100000, 10, 1, 1) }),
                kits, null, new SeededRandom(4))
            {
                DamageVarianceRange = 0f,
            };

            Assert.AreSame(party[0], session.Current, "fixture: the caster has to be the one acting");
            return session;
        }

        private static void Root(CombatantState combatant) =>
            combatant.Statuses.Add(new ActiveStatus(StatusEffectType.Rooted, 0, 2));

        private static List<string> Messages(FightSession session) =>
            session.DrainBeats().SelectMany(b => b.Messages)
                .Concat(session.DrainImmediateMessages()).ToList();

        // ---- the empty seat ---------------------------------------------------------

        [Test]
        public void ASoloShawnPassesFrontToMiddle()
        {
            var shawn = Member("Shawn", 30);
            var session = Fight(shawn);
            int manaBefore = shawn.PrimaryPool.Current;

            Assert.IsTrue(session.CastSkillToSeat(0, shawn, 1));

            Assert.AreEqual(1, session.Encounter.SeatOf(shawn));
            Assert.IsNull(session.Encounter.OccupantOf(0), "the front he left is empty");
            Assert.AreEqual(PassageMana, manaBefore - shawn.PrimaryPool.Current);
            Assert.AreEqual(PassageCooldown, session.CooldownRemaining(shawn, "palace_passage"));
            Assert.AreSame(shawn, session.Current, "a free action ended the turn");
            CollectionAssert.Contains(Messages(session), "Shawn steps through to the empty middle.");
        }

        [Test]
        public void ASoloShawnPassesFrontToRear_ThenStillActs()
        {
            var shawn = Member("Shawn", 30);
            var session = Fight(shawn);

            Assert.IsTrue(session.CastSkillToSeat(0, shawn, 2));
            Assert.AreEqual(2, session.Encounter.SeatOf(shawn));
            Assert.AreSame(shawn, session.Current);

            // THE REST OF HIS TURN IS HIS: a Move forward from the rear is a
            // turn-ending action that is accepted, one seat, to the middle.
            Assert.IsTrue(session.Move(MoveDirection.Forward), "the turn was not left to act with");
            Assert.AreEqual(1, session.Encounter.SeatOf(shawn));
        }

        [Test]
        public void ADuoMemberPassesToTheEmptyRear_AndTheOtherStaysInHisSeat()
        {
            var shawn = Member("Shawn", 30);
            var odette = Member("Odette", 12);
            var session = Fight(shawn, odette);

            Assert.IsTrue(session.CastSkillToSeat(0, shawn, 2));

            Assert.AreEqual(2, session.Encounter.SeatOf(shawn));
            Assert.AreEqual(1, session.Encounter.SeatOf(odette), "an empty-seat step traded with somebody");
            Assert.IsNull(session.Encounter.OccupantOf(0));
            CollectionAssert.Contains(Messages(session), "Shawn steps through to the empty rear.");
        }

        [Test]
        public void TheCasterCanSendAnotherAllyToAnEmptySeat()
        {
            var shawn = Member("Shawn", 30);
            var odette = Member("Odette", 12);
            var session = Fight(shawn, odette);

            Assert.IsTrue(session.CastSkillToSeat(0, odette, 2));

            Assert.AreEqual(0, session.Encounter.SeatOf(shawn));
            Assert.AreEqual(2, session.Encounter.SeatOf(odette));
        }

        // ---- the occupied seat is the two-ally swap, exactly --------------------------

        [Test]
        public void AnOccupiedSeat_IsTheTwoAllySwap_BoardAndMessagesAlike()
        {
            // Same board twice: once through the two-ally door, once through
            // the seat door naming the second ally's seat. Everything the
            // player can see has to come out identical.
            var byPicks = Fight(Member("Caster", 30), Member("Mid", 12), Member("Rear", 6));
            var bySeat = Fight(Member("Caster", 30), Member("Mid", 12), Member("Rear", 6));

            Assert.IsTrue(byPicks.CastSkillOnPicks(0, new[] { byPicks.Encounter.PlayerParty[0], byPicks.Encounter.PlayerParty[1] }));
            Assert.IsTrue(bySeat.CastSkillToSeat(0, bySeat.Encounter.PlayerParty[0], 1));

            CollectionAssert.AreEqual(new[] { "Mid", "Caster", "Rear" },
                byPicks.Encounter.PlayerParty.Select(p => p.Name).ToList(), "fixture: the two-ally swap");
            CollectionAssert.AreEqual(new[] { "Mid", "Caster", "Rear" },
                bySeat.Encounter.PlayerParty.Select(p => p.Name).ToList());
            CollectionAssert.AreEqual(Messages(byPicks), Messages(bySeat));
            Assert.AreEqual(byPicks.Encounter.PlayerParty[1].PrimaryPool.Current,
                bySeat.Encounter.PlayerParty[1].PrimaryPool.Current, "the two doors charged differently");
        }

        // ---- refusals, all before payment ----------------------------------------------

        [Test]
        public void ARootedTraveller_IsRefusedAnEmptySeat_BeforePayment()
        {
            var shawn = Member("Shawn", 30);
            var session = Fight(shawn);
            Root(shawn);
            int manaBefore = shawn.PrimaryPool.Current;

            Assert.IsFalse(session.CastSkillToSeat(0, shawn, 2));

            Assert.AreEqual(0, session.Encounter.SeatOf(shawn));
            Assert.AreEqual(manaBefore, shawn.PrimaryPool.Current, "a refused Passage spent mana");
            Assert.AreEqual(0, session.CooldownRemaining(shawn, "palace_passage"), "a refused Passage started its cooldown");
            CollectionAssert.Contains(Messages(session), "Shawn is rooted.");
        }

        [Test]
        public void ARootedOccupant_RefusesTheSeat_BeforePayment()
        {
            var caster = Member("Caster", 30);
            var mid = Member("Mid", 12);
            var session = Fight(caster, mid, Member("Rear", 6));
            Root(mid);
            int manaBefore = caster.PrimaryPool.Current;

            Assert.IsFalse(session.CastSkillToSeat(0, caster, 1));

            Assert.AreEqual(0, session.Encounter.SeatOf(caster));
            Assert.AreEqual(1, session.Encounter.SeatOf(mid));
            Assert.AreEqual(manaBefore, caster.PrimaryPool.Current);
            CollectionAssert.Contains(Messages(session), "Mid is rooted.");
        }

        [Test]
        public void TheTravellersOwnSeat_AndASeatThatDoesNotExist_AreRefused()
        {
            var shawn = Member("Shawn", 30);
            var session = Fight(shawn);
            int manaBefore = shawn.PrimaryPool.Current;

            Assert.IsFalse(session.CastSkillToSeat(0, shawn, 0), "a Passage to where he already stands");
            Assert.IsFalse(session.CastSkillToSeat(0, shawn, 3), "a fourth seat");
            Assert.AreEqual(manaBefore, shawn.PrimaryPool.Current);
            Assert.AreEqual(0, session.Encounter.SeatOf(shawn));
        }

        // ---- the menu's seat pick -------------------------------------------------------

        [Test]
        public void TheMenuTakesATravellerThenASeat_AndCancelStepsBackOnePickAtATime()
        {
            var shawn = Member("Shawn", 30);
            var menu = new FightMenuState();
            menu.OpenBranch(MenuBranch.Skill);
            menu.Select(0, PassageMana);
            menu.EnterTargeting(TargetSide.Allies, SkillEffects.PicksRequired(SkillEffect.SwapAllies),
                SkillEffects.LastPickIsSeat(SkillEffect.SwapAllies));

            Assert.IsFalse(menu.IsPickingSeat, "the first press is the traveller, not a seat");
            Assert.IsFalse(menu.RecordSeatPick(2, out _), "a seat was taken before the traveller");

            Assert.IsTrue(menu.RecordPick(shawn, out bool afterTraveller));
            Assert.IsFalse(afterTraveller);
            Assert.IsTrue(menu.IsPickingSeat);

            Assert.IsTrue(menu.RecordSeatPick(2, out bool complete));
            Assert.IsTrue(complete, "an empty seat did not complete the cast");
            Assert.AreEqual(2, menu.DestinationSeat);

            Assert.IsTrue(menu.Back(), "cancel with a seat held");
            Assert.AreEqual(-1, menu.DestinationSeat);
            Assert.IsTrue(menu.IsPickingSeat, "dropping the seat left the seat pick");

            Assert.IsTrue(menu.Back());
            Assert.AreEqual(0, menu.PickCount);
            Assert.IsFalse(menu.IsPickingSeat);

            Assert.IsTrue(menu.Back());
            Assert.AreEqual(MenuDepth.Sub, menu.Depth);
            Assert.IsFalse(menu.LastPickIsSeat, "the seat flag outlived the pick");
        }

        // ---- the bot's action ------------------------------------------------------------

        [Test]
        public void ABotActionWithASeatDestination_RoundTrips()
        {
            var shawn = Member("Shawn", 30);
            var session = Fight(shawn);

            var offered = FightAction.SeatDestinationActions(session, shawn);
            CollectionAssert.AreEqual(new[] { 1, 2 }, offered.Select(a => a.DestinationSeat).ToList(),
                "a solo Shawn's legal destinations are the two empty seats");
            Assert.IsTrue(offered.All(a => a.Kind == FightActionKind.Skill && a.Target == shawn && a.SkillIndex == 0));

            var action = new FightAction(FightActionKind.Skill, shawn, 0, destinationSeat: 2);
            Assert.AreEqual(2, action.DestinationSeat);
            Assert.AreEqual("Skill[0](Shawn->seat 2)", action.ToString());

            FightAction.Apply(session, action);

            Assert.AreEqual(2, session.Encounter.SeatOf(shawn));
            Assert.AreSame(shawn, session.Current);
            Assert.AreEqual(0, FightAction.SeatDestinationActions(session, shawn).Count,
                "a cooling Passage is still offered");

            // A plain action carries no destination.
            Assert.AreEqual(-1, new FightAction(FightActionKind.Skill, shawn, 0).DestinationSeat);
        }

        [Test]
        public void LegalActionsDoesNotYetOfferThePassage()
        {
            // M6 decides WHEN a bot takes it (PLAN_BELLWETHER_KIT 3.10); until
            // then the legal menu every policy (RandomLegal included) picks
            // from is unchanged.
            var shawn = Member("Shawn", 30);
            var session = Fight(shawn);

            var legal = FightAction.LegalActions(session, shawn, System.Array.Empty<SatchelStack>());

            Assert.IsFalse(legal.Any(a => a.Kind == FightActionKind.Skill), "a Passage reached the legal menu");
        }
    }
}
