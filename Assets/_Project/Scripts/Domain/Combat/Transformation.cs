using System;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Combat
{
    // HOW A BLOW LANDS WHILE THE FORM IS WORN -- authored, on the form, in
    // content, and read by nothing that knows which form it belongs to.
    //
    // "Some hit markers when Shawn hits as the black ram, some cool OOMPH
    // behind his hits." The obvious shape for that is a branch on the Black
    // Ram somewhere in the view, and it is the shape docs/CODE_STANDARDS.md
    // "Build the model" exists to refuse: a second form would be a second branch, and
    // no skill id or character id belongs in code.
    //
    // WHY IT SITS ON THE TRANSFORM RATHER THAN ON THE SKILLS. The holder's
    // plain Attack, his headbutt and a Trample he never authored all have to
    // punctuate harder while the mode runs, and none of them knows the mode
    // exists. What they have in common is the TRANSFORMATION, which is already
    // the thing that arrives and leaves with the mode -- the same argument
    // TransformGrant.splashPercent's own comment makes about why the splash is
    // not a talent.
    //
    // THREE STATEMENTS ABOUT ONE BLOW, and they are three fields rather than
    // one derived number on purpose. HitStop.Response exists so the stop, the
    // stage kick and the squash cannot disagree about how hard a blow was --
    // but that is about one blow's own WEIGHT, and these are floors under it.
    // A convergence that reads as fast and weightless (the mage's, when its
    // strand lands) wants a loud contact effect with very little freeze, which
    // a single number cannot say.
    [Serializable]
    public class TransformHitCue
    {
        // The contact effect, in exactly the vocabulary `vfx` uses -- a
        // layered SpellPresentation, resolved and validated by the same
        // SpellLayerRules/SpellPresentationPaths checks (see
        // SkillEntryResolver.TryResolveTransform).
        //
        // PLAYED ALONGSIDE the skill's own presentation, never instead of it:
        // a headbutt that draws something draws both. See CombatBeat.FormVfx
        // for why that is a second slot on the beat rather than a merge.
        public SpellPresentation vfx = new SpellPresentation();

        // A FLOOR on the stage kick, 0..1, exactly like a skill's own `shake`
        // -- FightBeatPlayer.ShakeStrength already takes the larger of the
        // blow's weight and whatever floor the beat authored, so this needs no
        // new rule, only a second place a floor can come from.
        public float shake;

        // A FLOOR on the hit-stop, in SECONDS, because that is the unit
        // HitStop.SecondsFor answers in and the only unit an author can check
        // against its own MinSeconds/MaxSeconds. Clamped to HitStop.MaxSeconds
        // at record time: past that the freeze reads as a hitch rather than as
        // weight, and that ceiling is a decision the house already made once.
        public float hitStopSeconds;

        // Authored at all. A form that states none of the three punctuates
        // exactly as it did before this existed.
        public bool IsAuthored =>
            (vfx != null && vfx.HasArt) || shake > 0f || hitStopSeconds > 0f;
    }

    // The AUTHORED half: what a transform grants, as typed into skills.json
    // and carried on the SkillDefinition that hands it out.
    //
    // A nested block rather than five more loose fields on SkillDefinition,
    // because these five only ever mean anything together — an authored
    // speedPercent on a skill that does not transform is a typo the resolver
    // can then name, instead of a number sitting quietly in the asset doing
    // nothing. Exactly one skill uses it today (Black Ram Mode); the Lamb's
    // and mage's convergences are the same shape when their strands land.
    [Serializable]
    public class TransformGrant
    {
        // What the combat log and the plate call it while it is running.
        public string displayName = "";

        // Base duration in the holder's own turns, before Wrath's extensions.
        public int turns;

        // Percent bonuses applied on entry and taken back exactly on exit —
        // see Transformation.Enter/Exit for why the deltas are stored rather
        // than recomputed.
        public int attackPercent;
        public int speedPercent;

        // Temporary health as a percent of max, expiring with the transform.
        // Temporary ON PURPOSE: permanent max health would fight the Ram's
        // own low-health generator.
        public int temporaryHealthPercent;

        // What fraction of a landed hit spills onto the enemies either side
        // of the target, while the transform is running. 0 for a transform
        // that does not splash.
        //
        // A property of the TRANSFORM rather than a TalentEffectType,
        // unlike every other rule the Black Ram grants, because it is not
        // something a node hands you — it is part of what the mode IS
        // (handoff §6.4), and it arrives and leaves with the mode. Putting
        // it in the talent vocabulary would have meant a rule that is only
        // ever true while an unrelated system is active, which is exactly
        // the kind of hidden coupling the closed enum is meant to avoid.
        public int splashPercent;

        // WHOSE ART THE HOLDER WEARS WHILE THIS IS RUNNING — a
        // Resources-relative stance folder ("Characters/sheep_black_ram"),
        // empty for a transform that only changes the numbers.
        //
        // A FOLDER, not a set of stance names: the form is a whole actor, so
        // every stance name the fight already asks for ("cast", "hurt",
        // "victory", "defeated") resolves inside it, and its own
        // StanceManifest entry re-lays the figure out — the ram's canvas is
        // not Shawn's 540x370 and its feet are not on his ground line. That
        // is why this is one string rather than a per-stance map: a second
        // actor already IS a per-stance map, on disk, with a manifest entry.
        //
        // Missing on disk degrades to the holder's own art (see
        // FightController.WearForm), the house posture everywhere else.
        public string spritePath = "";

        // HOW THE HOLDER'S BLOWS LAND WHILE THIS RUNS -- see TransformHitCue.
        // Never null, so a grant that authors nothing still answers
        // IsAuthored rather than needing a guard at every read.
        public TransformHitCue hit = new TransformHitCue();

        public bool IsAuthored => turns > 0;
    }

    // A timed transform a combatant is currently under — Shawn's Black Ram
    // Mode today, and the shape the other two paths' convergences will take.
    //
    // Holds the DELTAS it applied rather than the percentages it was asked
    // for. Entering multiplies Attack by 1.5 and Speed by 1.3; leaving has to
    // undo exactly that, and recomputing the inverse from the percentages
    // does not round-trip through integer arithmetic (a 7 Attack becomes 11,
    // and 11/1.5 is 7.33). Storing what was actually added is the only way
    // out that is exact — and it stays exact even if something else modifies
    // Attack while the transform is running.
    //
    // Temporary health is granted the same way and matters more. It is
    // temporary ON PURPOSE (handoff §6.4): it saves him during the window and
    // then drops him straight back into the wounded band where his engine
    // runs hot. Permanent max health would fight his own generator, which is
    // why there are no max-HP nodes anywhere in the reworked tree.
    public sealed class Transformation
    {
        // What the player sees this called. Carried on the instance rather
        // than hardcoded so the Lamb's and the mage's convergences reuse this
        // type rather than each growing a parallel one.
        public readonly string DisplayName;

        // Turns of the HOLDER's own remaining. Counts down at the start of
        // each of their turns, the same unit ActiveStatus.TurnsRemaining uses
        // and for the same reason (see its own comment): anything else makes
        // a transform quietly last longer on a slow combatant.
        public int TurnsRemaining;

        // How much of each stat this transform added, for exact reversal.
        public readonly int AttackBonus;
        public readonly int SpeedBonus;
        public readonly int TemporaryHealth;

        // Percent of a landed hit that spills onto the target's neighbours
        // while this is running — see TransformGrant.splashPercent.
        public readonly int SplashPercent;

        // The stance folder the holder is DRAWN from while this runs, "" for
        // "keep their own art" — see TransformGrant.spritePath. Read by the
        // view in exactly two places: the beat that records the change, and
        // the post-playback resync that puts every figure back on live state.
        public readonly string SpritePath;

        // HOW THIS FORM'S BLOWS LAND -- the authored block, carried whole
        // rather than unpacked into three more readonly scalars, for the
        // reason TransformGrant itself is a block: these only ever mean
        // anything together, and a fourth field on the cue would otherwise be
        // a fourth field here and a fourth argument to Enter.
        //
        // Never null. An unauthored form carries an empty cue, which every
        // reader already answers with "do what you did before".
        public readonly TransformHitCue Hit;

        // Turns already added by Wrath T2's on-kill extension, against its
        // own cap. Tracked here rather than on the combatant because it is
        // meaningless outside a running transform and has to reset with it.
        public int ExtensionsGranted;

        // Set once the capstone's health gate has been met during THIS
        // transform. A permanent transform stops ticking down at all rather
        // than being handed a huge duration — a number the UI would then have
        // to render as "47 turns left", which reads as a bug rather than as
        // "this is who he is now".
        public bool IsPermanent;

        public Transformation(string displayName, int turns, int attackBonus, int speedBonus,
            int temporaryHealth, int splashPercent = 0, string spritePath = "",
            TransformHitCue hit = null)
        {
            Hit = hit ?? new TransformHitCue();
            DisplayName = string.IsNullOrEmpty(displayName) ? "Transformed" : displayName;
            TurnsRemaining = Math.Max(1, turns);
            AttackBonus = Math.Max(0, attackBonus);
            SpeedBonus = Math.Max(0, speedBonus);
            TemporaryHealth = Math.Max(0, temporaryHealth);
            SplashPercent = Math.Max(0, splashPercent);
            SpritePath = spritePath ?? "";
        }

        // Applies the transform's grants to a combatant and hands back the
        // instance to store on them. Both halves in one place so nothing can
        // apply the bonuses without recording how to take them away.
        //
        // Percentages are of the combatant's CURRENT values at the moment of
        // entry, which is the reading that keeps a transform worth taking
        // late in a run: a Ram who has found a better weapon transforms into
        // a proportionally bigger ram.
        public static Transformation Enter(CombatantState combatant, string displayName, int turns,
            int attackPercent, int speedPercent, int temporaryHealthPercentOfMax, int splashPercent = 0,
            string spritePath = "", TransformHitCue hit = null)
        {
            int attackBonus = combatant.Attack * Math.Max(0, attackPercent) / 100;
            int speedBonus = combatant.Speed * Math.Max(0, speedPercent) / 100;
            int temporary = combatant.MaxHealth * Math.Max(0, temporaryHealthPercentOfMax) / 100;

            var transformation = new Transformation(displayName, turns, attackBonus, speedBonus, temporary,
                splashPercent, spritePath, hit);

            combatant.Attack += transformation.AttackBonus;
            combatant.Speed += transformation.SpeedBonus;
            combatant.MaxHealth += transformation.TemporaryHealth;
            combatant.CurrentHealth += transformation.TemporaryHealth;

            combatant.Transformation = transformation;
            return transformation;
        }

        // Takes every grant back. Current health is clamped to the restored
        // maximum rather than reduced by the granted amount, which is what
        // makes the temporary pool behave like temporary health should: if it
        // was spent absorbing hits it is already gone, and if it was not, the
        // unspent surplus is what evaporates. Either way he lands back in the
        // wounded band, never below where the fight actually left him.
        public static void Exit(CombatantState combatant)
        {
            var transformation = combatant?.Transformation;
            if (transformation == null)
            {
                return;
            }

            combatant.Attack = Math.Max(0, combatant.Attack - transformation.AttackBonus);
            combatant.Speed = Math.Max(0, combatant.Speed - transformation.SpeedBonus);
            combatant.MaxHealth = Math.Max(1, combatant.MaxHealth - transformation.TemporaryHealth);
            combatant.CurrentHealth = Math.Min(combatant.CurrentHealth, combatant.MaxHealth);

            combatant.Transformation = null;
        }
    }
}
