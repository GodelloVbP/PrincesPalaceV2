import sys
frames=open('frames.svgs').read().split('\n<!--FRAME-->\n')
caps=[
("Arriving in a realm","The camera opens on the heart at the bottom: the Forest Troll, with the Elder Treant and the Ironback Beetle standing beside it. Then it rises to the party at the top. The whole realm is visible: the spine, a fork (shop or fight), and two pockets that each hold an anchor. Dotted red roots tie each anchor to the heart.","The player learns the goal before taking a step: which monsters will stand with the boss, and where they can be reached."),
("Choosing a pocket","Close-up at a pocket's mouth. The card compares the two open choices. Spine: two Giant Rats, strength 2, gold plus a consumable, and +8% HP for walking past the pocket. Pocket: the Elder Treant, strength 3, a spell book with treasure behind it, two extra rooms, and if left alive it stands with the boss.","Prize and HP against restraint and breath. The answer depends on the party's HP and build, which is the decision the spec asks for."),
("An anchor falls","Back on the map after the Treant fight. Its node withers, its root dies back toward the heart, and warm light spreads around the cleared ground. The spine fight the party skipped is greyed out. An inset shows the heart far below, with the Treant's place beside it empty.","The world responds in two places at once: the realm eases where the party fought, and the boss visibly loses an ally. Nothing on screen counts or labels it."),
("The heart","The bottom of the realm. The Forest Troll waits with the Ironback Beetle, the anchor this party left alive, still at its side, its root running back up to the pocket the party skipped. The card says it plainly: the fight is Troll plus Beetle, and the drop gains one reward for the Beetle.","The route the party chose has built this fight. Players feel the consequence before they commit to it."),
("The same realm, run 12","Same realm, later run. The walked route glows, the dead Treant's node stays withered, and a warm dotted thread now runs through the whole realm. The thread is a recurring motif, seen in every realm and clearer each run, gated by meta-progression. It never explains itself.","The subtext layer: an emotional motif rather than a real-life object, which the bible rules out. Early runs show almost none of it."),
]
rows=[
("Slay the Spire","Boss icon visible from the start of each act. Every path is the same length. An optional burning elite pays a key for a later goal.","Showing the boss is not new. Our different-length routes are the real departure from its map.","Keep. Don't sell the visible boss as novel."),
("Hades","Each door previews its reward. Chaos gates cost HP now for a payoff later. Healing is scarce, so HP is a currency.","A pocket is a Chaos gate. It only works while healing stays scarce.","Keep rest at one per realm. Add no casual heals."),
("Shadow of Mordor / War","Kill a warchief's bodyguards first, and the ones left alive stand with him in the fight.","This is the anchor idea, done better than our abstract wards: easier to read, and it needs no new ability system.","<b>Changed:</b> living anchors join the boss fight."),
("Balatro","The boss blind is shown up front. Skipping a blind pays a tag, so restraint has its own reward.","Our spine paid nothing, which is why \"always take pockets when healthy\" threatened to dominate.","<b>Changed:</b> walking past a pocket heals +8%."),
("FTL","Explore as much of a sector as you dare, held in check by the rebel fleet behind you.","You banned pressure. Without the fleet, FTL collapses to \"explore everything\". HP before the boss plus the bypass heal have to do the fleet's job.","Risk confirmed. The bot gate stays mandatory."),
("Into the Breach","Perfect information; the difficulty lives in execution.","Supports showing exact enemies: the challenge stays in the turn-based fight.","Keep full enemy previews."),
("Darkest Dungeon II","The journey between nodes is part of the game. Critics found the travel slow and the roads repetitive.","A map that wants attention every step wears thin.","<b>Changed:</b> skippable 2.5 s opener; walks stay capped."),
("Monster Train","Two short routes per ring, each with visible rewards.","Same role as our forks (which waypoint), and it confirms that forks should stay short.","Keep."),
("Loop Hero","The world visibly fills in from your choices. Its boss arrives on a meter, a pressure mechanic you've banned.","Validates the easing paintings. Its meter is the part to avoid.","Keep the easing; no meter."),
]
def tr(r): return '<tr>'+''.join(f'<td>{c}</td>' for c in r)+'</tr>'
html=f'''<title>Run Map Storyboards</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Cormorant+Garamond:wght@500;600;700&family=Source+Sans+3:wght@400;600&family=IBM+Plex+Mono:wght@400;500&display=swap">
<style>
/* Layout: reading column; frames run full column width, each followed by its caption pair. */
:root{{--bg:#f3f1f5;--panel:#fff;--fg:#26222e;--muted:#6b6478;--line:#d9d4e0;--accent:#6b4fa0;--warm:#b5782c;
--display:"Cormorant Garamond",Georgia,serif;--body:"Source Sans 3",system-ui,sans-serif;--mono:"IBM Plex Mono",ui-monospace,monospace}}
@media (prefers-color-scheme: dark){{:root:not([data-theme="light"]){{--bg:#17151c;--panel:#211e28;--fg:#ebe6f2;--muted:#a39bb3;--line:#363140;--accent:#b39ae0;--warm:#e0a85a;color-scheme:dark}}}}
:root[data-theme="dark"]{{--bg:#17151c;--panel:#211e28;--fg:#ebe6f2;--muted:#a39bb3;--line:#363140;--accent:#b39ae0;--warm:#e0a85a;color-scheme:dark}}
body{{background:var(--bg);color:var(--fg);font-family:var(--body);font-size:16px;line-height:1.55}}
.wrap{{max-width:1120px;margin:0 auto;padding-inline:20px;padding-block:32px 64px}}
h1,h2,h3{{font-family:var(--display);text-wrap:balance;line-height:1.15;margin:0}}
h1{{font-size:clamp(2rem,5vw,2.8rem)}} h2{{font-size:1.75rem;margin:52px 0 14px}} h3{{font-size:1.45rem}}
p{{margin:0 0 12px;max-width:68ch}}
.eyebrow{{font-family:var(--mono);font-size:.75rem;letter-spacing:.08em;text-transform:uppercase;color:var(--muted)}}
.lede{{font-size:1.12rem;margin-top:12px}}
.tbl{{overflow-x:auto;background:var(--panel);border:1px solid var(--line);border-radius:8px}}
table{{border-collapse:collapse;width:100%;min-width:760px;font-size:.93rem}}
th,td{{text-align:left;padding:10px 12px;border-bottom:1px solid var(--line);vertical-align:top}}
th{{font-family:var(--mono);font-size:.72rem;letter-spacing:.06em;text-transform:uppercase;color:var(--muted);font-weight:500}}
td:first-child{{font-weight:600;white-space:nowrap}} tr:last-child td{{border-bottom:0}}
td b{{color:var(--accent)}}
.changes{{display:grid;grid-template-columns:repeat(auto-fit,minmax(240px,1fr));gap:14px;margin-top:6px}}
.change{{background:var(--panel);border:1px solid var(--line);border-radius:8px;padding:14px 16px}}
.change h3{{font-size:1.2rem;margin-bottom:4px}} .change p{{margin:0;font-size:.95rem;color:var(--muted)}}
.frame{{margin-top:40px;display:grid;gap:12px}}
.frame .num{{font-family:var(--mono);font-size:.75rem;color:var(--muted);letter-spacing:.08em}}
.frame svg{{display:block;width:100%;height:auto;border-radius:8px}}
.pair{{display:grid;grid-template-columns:minmax(0,3fr) minmax(0,2fr);gap:22px}}
@media (max-width:760px){{.pair{{grid-template-columns:minmax(0,1fr)}}}}
.why{{border-left:3px solid var(--warm);padding-left:14px;color:var(--fg)}}
.note{{color:var(--muted);font-size:.9rem}}
</style>
<div class="wrap">
<div class="eyebrow">Prince's Palace v2 · run map · storyboards · 13 Oct 2026</div>
<h1>The map builds the boss, in five frames</h1>
<p class="lede">First, the plan held up against nine acclaimed games. Three of them changed it. Then, the five frames your decision spec asks for, drawn with the changes in.</p>

<h2>Against the games</h2>
<div class="tbl"><table><thead><tr><th>Game</th><th>What it does</th><th>What it says about our plan</th><th>Verdict</th></tr></thead><tbody>
{''.join(tr(r) for r in rows)}
</tbody></table></div>

<h2>What changed</h2>
<div class="changes">
<div class="change"><h3>Anchors stand with the boss</h3><p>Beat an elite on the map and it is gone. Leave it alive and it fights beside the boss. This replaces the abstract wards, so no new combat system is needed. The enemy stage has three slots, so the cap is two anchors per realm.</p></div>
<div class="change"><h3>Restraint pays</h3><p>Walking the spine past a pocket heals 8%. Bypassing becomes a choice with a reward, so "always pocket when healthy" stops being free.</p></div>
<div class="change"><h3>A time budget</h3><p>One skippable opening pan, at most 2.5 s, from the heart up to the party. Walks stay capped.</p></div>
<div class="change"><h3>An honest pitch</h3><p>A visible boss is standard. The pitch is that the boss you face is the one your route left behind.</p></div>
</div>

<h2>Storyboards</h2>
<p class="note">Greybox frames at 16:9: shapes and placement, not final art. Medallions: circle = fight, hexagon = anchor elite, square = shop, rounded square = event, triangle = rest, diamond = treasure, chest = cache.</p>
'''
for i,(f,(t,what,why)) in enumerate(zip(frames,caps),1):
    html+=f'<section class="frame"><div class="num">FRAME {i} OF 5</div><h3>{t}</h3><div role="img" aria-label="Storyboard frame {i}: {t}">{f}</div><div class="pair"><p>{what}</p><p class="why">{why}</p></div></section>'
html+='''
<h2>What to judge</h2>
<p>Your spec's two tests. Surface: does frame 1 read as "a strange realm full of monsters", not a board game? (In greybox, no. The test only means something with the painted realm and real monster portraits on the medallions. Treat that as the open risk.) Thoroughness: does frame 2 feel like a decision you'd hesitate over?</p>
<p class="note">Full plan: <code>docs/PLAN_RUN_MAP.md</code> on branch <code>claude/sweet-goodall-g4yi0n</code>.</p>
</div>'''
open(sys.argv[1],'w').write(html)
