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

        // Earns the held beat, the flare and the linger: a live Legendary
        // over a worn item below Legendary (a relic: any top-tier live one).
        public bool EarnsHold => LiveUpgrade && IsTop && (IsRelic || WornTier < RevealSchedule.TopTier);
    }

    public enum RevealBeatKind
    {
        Wipe,          // the page's entrance: the frame's wipe on the first page, the map chip flying in on a later one (C1, C2)
        HaulTick,      // one soft tick of the gold count, inside the wipe (C2); Card is the tick's index
        OpenWait,      // the cache sits closed before it opens by itself
        Seal,          // the seal snaps
        Slide,         // k + 2 cards slide out face-down
        Deal,          // no cache: the cards deal face-up; Card -1, spans the whole deal
        Hold,          // silent beat before a top-tier live upgrade's own flip; Card is that card
        Flip,          // PitchStep is the flip clip
        Land,          // the card is face-up; Style says how it arrives
        Ember,         // a choice ember lights (rides the landing)
        Chevron,       // a card whose face shows the up-chevron; PitchStep is its ordinal in row order
        Choosable,     // the row accepts Take, Hone and Leave
        Linger,        // non-blocking; the top-tier halo holds at full strength
    }

    public enum LandStyle
    {
        Flat,          // Common: a flat tap
        Puff,          // Uncommon: a cloth puff
        Shimmer,       // Rare, and a top tier that is not a live upgrade
        Burst,         // top-tier live upgrade: burst, flare, low bell
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
    public sealed class RevealSchedule
    {
        public const int RareTier = 3;
        public const int TopTier = 4;

        // Authored at the default battle speed (C4 table).
        public const float WipeSeconds = 0.34f;
        public const float DealSeconds = 0.25f;
        public const float OpenWaitSeconds = 0.2f;
        public const float SealSeconds = 0.12f;
        public const float SlideSeconds = 0.25f;
        public const float FlipSeconds = 0.12f;
        public const float FlipStaggerSeconds = 0.06f;
        public const float HoldSeconds = 0.25f;
        public const float FlareSeconds = 0.12f;
        public const float LingerSeconds = 1.2f;
        public const float GuardSeconds = 0.15f;

        // The gold count lands in at most this many soft ticks (C2).
        public const int MaxHaulTicks = 8;

        // Wipe start to choosable, at every preset. A page of up to five cards
        // fits unshortened; a longer one gives up its waits in the order of Fit.
        public const float CapSeconds = 1.6f;

        // The floors Fit shortens to: the stagger and the hold keep a readable
        // sliver, the open wait can go to nothing.
        public const float MinStaggerSeconds = 0.02f;
        public const float MinHoldSeconds = 0.1f;

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

        // The held beat, the flare and the linger key on a Legendary live
        // upgrade over a worn item below Legendary, boss page or not.
        public static bool HoldsFor(RevealCard card) => card.EarnsHold;

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
            for (int i = 0; i < n; i++) topLive |= HoldsFor(cards[i]);

            var beats = new List<RevealBeat>();
            float wipe = W(WipeSeconds);
            beats.Add(new RevealBeat(RevealBeatKind.Wipe, 0f, wipe));

            int ticks = Math.Max(0, Math.Min(MaxHaulTicks, haulTicks));
            for (int i = 0; i < ticks; i++)
                beats.Add(new RevealBeat(RevealBeatKind.HaulTick, wipe * i / ticks, 0f, i));

            float t = wipe;
            var landAt = new float[n];
            if (!cache && n > 0)
            {
                float deal = W(DealSeconds);
                beats.Add(new RevealBeat(RevealBeatKind.Deal, wipe, deal));
                for (int i = 0; i < n; i++) landAt[i] = wipe + deal * (i + 1) / n;
                t = wipe + deal;
            }
            else if (cache)
            {
                Fit(W, n, CountHolds(cards), out float wait, out float stagger, out float hold);
                float seal = W(SealSeconds), slide = W(SlideSeconds), flip = W(FlipSeconds);
                if (wait > 0f) beats.Add(new RevealBeat(RevealBeatKind.OpenWait, t, wait));
                t += wait;
                beats.Add(new RevealBeat(RevealBeatKind.Seal, t, seal));
                t += seal;
                beats.Add(new RevealBeat(RevealBeatKind.Slide, t, slide));
                t += slide;

                // Flip i starts `stagger` after flip i-1 started; a top-tier
                // live upgrade holds before its own flip and delays the rest.
                float start = t;
                for (int i = 0; i < n; i++)
                {
                    if (i > 0) start += stagger;
                    if (HoldsFor(cards[i]))
                    {
                        beats.Add(new RevealBeat(RevealBeatKind.Hold, start, hold, i));
                        start += hold;
                    }
                    beats.Add(new RevealBeat(RevealBeatKind.Flip, start, flip, i, cards[i].Tier));
                    landAt[i] = start + flip;
                }
                t = landAt[n - 1];
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
            if (HoldsFor(card)) return LandStyle.Burst;
            if (card.Tier >= RareTier) return LandStyle.Shimmer;
            return card.Tier == 2 ? LandStyle.Puff : LandStyle.Flat;
        }

        private static int CountHolds(IReadOnlyList<RevealCard> cards)
        {
            int h = 0;
            for (int i = 0; i < cards.Count; i++) if (HoldsFor(cards[i])) h++;
            return h;
        }

        // SHORTENING ORDER when a cache page would pass the cap (two holds
        // on four cards can, as can one hold on seven): the open wait goes first, then the flip
        // stagger down to its floor, then each hold down to its floor, then
        // holds and stagger to nothing. The wipe, seal, slide and the flips
        // themselves never shorten.
        private static void Fit(Func<float, float> w, int n, int holds, out float wait, out float stagger, out float hold)
        {
            float waitW = w(OpenWaitSeconds), staggerW = w(FlipStaggerSeconds), holdW = w(HoldSeconds);
            float fixedPart = w(WipeSeconds) + w(SealSeconds) + w(SlideSeconds) + w(FlipSeconds);
            int gaps = Math.Max(0, n - 1);

            float over = fixedPart + waitW + gaps * staggerW + holds * holdW - CapSeconds;
            if (over > 0f)
            {
                float cut = Math.Min(waitW, over);
                waitW -= cut;
                over -= cut;
            }
            if (over > 0f && gaps > 0)
            {
                float cut = Math.Min(staggerW - w(MinStaggerSeconds), over / gaps);
                staggerW -= cut;
                over -= cut * gaps;
            }
            if (over > 0f && holds > 0)
            {
                float cut = Math.Min(holdW - w(MinHoldSeconds), over / holds);
                holdW -= cut;
                over -= cut * holds;
            }

            // Past every floor (many holds on one page): the cap wins over
            // the sliver, holds first, then the stagger.
            if (over > 0f && holds > 0)
            {
                float cut = Math.Min(holdW, over / holds);
                holdW -= cut;
                over -= cut * holds;
            }
            if (over > 0f && gaps > 0) staggerW = Math.Max(0f, staggerW - over / gaps);
            wait = waitW;
            stagger = staggerW;
            hold = holdW;
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
