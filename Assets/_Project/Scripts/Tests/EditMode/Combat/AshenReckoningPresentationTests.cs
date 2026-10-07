using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // ASHEN RECKONING'S BEATS as literal seconds (docs/PLAN_SPELL_REWORK.md 3.7):
    // the venom builds for 0.6 s, the Poison packet lands on the cue, and the
    // Fire packet, one stagger step (3 frames, 0.05 s) behind it, is the ignition.
    // Each packet's number has its own accent and its own sound.
    public class AshenReckoningPresentationTests
    {
        private static SpellPresentation Ashen()
        {
            string skills = File.ReadAllText(Path.Combine(RepoTree.Root(), "Assets", "_Project",
                "ContentData", "skills.json"));
            string skill = JsonBlocks.ObjectsInArray(skills, "skills")
                .Single(s => JsonBlocks.String(s, "id") == "ashen_reckoning");
            return SpellVfxJson.OwnVfx(skill);
        }

        [Test]
        public void TheVenomConvergesForTheCueAndEachPacketHasItsOwnAccent()
        {
            var vfx = Ashen();
            Assert.AreEqual(0.6f, vfx.hitCueSeconds, 1e-6f);

            var wind = vfx.layers.Single(l => l.id == "ritual");
            Assert.AreEqual(0.6f, wind.seconds, 1e-6f, "f0-f4 at 0.12 s");
            Assert.AreEqual(0.4f, wind.fadeIn, 1e-6f, "it builds from faint wisps, no pop-in");
            Assert.AreEqual(0f, wind.fade, 1e-6f);

            var catchLayer = vfx.layers.Single(l => l.id == "catch");
            Assert.AreEqual("release", catchLayer.at, "release clock, because an at: hit still is capped at 0.75 of the body");
            Assert.AreEqual("still", catchLayer.render);
            Assert.AreEqual("Spells/ashen_reckoning_catch_cel", catchLayer.path, "the cel catch sheet, cell 1: the core");
            Assert.AreEqual(1, catchLayer.startFrame);
            Assert.AreEqual(0.6f, catchLayer.offset, 1e-6f, "the Poison packet's number is on the cue");
            Assert.AreEqual(1.4f, catchLayer.glow, 1e-6f);
            Assert.IsTrue(string.IsNullOrEmpty(catchLayer.tint), "no green tint: it pushed the flame salmon");
            Assert.AreEqual(0.35f, catchLayer.punch, 1e-6f, "the whoomph: opens oversized and settles");

            var flare = vfx.layers.Single(l => l.id == "flare");
            Assert.AreEqual("release", flare.at);
            Assert.AreEqual(catchLayer.path, flare.path, "the same sheet flares; no second sheet");
            Assert.AreEqual(3, flare.startFrame, "the widest ring, hotter");
            Assert.AreEqual(0.65f, flare.offset, 1e-6f, "the cue plus 3 frames at 60 fps, StageHitFlash.StaggerSeconds");
            Assert.AreEqual(1.8f, flare.glow, 1e-6f);
            Assert.Greater(flare.size, vfx.layers.Single(l => l.id == "catch_c").size);
            Assert.LessOrEqual(vfx.layers.Where(l => l.id.StartsWith("catch") || l.id == "flare").Max(l => l.size), 280f, "the ring is 0.61 of its box and the box is about 2.4x on a treant: 250 draws about the body's width");
        }

        [Test]
        public void TheSecondPacketHasItsOwnSoundAndItsEmbersLandWithIt()
        {
            var vfx = Ashen();
            Assert.AreEqual("Audio/Sfx/censer_of_embers_impact", vfx.secondSfxPath);
            Assert.AreNotEqual(vfx.sfxPath, vfx.secondSfxPath);

            Assert.AreEqual(0f, vfx.layers.Single(l => l.id == "drops").offset, 1e-6f);
            Assert.AreEqual(0.05f, vfx.layers.Single(l => l.id == "settle").offset, 1e-6f);
            Assert.AreEqual(0.62f, vfx.layers.Single(l => l.id == "settle").emitter.lifeMax, 1e-6f);
        }

        [Test]
        public void TheCueKicksTheStageAndEveryLayerIsOnTheBody()
        {
            var vfx = Ashen();
            Assert.AreEqual(0.025f, vfx.kick.strength, 1e-6f);
            Assert.AreEqual(0.03f, vfx.kick.rise, 1e-6f);
            Assert.AreEqual(0.18f, vfx.kick.fall, 1e-6f);
            CollectionAssert.IsEmpty(vfx.layers.Where(l => l.unkicked), "nothing hangs in the air: every layer is on the body and kicks with it");
        }

        [Test]
        public void TheWindUpTurnsFasterToTheCueAndTheEndingDiesInOrder()
        {
            var vfx = Ashen();
            var turn = vfx.layers.Single(l => l.id == "ritual").rotate;
            Assert.AreEqual(270f, turn.phase, 1e-6f, "starts at rest, fastest on the cue");
            Assert.AreEqual(2.4f, turn.period, 1e-6f, "a quarter period is the 0.6 s wind-up");

            // Flames step down in size and glow, then the falling ash is the final thing.
            var fires = new[] { "fire_a", "fire_b", "fire_c", "fire_d" }.Select(id => vfx.layers.Single(l => l.id == id)).ToArray();
            for (int i = 1; i < fires.Length; i++)
            {
                Assert.Greater(fires[i - 1].size, fires[i].size, fires[i].id + " is smaller");
                Assert.Greater(fires[i - 1].glow, fires[i].glow, fires[i].id + " is dimmer");
                Assert.Greater(fires[i].offset, fires[i - 1].offset);
            }
            var embers = new[] { "embers_a", "embers_b", "embers_c" }.Select(id => vfx.layers.Single(l => l.id == id)).ToArray();
            Assert.Greater(embers[0].glow, embers[1].glow);
            Assert.Greater(embers[1].glow, embers[2].glow);

            float End(SpellLayer l) => (l.at == "hit" ? vfx.hitCueSeconds : 0f) + l.offset + (l.emitter != null
                ? (l.emitter.window > 0f ? l.emitter.window : 0f) + l.emitter.lifeMax
                : l.seconds + l.fade);
            CollectionAssert.IsEmpty(vfx.layers.Where(l => l.id == "decay" || l.id == "smoke" || l.id == "curl"),
                "the burst smoke and the S-curl were removed by the owner: only the ash cells draw from the smoke sheet");
            var ash = vfx.layers.Single(l => l.id == "ash");
            foreach (var l in vfx.layers.Where(l => l.id != "ash" && l.id != "ritual"))
                Assert.Less(End(l), End(ash), $"{l.id} ends before the last of the ash");
        }

        [Test]
        public void TheFireThrowsEmbersFlameAndAshAndNoRock()
        {
            var settle = Ashen().layers.Single(l => l.id == "settle").emitter;
            Assert.AreEqual("Spells/censer_of_embers_particles", settle.path,
                "the ashen sheet's f1 and f2 are rocks");
            CollectionAssert.AreEqual(new[] { 1f, 0f, 1f, 1f }, settle.weights, "coal, ash wisp, flame tongue; not the lava rock f1");

            var drops = Ashen().layers.Single(l => l.id == "drops").emitter;
            Assert.AreEqual("Spells/ashen_reckoning_particles", drops.path, "the venom drop stays on the first packet");
            CollectionAssert.AreEqual(new[] { 1f, 0f, 0f, 0f }, drops.weights);
        }
    }
}
