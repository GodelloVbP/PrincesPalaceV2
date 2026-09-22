namespace PrincesPalace.Domain.Combat.Session
{
    // The fight HUD's fixed capacities, in ONE place that both the screen tree
    // and the combat session read.
    //
    // This exists because of a specific v1 bug: the turn-order snapshot was
    // sized by `initiativeIcons.Length` -- a UI ARRAY LENGTH reaching into
    // combat logic to decide how far ahead to simulate. Domain cannot see a
    // Unity array, and it should not want to: the number is a design decision
    // about the HUD, so it is declared here and passed in.
    //
    // Nothing here is derived from content at build time on purpose. These are
    // capacities the layout reserves; the RUNTIME fill count is a separate
    // thing, guarded at the Domain seam by layout functions that take a count,
    // and by content-side pins asserting no character or signature can exceed
    // what is reserved.
    public static class FightHudSpec
    {
        // How far ahead the turn order is shown, and therefore how far the
        // encounter is asked to project. v1: InitiativeTrackerSlots.
        public const int InitiativeSlots = 6;

        // Pooled, genuinely runtime-positioned - one of only two elements on
        // this screen that earns the pool audit exemption.
        //
        // 12, NOT 6 -- v1's number, carried over without being re-derived
        // against what actually spends a pool this size. An AllEnemies cast
        // can land on all three enemy slots inside one beat sequence, and a
        // popup does not vanish the instant the next beat starts (LifeSeconds
        // outlives BeatGapSeconds) -- six was already tight for three
        // simultaneous hits with nothing else in flight. Doubled rather than
        // precisely counted: the pool is a display reservation rather than a
        // hard ceiling worth chasing exactly -- see
        // FightBeatPlayer.ShowAmount's own "dropped, not queued" fallback for
        // what happens on the rare frame that still runs out.
        public const int DamagePopups = 12;

        // HOW MANY COMBATANTS A SIDE CAN FIELD, and it is a rule rather than a
        // drawing detail.
        //
        // The stage has this many slots and the encounter roll draws this many
        // enemies at most, because a combatant with no slot is not merely
        // undrawn -- it is INVISIBLE AND STILL SWINGING. RefreshStage walks the
        // slots, so a fourth monster in a three-slot stage never appears while
        // the session goes on giving it turns, and the player takes damage from
        // something that is not on screen.
        //
        // Which is why this lives here, beside the session, rather than in the
        // screen: it bounds what combat may CREATE, and the stage having that
        // many slots is the consequence, not the cause.
        public const int StageSlotsPerSide = 3;

        // ---- what a cast may draw at once ---------------------------------------
        //
        // All three are RESERVATIONS, not counts -- and none of them is
        // trusted. SpellPoolCapacityTests counts
        // what the authored content of every skill and element actually asks
        // for, per band, and fails NAMING THE SKILL when it outgrows one. That
        // pin is what carries the weight; the numbers below are headroom over a
        // measurement rather than a measurement somebody re-did by hand, which
        // is the failure mode the hand-measured tab-width table already
        // demonstrated.

        // The `effects` band: everything drawn over the HUD and under the
        // damage numbers.
        //
        // The measured worst case is Cinderfault's three eruptions plus one
        // Water cast's three per-target layers overlapping as a tail -- six --
        // against a stage that can field StageSlotsPerSide combatants. Four per
        // slot leaves six clear of that, which covers a second overlapping cast
        // rather than only the one this plan authors.
        public const int SpellLayerRenderers = StageSlotsPerSide * 4;

        // The `ground` band: shared formation layers, behind every figure.
        //
        // TWO, AND FORCED BY OWNERSHIP RATHER THAN BY TASTE. A second
        // Cinderfault opening while the first fault is still cooling must get
        // its OWN member, because a cast taking over a live one is exactly the
        // restart per-cast ownership exists to remove. Two and not more, and
        // the arithmetic is checkable: a fault runs 0.78s while the shortest
        // possible gap between two beats opening is BeatHoldSeconds -- and the
        // real gap is larger, since hit-stop, the settle and BeatGapSeconds all
        // sit on top -- so two of those gaps already exceed one fault's life
        // and a third can never overlap the first.
        public const int SpellGroundRenderers = 2;

        // Detached droplets, in the same band as the sprite layers and in their
        // own pool node.
        //
        // One Water cast's steady state is rate * window = 10 alive during the
        // shed (every droplet's lifeMin exceeds the window, so all ten are)
        // plus an 18-drop burst at the cue: 28 at peak. 64 holds two
        // overlapping casts, which is the case the allocation measurement
        // records.
        public const int SpellParticles = 64;

        // One plate per enemy stage slot; v1 tied these together with
        // `EnemyPlateCount = EnemyStageSlots` and so does this.
        public const int EnemyPlates = StageSlotsPerSide;

        // WoolPips is GONE (2026-09-10). It reserved a 16-pip charge meter
        // on the acting character's card; the HUD column draws every party
        // member's signature as one numeric line ("Wool 3/10") on their own
        // plate now, which has no ceiling to reserve and no pip to hide past
        // the resource's own maximum. FightCapacityPinTests' pin went with
        // it -- there is nothing left for a signature capacity to overflow.

        // The detail column's MAXIMUM stat rows -- how many PHYSICAL row
        // slots the tree reserves, not how many any one card shows. The
        // canonical order FightHudModel can draw from is MANA, COST
        // (resource/health), COOLDOWN, POWER, DEFENSE, TARGET, EFFECT,
        // SCALES (eight), but FightHudModel.DetailPanel.Stats is COMPACT: a
        // stat that does not apply to what is hovered is not added at all
        // (2026-09-22 rework #2), so a skill using only three of the eight
        // paints a three-row card, not an eight-row one with five gaps.
        // FightController.Hud.cs's RefreshDetail paints panel.Stats[i] into
        // physical row i for i < Stats.Count and hides the rest -- the same
        // fixed-pool/toggle-visibility idiom FightSubmenuLayout's rows use.
        public const int DetailStatRows = 8;

        // Attack / Skill / Item / Hold Back. RUN was removed -- see
        // FightScreen.BuildVerbColumn's own comment -- rather than joining
        // HOLD BACK's old hidden-but-wired spot.
        public const int Verbs = 4;
    }
}
