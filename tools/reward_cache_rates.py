#!/usr/bin/env python3
"""A9: reward-cache frequency report over a tools/bot.ps1 batch.

Reads <batch>/pages.jsonl (and shard-*/pages.jsonl), written by
BalanceBotRunner.WritePages. Gear pages only. Act = step // 8 + 1 (a realm
is 8 steps). Each table compares two rules:

  old   cache = a live card of quality Rare+; beat = a live Legendary;
        wall = the C4 table arithmetic (no cache 0.59 s).
  new   the shipped rule, taken from RevealSchedule.For in the bot run
        (fields cache/beat/wall): the cache needs a live Rare+ card above
        the worn tier, boss pages always cache, the beat needs a live
        Legendary over a worn tier below 4.

Usage: python tools/reward_cache_rates.py <batch-dir>
"""
import glob
import json
import os
import sys

RARE_PLUS = ("Rare", "Legendary")


def old_wall(cards, cache, beat):
    if not cache:
        return 0.59
    return 0.34 + 0.2 + 0.12 + 0.25 + (len(cards) - 1) * 0.06 + 0.12 + (0.25 if beat else 0.0)


def load(batch):
    paths = [os.path.join(batch, "pages.jsonl")] + sorted(glob.glob(os.path.join(batch, "shard-*", "pages.jsonl")))
    for p in paths:
        if os.path.exists(p):
            with open(p, encoding="utf-8") as f:
                for line in f:
                    if line.strip():
                        yield json.loads(line)


def pct(k, n):
    return "%.1f%%" % (100.0 * k / n)


def row(label, pages):
    n = len(pages)
    if n == 0:
        return [label, 0] + ["-"] * 7
    oc = [any(c["live"] and c["q"] in RARE_PLUS for c in p["cards"]) for p in pages]
    ob = [any(c["live"] and c["q"] == "Legendary" for c in p["cards"]) for p in pages]
    ow = sum(old_wall(p["cards"], c, b) for p, c, b in zip(pages, oc, ob)) / n
    nc = sum(p["cache"] for p in pages)
    nb = sum(p["beat"] for p in pages)
    nw = sum(p["wall"] for p in pages) / n
    return [label, n, pct(sum(oc), n), pct(nc, n), pct(sum(ob), n), pct(nb, n),
            "%.2f s" % ow, "%.2f s" % nw, "%d" % sorted(p["favor"] for p in pages)[n // 2]]


def table(rows, first):
    head = [first, "gear pages", "cache old", "cache new", "beat old", "beat new",
            "wall old", "wall new", "median Favor"]
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
    rows = [row("act %d" % a, by_act[a]) for a in sorted(by_act)]
    rows.append(row("all", pages))
    rows.append(row("non-boss", [p for p in pages if p["encounter"] != "Boss"]))
    print(table(rows, "act"))
    print()
    enc = {}
    for p in pages:
        enc.setdefault(p["encounter"], []).append(p)
    print(table([row(k, enc[k]) for k in sorted(enc)], "encounter"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
