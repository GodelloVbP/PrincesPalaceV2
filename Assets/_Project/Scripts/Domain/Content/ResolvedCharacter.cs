using System;
using PrincesPalace.Domain.Stage;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Content
{
    // A validated character -- and the shape CharacterDefinition now STORES
    // rather than restates. Every string has been parsed to its enum and every
    // number checked, so the Editor-side glue has no decisions left to make.
    //
    // [Serializable] class with public fields, for the reason ResolvedSkill
    // records. System.Serializable is BCL, so Domain stays engine-free.
    [Serializable]
    public sealed class ResolvedCharacter
    {
        public string Id = "";
        public string DisplayName = "";
        public CharacterRole Role;

        // Stats and ability scores before any talents are applied.
        public StatBlock BaseStats;
        public AbilityScoreBlock AbilityScores;

        // Editor-relative path to a neutral head-and-shoulders portrait, for
        // the Character Sheet. Distinct from BattleSpritePath, which is the
        // full-body figure that stands on the fight stage; empty means no art
        // yet and the screen falls back to a plain plate rather than showing a
        // broken sprite.
        public string PortraitPath = "";
        public string BattleSpritePath = "";

        // Which way BattleSpritePath's art is drawn in its source file. The
        // stage mirrors it as needed so they always face the enemy -- it does
        // NOT assume every sprite faces the same way.
        public SpriteFacing BattleSpriteFacing = SpriteFacing.Right;

        // The elemental type of this character's Attack and Skill, checked
        // against an enemy's weakness/resistance.
        public DamageType AttackType = DamageType.Physical;

        // This character's own private combat resource (e.g. "wool"). An empty
        // id means they have none, which is everyone but Shawn.
        public string SignatureId = "";
        public string SignatureDisplayName = "";
        public int SignatureCapacity;
        public int SignatureGainPerTurn;
        public int SignatureGainOnAttack;
        public int SignatureGainOnDamageTaken;

        // Whether the resource soaks incoming damage before health. False for
        // Wool since the talent rework -- see RawCharacterEntry.
        public bool SignatureAbsorbsDamage;

        // Prince's Favor: this character's luck. The squad's HIGHEST value
        // drives loot rolls -- it never compounds across members.
        public int PrincesFavor;

        public int SortOrder;

        // Whether a fresh profile fields this character, and where in the
        // squad. See RawCharacterEntry's own note: exactly three characters
        // carry the flag and their slots are 1-3 and unique, which
        // CharacterEntryResolver enforces on the whole file.
        public bool StartsInSquad;
        public int SquadSlot;

        // The character's identity colour on every fight-HUD card that
        // stands for them (Domain.UiKit.PcTheme turns it into a rim hex and
        // a name hex) -- content-authored (RawCharacterEntry.plateTheme),
        // not a Core-side default, so a new character's colour is a one-line
        // JSON edit rather than a code change.
        //
        // THE INITIALISER IS A FIXTURE DEFAULT, NOT A CONTENT ONE.
        // CharacterEntryResolver now REFUSES an unauthored plateTheme, so
        // nothing that came through content can reach this value; what can
        // is the parameterless serializer constructor and the dozen tests
        // that build a ResolvedCharacter by hand. Blue rather than nothing,
        // because default(ButtonTheme) is Gold -- and a fixture silently
        // wearing Gold looks authored, which is worse than one wearing the
        // colour the untinted card is baked with.
        public ButtonTheme PlateTheme = ButtonTheme.Blue;

        // Which pools.json resource this character's skills spend -- their
        // PRIMARY pool, distinct from the optional private SignatureId
        // above. "mana" for everyone shipped today; CharacterEntryResolver
        // refuses an id pools.json does not define, so this is always a real
        // pool by the time anything holds it.
        //
        // NOTHING READS IT YET. Phase A of the pool work is the catalogue
        // only; CombatantState learns about pools in phase B.
        public string PrimaryPoolId = "mana";

        // Empty id means no resource at all, rather than a zero-capacity one
        // -- see CombatantState.Signature for why that distinction is kept
        // sharp.
        public bool HasSignatureResource => !string.IsNullOrWhiteSpace(SignatureId);

        // For the serializer only.
        public ResolvedCharacter()
        {
        }

        public ResolvedCharacter(
            string id, string displayName, CharacterRole role, StatBlock baseStats,
            AbilityScoreBlock abilityScores,
            string portraitPath, string battleSpritePath, SpriteFacing battleSpriteFacing,
            DamageType attackType, string signatureId, string signatureDisplayName,
            int signatureCapacity, int signatureGainPerTurn, int signatureGainOnAttack,
            int signatureGainOnDamageTaken, bool signatureAbsorbsDamage, int princesFavor, int sortOrder,
            // OPTIONAL, at the end, and that is not laziness about the
            // eighteen positional arguments above it. Every existing caller --
            // the resolver, and a dozen fixtures -- is asking about a
            // character's stats and art, not about who a fresh profile opens
            // with; making them all pass `false, 0` would be eighteen
            // arguments of noise for one fact only characters.json has.
            bool startsInSquad = false, int squadSlot = 0,
            // Same reasoning, same place, and it stays OPTIONAL even though
            // content must now author the theme: C# forbids a required
            // parameter after an optional one, so dropping the value would
            // mean a dozen fixtures passing `false, 0, <theme>` for a fact
            // none of them has an opinion about. See PlateTheme's own note
            // above for why the value is Blue rather than absent.
            ButtonTheme plateTheme = ButtonTheme.Blue,
            // Same place, same reasoning again, and here the default is
            // also the shipped answer: every character in the game spends
            // mana, so a fixture that has no opinion about pools gets the
            // one every real row has.
            string primaryPoolId = "mana")
        {
            Id = id ?? "";
            DisplayName = displayName ?? "";
            Role = role;
            BaseStats = baseStats;
            AbilityScores = abilityScores;
            PortraitPath = portraitPath ?? "";
            BattleSpritePath = battleSpritePath ?? "";
            BattleSpriteFacing = battleSpriteFacing;
            AttackType = attackType;
            SignatureId = signatureId ?? "";
            SignatureDisplayName = signatureDisplayName ?? "";
            SignatureCapacity = signatureCapacity;
            SignatureGainPerTurn = signatureGainPerTurn;
            SignatureGainOnAttack = signatureGainOnAttack;
            SignatureGainOnDamageTaken = signatureGainOnDamageTaken;
            SignatureAbsorbsDamage = signatureAbsorbsDamage;
            PrincesFavor = princesFavor;
            SortOrder = sortOrder;
            StartsInSquad = startsInSquad;
            SquadSlot = squadSlot;
            PlateTheme = plateTheme;
            PrimaryPoolId = string.IsNullOrWhiteSpace(primaryPoolId) ? "mana" : primaryPoolId;
        }
    }
}
