namespace PrincesPalace.Domain.Combat.Session
{
    // Balance constants for a fight, moved out of v1's FightController.
    //
    // In Domain because they are combat rules, not presentation. How often a
    // relic fires is the same kind of fact as how much armour softens a hit.
    // A relic's HEADLINE number is its Magnitude in relics.json
    // (EffectKind); what stays here is the secondary shape around it.
    public static class FightTuning
    {
        // ---- counting relics -----------------------------------------------------
        //
        // "Every Nth" is counted PER FIGHT and per character, not per run: a
        // counter carried between fights would make the first cast of a fight
        // arbitrarily lucky depending on how the last one ended, which is not a
        // thing a player can plan around.
        //
        // The percentages (the relics' Magnitude) are of the action's OWN
        // BASE. See FightSession.Potency.
        public const int ChargingCrystalEvery = 4;

        // ---- Bell in the Fog (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.4) ----------
        //
        // Toll of the Flock: every TollOfTheFlockEvery-th turn the bearer OPENS
        // in a fight, a packet of the relic's Magnitude % of their current
        // attack to every living enemy, times the multiplier while transformed.
        // Owner's contract: a small bonus, under ~5% of the bearer's damage
        // untransformed and ~10% on a Black Ram build (measured by the bot, M8a;
        // the magnitude and this multiplier are what moves).
        public const int TollOfTheFlockEvery = 3;
        public const float TollOfTheFlockTransformMultiplier = 1.5f;

        // An enemy's per-round rally (RawEnemyEntry.rallyPerRound) is a
        // FallingOffStacks list under this key whose stacks never fall off
        // (FightSession.Rounds.RallyStackLifetime).
        public const string RallyStackKey = "rally_per_round";

        // ---- speed relics --------------------------------------------------------
        //
        // The Slippers accumulate and the Pipe does not, which is the whole
        // difference between them: one rewards a long fight of swinging, the
        // other rewards casting at the right moment. Same stat, opposite shape.
        public const int SlippersCapPercent = 40;

        public const int PipeTurns = 1;

        // ---- the necklace --------------------------------------------------------
        //
        // Full value at a quarter health, nothing at full, and a straight ramp
        // between. The floor is a QUARTER rather than zero because a bonus that
        // only pays at 1hp pays on the turn you die. The damage half's ceiling
        // is the relic's Magnitude.
        public const int NecklaceMaxSpeedPercent = 20;
        public const int NecklaceFloorHealthPercent = 25;

        // ---- lucky deck ------------------------------------------------------------
        //
        // Three effects, one roll per swing, equal odds -- the relic's whole
        // appeal is not knowing which one you get.
        public const int LuckyDeckHealHealthPercent = 5;
        public const int LuckyDeckHealManaPercent = 10;
        public const int LuckyDeckSplashPercent = 25;
        public const int LuckyDeckSlowPercent = 30;
        public const int LuckyDeckSlowTurns = 1;

        public const int LongCountEvery = 3;

        // ---- relics ----------------------------------------------------------

        // ---- wards ------------------------------------------------------------

        // HOW LONG A SKILL'S WARD STANDS when its row authors no `wardTurns`
        // -- ONE of the WEARER'S own turns, counted down at the END of that
        // turn (StatusEffects.TickWardsAtTurnEnd) rather than at its start
        // like every other duration in the game. See that file's WARDS header
        // for why the exception exists: it is the difference between a shield
        // the player can see on the turn it protects and one they cannot.
        //
        // One rather than "until something hits it", which is what every ward
        // in this game used to be (they were all applied at 999 turns). A
        // shield POOL survives small hits, so without a clock one Bulwark
        // would stand for the rest of the dungeon and the skill would stop
        // being a decision about WHEN.
        //
        // ONE RATHER THAN TWO, owner 2026-09-16, and it is the number that
        // makes stacking a decision instead of an accumulator: wards add up
        // now, so a two-turn window let a free action bank shields faster than
        // anything could spend them. Every one of the five ward skills takes
        // this default -- none of them authors `wardTurns` -- and Magical
        // Shield takes it too. The two standing relic wards (Sparring Buckler,
        // Runic's mana conversion) opt out by authoring the whole fight
        // (RelicWardDurationTurns) instead. See StatusEffects' own WARDS
        // header for the model this belongs to.
        public const int DefaultWardTurns = 1;

