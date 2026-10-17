using System.Collections.Generic;
using UnityEngine;

namespace PrincesPalace
{
    // Discovers an f0..fN frame sequence under a Resources folder.
    //
    // StanceAnimationLibrary (actor stances), SpellVfxPlayer (spell/ability
    // effects) and HubBuildingAnimator (hub buildings) all probe
    // "{path}/f{i}" until null, and Resources.LoadAll is wrong here.
    //
    // ONLY frame discovery is shared. The three PLAYBACK classes stay
    // separate on purpose, and HubBuildingAnimator's own header argues the
    // case: a spell plays once and a building loops forever, so "bolting a
    // loop flag onto the spell player would have given one class two
    // lifecycles and two meanings for 'done'". That reasoning is about
    // playback, and none of it applies to reading files off disk.
    public static class FrameSequenceLoader
    {
        // The cap only stops a typo'd path (or an accidental stance FOLDER
        // with no f0 in it) from probing Resources forever. It is a guess at
        // "more than anything real", not a considered limit.
        public const int MaxFrames = 64;

        // Keyed by folder path, which holds no object references — so unlike
        // a cache keyed by Sprite this cannot pin destroyed textures alive
        // across a scene load.
        private static readonly Dictionary<string, Sprite[]> _cache = new Dictionary<string, Sprite[]>();

        // Returns null for a blank path and an empty array for a path that
        // simply has no frames under it. That two-mode result is not a design
        // preference — it is the contract all three callers already had, and
        // HubCompositionTests pins both halves of it
        // (AnUnknownBuildingPath_LoadsNothingRatherThanThrowing). This
        // extraction is meant to be behaviour-neutral, so the distinction is
        // preserved rather than tidied away; collapsing it to never-null is a
        // reasonable change but belongs in its own commit where it is visible.
        //
        // Resources.LoadAll is deliberately NOT used: it returns filename
        // order, which puts f10 before f2 the moment a sequence passes ten
        // frames — the same trap an explicit sortOrder guards against for
        // content (CLAUDE.md gotcha 4).
        public static Sprite[] Load(string resourcesFolder)
        {
            if (string.IsNullOrWhiteSpace(resourcesFolder))
            {
                return null;
            }

            if (_cache.TryGetValue(resourcesFolder, out var cached))
            {
                return cached;
            }

            var frames = new List<Sprite>();
            for (int i = 0; i < MaxFrames; i++)
            {
                var sprite = Resources.Load<Sprite>($"{resourcesFolder}/f{i}");
                if (sprite == null)
                {
                    break;
                }

                frames.Add(sprite);
            }

            var result = frames.ToArray();
            // Cached even when EMPTY, which all three callers independently
            // decided was right: a path with no art costs one failed probe,
            // not one per repaint / per cast / per Hub visit for the rest of
            // the session.
            _cache[resourcesFolder] = result;
            return result;
        }
    }
}
