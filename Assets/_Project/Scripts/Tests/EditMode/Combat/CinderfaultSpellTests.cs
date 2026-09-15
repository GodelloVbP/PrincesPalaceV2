using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // Cinderfault: a tier-2 book spell that opens one fault under the whole
    // enemy formation and erupts on everything standing in it.
    //
    // THE HALF WORTH TESTING IS THE PACKETS, and specifically the fact that
    // there are TWO of them landing on THREE enemies. Before this spell,
    // damageInstances only ever reached ResolveDamageSingle -- a DamageAll
    // skill with authored packets fell through to the Attack-scaled formula,
    // read Power and FlatAmount (both zero on a packet skill) and dealt the
    // floor of 1 per enemy with no elemental check anywhere in it. Silent, and
    // the exact failure the packets exist to prevent.
    //
    // EVERY EXPECTED NUMBER BELOW IS A LITERAL with its arithmetic spelled out,
    // never recomputed by calling the thing under test -- CLAUDE.md gotcha 5.
    // The fixture is chosen to make that possible: zero defense, zero typed
    // resistance, variance off, so 5 Fire on a Fire-weak enemy is
    // round(5 x 1.5) = 8 and nothing else moves it.
    public class CinderfaultSpellTests
    {
        // The authored contract, restated once so every assertion below reads
        // against a name rather than a bare number, and so the content pin and
        // the behaviour tests cannot drift apart.
        private const int ManaCost = 15;
        private const int Cooldown = 3;
        private const int FirePacket = 5;
        private const int NaturePacket = 5;

        // The two sliced sequences and where they rupture. Both layers compose
        // NINE frames peaking on frame FIVE (tools/slice_spell_sheet.py), which
        // is what lets one authored impactFrame drive both.
        private const int RuptureFrame = 5;
        private const int FrameCount = 9;

        // ---- the content entry ----------------------------------------------------

        [Test]
        public void TheContentEntryAuthorsTheGameplayContract()
        {
            string entry = CinderfaultEntry();

            StringAssert.Contains("\"bookOnly\": true", entry);
            StringAssert.Contains("\"bookTier\": 2", entry);
            StringAssert.Contains("\"effect\": \"DamageAll\"", entry);
            StringAssert.Contains($"\"manaCost\": {ManaCost}", entry);
            StringAssert.Contains($"\"cooldownTurns\": {Cooldown}", entry);

            // No scalingAxis: damageInstances replace the Attack-scaled
            // formula entirely (skills.json's own _readme, and
            // SkillEntryResolver's refusal if both are authored together --
            // AUDIT hunt 2026-09-11, d6a7ed0c).
            StringAssert.DoesNotContain("scalingAxis", entry);

            // No aftereffects at all: no burn, no stun, no status, no DoT. The
            // absence is the contract, so it is asserted rather than assumed --
            // a status added later would otherwise reach a player unremarked.
            StringAssert.DoesNotContain("appliesStatus", entry);
            StringAssert.DoesNotContain("statusDuration", entry);
        }

        [Test]
        public void TheContentEntryAuthorsFiveFireAndFiveNature()
        {
            string entry = CinderfaultEntry();
            var packets = Regex.Matches(entry, "\"type\"\\s*:\\s*\"([A-Za-z]+)\"\\s*,\\s*\"amount\"\\s*:\\s*(\\d+)")
                .Cast<Match>()
                .Select(m => (Type: m.Groups[1].Value, Amount: int.Parse(m.Groups[2].Value)))
                .ToList();

            Assert.AreEqual(2, packets.Count, "Cinderfault is exactly two packets");
            CollectionAssert.Contains(packets, ("Fire", FirePacket));
            CollectionAssert.Contains(packets, ("Nature", NaturePacket));
        }

        // THE TWO LAYERS SHARE ONE IMPACT INSTANT, and the content is where
        // that is settled.
        //
        // WHAT M6 CHANGED, AND WHAT IT DID NOT. Until M6 this read: the ground
        // sheet and the eruption sheet are both cut to nine frames peaking on
        // five, so Cinderfault authors impactFrame ONCE and leaves
        // groundImpactFrame/groundSeconds unset, and the presentation's
        // fallbacks make the second layer inherit the first's numbers. The
        // claim is unchanged -- one instant, stated once, for both layers --
        // but the mechanism is no longer an inheritance between two blocks. The
        // spell authors two LAYERS that both open at release and both run
        // 0.78s, and the cue is a number in seconds rather than a frame index,
        // which is the coupling the layered format exists to remove.
        //
        // AnUnsetGroundTimingFallsBackToThePerTargetSequence below still pins
        // the fallback itself, because every pre-layer spell in the game is
        // still resolved through it.
        [Test]
        public void TheTwoLayersRuptureOnOneAuthoredInstant()
        {
            string entry = CinderfaultEntry();

            StringAssert.Contains("\"layerFormat\": 1", entry);
            StringAssert.Contains("\"path\": \"Spells/cinderfault_ground\"", entry);
            StringAssert.Contains("\"path\": \"Spells/cinderfault_eruption\"", entry);
            StringAssert.Contains("\"castSfxPath\": \"Audio/Sfx/cinderfault_pressure\"", entry);
            StringAssert.Contains("\"sfxPath\": \"Audio/Sfx/cinderfault_impact\"", entry);

            // ONE CUE, AUTHORED, and it is the instant the pre-layer block
            // landed on: 0.78s x 5/9. SpellBaselineTimingTests holds that
            // equality against the old expression; here it is the file's own
            // text, so an edit to the number has to pass both.
            StringAssert.Contains("\"hitCueSeconds\": 0.43333334", entry);

            // NEITHER LAYER STATES A FRAME INDEX. impactFrame is what the cue
            // used to be derived from, and a layered block that still carried
            // one would be two answers to "when does the blow land".
            StringAssert.DoesNotContain("impactFrame", entry);
            StringAssert.DoesNotContain("groundPath", entry);
            StringAssert.DoesNotContain("groundSeconds", entry);

            // BOTH LAYERS OPEN AT RELEASE AND RUN 0.78s. That is what makes
            // their peaks land together now -- matching starts and matching
            // durations over two nine-frame sheets, rather than one field
            // being read twice.
            StringAssert.Contains("\"at\": \"release\"", entry);
            StringAssert.Contains("\"seconds\": 0.78", entry);
        }

        // The presentation's own fallbacks, which is the mechanism the test
        // above depends on. Pinned here rather than trusted: "unset means the
        // per-target sequence's own number" is the whole reason the content is
        // allowed to stay silent.
        [Test]
        public void AnUnsetGroundTimingFallsBackToThePerTargetSequence()
        {
            var vfx = new SpellPresentation
            {
                path = "Spells/cinderfault_eruption",
                seconds = 0.78f,
                impactFrame = RuptureFrame,
                groundPath = "Spells/cinderfault_ground",
            };

            Assert.AreEqual(0.78f, vfx.GroundSeconds, 0.0001f);
            Assert.AreEqual(RuptureFrame, vfx.GroundImpactFrame);
            Assert.IsTrue(vfx.HasGroundLayer);

            // Frame 5 of 9 -- five ninths, the same reading CombatBeat's own
            // header gives. 5/9 = 0.5555..., pinned as a literal.
            Assert.AreEqual(0.5556f, CombatBeat.ImpactFraction(vfx.GroundImpactFrame, FrameCount), 0.0005f);
        }

        // A SPELL THAT AUTHORS NO GROUND LAYER MUST BEHAVE EXACTLY AS BEFORE.
        // Six fields were added to a value every spell in the game carries; the
        // claim that they are inert is the claim worth a test of its own.
        [Test]
        public void AnOldSpellWithNoGroundLayerIsUnchanged()
        {
            var frostFlare = new SpellPresentation
            {
                path = "Spells/frost_flare",
                seconds = 0.52f,
                impactFrame = 5,
                impactX = 0.5f,
                impactY = 0.18f,
                sfxPath = "Audio/Sfx/frost_flare",
            };

            Assert.IsFalse(frostFlare.HasGroundLayer, "no groundPath means no ground layer");
            Assert.IsFalse(frostFlare.HasGroundImpactY, "an unauthored ground line must not read as 0");
            Assert.AreEqual("", frostFlare.castSfxPath);
            Assert.AreEqual(0f, frostFlare.groundAspect);
        }

        [Test]
        public void ThePresentationCopiesEveryNewFieldAcrossABoundary()
        {
            var original = new SpellPresentation
            {
                path = "Spells/cinderfault_eruption",
                seconds = 0.78f,
                impactFrame = RuptureFrame,
                groundPath = "Spells/cinderfault_ground",
                groundSeconds = 0.9f,
                groundImpactFrame = 4,
                groundAspect = 2.5f,
                groundImpactY = 0.104f,
                castSfxPath = "Audio/Sfx/cinderfault_pressure",
            };

            var copy = original.Copy();

            Assert.AreEqual("Spells/cinderfault_ground", copy.groundPath);
            Assert.AreEqual(0.9f, copy.groundSeconds, 0.0001f);
            Assert.AreEqual(4, copy.groundImpactFrame);
            Assert.AreEqual(2.5f, copy.groundAspect, 0.0001f);
            Assert.AreEqual(0.104f, copy.groundImpactY, 0.0001f);
            Assert.AreEqual("Audio/Sfx/cinderfault_pressure", copy.castSfxPath);

            // A COPY, NOT AN ALIAS. The whole reason Copy exists is that a
            // combat beat holding the catalogue's own object would let a fight
            // edit the content it was dealt from.
            copy.groundPath = "Spells/somewhere_else";
            Assert.AreEqual("Spells/cinderfault_ground", original.groundPath);
        }

        // ---- the resolver ---------------------------------------------------------

        [Test]
        public void TheEntryResolvesAsAGlobalBookSpellAimedAtEveryEnemy()
        {
            var resolved = ResolveCinderfault();

            Assert.IsTrue(resolved.BookOnly);
            Assert.AreEqual(2, resolved.BookTier);
            Assert.AreEqual(SkillEffect.DamageAll, resolved.Effect);
            Assert.AreEqual(SkillTargeting.AllEnemies, resolved.Targeting,
                "targeting is inferred from the effect and must reach every opponent");
            Assert.AreEqual(ManaCost, resolved.ManaCost);
            Assert.AreEqual(0, resolved.ResourceCost,
                "a global spell must never cost a signature resource -- see docs/SPELL_DESIGN_STANDARD.md");
            Assert.AreEqual(Cooldown, resolved.CooldownTurns);

            // No ScalingAxis assertion: unauthored (defaults to Auto) and
            // unread on a HasFixedDamage skill either way -- see the
            // scalingAxis omission above.

            // OFF THE LEVEL LADDER. A book spell that resolved to a real unlock
            // level would be handed to a level-1 character by the ordinary
            // route, which is the opposite of "learned from a book".
            Assert.AreEqual(int.MaxValue, resolved.UnlockLevel);
        }

        // ---- what it deals --------------------------------------------------------

        // NO EXTRA x10. Health pools are on a x10 scale and stat-derived damage
        // is scaled to match (CombatMath.DamageScale) -- damageInstances are the
        // exception and are written on the FINAL scale already. Ten and ten
        // against a neutral enemy is twenty, not two hundred.
        [Test]
        public void FiveFireAndFiveNatureLandAsTenOnANeutralEnemy()
        {
            var (session, _, foes) = Fight(Neutral, Neutral, Neutral);
            int before = foes[0].CurrentHealth;

            Assert.IsTrue(session.CastSkill(Cinderfault(), foes[0]));

            Assert.AreEqual(FirePacket + NaturePacket, before - foes[0].CurrentHealth,
                "5 Fire + 5 Nature on the final health scale is 10, not 100");
        }

        // THE HEADLINE: three enemies, three matchups, three different correct
        // numbers -- from one cast, on one beat.
        //
        //   neutral    5 + 5                              = 10
        //   fire-weak  round(5 x 1.5) + 5 = 8 + 5          = 13
        //   both-resistant  round(5 x 0.5) x 2 = 3 + 3     = 6
        //
        // 1.5 and 0.5 are CombatMath's WeaknessMultiplier and
        // ResistanceMultiplier; written out rather than referenced so the
        // expectation cannot move with the code it is checking. Both matchups
        // land on a rounded half-point (7.5, 2.5) -- Rounding.AwayFromZero
        // rounds each away from zero (8, 3) before the two packets sum, which
        // 10 and 20 never exercised.
        [Test]
        public void MixedWeaknessAndResistanceResolvePerEnemy()
        {
            var (session, _, foes) = Fight(Neutral, WeakToFire, ResistsBoth);
            var before = foes.Select(f => f.CurrentHealth).ToList();

            Assert.IsTrue(session.CastSkill(Cinderfault(), foes[0]));

            Assert.AreEqual(10, before[0] - foes[0].CurrentHealth, "neutral: 5 + 5");
            Assert.AreEqual(13, before[1] - foes[1].CurrentHealth, "fire-weak: round(5 x 1.5)=8 + 5");
            Assert.AreEqual(6, before[2] - foes[2].CurrentHealth, "resists both: round(5 x 0.5)=3 + 3");
        }

        // AND THE VIEW IS TOLD ALL THREE. One beat used to carry one Amount --
        // the largest single hit -- so the player was shown 25 over one enemy
        // and nothing over the two that took 20 and 10. See BeatTargetResult.
        [Test]
        public void TheBeatCarriesEachEnemysOwnNumber()
        {
            var (session, _, foes) = Fight(Neutral, WeakToFire, ResistsBoth);

            Assert.IsTrue(session.CastSkill(Cinderfault(), foes[0]));

            var beat = session.DrainBeats().Single(b => b.HasPerTargetResults);

            Assert.AreEqual(3, beat.Results.Count, "one result per living enemy");
            CollectionAssert.AreEquivalent(new[] { 10, 13, 6 },
                beat.Results.Select(r => r.Amount).ToList(),
                "the beat must carry each enemy's own amount, not three copies of the largest");

            Assert.AreEqual(13, beat.Amount,
                "the beat-wide Amount stays the largest single hit -- every existing consumer reads it");
        }

        // A CORPSE TAKES NOTHING AND IS DRAWN NOTHING. The snapshot is taken
        // before the loop resolves anyone, which is what lets an enemy killed
        // BY this cast still be drawn taking the hit -- the same list must not
        // also pick up someone who was already down when it was cast.
        [Test]
        public void AnAlreadyDeadEnemyIsNotStruckAgain()
        {
            var (session, _, foes) = Fight(Neutral, Neutral, Neutral);
            foes[1].CurrentHealth = 0;

            Assert.IsTrue(session.CastSkill(Cinderfault(), foes[0]));

            var beat = session.DrainBeats().Single(b => b.HasPerTargetResults);

            Assert.AreEqual(2, beat.Results.Count, "the fallen enemy is not a target");
            CollectionAssert.DoesNotContain(beat.Results.Select(r => r.Target).ToList(), foes[1]);
            Assert.AreEqual(0, foes[1].CurrentHealth, "and it certainly does not go further into the red");
        }

        // A PARTIAL FORMATION. Nothing about the sweep assumes three: one enemy
        // is one eruption, and the empty slots are simply not in the list.
        [Test]
        public void ALoneEnemyIsHitExactlyOnce()
        {
            var (session, _, foes) = Fight(Neutral);
            int before = foes[0].CurrentHealth;

            Assert.IsTrue(session.CastSkill(Cinderfault(), foes[0]));

            var beat = session.DrainBeats().Single(b => b.HasPerTargetResults);

            Assert.AreEqual(1, beat.Results.Count);
            Assert.IsNull(beat.SplashTargets, "there is nobody else to draw the effect on");
            Assert.AreEqual(10, before - foes[0].CurrentHealth, "hit once, not once per empty slot");
        }

        // ---- what it costs --------------------------------------------------------

        // ONE CAST, ONE CHARGE, however many enemies it lands on. The obvious
        // way to get this wrong is to spend inside the per-enemy loop.
        [Test]
        public void ManaIsChargedOncePerCastRatherThanOncePerEnemy()
        {
            var (session, hero, foes) = Fight(Neutral, Neutral, Neutral);
            int before = hero.CurrentMana;

            Assert.IsTrue(session.CastSkill(Cinderfault(), foes[0]));

            Assert.AreEqual(ManaCost, before - hero.CurrentMana,
                "three enemies must not cost three times the mana");
        }

        [Test]
        public void InsufficientManaRefusesTheCastAndSpendsNothing()
        {
            var (session, hero, foes) = Fight(Neutral, Neutral, Neutral);
            hero.PrimaryPool.Current = ManaCost - 1;
            int health = foes[0].CurrentHealth;

            Assert.IsFalse(session.CastSkill(Cinderfault(), foes[0]),
                $"{ManaCost - 1} mana cannot pay for a {ManaCost} spell");

            Assert.AreEqual(ManaCost - 1, hero.CurrentMana, "a refused cast spends nothing");
            Assert.AreEqual(health, foes[0].CurrentHealth, "and lands nothing");
        }

        // ---- the cooldown ---------------------------------------------------------

        // CAST ON TURN 1, BACK ON TURN 4. Three means "turns until usable
        // again, counted from the turn it was cast on" -- the reading an author
        // says out loud, and the one every other spelling of is off by one.
        // See SkillCooldownTests, which pins the rule itself; this pins that
        // Cinderfault's authored 3 actually produces it.
        [Test]
        public void ACooldownOfThreeBlocksExactlyTurnsTwoAndThree()
        {
            var cinderfault = Cinderfault();
            var (session, hero, foes) = Fight(Neutral, Neutral, Neutral);
            hero.PrimaryPool.Max = 9999;
            hero.PrimaryPool.Current = 9999;

            Assert.IsTrue(session.CastSkill(cinderfault, foes[0]), "turn 1 casts");

            Assert.IsFalse(session.CastSkill(cinderfault, foes[0]), "turn 2 is a wait");
            session.ExecuteAttack(foes[0]);

            Assert.IsFalse(session.CastSkill(cinderfault, foes[0]), "turn 3 is still a wait");
            session.ExecuteAttack(foes[0]);

            Assert.IsTrue(session.CastSkill(cinderfault, foes[0]), "turn 4 is back");
        }

        // OTHER ACTORS' TURNS DO NOT COUNT. The cooldown ticks on the caster's
        // own turns, so a fight full of enemies taking swings between them must
        // not shorten the wait -- the fixture below fields three enemies that
        // all act between the hero's turns, which is what makes this different
        // from the solo case above.
        [Test]
        public void EnemyTurnsBetweenTheCastersDoNotShortenTheWait()
        {
            var cinderfault = Cinderfault();
            var (session, hero, foes) = Fight(Neutral, Neutral, Neutral);
            hero.PrimaryPool.Max = 9999;
            hero.PrimaryPool.Current = 9999;

            Assert.IsTrue(session.CastSkill(cinderfault, foes[0]), "turn 1 casts");

            // One of the hero's own turns passes, and with three enemies on the
            // field several of theirs pass inside it.
            session.ExecuteAttack(foes[0]);

            Assert.IsFalse(session.CastSkill(cinderfault, foes[0]),
                "three enemies acting is still only ONE of the caster's turns gone");
        }

        // ---- the fixture ----------------------------------------------------------

        private static readonly ElementalAffinity Neutral = ElementalAffinity.Neutral;

        private static readonly ElementalAffinity WeakToFire =
            ElementalAffinity.Of(new[] { DamageType.Fire }, Array.Empty<DamageType>());

        private static readonly ElementalAffinity ResistsBoth =
            ElementalAffinity.Of(Array.Empty<DamageType>(), new[] { DamageType.Fire, DamageType.Nature });

        // The spell as the resolver builds it from the shipped content, so a
        // behaviour test cannot pass against numbers the file does not author.
        //
        // A FRESH INSTANCE PER CALL, which is safe because a cooldown is tracked
        // by skill ID rather than by object identity (FightSession.Cooldowns) --
        // the kit's copy and the cast's copy are the same spell as far as the
        // wait is concerned. A static cache would have been the other way to get
        // that, and it would have been a mutable static in a test class with no
        // reset seam, which is the shape CODE_STANDARDS section 7 refuses.
        private static ResolvedSkill Cinderfault() => ResolveCinderfault();

        private static ResolvedSkill ResolveCinderfault()
        {
            var raw = new RawSkillEntry
            {
                id = "cinderfault",
                displayName = "Cinderfault",
                characterId = "sheep",
                bookOnly = true,
                bookTier = 2,
                effect = "DamageAll",
                manaCost = ManaCost,
                cooldownTurns = Cooldown,
                damageInstances = new[]
                {
                    new RawDamageInstance { type = "Fire", amount = FirePacket },
                    new RawDamageInstance { type = "Nature", amount = NaturePacket },
                },
            };

            bool ok = SkillEntryResolver.TryResolveAll(
                new List<RawSkillEntry> { raw }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            return resolved[0];
        }

        // ENEMIES WITH NO DEFENCE AND NO TYPED RESISTANCE, and variance off, so
        // every number above is the packet times its matchup and nothing else.
        // A fixture that left either in would make each expectation a
        // three-term calculation nobody can check by reading it.
        private static (FightSession session, CombatantState hero, List<CombatantState> foes) Fight(
            params ElementalAffinity[] affinities)
        {
            var hero = new CombatantState("Shawn", true, 999999, 999, 20, 10);

            var foes = affinities
                .Select((_, i) => new CombatantState($"Foe{i}", false, 999999, 0, 1, 1))
                .ToList();

            var kit = new PlayerKit("hero", CharacterRole.Tank,
                new List<ResolvedSkill> { Cinderfault() }, new List<ResolvedRelic>(), null);

            var enemyKits = affinities
                .Select((affinity, i) => new EnemyKit(
                    new ResolvedEnemy($"foe{i}", $"Foe{i}", new StatBlock(), 0, 0, false, affinity, i), false))
                .ToList();

            var session = new FightSession(new CombatEncounter(new[] { hero }, foes.ToArray()),
                new List<PlayerKit> { kit }, enemyKits,
                new SeededRandom(11)) { DamageVarianceRange = 0f };
            session.Begin();

            return (session, hero, foes);
        }

        // ---- reading the shipped content ------------------------------------------

        // The cinderfault record, sliced out of skills.json as text.
        //
        // TEXT RATHER THAN A PARSE, the same choice SkillEntryResolverTests
        // makes for the same reason: EditMode sees Domain only, JsonUtility is
        // UnityEngine, and nothing here needs a real object graph -- only the
        // set of fields the file actually states. Sliced to ONE record so a
        // field authored on a neighbouring skill cannot satisfy an assertion
        // about this one.
        //
        // BY BRACE DEPTH RATHER THAN BY THE NEXT `"id":`, which is what this
        // did until M6. A layered vfx block gives its layers ids of their own,
        // so the first `"id"` after the skill's is now `"fault"` INSIDE this
        // record -- the old slice ended there and threw away everything from
        // the vfx block to bookTier, quietly turning three StringAssert.Contains
        // into assertions about text that was no longer being read.
        private static string CinderfaultEntry()
        {
            string json = File.ReadAllText(SkillsJsonPath());

            string entry = JsonBlocks.ObjectsInArray(json, "skills")
                .FirstOrDefault(s => JsonBlocks.String(s, "id") == "cinderfault");

            Assert.IsNotNull(entry, "skills.json has no cinderfault entry");
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