        // 99 turns is "for the rest of the fight" spelled as a duration --
        // the promise the two standing relic wards make (Sparring Buckler and
        // Runic's mana conversion), and deliberately NOT the one-turn clock a
        // skill's ward runs on. Magical Shield is not one of them: its ward
        // dissipates on DefaultWardTurns, because a free ward that never
        // lapsed was too strong.
        //
        // Raising it again while it stands DOES compound: StatusEffects
        // .ApplyWard always adds an entry and wards drain soonest-first. That
        // has been true since the shield model landed on 2026-09-16; the line
        // that used to sit here still described the percentage ward it
        // replaced.
        public const int RelicWardDurationTurns = 99;

        // ---- item modifiers ---------------------------------------------------

        // How many turn-order slots Hardened's on-hit push knocks a target
        // back -- the same unit TurnOrder.PushBack itself takes. One,
        // matching the Black Ram's Headbutt (the talent this reuses the
        // mechanism from): "one slot later" needs no calibration against the
        // scheduler's own arbitrary charge units (see PushBack's own header).
        public const int ModifierPushBackSlots = 1;

        // RUNIC'S MANA->WARD CONVERSION IS DELIBERATELY WEAK -- the plan's
        // own words for it. It turns a resource the wearer was merely
        // HOLDING (not spent, not committed to anything) into flat damage
        // reduction at the start of every one of their own turns, for free.
        // A generous rate here would make "hoard mana, never cast" the
        // correct answer to "how do I tank", which is backwards for a
        // resource whose entire other purpose is being spent. SHIELD POINTS
        // granted per point of UNSPENT mana at turn start -- the same rate
        // the number has always carried, now read in the unit the ward model
        // actually uses.
        public const float RunicWardConversionRate = 0.25f;

        // RunicWardPointsCap IS GONE, owner 2026-09-16 (AUDIT #154). It was 20
        // -- one leg-two enemy hit's worth -- and it held while a new ward
        // REPLACED the standing one. Wards stack now, and the owner's answer
        // to "a Runic wearer banks 20 points a turn forever" was that this is
        // the design: no cap, on anything. There is no ward ceiling anywhere
        // in this file, and that absence is deliberate rather than an
        // oversight, which is why it is written down.

        // ---- chilled (Phase D2, item-modifier plan) ----------------------------
        //
        // Frosty's own chill, on a successful ChilledOnHitChancePercent
        // proc. NOT authored per-modifier -- see that enum member's own
        // comment for why the chance is the one number modifiers.json
        // tunes and this pair stays a fixed constant, the same split
        // ModifierPushBackSlots already draws for Hardened's push.
        //
        // Deliberately its OWN numbers rather than reusing
        // LuckyDeckSlowPercent/LuckyDeckSlowTurns even though both procs
        // now land through the identical ApplyChilled call -- Frosty is a
        // droppable item modifier balanced against the item tier the
        // way every other on-hit rider in this table is (Vampiric's
        // LifestealPercent, Hardened's push chance), Lucky Deck is a fixed
        // relic with its own long-shipped balance; tying the two together
        // would mean retuning one every time the other needed to move.
        public const int ChilledOnHitSpeedPercent = 20;

        // One affected turn on the turn-end clock; ChilledOnHitTurns_Pin
        // (FightTuningItemModifierConstantsTests) pins it.
        public const int ChilledOnHitTurns = 1;

