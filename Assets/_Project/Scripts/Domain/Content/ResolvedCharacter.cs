using PrincesPalace.Domain.Stage;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // A validated character, ready for ContentBuilder to copy onto a
    // CharacterDefinition. Every string has been parsed to its enum, every
    // number checked, so the Editor-side glue has no decisions left to make.
    public readonly struct ResolvedCharacter
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly CharacterRole Role;
        public readonly StatBlock BaseStats;
        public readonly AbilityScoreBlock AbilityScores;
        public readonly string PortraitPath;
        public readonly string BattleSpritePath;
        public readonly SpriteFacing BattleSpriteFacing;
        public readonly DamageType AttackType;
        public readonly string SignatureId;
        public readonly string SignatureDisplayName;
        public readonly int SignatureCapacity;
        public readonly int SignatureGainPerTurn;
        public readonly int SignatureGainOnAttack;
        public readonly int SignatureGainOnDamageTaken;
        public readonly bool SignatureAbsorbsDamage;
        public readonly int PrincesFavor;
        public readonly int SortOrder;

        public ResolvedCharacter(
            string id, string displayName, CharacterRole role, StatBlock baseStats,
            AbilityScoreBlock abilityScores,
            string portraitPath, string battleSpritePath, SpriteFacing battleSpriteFacing,
            DamageType attackType, string signatureId, string signatureDisplayName,
            int signatureCapacity, int signatureGainPerTurn, int signatureGainOnAttack,
            int signatureGainOnDamageTaken, bool signatureAbsorbsDamage, int princesFavor, int sortOrder)
        {
            Id = id;
            DisplayName = displayName;
            Role = role;
            BaseStats = baseStats;
            AbilityScores = abilityScores;
            PortraitPath = portraitPath;
            BattleSpritePath = battleSpritePath;
            BattleSpriteFacing = battleSpriteFacing;
            AttackType = attackType;
            SignatureId = signatureId;
            SignatureDisplayName = signatureDisplayName;
            SignatureCapacity = signatureCapacity;
            SignatureGainPerTurn = signatureGainPerTurn;
            SignatureGainOnAttack = signatureGainOnAttack;
            SignatureGainOnDamageTaken = signatureGainOnDamageTaken;
            SignatureAbsorbsDamage = signatureAbsorbsDamage;
            PrincesFavor = princesFavor;
            SortOrder = sortOrder;
        }

        public bool HasSignatureResource => !string.IsNullOrWhiteSpace(SignatureId);
    }
}
