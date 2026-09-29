using System;

namespace PrincesPalace.Domain.Content
{
    // One character skill, exactly as typed into skills.json. Same -1 / ""
    // sentinel convention as RawEnemyEntry and RawItemEntry: JsonUtility only
    // overwrites fields actually present in the source, so a C# initializer
    // surviving untouched is how the resolver distinguishes "the author
    // omitted this" from "the author wrote 0" — which matters here because 0
    // is a legitimate mana cost.
    [Serializable]
    public class RawSkillEntry
    {
        [ContentDoc("Stable identifier; written into save files and never renamed once used.")]
        public string id;
        [ContentDoc("The name shown on the skill's own button.")]
        public string displayName;
        [ContentDoc("Flavor text shown to the player; read by no formula.")]
        public string description = "";

        [ContentDoc("Editor-time path to this skill's icon (Assets/_Project/Art/...); empty means no art, and the slot hides rather than showing a placeholder. Used today only by bookOnly skills, whose spell-book art carries the glyph baked in.")]
        public string iconPath = "";

        // Which character this belongs to. Required: a skill with no owner
        // would be offered to everyone, and the whole point of this content
        // type is that a kit is a character's own.
        [ContentDoc("The character this skill belongs to; required so it is never offered to everyone.")]
        public string characterId = "";

        // The level at which it becomes available. 1 means "from the start".
        [ContentDoc("The character level this skill becomes available at; see notes for the bookOnly exception.")]
        public int unlockLevel = -1;

        // "DamageSingle" (default), "DamageAll", "HealSelf", "HealParty",
        // "RestorePartyMana" — matched case-insensitively against
        // Domain.Combat.SkillEffect.
        [ContentDoc("Which SkillEffect this casts, matched case-insensitively.")]
        public string effect = "";

        // "SingleEnemy", "AllEnemies", "Self", "Party". Defaults to whatever
        // the effect implies, so most entries never write it.
        [ContentDoc("Which SkillTargeting this hits; defaults to whatever the effect implies.")]
        public string targeting = "";

        [ContentDoc("Mana spent to cast; a skill must cost this and/or resourceCost.")]
        public int manaCost = -1;

        // How much of the owner's signature resource a cast consumes, and
        // whether it takes the lot instead. spendsAllResource still requires
        // resourceCost as a minimum, so a capstone cannot be fired on an
        // empty gauge for nothing.
        [ContentDoc("How much of the owner's signature resource a cast consumes.")]
        public int resourceCost = -1;
        [ContentDoc("Whether the cast takes the caster's entire signature resource instead of resourceCost.")]
        public bool spendsAllResource;

        // A CEILING ON "ALL" -- Shawn's Tuck In spends up to four banked
        // Wool and no more, so holding twelve does not make it three times
        // the ward. 0, the default, is no ceiling, which is every
        // spendsAllResource skill authored before this existed.
        // resourceCost is still the MINIMUM, so a capped skill has a floor
        // and a ceiling and scales between them.
        [ContentDoc("A ceiling on how much a spendsAllResource cast takes; 0 means no ceiling. Meaningless without spendsAllResource.")]
        public int resourceSpendCap;

        // THE PRIMARY-POOL TWIN OF spendsAllResource -- Bjorn's Second Wind
        // spends every point of Fury he has, and Fury is his PRIMARY pool,
        // not a signature resource. manaCost is the minimum the same way
        // resourceCost is for the signature version ("requires at least 25
        // Fury"), so a capstone still cannot be fired on an empty gauge.
        //
        // A SECOND FLAG RATHER THAN A WIDENING OF THE FIRST, because a skill
        // may cost both pools and "spends all" would then be ambiguous about
        // which one it emptied. Whichever flag is set also decides which
        // pool's spend feeds `power` -- see FightSession.CastSkill.
        [ContentDoc("Whether the cast takes the caster's entire PRIMARY pool (Fury, mana) instead of manaCost, with manaCost as the minimum.")]
        public bool spendsAllPrimary;

        // PER POINT OF THE POOL ACTUALLY SPENT, this many percent of the
        // CASTER'S OWN max health, added to a heal. Bjorn's Second Wind is
        // 1: a hundred Fury spent is a full heal, whatever his health bar
        // has grown to.
        //
        // A PERCENTAGE RATHER THAN A BIGGER `power`, because `power` is a
        // flat int and the authored promise is "at 100 Fury, a full heal" --
        // a flat number tracks that at exactly one point on the health
        // curve and drifts everywhere else. Read only by the three heal
        // effects; 0, the default, is every skill that has ever existed.
        [ContentDoc("Percent of the caster's own max health added to a heal per point of the pool actually spent; 0 means none.")]
        public int percentOfMaxHealthPerPoint;

