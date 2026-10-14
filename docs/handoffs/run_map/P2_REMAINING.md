# Run map P2: remaining work is closed; open calls only

Plan: `PLAN_RUN_MAP.md` rev 5 §8 P2 row. Items 1-5 are done (Map scene
regenerated, map capture reviewed, test selection rows, stale names, bot
smoke: a realm walks to floor 5 and `AdvanceLeg` opens the next).

## Seen in the map capture (P3 layout call)

`MapCrowdedRealm` (floor 14): the right column stacks five rooms in three
tile slots, so tiles overlap and the `FIGHT` labels sit on the neighbouring
node. Every node is drawn and the reachable one is boxed; the overlap is
cosmetic. Needs a taller column or smaller tiles when a column holds more
than three rooms.
## Open calls (orchestrator or owner, not the implementer)

- **Strength thresholds.** Settled in `P3_BOT_REPORT.md`: rescaled to 0.27 / 0.48 for the real party weight 5760; floor-1 mix is 60/35/4.
- **`restBeforeBoss` is frozen into the profile at realm open**
  (`RealmProfiles.ForRealm`). A mid-realm grant applies from the next realm.
  Nothing grants it today.
- **`ContentFreshnessTests`** fails until I6 commits with `-BuildContent`.

