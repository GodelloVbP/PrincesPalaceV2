using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.PlayModeTests
{
    // The Reckoning, driven through the real fight scene.
    //
    // Fed a FIXTURE reward rather than a fought fight. What is under test is
    // the screen -- what it shows, what taking an item does, where dismissing
    // it goes -- and driving a whole encounter to reach it would make every
    // assertion here hostage to combat balance.
    public class ReckoningTests
    {
        private string _root;
        private ReckoningController _reckoning;
        private string _wentTo;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-reckoning-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();

            _wentTo = null;
            Navigation.LoadOverride = scene => _wentTo = scene;

            // ReckoningController's whole animation is unscaled -- see its
            // own SpeedMultiplier comment -- so this collapses the wipe,
            // sweep and bar-fill waits below to well under a frame.
            ReckoningController.SpeedMultiplier = 40f;
        }

        [TearDown]
        public void Restore()
        {
            SharedScene.AfterTest();
            ReckoningController.SpeedMultiplier = 1f;
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private GameObject Named(string name) =>
            _reckoning.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        // A FIXED WAIT STILL, not a poll -- there is no exposed "this bar is
        // still animating" flag to poll on (FillBar's coroutine reference is
        // private, and a level-up's Sweep-Flash-Sweep sequence PLATEAUS
        // between passes, which rules out watching the fill rect settle: it
        // legitimately stops moving mid-sequence during the flash).
        //
        // What changed is the duration. [SetUp]'s SpeedMultiplier = 40 runs
        // the whole Sweep/Flash/Sweep sequence (worst case here: two sweeps
        // and a flash, well under 0.55s + 0.09s of *scaled* clock) in low
        // tens of milliseconds of real time -- 0.2s real is roughly 6x that,
        // which still catches a real stall (the bar never reaching its final
        // span) while being ~7x faster than the flat 1.5s this replaced.
        private IEnumerator WaitForTheBarSequence()
        {
            float deadline = Time.realtimeSinceStartup + 0.2f;
            while (Time.realtimeSinceStartup < deadline) yield return null;
        }

        private static SaveData Save => SaveSlotManager.CurrentSave;

        private static CombatReward Reward(int gold = 40, params CharacterReward[] rows)
        {
            var reward = new CombatReward { GoldGained = gold };
            reward.Characters.AddRange(rows);
            return reward;
        }

        private static CharacterReward Row(string id, string name, int gained = 30,
            int levelBefore = 2, int levelAfter = 2, bool downed = false)
        {
            return new CharacterReward(id, name, levelBefore, 20, levelAfter, 50, 100, gained, 0, downed);
        }

        // THE FIGHT SCENE IS SHARED ACROSS THIS FIXTURE (SharedScene). Show()
        // resets everything the screen paints (reward, offers, taken, phase,
        // tab), so the reset is only what Show does not own: the screen is put
        // back down -- OnDisable stops its animation and removes its nav
        // context -- and the Dismissed handler the scene wired is put back
        // over the one DismissingItLeavesTheFight installs.
        //
        // AND THE GLOOM. PlayIn reads its fade-up target off the dimmer's
        // CURRENT alpha, so a Show that cuts a running PlayIn short (a second
        // Show, or the screen going down mid-fade) leaves a lower alpha that
        // every later PlayIn in the same scene then treats as the target. A
        // fresh scene carries the authored value; a reused one gets it put
        // back from what this fixture read off its fresh load.
        private ReckoningController _capturedFor;
        private System.Action _sceneDismissed;
        private float _sceneGloom;

        private IEnumerator OpenTheFight()
        {
            yield return SharedScene.Ensure("Fight");

            _reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_reckoning, "the Reckoning was never wired into the fight scene");

            if (_capturedFor != _reckoning)
            {
                _capturedFor = _reckoning;
                _sceneDismissed = _reckoning.Dismissed;
                _sceneGloom = Dimmer().color.a;
            }
            else
            {
                // A reuse. A fresh scene is left exactly as it loaded, so
                // ItStartsHiddenAndShowOpensIt reads the scene, not this.
                _reckoning.Dismissed = _sceneDismissed;
                _reckoning.gameObject.SetActive(false);
                var dimmer = Dimmer();
                var colour = dimmer.color;
                colour.a = _sceneGloom;
                dimmer.color = colour;
            }
        }

        private Image Dimmer()
        {
            var dimmer = Named("ReckoningPanelDimmer")?.GetComponent<Image>();
            Assert.IsNotNull(dimmer, "the Reckoning has no ReckoningPanelDimmer");
            return dimmer;
        }

        private IEnumerator ShowIt(CombatReward reward, List<ItemOffer> offers = null)
        {
            yield return OpenTheFight();

            _reckoning.Show(reward, offers ?? new List<ItemOffer>());

            // Start() runs one frame AFTER SetActive, so the listeners do not
            // exist yet (CODE_STANDARDS section 5).
            yield return null;
            yield return null;
        }

        // ---- what it shows ---------------------------------------------------------

        [UnityTest]
        public IEnumerator ItStartsHiddenAndShowOpensIt()
        {
            SharedScene.MarkDirty("asserts the Reckoning is down in a freshly loaded fight, not after a reset hid it");
            yield return OpenTheFight();
            Assert.IsFalse(_reckoning.gameObject.activeSelf, "the fight opens with the Reckoning down");

            _reckoning.Show(Reward(), new List<ItemOffer>());
            yield return null;

            Assert.IsTrue(_reckoning.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator TheGoldItShowsIsTheGoldTheFightPaid()
        {
            yield return ShowIt(Reward(gold: 137, rows: Row("shawn", "Shawn")));

            StringAssert.Contains("137", Named("ReckoningGoldLabel").GetComponent<TMP_Text>().text);
        }

        [UnityTest]
        public IEnumerator OneRowPerFieldedCharacterAndTheRestStayHidden()
        {
            yield return ShowIt(Reward(rows: new[] { Row("shawn", "Shawn"), Row("other", "Other") }));

            Assert.IsTrue(Named("ReckoningRow0").activeSelf);
            Assert.IsTrue(Named("ReckoningRow1").activeSelf);

            for (int i = 2; i < ReckoningScreen.RowCount; i++)
            {
                Assert.IsFalse(Named($"ReckoningRow{i}").activeSelf,
                    $"row {i} is drawn empty rather than hidden");
            }
        }

        [UnityTest]
        public IEnumerator ADownedMemberGetsARowSayingSoRatherThanVanishing()
        {
            // Three went in and two came back with nothing explaining the
            // third is the confusion CharacterReward.IsDowned exists to stop.
            yield return ShowIt(Reward(rows: new[]
            {
                Row("shawn", "Shawn"),
                Row("ghost", "Ghost", gained: 0, downed: true),
            }));

            Assert.IsTrue(Named("ReckoningRow1").activeSelf, "the downed member still gets a row");
            StringAssert.Contains("DID NOT FIGHT", Named("ReckoningRow1Level").GetComponent<TMP_Text>().text);
        }

        [UnityTest]
        public IEnumerator ALevelUpIsCalledOutOnTheRowThatEarnedIt()
        {
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn", levelBefore: 2, levelAfter: 3)));

            StringAssert.Contains("UP", Named("ReckoningRow0Level").GetComponent<TMP_Text>().text);
        }

        [UnityTest]
        public IEnumerator TheRowCountCoversTheLargestPartyTheSaveCanField()
        {
            // The tree is fixed at build time. A squad bigger than RowCount
            // would silently lose its last members from the results.
            yield return OpenTheFight();

            Assert.GreaterOrEqual(ReckoningScreen.RowCount, Save.EffectiveMaxSquadSize());
        }

        // ---- the one decision --------------------------------------------------------

        private static List<ItemOffer> ThreeOffers()
        {
            var candidates = ItemOfferRoll.Candidates();
            Assert.IsNotEmpty(candidates, "fixture: content has equippable items");

            return candidates.Take(3).Select(c => c.WithPlus(2)).ToList();
        }

        // Whether `itemId` at `plus` ended up ANYWHERE this save can hold it --
        // the bag, or auto-equipped into a squad member's slot. Auto-equip
        // (RunOrchestrator.AutoEquipIntoAnEmptySlot) moves a picked item
        // straight into an empty slot instead of leaving it in the bag, so
        // "the pick reached the save" can no longer be read off bag count
        // alone -- only one of these two places will have gained it.
        private static bool ItemLandedSomewhere(string itemId, int plus)
        {
            if (Save.stockpiledItems.Any(e => e.itemId == itemId && e.plus == plus)) return true;

            foreach (var character in Save.ActiveSquad())
            {
                if (character?.equipment == null) continue;
                foreach (var slot in Domain.Equipment.EquipmentSlots.All)
                {
                    if (character.equipment.Get(slot) == itemId && character.equipment.GetPlus(slot) == plus)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        [UnityTest]
        public IEnumerator TakingAnOfferPutsItInTheSaveAtItsPlusAndSavesIt()
        {
            // NOT necessarily the bag any more -- every offer is an equippable
            // item (ThreeOffers' own fixture guard), so a fresh Shawn with an
            // empty slot for it auto-equips instead. Bag-or-equipped is the
            // real guarantee this test states now; ItemLandedSomewhere checks
            // both rather than assuming which one won.
            var offers = ThreeOffers();
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")), offers);

            Assert.IsFalse(ItemLandedSomewhere(offers[0].ItemId, 2),
                "fixture: should not exist anywhere in a fresh save before it is taken");

            Named("ReckoningOffer0").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.IsTrue(ItemLandedSomewhere(offers[0].ItemId, 2),
                "the plus the offer was rolled at did not survive into the save, wherever it landed");

            SaveSlotManager.Forget();
            Assert.IsTrue(ItemLandedSomewhere(offers[0].ItemId, 2),
                "the pick never reached the file");
        }

        // Three offers, each also carrying a real rolled RiftTier/Modifiers
        // set -- ThreeOffers() above stays plus-only so every OTHER test in
        // this file is unaffected by this axis existing.
        private static List<ItemOffer> ThreeOffersWithRolls()
        {
            var pool = Content.ContentDatabase.Modifiers.Select(m => m.id).ToList();
            Assert.IsNotEmpty(pool, "fixture: content has at least one modifier to roll");

            var candidates = ItemOfferRoll.Candidates();
            Assert.IsNotEmpty(candidates, "fixture: content has equippable items");

            return candidates.Take(3)
                .Select(c => c.WithPlus(2)
                    .WithModifiers(Domain.Content.RiftTier.Convergent, pool.Take(System.Math.Min(3, pool.Count)).ToList()))
                .ToList();
        }

        // Whether `itemId` at `plus` with EXACTLY this modifier roll ended up
        // anywhere the save can hold it -- the bag, or auto-equipped into a
        // squad member's slot. Mirrors ItemLandedSomewhere, extended to the
        // full stacking key rather than just (itemId, plus): a claim that
        // dropped the roll on the floor would still pass the plus-only check,
        // which is exactly the gap this test closes.
        private static bool RolledItemLandedSomewhere(string itemId, int plus, List<string> modifierIds, int riftTier)
        {
            bool SameRoll(List<string> a) => new HashSet<string>(a ?? new List<string>()).SetEquals(modifierIds);

            if (Save.stockpiledItems.Any(e => e.itemId == itemId && e.plus == plus
                    && e.riftTier == riftTier && SameRoll(e.modifierIds)))
            {
                return true;
            }

            foreach (var character in Save.ActiveSquad())
            {
                if (character?.equipment == null) continue;
                foreach (var slot in Domain.Equipment.EquipmentSlots.All)
                {
                    if (character.equipment.Get(slot) == itemId && character.equipment.GetPlus(slot) == plus
                        && character.equipment.GetRiftTier(slot) == riftTier
                        && SameRoll(character.equipment.GetModifierIds(slot)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        [UnityTest]
        public IEnumerator TakingAnOfferPutsItsRollInTheSaveToo()
        {
            var offers = ThreeOffersWithRolls();
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")), offers);

            var taken = offers[0];
            var modifierIds = taken.Modifiers.ToList();

            Assert.IsFalse(RolledItemLandedSomewhere(taken.ItemId, taken.Plus, modifierIds, (int)taken.RiftTier),
                "fixture: should not exist anywhere in a fresh save before it is taken");

            Named("ReckoningOffer0").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.IsTrue(RolledItemLandedSomewhere(taken.ItemId, taken.Plus, modifierIds, (int)taken.RiftTier),
                "the offer's rolled modifiers/riftTier did not survive into the save, wherever it landed");
        }

        // AutoEquipIntoAnEmptySlot moves the item out of the bag via
        // EquipMove.TryEquip, which keys its removal on the FULL
        // (itemId, plus, modifierIds, riftTier) stack now that modifiers
        // exist. Passing only `plus` there would look for the wrong stack
        // and silently leave a rolled item stuck in the bag on a fresh
        // character who should have auto-equipped it -- this pins that the
        // roll specifically does not break auto-equip.
        [UnityTest]
        public IEnumerator ARolledOfferStillAutoEquipsIntoAnEmptySlot()
        {
            var offers = ThreeOffersWithRolls();
            var taken = offers[0];
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")), offers);

            Named("ReckoningOffer0").GetComponent<Button>().onClick.Invoke();
            yield return null;

            // Same fixture guarantee ThreeOffers()/TakingAnOfferPutsItInThe-
            // SaveAtItsPlusAndSavesIt already relies on: a fresh Shawn has an
            // empty slot for every offer, so this always auto-equips rather
            // than sitting in the bag.
            bool equipped = Save.ActiveSquad().Any(character =>
                character?.equipment != null && Domain.Equipment.EquipmentSlots.All.Any(slot =>
                    character.equipment.Get(slot) == taken.ItemId
                    && character.equipment.GetPlus(slot) == taken.Plus
                    && character.equipment.GetRiftTier(slot) == (int)taken.RiftTier));

            Assert.IsTrue(equipped,
                "a rolled item did not auto-equip -- TryEquip likely could not find its stack in the bag " +
                "because its modifierIds/riftTier were not passed through to the removal key");
        }

        [UnityTest]
        public IEnumerator OnlyOneOfferCanBeTaken()
        {
            // It is a CHOICE. Two presses handing over two items would make the
            // decision decorative. Counts bag PLUS equipped for the same reason
            // TakingAnOfferPutsItInTheSaveAtItsPlusAndSavesIt does -- a taken
            // offer may auto-equip rather than sit in the bag, so "exactly one
            // item arrived" has to be read off both.
            var offers = ThreeOffers();
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")), offers);

            Named("ReckoningOffer0").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Named("ReckoningOffer1").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.IsTrue(ItemLandedSomewhere(offers[0].ItemId, 2), "the one offer taken must have arrived");
            Assert.IsFalse(ItemLandedSomewhere(offers[1].ItemId, 2), "the second press must not also hand over its item");
            Assert.IsTrue(_reckoning.HasTakenAnItem);
        }

        [UnityTest]
        public IEnumerator TheOffersNotTakenStayOnScreenButStopResponding()
        {
            // Hiding them would erase the decision the player just made from
            // the screen that asked for it.
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")), ThreeOffers());

            Named("ReckoningOffer0").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.IsTrue(Named("ReckoningOffer1").activeSelf, "the offer not taken vanished");
            Assert.IsFalse(Named("ReckoningOffer1").GetComponent<Button>().interactable);
        }

        [UnityTest]
        public IEnumerator AFightWithNoOffersSkipsStraightToTheSummary()
        {
            // Reachable: ItemOfferTable returns fewer than three when the
            // candidate pool is genuinely smaller, and zero if content has no
            // equippables at all.
            //
            // Now that the choice is its OWN phase and there is no skip on it,
            // an empty offer list would be a phase with no way out at all --
            // so it is bypassed rather than shown empty. Stronger than the old
            // assertion, which only checked that a heading was hidden.
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")), new List<ItemOffer>());

            Assert.IsFalse(Named("ReckoningOfferPhase").activeSelf,
                "an offer phase with nothing on it has no way out");
            Assert.IsTrue(Named("ReckoningSummaryPhase").activeSelf,
                "the screen has to land somewhere");
        }

        [UnityTest]
        public IEnumerator AnOfferNamingContentThatIsGoneDegradesRatherThanThrowing()
        {
            var offers = new List<ItemOffer> { new ItemOffer("no_such_item_at_all", 3) };

            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")), offers);

            Assert.IsTrue(Named("ReckoningOffer0").activeSelf);
            Assert.IsNotEmpty(Named("ReckoningOffer0Name").GetComponent<TMP_Text>().text);
        }

            [UnityTest]
        public IEnumerator TheTwoBarSegmentsSitBesideEachOtherRatherThanOnTopOfEachOther()
        {
            // Both anchored from zero would put the bright segment ON the dim
            // one -- it is declared last, so it paints over -- and the "before"
            // half would never be visible. That is the entire reason
            // CharacterReward carries a before as well as an after, and a
            // composite of the layout is what caught it; every test passed.
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")));

            var before = Named("ReckoningRow0BarBefore").GetComponent<RectTransform>();
            var fill = Named("ReckoningRow0BarFill").GetComponent<RectTransform>();

            // Let the fill animation finish so the assertion is on the settled
            // state rather than on a frame partway through it.
            yield return WaitForTheBarSequence();

            Assert.AreEqual(0f, before.anchorMin.x, 0.001f, "the dim segment starts at the left edge");
            Assert.AreEqual(before.anchorMax.x, fill.anchorMin.x, 0.001f,
                "the earned segment must start exactly where the already-had segment ends");
            Assert.Greater(fill.anchorMax.x, fill.anchorMin.x,
                "a fight that granted experience drew a zero-width earned segment");
        }

        [UnityTest]
        public IEnumerator ALevelUpGivesTheWholeBarToWhatWasJustEarned()
        {
            // BarFillBefore01 returns 0 after a level-up, and that is not a
            // fudge: the level reset the bar, so every point now showing
            // genuinely was earned in this fight.
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn", levelBefore: 2, levelAfter: 3)));

            var before = Named("ReckoningRow0BarBefore").GetComponent<RectTransform>();
            var fill = Named("ReckoningRow0BarFill").GetComponent<RectTransform>();

            yield return WaitForTheBarSequence();

            Assert.AreEqual(0f, before.anchorMax.x, 0.001f, "nothing was carried over the level boundary");
            Assert.AreEqual(0f, fill.anchorMin.x, 0.001f);
        }

        // ---- leaving ---------------------------------------------------------------------

        [UnityTest]
        public IEnumerator DismissingItLeavesTheFight()
        {
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")));

            bool dismissed = false;
            _reckoning.Dismissed = () => dismissed = true;

            Named("ReckoningContinueButton").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.IsTrue(dismissed, "the continue button raised nothing");
        }

        [UnityTest]
        public IEnumerator TheExpandSettlesTheFrameAtFullSize()
        {
            // The animation is presentation only -- every number is already
            // correct before it starts -- but a frame left mid-expand would be
            // a permanently undersized panel.
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")));

            var wipe = Named("ReckoningFrameWipe").GetComponent<RectTransform>();
            var frame = Named("ReckoningFrame").GetComponent<RectTransform>();

            // BOUNDED ON THE WIPE'S OWN WIDTH, not a fixed real-time guess.
            // PlayIn is a single monotonic ease with no plateau, so unlike the
            // bar-fill sequence above it is safe to poll directly: it either
            // reaches full width or the 2s ceiling fires and the assertions
            // below fail on the truth (a permanently narrow panel) rather
            // than on a timing guess.
            float deadline = Time.realtimeSinceStartup + 2f;
            while (wipe.rect.width < ReckoningScreen.PanelWidth - 0.5f && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.AreEqual(PrincesPalace.Domain.UiKit.Screens.ReckoningScreen.PanelWidth,
                wipe.rect.width, 0.5f, "the panel is permanently narrower than it should be");
            Assert.AreEqual(PrincesPalace.Domain.UiKit.Screens.ReckoningScreen.PanelHeight,
                wipe.rect.height, 0.5f);

            // The frame is revealed, never resized -- so full size here is not
            // a thing the animation had to arrive at, it is a thing nothing was
            // ever allowed to change.
            Assert.AreEqual(PrincesPalace.Domain.UiKit.Screens.ReckoningScreen.PanelWidth,
                frame.rect.width, 0.5f);
            Assert.AreEqual(Vector3.one, frame.localScale);
        }

    }
}