        // A DAMAGE SKILL THAT STRIKES MORE THAN ONCE (Bjorn's Hack: one target,
        // two blows). Every blow is a full swing of its own -- its own dodge
        // and crit roll, its own pools hearing it, its own beat -- and the cast
        // is paid for once. 0 and 1 both mean a single blow, the default of
        // every skill authored before this existed.
        [ContentDoc("How many separate blows a DamageSingle cast strikes at its target (the cast is paid once); 0 or 1 means one.")]
        public int hitCount;

        // A FINISHER'S SCALING with the target's WOUNDS: each 1% of the
        // target's max health already missing adds this many percent to the
        // damage, before defences. Bjorn's Headsplitter is 100 (a target at
        // 20% health takes +80%). Read only by damage effects; 0 is none.
        [ContentDoc("Percent added to a damage skill's raw damage for each 1% of the target's max health already missing; 0 means none.")]
        public int damagePerMissingHealthPercent;

        // AND ITS PAYBACK: when the cast kills, this percent of the primary
        // pool it spent is returned. Read only by a spendsAllPrimary damage
        // skill; 0 is none.
        [ContentDoc("Percent of the primary pool a killing cast spent that is returned to the caster; 0 means none.")]
        public int refundsSpentOnKillPercent;

        // A DAMAGE SKILL THAT HEALS ITS CASTER for a share of what it deals
        // (Bjorn's Gorge: 30). Paid through the heal funnel, so Cursed Blood
        // converts it like any other heal. A talent may raise it for its own
        // skill (TalentEffectType.SkillLifestealPercent). 0 is none.
        [ContentDoc("Percent of the damage this skill deals that is healed back to its caster; 0 means none. Only a DamageSingle skill reads it.")]
        public int lifestealPercent;

        // A SKILL THAT CAN BE CAST ONCE PER FIGHT, whatever the caster's
        // cooldowns do (Bjorn's Cursed Blood). A cooldown counts turns and a
        // relic can shorten it; this cannot come back.
        [ContentDoc("The skill can be cast once per fight; the menu then shows it as used.")]
        public bool oncePerFight;

        // THE TWO NUMBERS OF A SELF WINDOW SKILL (Unbroken's regen and
        // Unstoppable, Cursed Blood's healing ban). windowTurns is a count of
        // the caster's own turns and is required by both effects;
        // regenPercentOfMaxHealth is Unbroken's regen per turn.
        [ContentDoc("How many of the caster's own turns the window this skill opens lasts. Required by an Unbroken or CursedBlood skill, refused on any other.")]
        public int windowTurns;

        [ContentDoc("Percent of the caster's max health the Regen an Unbroken skill opens heals each turn. Required by an Unbroken skill, refused on any other.")]
        public int regenPercentOfMaxHealth;

        // A DAMAGE SKILL THAT SPENDS THE CASTER'S PLANTED SHIELD (Bjorn's
        // Shield Bash: 30). The cast is refused while no shield is down, takes
        // it, and adds this percent of everything it soaked this placement to
        // the blow, before defences. 0 is none.
        [ContentDoc("Percent of the damage the caster's planted shield absorbed this placement that is added to the hit; the cast consumes the shield and needs one down. Only a DamageSingle skill reads it; 0 means none.")]
        public int plantedShieldBashPercent;

        // THE BUTTON THIS SKILL TAKES OVER (Plant the Shield replaces Brace).
        // A character who owns both sees only this one; the replaced skill
        // returns if this one is not owned.
        [ContentDoc("Id of a skill of the same character that this skill replaces on the menu once both are owned; empty means it replaces nothing.")]
        public string replacesSkillId = "";

        // A FLAT percent of the CASTER'S own max health, in shield points,
        // added to a Ward. Bjorn's Bulwark is 30: a shield worth roughly
        // three enemy hits at the start of a run, and still worth three of
        // them once his bar and theirs have both grown.
        //
        // NOT percentOfMaxHealthPerPoint above. That one multiplies by the
        // pool actually spent, which is right for Second Wind ("a hundred
        // Fury is a full heal") and wrong for Bulwark, whose fifty Fury is a
        // PRICE rather than a dial. Read only by Ward; 0, the default, is
        // every other skill.
        [ContentDoc("Percent of the caster's own max health added to a Ward's shield pool, flat; 0 means none. Ward only.")]
        public int percentOfCasterMaxHealth;

        // How many of the WEARER'S own turns a Ward stands before it times
        // out. 0, the default, means the house rule -- FightTuning.
        // DefaultWardTurns, ONE of them -- and 0 is what all five shipped ward
        // skills author, so the house rule is the only rule in play today.
        //
        // A ward used to be applied at 999 turns by every one of its callers,
        // because it was spent by the first hit that landed and a clock would
        // only ever have taken it away early. A shield pool survives small
        // hits AND stacks with the next one, so it needs a real duration or a
        // free-action ward would bank shields faster than anything could spend
        // them. Meaningless on anything but a Ward.
        [ContentDoc("How many of the wearer's own turns a Ward stands before expiring; 0 means the default of one.")]
        public int wardTurns;

