using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.PlayModeTests
{
    // PLAYING THE GAME DOES NOT EDIT THE GAME'S CONTENT.
    //
    // Every Resolved* record under ContentDatabase is a SHARED object. One
    // EnemyDefinition.Data is handed to every encounter that fields that
    // monster, one ResolvedSkill to every cast of that skill, for the whole
    // session -- the assets are loaded once and never reloaded. So a single
    // write to one of them during a fight is not a local mistake: it is a
    // change to the catalogue that outlives the fight, survives into the next
    // run, and is invisible until some later fight reads a number nobody
    // authored.
    //
    // WHY THIS IS A BEHAVIOURAL TEST AND NOT A LINT. `data` is private with an
    // Editor-only setter now, and that closes whole-record replacement and
    // nothing else: it holds a class with public fields, so
    // `definition.Data.Cost = 3` is still legal C# everywhere. A source-text
    // rule can say the pattern does not appear today (ContentOwnershipLintTests
    // does exactly that, and calls itself a diagnostic); it cannot say a fight
    // did not mutate anything, because the write that matters may be through a
    // local, an out parameter, or an array element. This runs a real fight
    // through the real content graph and looks at the records afterwards.
    //
    // WHAT IS COMPARED, and both halves matter:
    //
    //   VALUE -- a deep structural hash of every public field, walking arrays
    //   element-wise and nested records (SpellPresentation, TransformGrant)
    //   field-wise. Catches a number or a string being written.
    //
    //   IDENTITY -- the reference of every array and every nested object,
    //   collected in visit order and compared with AreSame. Catches the write a
    //   value hash CANNOT see: a record handed a fresh array with the same
    //   contents. That is not harmless, because whoever held the old array --
    //   a kit built earlier in the same fight -- keeps reading the old one, so
    //   the catalogue and the fight quietly disagree from then on.
    //
    // BOTH HALVES HAVE BEEN SEEN TO FAIL, which is the only reason to believe
    // either. One line added to FightSession.ExecuteAttack, writing to the
    // defender's own ResolvedEnemy through EnemyKit.Source, and run twice:
    //
    //   Source.AttackType = DamageType.Fire
    //     -> "A fight wrote to shared content. First difference: line 206
    //         before: Enemies/bog_witch.AttackType=Poison
    //         after:  Enemies/bog_witch.AttackType=Fire"
    //
    //   Source.Weaknesses = (DamageType[])Source.Weaknesses.Clone()
    //     -> "'Enemies/bog_witch.Weaknesses' is a DIFFERENT object after the
    //         fight, even though its contents match."
    //
    // The second is the one worth having provoked on purpose: the arrays are
    // equal, the value hash is identical, and only the identity half says
    // anything at all. Both lines were then removed.
    public class ContentIsolationTests
    {
        // Below this, the walk is not looking at the catalogue -- it is looking
        // at an empty or half-loaded one and agreeing with itself perfectly.
        // Nine record types with a handful of rows each; 25 is comfortably under
        // the real count and comfortably over "something went wrong".
        private const int MinimumRecordsVisited = 25;

        [Test]
        public void AFullFightLeavesEveryResolvedRecordExactlyAsItFoundIt()
        {
            ContentDatabase.Reset();

            var before = Walk();

            Assert.GreaterOrEqual(before.RecordsVisited, MinimumRecordsVisited,
                $"The walk visited {before.RecordsVisited} record(s), which is not a catalogue. " +
                "ContentDatabase loaded nothing, or Records() stopped returning the types it names -- " +
                "either way the comparison below would agree with itself about nothing.");

            PlayOneFight();

            var after = Walk();

            Assert.AreEqual(before.RecordsVisited, after.RecordsVisited,
                "the fight changed how many records the catalogue holds");

            // VALUE FIRST, because its failure message names the field and the
            // identity one cannot.
            if (before.Text != after.Text)
            {
                Assert.Fail("A fight wrote to shared content. First difference:\n" +
                            FirstDifference(before.Text, after.Text));
            }

            Assert.AreEqual(before.References.Count, after.References.Count,
                "the walk found a different number of arrays/nested objects the second time");

            for (int i = 0; i < before.References.Count; i++)
            {
                Assert.AreSame(before.References[i], after.References[i],
                    $"'{before.ReferencePaths[i]}' is a DIFFERENT object after the fight, even though its " +
                    "contents match. Something replaced an array or a nested record rather than editing it; " +
                    "anything that captured the old reference during the fight is now reading a different " +
                    "object from the one the catalogue holds.");
            }
        }

        // ---- the fight ---------------------------------------------------------

        // A fight chosen to touch as much of the graph as one fight can: a
        // player kit carrying relics, the Black Ram transform skill (the one
        // skill whose cast rewrites the caster's whole kit), and an ELITE pack,
        // which is the path that scales enemy stats and is therefore the one
        // most tempted to write a scaled number back onto the record it read.
        private static void PlayOneFight()
        {
            var transform = ContentDatabase.Skills
                .FirstOrDefault(s => s != null && s.Data != null
                                     && s.Data.Effect == SkillEffect.Transform
                                     && !string.IsNullOrEmpty(s.Data.CharacterId));

            Assert.IsNotNull(transform,
                "no Transform skill is authored against a character, so this fight would not exercise the " +
                "one cast that rebuilds a kit. Point it at whatever replaced Black Ram Mode.");

            string caster = transform.Data.CharacterId;
            var party = new List<string> { caster };

            // Three, so the pack fills more than the front slot and DamageAll
            // has something to be plural about.
            var enemies = ContentDatabase.Enemies
                .Where(e => e != null && e.Data != null && !e.Data.IsBoss)
                .Take(3).Select(e => e.id).ToList();
            Assert.IsNotEmpty(enemies, "no non-boss enemies in content");

            var relics = ContentDatabase.Relics
                .Where(r => r != null).Take(3).Select(r => r.id).ToList();
            Assert.IsNotEmpty(relics, "no relics in content, so the kit carries none");

            // GRANTED AND PAID FOR, both through seams that already exist for
            // tools/preview.ps1 -Spell. Black Ram Mode is authored at
            // unlockLevel 999 and costs 7 Wool, and Wool starts every fight at
            // zero -- so a level-1 caster can neither see it nor afford it, and
            // this fight would quietly not exercise the transform at all. The
            // grant is preview-owned and dies with the kit; the top-up is
            // written to the COMBATANT, which is per-fight state and not
            // content.
            var built = FightEncounterAdapter.Build(party, enemies, new SeededRandom(20260906),
                isElite: true, relicIds: relics,
                previewExtraSkillIds: new[] { transform.id });
            Assert.IsNotNull(built?.Session, "the encounter would not build");

            var session = built.Session;

            var hero = built.Party[0];
            hero.PrimaryPool.Current = hero.MaxMana;
            if (hero.SignaturePool != null) hero.SignaturePool.Current = hero.SignaturePool.Max;

            session.Begin();

            bool transformed = false;

            // Bounded rather than "until it ends": a fight that will not end is
            // a different test's problem, and an unbounded loop here would hang
            // the suite rather than report anything.
            for (int turn = 0; turn < 200 && !session.IsOver; turn++)
            {
                if (!session.IsPlayerTurn)
                {
                    // Enemy turns resolve inside the session after a player
                    // action; reaching here means the session is waiting on
                    // something this driver does not know how to give it.
                    //
                    // AutoResolveEnemyTurns, which is what FightRunner and
                    // FightController's own stalled-turn rescue both do. This
                    // used to spend a Hold Back, which worked only because
                    // Hold Back was unconditionally legal -- Move, its
                    // replacement, is not, and a driver that cannot get past
                    // an owed enemy turn would spin here.
                    session.AutoResolveEnemyTurns();
                    continue;
                }

                var target = session.Encounter.LivingEnemies.FirstOrDefault();
                if (target == null) break;

                var options = session.SkillOptionsFor(session.Current)
                    .Where(o => o.Ready).ToList();

                // The transform first and once, then whatever else is ready --
                // so the fight both enters the form and then acts from inside
                // it, which is where a transformed kit's records get read.
                ResolvedSkillOption pick = default;

                if (!transformed)
                {
                    pick = options.FirstOrDefault(
                        o => o.Skill != null && o.Skill.Effect == SkillEffect.Transform);
                    if (pick.Skill != null) transformed = true;
                }

                if (pick.Skill == null)
                {
                    pick = options.FirstOrDefault(o => o.Skill != null);
                }

                if (pick.Skill != null) session.CastSkill(pick.Index, target);
                else session.ExecuteAttack(target);
            }

            Assert.IsTrue(transformed,
                "the fight never cast the transform, so the most kit-rewriting path in the game went " +
                "unexercised. Its caster could not afford it, or it is gated behind something this " +
                "driver does not set up.");
        }

        // ---- the walk ----------------------------------------------------------

        private sealed class Snapshot
        {
            public string Text;
            public readonly List<object> References = new List<object>();
            public readonly List<string> ReferencePaths = new List<string>();
            public int RecordsVisited;
        }

        // EVERY Resolved* RECORD REACHABLE FROM ContentDatabase. Nine types, not
        // the eight this was scoped against: upgrades gained a ResolvedUpgrade
        // in the same session. Listed rather than reflected off ContentDatabase
        // because a list that is wrong fails the vacuity guard above, whereas a
        // reflection sweep that silently stops finding a property does not.
        private static IEnumerable<(string Kind, object Record)> Records()
        {
            foreach (var x in ContentDatabase.Characters) yield return ("Characters/" + x.id, x.Data);
            foreach (var x in ContentDatabase.Enemies) yield return ("Enemies/" + x.id, x.Data);
            foreach (var x in ContentDatabase.Skills) yield return ("Skills/" + x.id, x.Data);
            foreach (var x in ContentDatabase.Relics) yield return ("Relics/" + x.id, x.Data);
            foreach (var x in ContentDatabase.Talents) yield return ("Talents/" + x.id, x.Data);
            foreach (var x in ContentDatabase.Modifiers) yield return ("Modifiers/" + x.id, x.Data);
            // KEYED BY LEVEL, because a spell tier is the one definition with
            // no id -- it is a row of the power curve, identified by the level
            // it applies from.
            foreach (var x in ContentDatabase.SpellTiers) yield return ("SpellTiers/" + x.SortOrder, x.Data);
            foreach (var x in ContentDatabase.Achievements) yield return ("Achievements/" + x.id, x.Data);
            foreach (var x in ContentDatabase.Upgrades) yield return ("Upgrades/" + x.id, x.Data);
        }

        private static Snapshot Walk()
        {
            var snapshot = new Snapshot();
            var text = new StringBuilder();

            // Sorted, so a change in Resources.LoadAll's order is not read as a
            // change in content.
            foreach (var (kind, record) in Records().OrderBy(r => r.Kind, StringComparer.Ordinal))
            {
                snapshot.RecordsVisited++;
                Describe(record, kind, text, snapshot, depth: 0);
                text.Append('\n');
            }

            snapshot.Text = text.ToString();
            return snapshot;
        }

        private static void Describe(object value, string path, StringBuilder text, Snapshot snapshot, int depth)
        {
            // Content is a tree, but a cycle would be an infinite loop rather
            // than a failure, and a hang says nothing.
            if (depth > 12)
            {
                text.Append(path).Append("=<too deep>\n");
                return;
            }

            if (value == null)
            {
                text.Append(path).Append("=null\n");
                return;
            }

            var type = value.GetType();

            if (type.IsPrimitive || type.IsEnum || value is string || value is decimal)
            {
                text.Append(path).Append('=')
                    .Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append('\n');
                return;
            }

            if (type.IsArray)
            {
                var array = (Array)value;
                snapshot.References.Add(value);
                snapshot.ReferencePaths.Add(path);
                text.Append(path).Append(".Length=").Append(array.Length).Append('\n');
                for (int i = 0; i < array.Length; i++)
                {
                    Describe(array.GetValue(i), $"{path}[{i}]", text, snapshot, depth + 1);
                }
                return;
            }

            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                .OrderBy(f => f.Name, StringComparer.Ordinal)
                .ToList();

            if (fields.Count == 0)
            {
                // A struct that keeps its state private and exposes it only
                // through properties -- ElementalAffinity is the one -- has a
                // ToString written for exactly this purpose.
                text.Append(path).Append('=').Append(value).Append('\n');
                return;
            }

            // REFERENCE TYPES ONLY. A struct read out of a field is a COPY, so
            // its identity says nothing about whether anything was written; a
            // class is the shared thing this test is about.
            if (!type.IsValueType)
            {
                snapshot.References.Add(value);
                snapshot.ReferencePaths.Add(path);
            }

            foreach (var field in fields)
            {
                Describe(field.GetValue(value), $"{path}.{field.Name}", text, snapshot, depth + 1);
            }
        }

        // The whole hash is tens of thousands of lines; the useful half of the
        // failure is the first line that differs and the two records around it.
        private static string FirstDifference(string before, string after)
        {
            var a = before.Split('\n');
            var b = after.Split('\n');

            for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
            {
                if (a[i] == b[i]) continue;
                return $"  line {i + 1}\n  before: {a[i]}\n  after:  {b[i]}";
            }

            return $"  the two walks are different lengths ({a.Length} vs {b.Length}) with no differing line, " +
                   "so a record was added or removed rather than edited.";
        }
    }
}
