using System.Collections.Generic;

namespace PrincesPalace.Domain.Content
{
    // THE TWO POINTS EVERY DRAWN ACTOR SHIPS: where its head is and where its
    // body is, as fractions of the idle drawing's opaque box on the sprite as
    // drawn (before any facing mirror): X from the left edge, Y from the
    // bottom edge. A monster carries them flat on RawEnemyEntry; a transform
    // form carries them flat on TransformGrant, because a form is a whole
    // actor with its own drawing and its own box.
    //
    // HEAD: `target-head` places there, `fit: head` sizes by headSize (head
    // height over box height). BODY: the torso aim point, where `target-centre`,
    // `above`, `sky` and the melee contact burst aim. The box centre is wrong
    // for a long-bodied or tall-necked animal, which is why the point exists.
    //
    // -1 is the only unauthored value. Unauthored reads as the TargetBody
    // fallbacks, and ActorMarksRules refuses it on any entry with a sprite, so
    // a fallback is only ever what a placeholder with no art gets.
    public readonly struct ActorMarks
    {
        public readonly float HeadX;
        public readonly float HeadY;
        public readonly float HeadSize;
        public readonly float BodyX;
        public readonly float BodyY;

        public ActorMarks(float headX, float headY, float headSize, float bodyX, float bodyY)
        {
            HeadX = headX;
            HeadY = headY;
            HeadSize = headSize;
            BodyX = bodyX;
            BodyY = bodyY;
        }

        public static ActorMarks Unauthored => new ActorMarks(-1f, -1f, -1f, -1f, -1f);
    }

    // THE ONE CHECK for an actor's marks, shared by the monster resolver and
    // the transform resolver so a form is refused exactly as a base is.
    public static class ActorMarksRules
    {
        public static bool Check(string label, bool drawn, string spritePath, float headX, float headY,
            float headSize, float bodyX, float bodyY, out string error)
        {
            error = null;
            if (headX != -1f && (headX < 0f || headX > 1f))
            {
                error = $"{label}: headX {headX} must be -1 (unauthored) or between 0 and 1.";
            }
            else if (headY != -1f && (headY < 0f || headY > 1f))
            {
                error = $"{label}: headY {headY} must be -1 (unauthored) or between 0 and 1.";
            }
            else if (headSize != -1f && (headSize <= 0f || headSize > 1f))
            {
                error = $"{label}: headSize {headSize} must be -1 (unauthored) or above 0 and at most 1.";
            }
            else if (bodyX != -1f && (bodyX < 0f || bodyX > 1f))
            {
                error = $"{label}: bodyX {bodyX} must be -1 (unauthored) or between 0 and 1.";
            }
            else if (bodyY != -1f && (bodyY < 0f || bodyY > 1f))
            {
                error = $"{label}: bodyY {bodyY} must be -1 (unauthored) or between 0 and 1.";
            }

            if (error != null) return false;

            // A DRAWN ACTOR MUST SAY WHERE ITS HEAD AND BODY ARE. The
            // fallbacks (head 0.5 / 0.8 / 0.3, body 0.5 / 0.5) are only there
            // for a placeholder with no sprite; on a real drawing they put a
            // crown or a burst on the shoulder and nothing in the build says
            // so. Every field or the build stops -- partial authoring is the
            // same mistake.
            if (!drawn) return true;

            var missing = new List<string>();
            if (headX == -1f) missing.Add("headX");
            if (headY == -1f) missing.Add("headY");
            if (headSize == -1f) missing.Add("headSize");
            if (bodyX == -1f) missing.Add("bodyX");
            if (bodyY == -1f) missing.Add("bodyY");
            if (missing.Count == 0) return true;

            error = $"{label}: has a spritePath ('{(spritePath ?? "").Trim()}') but {string.Join(", ", missing)} " +
                    "unauthored. Every drawn monster and every drawn form needs its head and body point: read " +
                    "them off its idle.png as fractions of the opaque box, write headX/headY/headSize/bodyX/bodyY " +
                    "into the json, then run tools/slice_actor_sheet.py (its --recipe replay) and check " +
                    "head_check.png (docs/ART_PIPELINE.md 5a, step 5).";
            return false;
        }
    }
}