        // A CAST THAT DOES NOT END THE TURN -- Shawn's Tuck In, and the
        // Fragile Lamb's Fleece Ward T3 talent, which sets the same flag
        // rather than being a second rule.
        //
        // ONCE PER TURN, enforced by FightSession rather than by an authored
        // number: a free action that could be taken twice is an unbounded
        // turn, and no skill has ever wanted a second one. The cast still
        // pays its cost, still plays its beat and still starts its cooldown
        // -- only the turn does not advance.
        [ContentDoc("Whether casting this does not end the caster's turn; at most one free action per turn.")]
        public bool freeAction;

        // Added per point of resource actually spent. Integer on purpose —
        // see SkillResolution.
        [ContentDoc("Added per point of resource actually spent when casting.")]
        public int power = -1;

        // A flat contribution before scaling: the whole amount for a heal,
        // an offset on top of Attack for damage.
        [ContentDoc("A flat contribution before scaling: the whole amount for a heal, an offset on top of Attack for damage.")]
        public int flatAmount = -1;

        // Damage only. The one line in Shawn's kit that answers a
        // high-Defense wall; see SkillResolution.Damage.
        [ContentDoc("Whether this skill's damage skips the target's Defense entirely.")]
        public bool ignoresDefense;

        // Fixed, typed damage packets. When present these REPLACE the
        // Attack + power x resource formula entirely: the spell deals exactly
        // what is authored here, and each packet is checked against the
        // target's weakness and resistance on its own.
        //
        // That independence is the point. 25 Fire + 25 Frost against
        // something that resists Frost and burns to Fire is 37 + 12, not 50
        // of something averaged.
        [ContentDoc("Fixed, typed damage packets that replace the Attack/power formula entirely; each is checked against weakness and resistance on its own.")]
        public RawDamageInstance[] damageInstances = Array.Empty<RawDamageInstance>();

        // HOW MANY OF THE CASTER'S OWN TURNS BEFORE IT COMES BACK.
        //
        // 0, the default, is a skill with no cooldown at all -- which is every
        // skill authored before this existed and still most of them. 2 means
        // "use it turn one, then again turn three": the number is counted from
        // the turn it was cast on, which is how an author says it out loud.
        //
        // Counted in the CASTER'S turns rather than in rounds. The turn order
        // is charge-based, so a fast character acts more often than a slow one
        // and a cooldown measured in rounds would be worth twice as much to one
        // of them for reasons nobody chose.
        [ContentDoc("How many of the caster's own turns must pass before this skill can be cast again; 0 means no cooldown.")]
        public int cooldownTurns;

        // DOES A PLAYER EVER PICK THIS FROM A MENU?
        //
        // True for a character's kit. False for a skill a MONSTER owns, and the
        // difference is not cosmetic: nearly every rule about what a skill may
        // cost exists because a player chooses between actions, and a free one
        // would dominate that choice. A monster does not choose -- a weighted
        // draw does -- so "free" is not exploitable and Boulder Slam has no
        // wallet to charge.
        //
        // A FIELD RATHER THAN INFERRED FROM THE OWNER. The resolver cannot see
        // the enemy catalogue, and making it wait for one so it could look up
        // whether characterId names a monster would couple two resolutions that
        // have no other reason to know about each other. ContentDatabase checks
        // the two facts agree once both catalogues exist, which is where every
        // other cross-catalogue check already lives.
        [ContentDoc("Whether a player ever picks this from a menu, as against a monster-only skill drawn by weighted chance.")]
        public bool playerSelectable = true;

        // HOW IT LOOKS, nested under "vfx" in the JSON.
        //
        // Six flat fields lived here -- vfxPath, vfxSeconds, vfxImpactFrame,
        // vfxFromCaster, vfxDepartFrame, sfxPath -- and every one of them had a
        // twin on the resolved skill, a twin on the ScriptableObject, and a
        // twin on the combat beat. Adding a seventh meant writing the same
        // field four times and copying it three. Holding the SAME TYPE the rest
        // of the chain holds is what makes a new knob one field instead of
        // four; see SpellPresentation.
        //
        // JsonUtility runs field initialisers before it fills anything in, so
        // an entry with no "vfx" block gets the defaults and an entry with a
        // partial one gets defaults for whatever it left out. That is what
        // retired the -1 and 0 sentinels these fields used to carry.
        [ContentDoc("How the skill looks and sounds when it resolves; see SpellPresentation.")]
        public SpellPresentation vfx = new SpellPresentation();


