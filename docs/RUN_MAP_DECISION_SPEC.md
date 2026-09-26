# What the map has to do

Status: 2026-09-26. Superseded in part: the owner rejected the "layer as a place / dens" candidate in section 5 and correlating the world to real-life objects; see DIRECTION_BIBLE.md. Live copy: https://claude.ai/artifact/26Hsv4btavjMSYnXgiDZuL

*Prince's Palace v2 · run map · decision spec · 2026-09-26*

This page defines the decision the map must create and the story it must carry, before any structure or screen is chosen. Every map concept is judged against it. It replaces the pick-a-node premise that the earlier plan and prototypes all started from.

## 1 · The fiction the map serves

- **World:** The owner's demons and trauma. Its depth maps to how deep into his mind the party is. A leg is a layer, and deeper layers are longer and heavier.
- **Player:** One of the deceased animal souls Prince pulled into his palace. The player knows no more than the souls do.
- **Prince:** Sends them in without explaining. He is fully aware of what he asks of them and follows his own priorities and agenda.
- **Clearing:** Clearing does help the owner. The game never says so outright.
- **Trauma:** Defined by the author, and shown vaguely in play.
- **Grey:** The grey is Prince using souls who have no stake in this. It lives in the story. The map does not score it.

## 2 · The decision

- **Core:** Between fights, the player decides how much risk to put the party through, given its HP, gold and build, and for which reward.
- **Thoroughness:** How much of this layer to clear before facing its heart (the boss): go straight for it, or take on optional ground first at a cost in HP. This one has a story payoff: clearing more helps the owner more, and the player can only notice that indirectly.
- **Horizon:** The whole current layer is visible, so a route can be planned several steps ahead. Nothing is hidden behind "?".
- **Knowledge:** Before committing, the player sees each option's type, promised reward and relative strength, and what the pick closes off. Inspecting never commits.
- **Variety:** Layers differ in shape and in size, and grow with depth. Two runs should not play the same.

## 3 · The two reading levels

**Surface test.** Show a screenshot to someone who has never played. It must read as "a strange world we were sent into to clear monsters", not as a board game and not as a hospital.

**Subtext rules.**

- The world visibly responds to being cleared. That response reads as someone slowly getting better, without saying it.
- Fragments of an ordinary life recur across layers and runs: objects, rooms, a window.
- How much of that shows is gated by meta-progression, so early runs stay ambiguous.

**Never:**

- text or UI saying you are healing him;
- a good/evil or mood meter;
- a victory fanfare framed as curing him;
- literal clinical imagery early on;
- a pressure mechanic (owner decision).

## 4 · Concepts judged against it

| Concept | Core decision | Thoroughness | Horizon | Surface test | World responds | Verdict |
|---|---|---|---|---|---|---|
| **A** · branching graph (plan rev 2) | Yes | No: one path, nothing optional | Yes | Reads as a board game | No: nodes, not a world | Its decision logic is sound, but the presentation is wrong and thoroughness is missing |
| **B** · reality lanes | Weak | No | Yes | Labelled strips | No | Drop |
| **C** · contract board | Only at the board | No | Partial | Contradicts the premise: Prince explains nothing, so he wouldn't hand out terms | No | Drop |
| **1** · exits in the scene | Yes | No | No | Strong | Yes, in the scene art | Right presentation, missing horizon. Keep as a presentation idea. |
| **2** · stakes per room | Solvable | No | Yes | It's a menu | No | Drop as a map. Risk against state is already the core. |
| **3** · palace floor plan | Answers a different question: the palace is where the souls are pulled *to*, which is the hub, not the map. (spans Core/Thoroughness/Horizon/Surface test/World responds) | | | | | Out of scope |

Nothing passes. A has the right decision engine and the wrong presentation. Concept 1 has the right presentation and no horizon. None of them offers thoroughness.

## 5 · Candidate that follows from the spec

### The layer as a place

Each layer is one painted place, not an icon graph. Its dens (fights, a shop, a rest, events) sit in the painting, joined by paths that are part of the terrain. The heart, which is the boss, is visible from the start and reachable once a set path has been walked.

- **Decision.** Choose which dens to clear on the way, and whether to detour for optional ones. The detours cost HP and time, and pay in rewards.
- **Surface.** You see a place, and you see where the monsters are in it.
- **Subtext.** Each cleared den changes its part of the painting. The darkness recedes, and what shows underneath is a fragment of an ordinary life, gated by meta-progression.
- **Length and variety.** Deeper layers are larger places with more dens. That covers the owner's "longer or varying" without a fixed step count.

**Weaknesses.**

- **It can be solved.** Clearing more helps the owner, so if full clearing is safe for a strong party, "clear everything" becomes the only answer and the decision dies. Attrition has to bite: scarce rests, and damage that carries over.
- **Art cost.** Each layer needs a painting with a cleared and an uncleared state per den region. That's modular overlays, but it's still one painted place per layer type.
- **Gamepad movement.** Moving between dens in a painting is harder on a gamepad than moving along columns. It needs the navigation-group treatment the gamepad work already uses.
- **It's a new generator.** A region graph replaces the column graph, and the rev-2 contracts carry over only in part: promises, strength and the inspect rule do; the column and route rules don't.

## 6 · Next step

Storyboard two options side by side as still frames, before any interactive build:

- the layer as a place;
- as a control, A's graph drawn into a painted place, which keeps the rev-2 generator.

Frames for each:

- arriving in a layer;
- choosing a detour;
- a den cleared, with the world responding;
- the heart;
- the same place on a later run, with more of the subtext showing.

Judge them by the surface test and by whether the thoroughness decision feels like a decision. Then rewrite the plan around the winner.

Assumption: "both" for the trauma means it is defined by the author and shown vaguely in play.
