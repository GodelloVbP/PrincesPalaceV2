using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.PlayModeTests
{
    // IS THIS MONSTER'S ART ACTUALLY FINISHED? Asked of every enemy in the
    // catalogue, headless, with no graphics device.
    //
    // This is what protects new content on a machine that cannot render:
    // screenshots are an addition, not the gate. The three failures it covers
    // are the ones that are invisible until somebody plays the exact turn that
    // exposes them --
    //
    //   a pose that resolves to nothing, so the stage shows a nameplate where
    //   the monster should be, and nothing logs;
    //
    //   stances drawn on different canvases, so the figure changes size and
    //   position as it changes pose (the stage sizes each slot to the sprite it
    //   is showing, so a 12px difference is a 12px jump);
    //
    //   no StanceManifest entry, so the figure stands on its own canvas bottom
    //   -- which for a padded canvas is a few pixels of nothing, and reads as
    //   hovering.
    //
    // OVER THE WHOLE ROSTER, not a hand-kept list. EnemyStanceCaptureTests
    // names three kits because it PHOTOGRAPHS them and a photograph has to name
    // its subject; a completeness sweep has no such excuse, and a list is the
    // thing that silently stops covering the mob added after it.
    // tools/preview.ps1 -Enemy <id> narrows this to one id through the preview
    // fixture rather than through this file.
    public class EnemyArtCompletenessTests
    {
        [TearDown]
        public void Restore() => StanceManifestLoader.Reset();

        // Enemies that promise art at all. One with no spritePath is a
        // deliberate state -- the stage shows a nameplate and says so -- and
        // holding it to an art standard would just make "not drawn yet" a
        // failing test.
        private static IEnumerable<EnemyDefinition> Drawn() =>
            ContentDatabase.Enemies.Where(e => e != null && !string.IsNullOrWhiteSpace(e.Data.SpritePath));

        // Every pose a monster can REACH, resolved the way the stage resolves
        // it -- "are the animations actually in" asked of content rather than
        // of a folder listing. Two different causes look identical in play:
        //
        //   - a stance folder missing or imported as a plain Texture, which
        //     FightController answers with its no-art nameplate;
        //   - a skill whose authored `stance` names a pose its caster does not
        //     have, which is a one-character typo in JSON that no build step
        //     reads and that only shows up as a monster VANISHING for exactly
        //     the turn it casts.
        //
        // The four the fight drives on its own (idle/attack/hurt/defeated) plus
        // whatever every ability it can draw asks for -- StanceFor's rule,
        // restated because it is private and one line: the skill's own stance
        // if it authored one, "cast" otherwise.
        [Test]
        public void EveryStanceEveryEnemyCanReachResolvesToFrames()
        {
            var missing = new List<string>();
            int probed = 0;

            foreach (var enemy in Drawn())
            {
                foreach (string stance in StancesReachableBy(enemy))
                {
                    probed++;
                    if (StanceAnimationLibrary.Resolve(enemy.Data.SpritePath, stance) == null)
                    {
                        missing.Add($"{enemy.id}:{stance}");
                    }
                }
            }

            Assert.Greater(probed, 0, "no enemy has art, so this pin is vacuous");
            Assert.IsEmpty(missing,
                "these monsters can reach a pose they have no art for, and will show a nameplate " +
                "(or nothing) on the turn they do: " + string.Join(", ", missing));
        }

        // ONE CANVAS PER ACTOR, which is what makes the manifest's single
        // authored ground line mean anything.
        [Test]
        public void EveryStanceOfAnEnemySharesOneCanvas()
        {
            var offenders = new List<string>();
            int checkedActors = 0;

            foreach (var enemy in Drawn())
            {
                var sizes = new Dictionary<string, Vector2>();

                foreach (string stance in StancesReachableBy(enemy))
                {
                    var sprite = Resources.Load<Sprite>($"{enemy.Data.SpritePath}/{stance}");
                    if (sprite != null) sizes[stance] = sprite.rect.size;
                }

                if (sizes.Count == 0) continue;
                checkedActors++;

                var distinct = sizes.Values.Distinct().ToList();
                if (distinct.Count > 1)
                {
                    offenders.Add($"{enemy.id} spans {distinct.Count} canvas sizes (" +
                                  string.Join(", ", sizes.Take(8).Select(p => $"{p.Key}={p.Value}")) + ")");
                }
            }

            Assert.Greater(checkedActors, 0, "no enemy resolved a single sprite, so this rule is vacuous");
            Assert.IsEmpty(offenders,
                "these monsters change size and position as they change pose, because their stances were not " +
                "composited onto one shared canvas (tools/slice_actor_sheet.py's ground-band anchor is what does " +
                "that): " + string.Join("; ", offenders));
        }

        [Test]
        public void EveryEnemyWithArtIsInTheStanceManifest()
        {
            var missing = Drawn()
                .Where(e => !StanceManifestLoader.Manifest.HasActor(e.Data.SpritePath))
                .Select(e => $"{e.id} ({e.Data.SpritePath})")
                .ToList();

            Assert.IsNotEmpty(Drawn().ToList(), "no enemy has art, so this rule is vacuous");
            Assert.IsEmpty(missing,
                "these monsters have no Resources/StanceManifest.json entry, so they stand on their own canvas " +
                "bottom rather than on the stage's ground line -- which for a padded canvas reads as hovering: " +
                string.Join(", ", missing));
        }

        // ONE DEFINITION OF "the poses this monster can reach", used by all
        // three rules above. Separate copies of this walk is exactly how the
        // canvas rule would come to check a smaller set than the resolution
        // rule and quietly stop covering an ability's stance.
        internal static IEnumerable<string> StancesReachableBy(EnemyDefinition enemy)
        {
            var wanted = new HashSet<string> { "idle", "attack", "hurt", "defeated" };

            foreach (var ability in enemy.Data.Abilities ?? System.Array.Empty<EnemyAbilityRef>())
            {
                if (string.IsNullOrWhiteSpace(ability.SkillId)) continue;

                var skill = ContentDatabase.Skills.FirstOrDefault(s => s != null && s.id == ability.SkillId);
                Assert.IsNotNull(skill,
                    $"{enemy.id} draws on skill '{ability.SkillId}', which is not in the catalogue");

                wanted.Add(string.IsNullOrEmpty(skill.Data.Stance) ? "cast" : skill.Data.Stance);

                // AND THE TWO PHASES IN FRONT OF THE STRIKE. A skill may
                // author up to three poses of one blow (CombatBeat's
                // precedence table); each is a folder name resolved exactly
                // the way the strike's is, so each fails exactly the way the
                // strike's does -- a monster that vanishes for the frames it
                // is winding up. Added here rather than as a fourth test, so
                // the resolution, canvas and manifest rules all cover them at
                // once: this walk is the one definition of "poses this monster
                // can reach", and that is the whole reason it is shared.
                foreach (string phase in new[] { skill.Data.ApproachStance, skill.Data.WindupStance })
                {
                    if (!string.IsNullOrWhiteSpace(phase)) wanted.Add(phase);
                }
            }

            // The legacy single-action trio poses as a cast.
            if (!string.IsNullOrWhiteSpace(enemy.Data.SkillName)) wanted.Add("cast");

            return wanted;
        }
    }
}