        // One of Domain.Combat.StatusEffectType — Poison, Regen, Protect,
        // Vulnerable, Stun — matched case-insensitively. Empty means this
        // skill applies no status at all, which is most of them. Landed on
        // whoever this skill's own effect already resolves against: a
        // damage effect's status lands on the enemy it hit, a heal effect's
        // lands on whoever was healed — there is no separate targeting
        // concept to author.
        [ContentDoc("Which StatusEffectType this skill applies on landing, or empty for none.")]
        public string appliesStatus = "";

        // Required together with appliesStatus. Meaning depends on the
        // status: damage or healing per tick for Poison/Regen, a percent for
        // Protect/Vulnerable, unused (but still required, as 1) for Stun.
        [ContentDoc("The magnitude of the applied status; required together with appliesStatus.")]
        public int statusMagnitude = -1;

        // How many of the AFFLICTED combatant's own turns the status lasts.
        // Required together with appliesStatus.
        [ContentDoc("How many of the afflicted combatant's own turns the applied status lasts; required together with appliesStatus.")]
        public int statusDuration = -1;

        // What a character needs, from base scores/talents/worn gear, before
        // this skill can be cast at all -- "<ability score> <amount>" lines,
        // same shape as an item's requires. Optional; a skill with none is
        // always castable once unlocked, which is every skill's behaviour
        // today.
        [ContentDoc("'<ability score> <amount>' lines gating whether this skill can be cast at all.")]
        public string[] requires = Array.Empty<string>();

        // Which axis this skill's damage rides: "Auto" (default) derives it
        // from the CASTER's own attackType (Physical -> Weapon, everything
        // else -> Spell) -- right for the overwhelming majority of skills.
        // "Weapon" or "Spell" overrides that derivation for a skill whose
        // flavour does not match the caster's own type (a headbutt authored
        // on a Nature-typed caster is still, narratively, a physical
        // attack). "None" rides neither axis at all.
        [ContentDoc("Which axis this skill's damage rides: Auto (derive from the caster's own attackType), Weapon, Spell, or None.")]
        public string scalingAxis = "";

        // How many places later in the turn queue this skill knocks its
        // target — the Black Ram's Headbutt. 0 (the default) means it does
        // not touch the queue, which is every other skill in the game.
        //
        // Slots, not charge and not a speed penalty: see
        // TurnOrder.PushBack's own header for why that phrasing is the one
        // that needs no calibration against the scheduler's arbitrary units.
        [ContentDoc("How many places later in the turn queue this skill knocks its target; 0 means it does not touch the queue.")]
        public int queuePushSlots;

        // What this skill transforms the caster into, for a Transform
        // effect. Left unauthored by every other skill, and rejected by the
        // resolver if authored on one — five numbers sitting in an asset
        // doing nothing is exactly the silent-typo case every other
        // cross-field rule in SkillEntryResolver exists to catch.
        [ContentDoc("What the caster transforms into, for a Transform effect; rejected if authored on any other effect.")]
        public Combat.TransformGrant transform = new Combat.TransformGrant();

        // Which of the caster's OWN stance folders plays while this skill
        // resolves. Empty means the long-standing default of "cast" — every
        // skill authored before this field existed still plays exactly the
        // pose it always did. Set this when a monster owns more than one
        // skill and each should look like a different thing happening,
        // rather than three abilities sharing one generic casting pose.
        [ContentDoc("Which of the caster's own stance folders plays while this skill resolves; empty means the default cast pose.")]
        public string stance = "";

        // THE OTHER TWO PHASES OF THE SAME BLOW. `stance` above is the STRIKE
        // — the drawing worn at the moment of impact — and these are the two
        // poses that can come before it: the travel, and the wind-up held at
        // the top of it.
        //
        // Bjorn's Slam is the case they exist for: "he rushes forward in the
        // first frame, then he holds his hammer over his head, then he slams
        // down" is three drawings of one beat, and a beat carried one. KEYED
        // BY PHASE rather than authored as a sequence of poses with their own
        // durations, because the phases already exist in the player and are
        // already timed — the walk-in a Close spends, the wind-up wait a
        // Lunge/Charge already takes before the impact instant — so a phase
        // key selects a drawing for a moment the beat already has, where a
        // sequence would be a second, parallel clock arguing with the first
        // about when the blow lands.
        //
        // Empty means unauthored, and an unauthored beat plays exactly as it
        // always did: the strike stance from the beat's open, idle at the end,
        // no extra waits. The full precedence table lives on CombatBeat.
        [ContentDoc("The pose worn while the caster travels to its target (Close's walk-in, Lunge/Charge's crossing); empty means the strike pose is worn throughout. Ignored on a Hold approach, which has no travel.")]
        public string approachStance = "";

        [ContentDoc("The pose held through the wind-up, between arrival and impact; empty means the strike pose is worn throughout. On a Hold or Close approach this buys the beat a wind-up wait it would not otherwise have.")]
        public string windupStance = "";

