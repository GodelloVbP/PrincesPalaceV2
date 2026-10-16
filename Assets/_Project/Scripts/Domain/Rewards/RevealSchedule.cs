using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Rewards
{
    // One card of a page as the reveal sees it, in row order.
    public readonly struct RevealCard
    {
        // The quality ladder, 1..4: Common, Uncommon, Rare, top tier. A relic
        // maps by RelicRarity (plan C5): Rare and UltraRare 3, Mythic and
        // Godlike 4. The tier is also the card's flip pitch step.
        public readonly int Tier;

        // LiveUpgrade.Of: good news for the party, not just a high quality.
        public readonly bool LiveUpgrade;

        // Gear: the tier (1..4) of the item its wearer has in that slot now,
        // 0 for an empty slot. Always 0 on a relic.
        public readonly int WornTier;

        // A relic card keeps the quality-only rule (A9 measured gear only).
        public readonly bool IsRelic;

        // The face shows the green up-chevron (RewardCardFace.DeltaSign > 0).
        // The chevron note follows the face, not LiveUpgrade: an affix-only
        // gain draws the flat bar and stays silent. A relic face has no chevron.
        public readonly bool ShowsUp;

        public RevealCard(int tier, bool liveUpgrade, int wornTier = 0, bool isRelic = false, bool showsUp = false)
        {
            Tier = Math.Max(1, Math.Min(RevealSchedule.TopTier, tier));
            LiveUpgrade = liveUpgrade;
            WornTier = isRelic ? 0 : Math.Max(0, Math.Min(RevealSchedule.TopTier, wornTier));
            IsRelic = isRelic;
            ShowsUp = showsUp && !isRelic;
        }

        public static RevealCard Gear(Quality quality, bool liveUpgrade, int wornTier = 0, bool showsUp = false) =>
            new RevealCard((int)quality + 1, liveUpgrade, wornTier, false, showsUp);

        public static RevealCard Relic(int tier, bool liveUpgrade) => new RevealCard(tier, liveUpgrade, 0, true);

        public bool IsRarePlus => Tier >= RevealSchedule.RareTier;
        public bool IsTop => Tier >= RevealSchedule.TopTier;

        // Earns the cache by itself: a live Rare+ card, and for gear also
        // better than what is worn (an empty slot is lower than anything). A
        // wrapped bundle for a Common would be a false promise.
        public bool EarnsCache => LiveUpgrade && IsRarePlus && (IsRelic || Tier > WornTier);

        // Earns the full ceremony (tremble, rays, the big shake, the bell, the
        // linger): a live Legendary over a worn item below Legendary (a
        // relic: any top-tier live one). Any other top tier lands as a Rare
        // in its own colour, so useless loot is never celebrated (C4).
        public bool EarnsCeremony => LiveUpgrade && IsTop && (IsRelic || WornTier < RevealSchedule.TopTier);
    }

    public enum RevealBeatKind
    {
        Wipe,          // the page's entrance: the frame's wipe on the first page, the map chip flying in on a later one (C1, C2)
        HaulTick,      // one soft tick of the gold count, inside the wipe (C2); Card is the tick's index
        OpenWait,      // the cache stands closed and hums before it opens by itself
        Seal,          // the seal snaps
        Slide,         // every card comes out face-down into the row; spans the whole motion
        Deal,          // no cache: card Card arrives face-down (its rustle); the deal runs left to right inside the slide
        Anticipate,    // the build under card Card before its flip; Style says how (a Common has none)
        Flip,          // PitchStep is the flip clip
        Land,          // the card is face-up; Style says how it arrives
        Ember,         // a choice ember lights (rides the landing)
        Chevron,       // a card whose face shows the up-chevron; PitchStep is its ordinal in row order
        Choosable,     // the row accepts Take, Hone and Leave
        Linger,        // non-blocking; the top-tier halo holds at full strength
    }

    // A card's treatment, for its anticipation and its landing alike (C4).
    public enum LandStyle
    {
        Flat,          // Common: no build, a flat tap and a little dust
        Puff,          // Uncommon: a faint under-glow, a puff of motes
        Shimmer,       // Rare, and a top tier that is not a live upgrade: glow and lift, ring and shimmer
        Burst,         // top-tier live upgrade: glow, tremble and embers, then rays, explosion, shake, flare, bell
    }

    public readonly struct RevealBeat
    {
        public readonly RevealBeatKind Kind;
        public readonly float Start;
        public readonly float Duration;
        public readonly int Card;
        public readonly int PitchStep;
        public readonly LandStyle Style;

        public RevealBeat(RevealBeatKind kind, float start, float duration, int card = -1, int pitchStep = 0,
                          LandStyle style = LandStyle.Flat)
        {
            Kind = kind;
            Start = start;
            Duration = duration;
            Card = card;
            PitchStep = pitchStep;
            Style = style;
        }

        public float End => Start + Duration;
    }

    // THE REVEAL: from the Reckoning's wipe start to a choosable row, one page
    // at a time (docs/PLAN_REWARD_SCREENS.md C3, C4, C10). Pure; the controller
    // plays the beats and never derives a time. Times are wall seconds from the
    // wipe start, already through ReckoningTime.
    //
    // ONE CARD AT A TIME, LEFT TO RIGHT: a card's anticipation starts only
    // after the card before it has landed and settled, so each card gets its
    // own moment and the row reads as a run of reveals, not one burst.
    public sealed class RevealSchedule
    {
        public const int RareTier = 3;
        public const int TopTier = 4;

        // Authored at the default battle speed (C4 table).
        public const float WipeSeconds = 0.34f;
        public const float OpenWaitSeconds = 0.5f;
        public const float SealSeconds = 0.12f;
        public const float SlideSeconds = 0.25f;
        public const float FlipSeconds = 0.18f;

        // From a landing to the next card's anticipation, so the landing's
        // effect reads before anything else moves: a Common's cadence is flip
        // plus settle, 0.45 s. The ceremony's rays and shake need longer.
        public const float SettleSeconds = 0.27f;
        public const float CeremonySettleSeconds = 0.55f;

        public const float LingerSeconds = 1.2f;
        public const float FlareSeconds = 0.3f;
        public const float GuardSeconds = 0.15f;

        // The gold count lands in at most this many soft ticks (C2).
        public const int MaxHaulTicks = 8;

        // The build before a flip, by treatment: none for a Common, a breath
        // for an Uncommon, a held lift for a Rare, a long tremble for the
        // ceremony. The quiet part is what makes the loud part loud.
        public static float AnticipationSeconds(LandStyle style)
        {
            switch (style)
            {
                case LandStyle.Puff: return 0.1f;
                case LandStyle.Shimmer: return 0.25f;
                case LandStyle.Burst: return 0.6f;
                default: return 0f;
            }
        }

        public readonly bool HasCache;
        public readonly bool HasTopLiveUpgrade;
        public readonly float ChoosableAt;
        public readonly float GuardUntil;
        public readonly IReadOnlyList<RevealBeat> Beats;

        // Set on the schedule SkippedAt returns: everything has landed at
        // ChoosableAt, and only a top-tier flare (wall seconds) still plays.
        // A press ends the linger, so a skipped schedule has none.
        public readonly bool Skipped;
        public readonly float SkipFlareSeconds;

        private readonly float _guardSeconds;
        private readonly float _flareSeconds;

        private RevealSchedule(bool hasCache, bool hasTopLive, float choosableAt, float guardSeconds,
                               float flareSeconds, IReadOnlyList<RevealBeat> beats, bool skipped = false,
                               float skipFlare = 0f)
        {
            HasCache = hasCache;
            HasTopLiveUpgrade = hasTopLive;
            ChoosableAt = choosableAt;
            GuardUntil = choosableAt + guardSeconds;
            Beats = beats;
            Skipped = skipped;
            SkipFlareSeconds = skipFlare;
            _guardSeconds = guardSeconds;
            _flareSeconds = flareSeconds;
        }

        // The cache appears on a boss page always, and on any other page that
        // holds a card earning it (C3): a downgrade or an equal-quality card
        // never does, so the cache never promises a downgrade.
        public static bool CacheApplies(IReadOnlyList<RevealCard> cards, EncounterClass pageClass = EncounterClass.Normal)
        {
            if (pageClass == EncounterClass.Boss) return true;
            for (int i = 0; i < cards.Count; i++)
                if (cards[i].EarnsCache) return true;
            return false;
        }

        // cards: row order. choices: the page's k. pageClass: the page's
        // SourceClass (a boss page always gets the cache). seam: a duration
        // multiplier for a test (1 in play). haulTicks: the gold count's soft
        // ticks, played inside the wipe (0 on a page after the first).
        public static RevealSchedule For(IReadOnlyList<RevealCard> cards, int choices, float presetDisplay,
                                         EncounterClass pageClass = EncounterClass.Normal, float seam = 1f,
                                         int haulTicks = 0)
        {
            float W(float authored) => ReckoningTime.Wall(authored, presetDisplay, seam);
            int n = cards.Count;

            // A page with no cards has nothing to wrap or flip.
            bool cache = n > 0 && CacheApplies(cards, pageClass);
            bool topLive = false;
            for (int i = 0; i < n; i++) topLive |= cards[i].EarnsCeremony;

            var beats = new List<RevealBeat>();
            float wipe = W(WipeSeconds);
            beats.Add(new RevealBeat(RevealBeatKind.Wipe, 0f, wipe));

            int ticks = Math.Max(0, Math.Min(MaxHaulTicks, haulTicks));
            for (int i = 0; i < ticks; i++)
                beats.Add(new RevealBeat(RevealBeatKind.HaulTick, wipe * i / ticks, 0f, i));

            float t = wipe;
            var landAt = new float[n];
            if (n > 0)
            {
                // The cache stands, hums and snaps; a page without one deals
                // its cards out of the chip instead, a rustle per card.
                if (cache)
                {
                    float wait = W(OpenWaitSeconds), seal = W(SealSeconds);
                    beats.Add(new RevealBeat(RevealBeatKind.OpenWait, t, wait));
                    t += wait;
                    beats.Add(new RevealBeat(RevealBeatKind.Seal, t, seal));
                    t += seal;
                }

                float slide = W(SlideSeconds);
                beats.Add(new RevealBeat(RevealBeatKind.Slide, t, slide));
                if (!cache)
                {
                    for (int i = 0; i < n; i++)
                        beats.Add(new RevealBeat(RevealBeatKind.Deal, t + slide * (i + 1) / n, 0f, i));
                }

                t += slide;

                float flip = W(FlipSeconds);
                for (int i = 0; i < n; i++)
                {
                    var style = StyleOf(cards[i]);
                    float build = W(AnticipationSeconds(style));
                    if (build > 0f) beats.Add(new RevealBeat(RevealBeatKind.Anticipate, t, build, i, cards[i].Tier, style));
                    t += build;

                    beats.Add(new RevealBeat(RevealBeatKind.Flip, t, flip, i, cards[i].Tier, style));
                    t += flip;
                    landAt[i] = t;

                    if (i < n - 1) t += W(style == LandStyle.Burst ? CeremonySettleSeconds : SettleSeconds);
                }
            }

            int ups = 0;
            for (int i = 0; i < n; i++)
            {
                beats.Add(new RevealBeat(RevealBeatKind.Land, landAt[i], 0f, i, cards[i].Tier, StyleOf(cards[i])));
                if (i < choices) beats.Add(new RevealBeat(RevealBeatKind.Ember, landAt[i], 0f, i));
                if (cards[i].ShowsUp) beats.Add(new RevealBeat(RevealBeatKind.Chevron, landAt[i], 0f, i, ++ups));
            }

            beats.Add(new RevealBeat(RevealBeatKind.Choosable, t, 0f));
            if (topLive) beats.Add(new RevealBeat(RevealBeatKind.Linger, t, W(LingerSeconds)));

            // Stable by start, then by kind, so beats that share an instant
            // always come out in the same order.
            beats.Sort((a, b) =>
            {
                int c = a.Start.CompareTo(b.Start);
                return c != 0 ? c : ((int)a.Kind).CompareTo((int)b.Kind);
            });
            return new RevealSchedule(cache, topLive, t, W(GuardSeconds), W(FlareSeconds), beats);
        }

        public static LandStyle StyleOf(RevealCard card)
        {
            if (card.EarnsCeremony) return LandStyle.Burst;
            if (card.Tier >= RareTier) return LandStyle.Shimmer;
            return card.Tier == 2 ? LandStyle.Puff : LandStyle.Flat;
        }

        // Whether a beat is running at `t` (the controller's Act gate: while
        // true, a press completes the beat and calls nothing).
        public bool IsRunningAt(float t) => t < ChoosableAt;

        // Whether a Submit at `t` is swallowed by the guard after the last
        // card lands, so a held press cannot take an unseen card. A skip
        // moves the landing to the press, so the guard follows it.
        public bool GuardsAt(float t) => t >= ChoosableAt && t < GuardUntil;

        // The schedule after a press at `t`: the cache open, every card
        // face-up and the row choosable now, behind the same guard window; a
        // top-tier live upgrade keeps only its flare, and the linger is gone
        // (C4.5, C10). A press when nothing runs changes nothing.
        public RevealSchedule SkippedAt(float t)
        {
            if (Skipped || t >= ChoosableAt) return this;

            float now = Math.Max(0f, t);
            var beats = new List<RevealBeat>();
            foreach (var beat in Beats)
                if (beat.Kind != RevealBeatKind.Linger) beats.Add(beat);
            return new RevealSchedule(HasCache, HasTopLiveUpgrade, now, _guardSeconds, _flareSeconds, beats,
                skipped: true, skipFlare: HasTopLiveUpgrade ? _flareSeconds : 0f);
        }

        // The pitch of ladder step `step` (1 = the root, as recorded): a
        // major pentatonic, one octave per five steps. Flips climb it by
        // quality, the chevron notes by up-card, and the take lands back on
        // the root (C3, C6, C11).
        public static float LadderPitch(int step)
        {
            int i = Math.Max(0, step - 1);
            int semis = PentatonicSemitones[i % 5] + 12 * (i / 5);
            return (float)Math.Pow(2.0, semis / 12.0);
        }

        private static readonly int[] PentatonicSemitones = { 0, 2, 4, 7, 9 };
    }
}
