using System;
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
    // Prismatic Orb: Odette's level-1 cast, and the element-choice mechanic it
    // is the first user of.
    //
    // THE HALF WORTH TESTING IS THE RETYPE. Everything else about the orb is a
    // single-target packet spell the pipeline already knew how to resolve; what
    // is new is that the packet's TYPE is decided between picking the skill and
    // picking the target, and that the wrong element (or none, or one on a
    // skill that offers none) is refused without spending anything.
    //
    // EVERY EXPECTED NUMBER IS A LITERAL with its arithmetic written out, never
    // recomputed from the thing under test -- CLAUDE.md gotcha 5. The fixture
    // makes that possible: zero defense, zero typed resistance, variance off,
    // so a 16 packet on a Fire-weak enemy cast as Fire is 16 x 1.5 = 24 and
    // nothing else moves it.
    public class PrismaticOrbTests
    {
        // The authored contract, restated once so the content pin and the
        // behaviour tests below cannot drift apart.
        private const int ManaCost = 8;
        private const int Packet = 16;

        // ---- the content entry ----------------------------------------------------

        [Test]
        public void TheContentEntryAuthorsAFourWayChoiceOverOneEarthPacket()
        {
            string entry = OrbEntry();

            StringAssert.Contains("\"characterId\": \"owl\"", entry);
            StringAssert.Contains("\"unlockLevel\": 1", entry);
            StringAssert.Contains("\"effect\": \"DamageSingle\"", entry);
            StringAssert.Contains($"\"manaCost\": {ManaCost}", entry);
            StringAssert.Contains("\"type\": \"Earth\"", entry);
            StringAssert.Contains($"\"amount\": {Packet}", entry);

            foreach (string element in new[] { "Earth", "Water", "Fire", "Wind" })
            {
                StringAssert.Contains($"\"type\": \"{element}\"", entry);
            }

            // ART ON THE WATER ELEMENT AND NOWHERE ELSE (M5). The absence used
            // to be the contract while the four sheets did not exist; one of
            // them does now, so what is asserted is that it landed on the
            // ELEMENT rather than on the skill -- a skill-level block would
            // draw water for a Fire cast. What the block CONTAINS is
            // SpellBaselineTimingTests' pin; this file only cares that the orb
            // itself still authors nothing.
            StringAssert.DoesNotContain("\"path\": \"Spells/prismatic_orb_water\"", entry,
                "the orb authors a spell folder of its own, which every element would then draw");
        }

        // The three placeholders are GONE, not merely superseded. A kit that
        // still carried Arcane Bolt would put the orb second in a list whose
        // whole point is that it is the one thing she does.
        [Test]
        public void OdetteCarriesTheOrbAndNothingElse()
        {
            string json = File.ReadAllText(SkillsJsonPath());

            StringAssert.Contains("prismatic_orb", json);
            StringAssert.DoesNotContain("placeholder_caster_bolt", json);
            StringAssert.DoesNotContain("placeholder_caster_firebolt", json);
            StringAssert.DoesNotContain("placeholder_caster_mend", json);
        }

        // ---- the resolver ---------------------------------------------------------

        [Test]
        public void TheEntryResolvesAsAChoiceOfFourOverOnePacket()
        {
            var orb = Orb();

            Assert.IsTrue(orb.HasElementChoice);
            Assert.AreEqual(4, orb.Elements.Length);
            CollectionAssert.AreEqual(
                new[] { DamageType.Earth, DamageType.Water, DamageType.Fire, DamageType.Wind },
                orb.Elements.Select(e => e.Type).ToList(),
                "the menu shows them in authored order, so the order is part of the content");

            Assert.AreEqual(SkillEffect.DamageSingle, orb.Effect);
            Assert.AreEqual(SkillTargeting.SingleEnemy, orb.Targeting);
            Assert.AreEqual(ManaCost, orb.ManaCost);
            Assert.AreEqual(0, orb.ResourceCost);
            Assert.AreEqual(1, orb.DamageInstances.Length);
            Assert.AreEqual(DamageType.Earth, orb.DamageInstances[0].type);
            Assert.AreEqual(Packet, orb.DamageInstances[0].amount);
        }

        // ---- AsElement ------------------------------------------------------------

        [Test]
        public void AsElementRetypesThePacketAndLeavesEverythingElseAlone()
        {
            var orb = Orb();
            var fire = orb.AsElement(DamageType.Fire);

            Assert.AreEqual(DamageType.Fire, fire.DamageInstances[0].type);
            Assert.AreEqual(Packet, fire.DamageInstances[0].amount,
                "a choice of element changes the TYPE of a packet, never its size");

            Assert.AreEqual(orb.Id, fire.Id, "a cast copy pays the same cost and shares the same cooldown key");
            Assert.AreEqual(orb.ManaCost, fire.ManaCost);
            Assert.AreEqual(orb.CooldownTurns, fire.CooldownTurns);
            Assert.AreEqual(orb.Reach, fire.Reach);
            Assert.AreEqual(orb.Targeting, fire.Targeting);
            Assert.AreEqual(orb.DisplayName, fire.DisplayName);

            // THE ORIGINAL IS UNTOUCHED. A cast copy sharing the catalogue's
            // own packet array would let one cast retype the skill for every
            // later one.
            Assert.AreEqual(DamageType.Earth, orb.DamageInstances[0].type);
        }

        // The copy is a spell with the question already answered, which is what
        // lets it go back through the ordinary cast path (no second element
        // gate) and what makes the detail card read "FIRE" rather than
        // re-listing all four.
        [Test]
        public void AsElementLeavesNoFurtherChoiceOnTheCopy()
        {
            var fire = Orb().AsElement(DamageType.Fire);

            Assert.IsFalse(fire.HasElementChoice);
            Assert.AreEqual(0, fire.Elements.Length);
        }

        // AN ELEMENT'S OWN ART REACHES THE CAST, and the test is written
        // against a LAYERED block because that is the case the path test it
        // replaces got wrong. A layered presentation authors no `path` -- the
        // rules refuse a block that authors both -- so "did this element bring
        // art" asked of `path` answered no for the pilot, and every Water cast
        // silently drew the skill's empty presentation instead.
        [Test]
        public void AnElementThatAuthorsOnlyLayersStillHandsItsArtToTheCast()
        {
            // THROUGH THE RESOLVER, not hand-built, so the element's block
            // makes the same journey the content build puts it through --
            // validation included. A hand-built ResolvedSkill would prove
            // AsElement alone and skip the half that reads the file.
            var orb = OrbWithWaterArt();
            var water = orb.AsElement(DamageType.Water);

            Assert.IsTrue(water.Vfx.HasLayers,
                "the element's layered block did not reach the cast at all");
            Assert.AreEqual(5, water.Vfx.layers.Length);

            // AND A COPY, never the catalogue's own object -- the same rule the
            // packet array is held to, and worse here because a layer is a
            // mutable object a renderer is handed every tick.
            var source = orb.Elements.First(e => e.Type == DamageType.Water).Vfx;
            Assert.AreNotSame(source.layers[0], water.Vfx.layers[0],
                "the cast was handed the catalogue's own layer objects");

            // An element with nothing authored still falls back to the skill's.
            Assert.IsFalse(orb.AsElement(DamageType.Earth).Vfx.HasLayers);
        }

        // ---- what a choice is worth ----------------------------------------------

        // THE HEADLINE. Against something that burns, picking Fire is worth
        // half again as much as picking anything it does not care about --
        // which is the entire reason the menu asks.
        //
        //   Earth on a Fire-weak enemy   16 x 1.0 = 16
        //   Fire  on a Fire-weak enemy   16 x 1.5 = 24
        //
        // 1.5 is CombatMath's WeaknessMultiplier, written out rather than
        // referenced so the expectation cannot move with the code it checks.
        [Test]
        public void FireOnAFireWeakEnemyLandsHalfAgainWhatEarthDoes()
        {
            var earth = Fight(WeakToFire);
            int earthBefore = earth.foe.CurrentHealth;
            Assert.IsTrue(earth.session.CastSkill(Orb(), earth.foe, DamageType.Earth));
            Assert.AreEqual(16, earthBefore - earth.foe.CurrentHealth,
                "Earth is neutral against a Fire-weak enemy: 16 lands as 16");

            var fire = Fight(WeakToFire);
            int fireBefore = fire.foe.CurrentHealth;
            Assert.IsTrue(fire.session.CastSkill(Orb(), fire.foe, DamageType.Fire));
            Assert.AreEqual(24, fireBefore - fire.foe.CurrentHealth,
                "Fire on a Fire-weak enemy is 16 x 1.5 = 24");
        }

        [Test]
        public void EveryElementLandsAtFaceValueOnANeutralEnemy()
        {
            foreach (var element in new[] { DamageType.Earth, DamageType.Water, DamageType.Fire, DamageType.Wind })
            {
                var (session, _, foe) = Fight(ElementalAffinity.Neutral);
                int before = foe.CurrentHealth;

                Assert.IsTrue(session.CastSkill(Orb(), foe, element));
                Assert.AreEqual(16, before - foe.CurrentHealth,
                    $"{element} on a neutral enemy is the authored 16, on the final health scale");
            }
        }

        // ---- the three refusals ---------------------------------------------------

        [Test]
        public void AnElementSkillCastWithNoElementIsRefusedAndSpendsNothing()
        {
            var (session, hero, foe) = Fight(ElementalAffinity.Neutral);
            int mana = hero.CurrentMana;
            int health = foe.CurrentHealth;

            Assert.IsFalse(session.CastSkill(Orb(), foe));

            Assert.AreEqual(mana, hero.CurrentMana, "a refused cast pays nothing");
            Assert.AreEqual(health, foe.CurrentHealth);
            Assert.AreSame(hero, session.Current, "and does not end the turn");
            StringAssert.Contains("must choose an element", string.Join(" ", Messages(session)));
        }

        [Test]
        public void AnElementTheSkillDoesNotOfferIsRefused()
        {
            var (session, hero, foe) = Fight(ElementalAffinity.Neutral);
            int mana = hero.CurrentMana;

            Assert.IsFalse(session.CastSkill(Orb(), foe, DamageType.Void));

            Assert.AreEqual(mana, hero.CurrentMana);
            StringAssert.Contains("cannot be cast as Void", string.Join(" ", Messages(session)));
        }

        [Test]
        public void AnElementOnASkillThatOffersNoneIsRefused()
        {
            var (session, hero, foe) = Fight(ElementalAffinity.Neutral);
            int mana = hero.CurrentMana;

            // The orb with the choice already made is exactly such a skill --
            // and casting it a second time with an element is the mistake a
            // double-dispatch through the menu would make.
            Assert.IsFalse(session.CastSkill(Orb().AsElement(DamageType.Fire), foe, DamageType.Fire));

            Assert.AreEqual(mana, hero.CurrentMana);
            StringAssert.Contains("has no element to choose", string.Join(" ", Messages(session)));
        }

        // THE ORDER OF THE TWO REFUSALS, and it is the whole reason the element
        // gate sits where it does. A cast the menu should never have offered
        // must not read to the player as one they cannot afford: that sends
        // them to the mana bar looking for an answer that is not there.
        [Test]
        public void AMissingElementIsRefusedBeforeTheCostCheck()
        {
            var (session, hero, foe) = Fight(ElementalAffinity.Neutral);
            hero.CurrentMana = 0;

            Assert.IsFalse(session.CastSkill(Orb(), foe));

            string log = string.Join(" ", Messages(session));
            StringAssert.Contains("must choose an element", log);
            StringAssert.DoesNotContain("cannot pay", log);
        }

        // ---- what the HUD says ----------------------------------------------------

        [Test]
        public void TheDamageTypeRowListsEveryChoiceUntilOneIsPicked()
        {
            var (session, hero, _) = Fight(ElementalAffinity.Neutral);

            Assert.AreEqual("Earth/Water/Fire/Wind",
                FightHudModel.DamageTypeLabel(session, hero, Orb()),
                "with nothing picked the honest answer is all four");

            Assert.AreEqual("Fire",
                FightHudModel.DamageTypeLabel(session, hero, Orb().AsElement(DamageType.Fire)),
                "once picked the card describes the cast, not the menu");
        }

        [Test]
        public void TheElementListIsOneFreeRowPerElement()
        {
            var rows = FightHudModel.ElementRows(Orb());

            Assert.AreEqual(4, rows.Count);
            CollectionAssert.AreEqual(new[] { "EARTH", "WATER", "FIRE", "WIND" },
                rows.Select(r => r.Name).ToList());

            foreach (var row in rows)
            {
                Assert.IsTrue(row.Affordable, "affordability was settled one depth up, on the skill's own row");
                Assert.AreEqual("", row.Cost, "the skill's price is not four prices");
                Assert.AreEqual(0, row.ManaCost, "hovering an element previews no second spend on the bar");
            }
        }

        [Test]
        public void ASkillWithNoChoiceHasNoElementRows()
        {
            Assert.AreEqual(0, FightHudModel.ElementRows(Orb().AsElement(DamageType.Fire)).Count);
            Assert.AreEqual(0, FightHudModel.ElementRows(null).Count);
        }

        // The POWER row is read off whatever copy the card was built on, so an
        // element skill previews the same number for every element -- the
        // preview is pre-mitigation and has no target to be weak to anything
        // (FightSession.PreviewSkillPower's own header). Pinned so the day that
        // stops being true is a test change rather than a surprise.
        [Test]
        public void ThePowerRowPreviewsThePacketItselfForEveryElement()
        {
            var (session, hero, _) = Fight(ElementalAffinity.Neutral);

            Assert.AreEqual("16", FightHudModel.PowerLabel(session, hero, Orb()));
            Assert.AreEqual("16", FightHudModel.PowerLabel(session, hero, Orb().AsElement(DamageType.Fire)));
        }

        // ---- the fixture ----------------------------------------------------------

        private static readonly ElementalAffinity WeakToFire =
            ElementalAffinity.Of(new[] { DamageType.Fire }, Array.Empty<DamageType>());

        // The orb as the resolver builds it from the shipped content, so a
        // behaviour test cannot pass against numbers the file does not author.
        // A fresh instance per call, for the reason CinderfaultSpellTests
        // states: cooldowns key on the id, not on object identity.
        // The orb as M5 authors it: one element with a layered block, three
        // without.
        private static ResolvedSkill OrbWithWaterArt()
        {
            var raw = new RawSkillEntry
            {
                id = "prismatic_orb",
                displayName = "Prismatic Orb",
                characterId = "owl",
                unlockLevel = 1,
                effect = "DamageSingle",
                manaCost = ManaCost,
                damageInstances = new[] { new RawDamageInstance { type = "Earth", amount = Packet } },
                elements = new[]
                {
                    new RawElementChoice { type = "Earth" },
                    new RawElementChoice { type = "Water", vfx = SpellLayerFixtures.Water() },
                },
            };

            bool ok = SkillEntryResolver.TryResolveAll(
                new List<RawSkillEntry> { raw }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            return resolved[0];
        }

        private static ResolvedSkill Orb()
        {
            var raw = new RawSkillEntry
            {
                id = "prismatic_orb",
                displayName = "Prismatic Orb",
                characterId = "owl",
                unlockLevel = 1,
                effect = "DamageSingle",
                manaCost = ManaCost,
                damageInstances = new[] { new RawDamageInstance { type = "Earth", amount = Packet } },
                elements = new[]
                {
                    new RawElementChoice { type = "Earth" },
                    new RawElementChoice { type = "Water" },
                    new RawElementChoice { type = "Fire" },
                    new RawElementChoice { type = "Wind" },
                },
            };

            bool ok = SkillEntryResolver.TryResolveAll(
                new List<RawSkillEntry> { raw }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            return resolved[0];
        }

        // ONE ENEMY WITH NO DEFENCE AND NO TYPED RESISTANCE beyond the affinity
        // asked for, and variance off, so every number above is the packet
        // times its matchup and nothing else.
        private static (FightSession session, CombatantState hero, CombatantState foe) Fight(
            ElementalAffinity affinity)
        {
            var hero = new CombatantState("Odette", true, 999999, 999, 20, 10);
            var foe = new CombatantState("Foe", false, 999999, 0, 1, 1);

            var kit = new PlayerKit("owl", CharacterRole.Utility,
                new List<ResolvedSkill> { Orb() }, new List<ResolvedRelic>(), null);

            var enemyKits = new List<EnemyKit>
            {
                new EnemyKit(new ResolvedEnemy("foe", "Foe", new StatBlock(), 0, 0, false, affinity, 0), false),
            };

            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { kit }, enemyKits, new SeededRandom(11)) { DamageVarianceRange = 0f };
            session.Begin();

            return (session, hero, foe);
        }

        // BOTH CHANNELS. A refusal happens before any beat is opened, so it
        // lands in the immediate list rather than on a beat -- and a test that
        // drained only the beats would read every refusal below as an empty
        // log and pass on the first half of its own assertion.
        private static IEnumerable<string> Messages(FightSession session) =>
            session.DrainBeats().SelectMany(b => b.Messages)
                .Concat(session.DrainImmediateMessages());

        // The orb's record, sliced out of skills.json as text -- the same
        // choice CinderfaultSpellTests makes for the same reason: EditMode sees
        // Domain only, JsonUtility is UnityEngine, and nothing here needs an
        // object graph. Sliced to ONE record so a field authored on a
        // neighbouring skill cannot satisfy an assertion about this one.
        private static string OrbEntry()
        {
            string json = File.ReadAllText(SkillsJsonPath());

            // SLICED BY BRACE DEPTH, not by "the text up to the next id".
            // A layer carries an `id` of its own -- the pilot's Water element
            // declares five -- so the old scan stopped inside the orb's own vfx
            // block and every assertion below would have been made against a
            // third of the record.
            string entry = JsonBlocks.ObjectsInArray(json, "skills")
                .FirstOrDefault(s => JsonBlocks.String(s, "id") == "prismatic_orb");

            Assert.IsNotNull(entry, "skills.json has no prismatic_orb entry");
            return entry;
        }

        private static string SkillsJsonPath()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "_Project", "ContentData")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "Could not locate Assets/_Project/ContentData from the working directory.");
            return Path.Combine(dir.FullName, "Assets", "_Project", "ContentData", "skills.json");
        }
    }
}