        // HOW THE CASTER GETS TO WHAT IT IS HITTING. "hold", "lunge" or
        // "close"; empty means hold, which is what every skill did before this
        // existed and therefore changes nothing for the ones that say nothing.
        //
        // A STRING rather than the enum, for the reason `anchor` and
        // StanceManifest's `loop` are: JsonUtility writes an enum as its
        // ordinal, so the file would read "approach": 2 and reordering the
        // enum would silently repoint every skill in the game.
        //
        // Worth authoring on any melee skill. A monster's claws going through
        // the skill path rather than the plain-attack one is not a reason for
        // them to stop reaching, and until this field existed it was.
        [ContentDoc("How the caster gets to what it is hitting: hold, lunge, or close; empty means hold.")]
        public string approach = "";

        // How hard this skill kicks the stage on its own account, 0..1. Zero
        // means "whatever the damage was worth", which is every skill that has
        // not thought about it. See CombatBeat.Shake for the blow that has to
        // be felt without landing.
        [ContentDoc("How hard this skill kicks the stage on its own account, 0..1; 0 means whatever the damage was worth.")]
        public float shake;

        // Summon only. The enemy id (Assets/_Project/ContentData/enemies.json)
        // this skill calls in on the caster's own side. Required together
        // with summonCap; empty means this skill does not summon anything,
        // which is every skill but a Summon effect's.
        [ContentDoc("The enemy id this skill calls onto the caster's own side, for a Summon effect.")]
        public string summonEnemyId = "";

        // Summon only. Don't summon another one once the caster's side
        // already fields this many LIVING copies of summonEnemyId — the cap
        // is read at the moment the ability is DRAWN, not at the moment it
        // resolves, so a boss capped at two rats picks something else that
        // turn instead of visibly failing to call a third. Required
        // together with summonEnemyId, same both-fields-or-neither rule
        // appliesStatus/statusMagnitude/statusDuration already follow.
        [ContentDoc("The most living copies of summonEnemyId allowed on the caster's side before this skill stops being drawn.")]
        public int summonCap = -1;

        // A SPELL LEARNED FROM A BOOK RATHER THAN BY LEVELLING (docs/
        // PLAN_SHOP.md §1a). False on every skill in the tree today,
        // including the five this will eventually apply to -- Phase A ships
        // the field and bookTier below purely additively, and only Phase E
        // flips this to true (and removes unlockLevel) on the five. The
        // resolver refuses an entry that authors both bookOnly and
        // unlockLevel: an absent unlockLevel resolves to int.MaxValue
        // (SkillEntryResolver's own DefaultUnlockLevel does NOT apply to a
        // book-only entry), which is what keeps a book-only skill off the
        // level ladder without a second gating mechanism.
        [ContentDoc("Whether this skill is learned from a shop book rather than by levelling.")]
        public bool bookOnly;

        // The shop's price band for this spell as a book (1-4, ShopPricing's
        // four-entry lookup) — authored independently of bookOnly and read
        // by the shop's roll from day one (Phase A/gate 3), because "which
        // spells can be found or bought as a book" and "does owning one
        // supersede the level route" are two different questions with two
        // different answers during the staged rollout. 0 means this skill
        // is not book-eligible at all, which is every skill but the five.
        [ContentDoc("The shop's price band (1-4) for this spell as a book; 0 means not book-eligible.")]
        public int bookTier;

        // DOES THE FRONT-RANK RULE APPLY TO THIS SKILL? False, the default,
        // is every skill authored before this existed and every ranged or
        // magical one authored after — FightSession.CanReachEnemy only ever gets
        // asked with Reach.Melee about a skill that says true here. A hand
        // striking through a monster's own bodyguard is a different claim
        // than a bolt of lightning doing it, and only the first one needed
        // a rule.
        //
        // Only means anything on a SingleEnemy skill — see
        // SkillEntryResolver's own check, the same "this field has no
        // meaning on that effect" rule ignoresDefense and queuePushSlots
        // already follow.
        [ContentDoc("Whether the front-rank melee-reach rule applies to this SingleEnemy skill.")]
        public bool meleeReach;

        // DOES THE ACTOR MOVE ITS BODY TO DO THIS? (plan D7/1.10.) A swing, a
        // charge, a lunge, a thrown boulder: true. A cast, a shout, a cloud, a
        // transformation: false. It is what Rooted forbids, so it decides
        // whether a shackled actor may take this action at all.
        //
        // INDEPENDENT OF THE DAMAGE ELEMENT, which is the owner's own rule and
        // the reason this is authored rather than derived. A flaming sword
        // strike is physical and a rock hurled by magic need not be, so neither
        // damageType nor damageInstances can answer it. Deriving it from
        // `approach` was the other candidate and is wrong on three of the nine
        // authored enemy abilities on day one -- boulder_slam, shear and
        // battering_ram author no approach and are all plainly physical.
        //
        // DEFAULTS FALSE, and for a damage skill that default is REFUSED
        // rather than taken: see physicalMoveOmitted just below and
        // SkillEntryResolver's own check. A ward or a heal is obviously not a
        // move and authors nothing.
        [ContentDoc("Whether this action is a physical move (a swing, charge or lunge) that Rooted forbids; required on every damage row.")]
        public bool physicalMove;