        // ---- rooted (Phase D3, item-modifier plan) -----------------------------
        //
        // Sylvan's own root, on a successful RootChancePercent proc. Same
        // split as ChilledOnHitSpeedPercent/ChilledOnHitTurns just above:
        // the CHANCE is the one number modifiers.json tunes, this stays a
        // fixed constant. No sibling "strength" constant -- Rooted has no
        // Magnitude of its own to author (EffectKind.RootChancePercent's
        // own comment), only a duration.
        // One affected turn on the turn-end clock; RootOnHitTurns_Pin
        // (FightTuningItemModifierConstantsTests) pins it.
        public const int RootOnHitTurns = 1;

        // ---- the balance pass -- mechanics a-g, and the relics built on them ----

        // World Ender's Crown: crossing below the relic's Threshold fears
        // every enemy for this many turns.
        public const int WorldEndersCrownFearTurns = 1;

        // Cursed Idol: each landed hit on an enemy stacks a resistance shred
        // of the relic's Magnitude, up to this many stacks, each falling off
        // CursedIdolStackTurns after it was added.
        public const int CursedIdolMaxStacks = 5;
        public const int CursedIdolStackTurns = 3;
        public const string CursedIdolStackKey = "cursed_idol";

        // Rampaging Bull's Horn: how long its damage reduction lasts. One affected
        // turn on the turn-end clock.
        public const int BullsHornDurationTurns = 1;

        // ---- balance pass 2 -- see FightSession.BalanceRelics2 ----------------

        // Ice Fingernail: 10% slow per stack (FallingOffStacks), up to 4
        // stacks (40%), each stack falling off 5 turns after it lands.
        public const int IceFingernailPercentPerStack = 10;
        public const int IceFingernailMaxStacks = 4;

        // Longer than Cursed Idol's own 3 -- a slow that fell off after
        // three of the TARGET's own turns would rarely hold more than one
        // or two stacks at once against an attacker swinging every one of
        // its own turns, since a fresh stack and a tick both land once per
        // attack/reply cycle. Five turns is what actually lets four stacks
        // (the stated cap) coexist under ordinary 1:1 swinging.
        public const int IceFingernailStackTurns = 5;
        public const string IceFingernailStackKey = "ice_fingernail";

        // Sparring Saber: how long the Move's Speed lasts.
        public const int SparringSaberSpeedTurns = 1;

        // ---- combat-lock keys (mechanic f) --------------------------------------
        //
        // BARE NAMES NOW, and the OWNER is passed to CombatLocks separately.
        // These used to be `...LockKeyFor(string combatantId)` helpers that
        // built "name:" + the LEDGER id, because CombatLocks was one flat set
        // of strings and the owner had nowhere else to go. The ledger id was
        // the wrong thing to hand it -- it groups every combatant of one enemy
        // type under one row on purpose -- and the suffix convention that made
        // it work was a convention rather than a rule. See CombatLocks' own
        // header for both halves. What is left here is what these constants
        // always were: the NAME of the gate.
        public const string PhoenixEggLockKey = "phoenix_egg";
        public const string BerserkersVestLockKey = "berserkers_vest";
        public const string SparringBucklerLockKey = "sparring_buckler";
        public const string DancersAnkletLockKey = "dancers_anklet";
        public const string InconspicuousKeyLockKey = "inconspicuous_key";

        // The PRIMARY pool's gainOnAttack, which fires from the damage funnel
        // and therefore needs a "once per damaging action" boundary the funnel
        // itself cannot supply: it runs once per TARGET, so a three-enemy
        // sweep would otherwise pay three times.
        //
        // Once per TURN is the same boundary here, and not an approximation of
        // it: an action ends the actor's turn, so the only way to act twice in
        // one turn is to have been granted an extra one -- and an extra turn
        // runs GrantTurnStart, which resets this lock, which is exactly the
        // answer a Trample chain should get. Dual Wield's second swing shares
        // the turn and so grants nothing extra, matching the rule the
        // signature pool has always had at the Attack verb.
        //
        // THE ONE ENEMY-SIDE LOCK IN THE GAME, which is what made the ledger-id
        // owner a live hazard rather than a tidy one: three rats shared it.
        // Inert only because every shipped enemy authors gainOnAttack 0.
        public const string PrimaryPoolAttackGainLockKey = "primary_pool_attack_gain";
    }
}
