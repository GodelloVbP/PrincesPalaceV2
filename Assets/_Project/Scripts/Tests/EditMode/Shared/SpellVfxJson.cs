using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // ONE READER OF AN AUTHORED vfx BLOCK, for every fixture that has to ask
    // skills.json what a spell actually draws.
    //
    // WHY IT EXISTS AT ALL. Two fixtures need the same answer and were about to
    // grow two parsers: SpellPoolCapacityTests counts what a cast spends per
    // pool band, and SpellVfxRecipeDriftTests checks that every folder a layer
    // plays has a recorded provenance and enough frames. Both read the same
    // array; a second reader is a second thing that can fall behind the
    // authoring format, and the first one to do so fails by going QUIET -- a
    // layer it cannot see is a layer it does not check.
    //
    // BY REFLECTION OVER THE TARGET TYPE, not off a hand-written field list.
    // That is the whole design of it. AUDIT #60 measured what a hand-written
    // list of a presentation's fields costs -- SpellPresentation.Copy() dropped
    // a field twice -- and a parser is the same shape with a worse failure: a
    // field nobody added here is simply absent from what the pins measure, and
    // every pin stays green. Walking the type's own public fields means a new
    // knob on SpellLayer or SpellEmitter is read the day it is declared, and a
    // field of a kind this cannot decode THROWS rather than being skipped.
    //
    // NOT JsonUtility, for the reason JsonBlocks' own header gives: a test that
    // calls it is a test Unity has to host, and these two run on the dotnet
    // loop in four seconds.
    internal static class SpellVfxJson
    {
        // The `vfx` block belonging to `owner` ITSELF, never one nested inside
        // its elements.
        //
        // The distinction is not academic: prismatic_orb authors no skill-level
        // vfx and its Water element authors a whole one, so a plain
        // ObjectFor(skill, "vfx") -- which finds the first match at any depth --
        // reports the element's block as the skill's, counting the same cast
        // twice and attributing it to a key that is not in the file.
        internal static SpellPresentation OwnVfx(string owner) =>
            Presentation(TopLevelObject(owner, "vfx"));

        // A vfx block, as the presentation the game resolves it to.
        internal static SpellPresentation Presentation(string block)
        {
            if (block == null) return null;

            var vfx = new SpellPresentation();
            Fill(vfx, block);
            return vfx;
        }

        // ---- the walk ------------------------------------------------------------

        private static void Fill(object target, string block)
        {
            string scalars = TopLevelOnly(block);

            foreach (var field in target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var kind = field.FieldType;

                // SCALARS ARE READ AT THE BLOCK'S OWN DEPTH ONLY. JsonBlocks
                // finds the first occurrence of a key at ANY depth, and `path`
                // is a key on the presentation AND on every layer -- so a
                // presentation that authors layers and no path of its own would
                // otherwise report its first layer's path as the legacy
                // single-block one and be refused as authoring both.
                if (kind == typeof(string))
                {
                    string value = JsonBlocks.String(scalars, field.Name);
                    if (value != null) field.SetValue(target, value);
                }
                else if (kind == typeof(float))
                {
                    double? value = JsonBlocks.Number(scalars, field.Name);
                    if (value.HasValue) field.SetValue(target, (float)value.Value);
                }
                else if (kind == typeof(int))
                {
                    double? value = JsonBlocks.Number(scalars, field.Name);
                    if (value.HasValue) field.SetValue(target, (int)value.Value);
                }
                else if (kind == typeof(bool))
                {
                    bool? value = JsonBlocks.Bool(scalars, field.Name);
                    if (value.HasValue) field.SetValue(target, value.Value);
                }
                else if (kind == typeof(SpellLayer[]))
                {
                    var layers = new List<SpellLayer>();
                    foreach (string one in JsonBlocks.ObjectsInArray(block, field.Name))
                    {
                        var layer = new SpellLayer();
                        Fill(layer, one);
                        layers.Add(layer);
                    }

                    if (layers.Count > 0) field.SetValue(target, layers.ToArray());
                }
                else if (kind.IsClass && kind != typeof(object))
                {
                    string nested = JsonBlocks.ObjectFor(block, field.Name);
                    if (nested == null) continue;

                    object value = field.GetValue(target) ?? Activator.CreateInstance(kind);
                    Fill(value, nested);
                    field.SetValue(target, value);
                }
                else
                {
                    // LOUD RATHER THAN SKIPPED. A field of a kind this cannot
                    // decode is a field the pins reading this would stop
                    // measuring, silently, on the day it was added.
                    throw new NotSupportedException(
                        $"SpellVfxJson cannot read {target.GetType().Name}.{field.Name} " +
                        $"of type {kind.Name}. Teach it that kind rather than leaving the field " +
                        "unread -- every fixture built on this would stop checking it.");
                }
            }
        }

        // The {...} value of `key` at `block`'s OWN depth, or null.
        //
        // Anchored through the masked copy and then read out of the ORIGINAL,
        // which is what the length-preserving mask below is for: the mask says
        // WHERE the key at this depth is, the original still holds the object
        // that key names.
        private static string TopLevelObject(string block, string key)
        {
            if (block == null) return null;

            int at = TopLevelOnly(block).IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            return at < 0 ? null : JsonBlocks.ObjectFor(block.Substring(at), key);
        }

        // `block` with every nested object and array blanked to spaces, so a
        // first-occurrence key lookup answers about THIS object rather than
        // about something inside it.
        //
        // BLANKED RATHER THAN DELETED, so every index still means what it means
        // in the original -- which is what lets TopLevelObject find a key at
        // this depth and then read the real object at that offset.
        //
        // Cheaper and less breakable than a depth-aware lookup per key: the
        // brace/string scan is the part that is easy to get subtly wrong, and
        // it is written once here rather than once per accessor.
        private static string TopLevelOnly(string block)
        {
            var kept = new StringBuilder(block.Length);
            int depth = 0;
            bool inString = false;
            bool escaped = false;
            bool keepingString = false;

            foreach (char c in block)
            {
                if (inString)
                {
                    kept.Append(keepingString ? c : ' ');
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }

                if (c == '{' || c == '[')
                {
                    depth++;
                    kept.Append(depth <= 1 ? c : ' ');
                    continue;
                }

                if (c == '}' || c == ']')
                {
                    kept.Append(depth <= 1 ? c : ' ');
                    depth--;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    keepingString = depth <= 1;
                    kept.Append(keepingString ? c : ' ');
                    continue;
                }

                kept.Append(depth <= 1 ? c : ' ');
            }

            return kept.ToString();
        }
    }
}