        // NOT CONTENT: whether the author actually WROTE physicalMove on this
        // row, stamped in by whoever parsed the file.
        //
        // A bool cannot carry the -1 / "" sentinel the rest of this class uses
        // to tell "the author omitted this" from "the author wrote the default"
        // -- false is both. So the one reader that holds the authored TEXT
        // (ContentBuilder, through a probe parse whose default is the opposite)
        // answers the question there and stamps it here, and the resolver keeps
        // the refusal beside every other skill refusal rather than growing a
        // second place where content is judged.
        //
        // DEFAULTS FALSE, meaning "stated", so an entry built in code -- every
        // resolver test, every fixture -- is never accused of omitting a field
        // it had no file to omit it from. Only a real parse can set it.
        [NonSerialized]
        public bool physicalMoveOmitted;

        // WHICH POSITIONS IN THE OPPOSING LINE THIS SKILL CAN BE AIMED AT,
        // COUNTED FROM THE FRONT AND 1-BASED. [2, 3] is "the back two only"
        // — a lobbed thing that cannot be aimed at what is right in front of
        // you. Empty, the default, means no positional restriction at all,
        // which is every skill authored before this existed.
        //
        // 1-BASED HERE AND ZERO-BASED EVERYWHERE ELSE, on purpose: a
        // designer counts a battle line from one. Reach.FromContent is the
        // single door that converts, and the resolver is its only caller —
        // see Reach's own header.
        //
        // Refused alongside meleeReach (that IS a reach, stated as a rule
        // rather than as a list), refused on anything but a SingleEnemy
        // skill, and refused outside 1..3 — a side never fields more than
        // FightHudSpec.StageSlotsPerSide, so a 4 is a restriction nothing
        // could ever satisfy.
        [ContentDoc("Which 1-based positions in the enemy line this SingleEnemy skill may target; empty means anywhere.")]
        public int[] reachSlots = Array.Empty<int>();

        // THE ELEMENTS THE PLAYER PICKS BETWEEN AFTER CHOOSING THIS SKILL AND
        // BEFORE CHOOSING A TARGET. Empty, the default, is every skill that has
        // ever existed: one cast, one authored typing, no question asked.
        //
        // A CHOICE RETYPES THE AUTHORED PACKETS AND NOTHING ELSE, which is why
        // the resolver requires damageInstances alongside it. An Attack-scaled
        // skill has no packet to retype -- it rides the caster's own attackType
        // at cast time (FightSession.ActorAttackType) -- so an elements list on
        // one would have to invent a second retyping rule for a case nobody
        // authored. Refused instead, along with a single-entry list (a choice of
        // one is not a choice) and a packet typed as something the list does not
        // offer (the JSON would then read as one thing and play as another the
        // moment the first element is picked).
        //
        // Each entry may carry its own vfx, so the day four sheets exist the art
        // lands as four blocks here and no C#.
        [ContentDoc("Elements the player chooses between before targeting; each retypes every authored damage packet.")]
        public RawElementChoice[] elements = Array.Empty<RawElementChoice>();

        // BJORN'S SLAM: THE FIRST FURY SINK, and the model every future
        // primary-pool tier skill authors through rather than a slam
        // special case. Ascending fractions of the owner's PRIMARY pool
        // capacity -- Fury for Bjorn, not a signature resource -- each
        // buying a damage multiplier and (optionally) a floor on how hard
        // the blow lands. THE HIGHEST TIER THE CASTER CAN CURRENTLY AFFORD
        // FIRES AUTOMATICALLY: there is no menu choice, because affording
        // more is always strictly better, and a resource that decays on an
        // idle turn already punishes hoarding past the point of use.
        //
        // Empty (the default) is every skill authored before this existed
        // and every skill that only ever spends mana/resourceCost. See
        // SkillEntryResolver.TryResolvePoolTiers for the ordering and range
        // rules, and PoolTierResolution for how the fired tier is picked at
        // cast time.
        [ContentDoc("Ascending fractions of the owner's primary pool this skill can spend for extra damage; the highest tier the caster can afford fires automatically.")]
        public RawPoolTier[] poolTiers = Array.Empty<RawPoolTier>();

