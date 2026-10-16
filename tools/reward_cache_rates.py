#!/usr/bin/env python3
"""A9: reward-cache frequency report over a tools/bot.ps1 batch.

Reads <batch>/pages.jsonl (and shard-*/pages.jsonl), written by
BalanceBotRunner.WritePages. Per act (act = step // 8 + 1, the realm
length) it prints, for GEAR pages only:

  cache   a card of quality Rare+ that is a live upgrade (LiveUpgrade.Of)
  beat    a Legendary card that is a live upgrade
  Also printed: how often Rare+ / Legendary is on the page at all, so the
  gap to cache/beat is the share that is not a live upgrade.
  wall    mean predicted seconds to choosable at 1x, from the C4 table in
          docs/PLAN_REWARD_SCREENS.md (placeholder until RevealSchedule
          lands): no cache 0.59; cache = 0.34 + 0.2 + 0.12 + 0.25 +
          (n-1)*0.06 + 0.12, plus 0.25 when the beat holds.

Usage: python tools/reward_cache_rates.py <batch-dir> [--md]
"""
import glob
import json
import os
import sys

RARE_PLUS = ("Rare", "Legendary")
FAVOR_BANDS = ((0, 7), (8, 13), (14, 19), (20, 10 ** 6))


def wall(cards, cache, beat):
    if not cache:
        return 0.59
    return 0.34 + 0.2 + 0.12 + 0.25 + (len(cards) - 1) * 0.06 + 0.12 + (0.25 if beat else 0.0)


def load(batch):
    paths = [os.path.join(batch, "pages.jsonl")] + sorted(glob.glob(os.path.join(batch, "shard-*", "pages.jsonl")))
    for p in paths:
        if not os.path.exists(p):
            continue
        with open(p, encoding="utf-8") as f:
            for line in f:
                if line.strip():
                    yield json.loads(line)


def classify(page):
    cards = page["cards"]
    cache = any(c["live"] and c["q"] in RARE_PLUS for c in cards)
    beat = any(c["live"] and c["q"] == "Legendary" for c in cards)
    return cache, beat, wall(cards, cache, beat)


def row(label, pages):
    n = len(pages)
    if n == 0:
        return [label, 0, "-", "-", "-", "-", "-"]
    res = [classify(p) for p in pages]
    favors = sorted(p["favor"] for p in pages)
    rare_any = sum(any(c["q"] in RARE_PLUS for c in p["cards"]) for p in pages)
    top_any = sum(any(c["q"] == "Legendary" for c in p["cards"]) for p in pages)
    return [
        label, n,
        "%.1f%% / %.1f%%" % (100.0 * rare_any / n, 100.0 * top_any / n),
        "%.1f%%" % (100.0 * sum(r[0] for r in res) / n),
        "%.1f%%" % (100.0 * sum(r[1] for r in res) / n),
        "%.2f s" % (sum(r[2] for r in res) / n),
        "%d / %d / %d" % (favors[0], favors[n // 2], favors[-1]),
    ]


def table(rows, first):
    head = [first, "gear pages", "Rare+ / Legendary on page (live or not)", "cache", "top-tier beat", "mean wall", "Favor min/med/max"]
    out = ["| " + " | ".join(head) + " |", "|" + "---|" * len(head)]
    out += ["| " + " | ".join(str(c) for c in r) + " |" for r in rows]
    return "\n".join(out)


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 1
    pages = [p for p in load(sys.argv[1]) if p["kind"] == "Gear" and p["cards"]]
    by_act = {}
    for p in pages:
        by_act.setdefault(p["step"] // 8 + 1, []).append(p)
    print(table([row("act %d" % a, by_act[a]) for a in sorted(by_act)] + [row("all", pages)], "act"))
    print()
    bands = []
    for lo, hi in FAVOR_BANDS:
        sel = [p for p in pages if lo <= p["favor"] <= hi]
        bands.append(row("%d-%s" % (lo, "up" if hi > 999 else hi), sel))
    print(table(bands, "Favor"))
    print()
    enc = {}
    for p in pages:
        enc.setdefault(p["encounter"], []).append(p)
    print(table([row(k, enc[k]) for k in sorted(enc)], "encounter"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
