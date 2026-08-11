using System;

namespace PrincesPalace
{
    // One character's combat totals across a whole run, flattened for the save
    // file.
    //
    // A list entry rather than a dictionary pair, for the same reason every
    // other map in SaveData is spelled this way: JsonUtility serializes fields
    // and Dictionary is not a shape it can write. CombatLedger keeps the
    // dictionary in memory where it belongs and this is the boundary form.
    //
    // Mirrors CombatLedger.Line field for field on purpose. A DTO that
    // paraphrased it would be a second place to forget a column.
    [Serializable]
    public class RunLedgerEntry
    {
        public string characterId = "";

        public int physicalDealt;
        public int otherDealt;
        public int damageTaken;
        public int shielded;
        public int healed;
        public int kills;
        public int timesDowned;

        public int TotalDealt => physicalDealt + otherDealt;
    }
}