        // A SKILL WHOSE DESIGN HASN'T BEEN WRITTEN YET -- a track Ability
        // node may reference one so the node exists on the rail before the
        // owner has decided what it does (docs/handoffs/progression_v2/
        // PLAN_PROGRESSION_V2.md §7 phase 3 package 3). The resolver refuses
        // a placeholder entry that authors ANY effect field (effect,
        // manaCost, resourceCost, power, flatAmount, damageInstances,
        // appliesStatus, elements, poolTiers) -- this is a stand-in, not an
        // ability, and forces PlayerSelectable false regardless of what was
        // authored.
        [ContentDoc("Whether this skill's design has not been written yet; never player-selectable and never drawn by a monster, and may author no effect fields at all.")]
        public bool placeholder;

        // Required together with placeholder. No AUDIT.md grep lint exists
        // in this codebase to check a note against (grepped: nothing under
        // Editor/ or Tests/ reads AUDIT.md's own text), so this is the
        // fallback the brief names instead of inventing one.
        [ContentDoc("Required together with placeholder: why this skill exists undesigned.")]
        public string placeholderNote = "";

        // ---- milestone B: consumption and health payment (plan §4) -------

        // Percent of the CASTER'S OWN max health paid, ceiling-rounded,
        // alongside manaCost -- Blackglass Spear's blood price (plan 1.2). 0,
        // the default, is every skill that has ever existed. Validated with
        // the other costs in one refusal, and paid directly rather than
        // through the damage funnel -- see HealthCost's own header.
        [ContentDoc("Percent of the caster's own max health paid as a cost, alongside manaCost; 0 means none. Rounds up.")]
        public int healthCostPercent;

        // One of Domain.Combat.StatusEffectType, matched case-insensitively.
        // The TARGET must carry this status or the cast is refused before
        // anything is paid -- Ashen Reckoning's "Poison" (plan 1.1/1.8).
        // Empty means no such requirement, which is every skill but one.
        [ContentDoc("A StatusEffectType the target must already carry, or the cast is refused before anything is paid; empty means no requirement.")]
        public string requiresStatus = "";

        // One of Domain.Combat.StatusEffectType, matched case-insensitively.
        // A landed hit spends the FIRST entry of this status off the target
        // (plan 1.8, 2.4) -- Crownfall's "Marked", read through the general
        // Marks facility rather than any relic's own private tracking.
        // Required together with damageInstancesIfConsumed.
        [ContentDoc("A StatusEffectType a landed hit consumes off the target; required together with damageInstancesIfConsumed.")]
        public string consumesStatus = "";

        // The heavier packet list resolved INSTEAD OF damageInstances when
        // the target is found to carry consumesStatus at the moment of the
        // read (plan 2.4) -- Crownfall's 12 Arcane in place of its ordinary
        // 7. Required together with consumesStatus, and meaningless without
        // an ordinary damageInstances to fall back to.
        [ContentDoc("The packet list used instead of damageInstances when the target carries consumesStatus; required together with consumesStatus.")]
        public RawDamageInstance[] damageInstancesIfConsumed = Array.Empty<RawDamageInstance>();

        // The percent a Reclaim effect's one detonation is marked up by
        // before it is split (plan D2/1.6) -- Ashen Reckoning's 150. Required
        // (and must be positive) on a Reclaim row; meaningless on any other
        // effect, which authors nothing here.
        [ContentDoc("The premium percent a Reclaim effect's detonation is marked up by; required and positive on a Reclaim row, meaningless elsewhere.")]
        public int detonationPercent;

        // The ordered list of DamageType names a Reclaim effect's consumed
        // total is split across (plan 1.7) -- Ashen Reckoning's
        // ["Poison", "Fire"], with the odd point going to whichever type is
        // FIRST in this list. Required together with detonationPercent.
        [ContentDoc("The ordered DamageType names a Reclaim effect's consumed total is split across, odd point to the first; required together with detonationPercent.")]
        public string[] detonationSplit = Array.Empty<string>();

        // ---- milestone C: initiative and formation (plan section 4) ------

        // How many places EARLIER in the turn queue this skill moves its
        // target -- Borrowed Moment's 2 (plan 1.9, 2.7). The exact mirror of
        // queuePushSlots above, and expressed in the same unit for the same
        // reason: a place is a place to a player, while a charge number means
        // something different to a fast combatant than to a slow one.
        //
        // REQUIRED AND POSITIVE ON A Hasten ROW, meaningless on every other
        // effect -- an advance of zero places is a cast that does nothing,
        // which is a content error rather than a cheap spell.
        [ContentDoc("How many places earlier in the turn queue this skill moves its target; required and positive on a Hasten row, meaningless elsewhere.")]
        public int advanceSlots;

        // ---- Bellwether kit M2 (docs/PLAN_BELLWETHER_KIT.md 3.3-3.5) ------

        // THE TYPE THIS SKILL'S DAMAGE IS DEALT AS, over the caster's own
        // attackType -- a Void knell from a Physical ram. Only on an
        // Attack-scaled (or seat-sized) damage row: fixed damageInstances
        // already carry a type per packet, and a row that deals nothing has
        // nothing to type.
        [ContentDoc("The DamageType this skill's Attack-scaled or seat-sized damage is dealt as, over the caster's own attackType; empty means the caster's. Refused on damageInstances rows and on effects that deal no damage.")]
        public string damageType = "";

        // [front, middle, rear], each a PERCENT OF THE TARGET'S MAX HEALTH,
        // picked by the seat the target stands in when the hit resolves
        // (CombatEncounter.SeatOf; an enemy's seat is its living rank, and a
        // rank past the rear reads the rear entry). Replaces the Attack/power
        // formula entirely, like damageInstances: no attack, rally or pool
        // tier scales it. It still meets affinity (damageType), defence
        // (unless ignoresDefense), Protect/Vulnerable, ward and dodge. 0 at a
        // seat means no hit there at all: no number, no riders.
        //
        // PERCENT OF MAX HEALTH, NOT A MULTIPLIER ON THE ATTACK FORMULA, by
        // the orchestrator's call (plan R2, recorded in 3.4): one power could
        // not both kill a full-health target at the front on the first knell
        // and spare a 60%-health one in the middle on the second while the
        // caster's rally grows ~+24% -> ~+64% between them.
        [ContentDoc("[front, middle, rear] percent of the TARGET's max health dealt by the seat it stands in when the hit lands; replaces the Attack formula. 0 = no hit at that seat. DamageSingle only; exactly three entries, each 0-1000.")]
        public int[] damageBySeatMaxHpPercent = Array.Empty<int>();

        // 1-BASED, like reachSlots: 1 front, 2 middle, 3 rear. Where a
        // Reposition puts its target (required there), or where a monster's
        // DamageSingle drags the target it landed on (optional there).
        [ContentDoc("The 1-based party seat (1 front, 2 middle, 3 rear) a Reposition puts its target in, trading with any occupant; required on Reposition, optional on a monster's DamageSingle (moves the target after a landed hit). Party-side targets only.")]
        public int toSeat;
    }

    // One tier of a poolTiers ladder -- see RawSkillEntry.poolTiers.
    //
    // NO -1 SENTINEL ON spend/damageMultiplier, unlike most of this file's
    // int fields. Both have a hard floor a legitimately authored value can
    // never cross (spend must be > 0, damageMultiplier must be >= 1), so an
    // unauthored 0 and an authored 0 need no telling apart -- either way
    // SkillEntryResolver.TryResolvePoolTiers refuses it with the same
    // message. The -1 convention exists for fields (power, flatAmount) where
    // 0 IS a legitimate authored value and would otherwise be indistinguishable
    // from "the author wrote nothing" -- that ambiguity does not exist here.
    [Serializable]
    public class RawPoolTier
    {
        [ContentDoc("Fraction of the owner's primary pool CAPACITY this tier spends, (0,1]; the pool's own gainOnAttack still fires afterwards as normal. Tiers must be authored in ascending order.")]
        public float spend;

        [ContentDoc("How much this tier multiplies the skill's own computed damage by; must be 1 or higher.")]
        public float damageMultiplier;

        [ContentDoc("A floor on how hard this tier's blow kicks the stage, 0..1, on top of the skill's own shake; 0 means no extra floor.")]
        public float shake;

        [ContentDoc("A floor on this tier's hit-stop, in seconds, clamped to HitStop.MaxSeconds; 0 means no extra floor.")]
        public float hitStopSeconds;
    }

    // One option in a skill's `elements` list: the element itself, and
    // optionally how that element's cast looks. The vfx block is the SAME type
    // the skill's own is, so an element that authors one needs no new field
    // anywhere in the chain -- see SpellPresentation.
    [Serializable]
    public class RawElementChoice
    {
        [ContentDoc("A DamageType name this choice retypes the skill's packets to, matched case-insensitively ('Frost' is accepted for Ice).")]
        public string type = "";
        [ContentDoc("How this element's cast looks and sounds; omitted, the skill's own vfx plays for every element.")]
        public SpellPresentation vfx = new SpellPresentation();
    }

    // One typed packet inside a spell. `type` is a DamageType name, matched
    // case-insensitively -- see DamageType's own header for the current
    // eleven -- and "Frost" is accepted as a friendlier spelling of Ice so an
    // author never has to remember which word the enum happened to use.
    [Serializable]
    public class RawDamageInstance
    {
        [ContentDoc("A DamageType name this packet is typed as, matched case-insensitively ('Frost' is accepted for Ice).")]
        public string type = "";
        [ContentDoc("The flat amount this packet deals, on the same x10 scale as every other damage number.")]
        public int amount;
    }

    // JsonUtility cannot deserialize a bare top-level array, so skills.json
    // is one object with a "skills" array inside it.
    [Serializable]
    public class RawSkillFile
    {
        public RawSkillEntry[] skills = Array.Empty<RawSkillEntry>();
    }
}
