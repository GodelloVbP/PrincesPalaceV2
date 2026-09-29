"""Refuse unrouted delegation before it launches.

Wired as a PreToolUse hook on the Agent and Workflow tools; see
.claude/settings.json. Reads the hook payload on stdin, writes a deny
decision on stdout when the call would launch outside the five project
agents' pinned routing, and stays silent otherwise.

Why this exists as code rather than a line in CLAUDE.md: on 2026-09-19,
measuring the 14 days up to that point, Opus was requested on 121 of 377
agent launches and accounted for 59% of spend over the same window. The
CLAUDE.md wording that produced this -- "Prefer Sonnet; Opus only when a
mistake would be costly" -- reads as a judgment call, and "costly" turned
out to be whatever the orchestrator felt like in the moment. A written
preference is exactly as strong as whoever happens to be reading it; this
hook makes the routing a fact about the tool call instead.

Sonnet 5.5 is the default lane for the four working agents: reader,
fixer, implementer and verifier all run on `claude-sonnet-5-5` (or the
bare `sonnet` alias). Opus 5.5 is reserved for `senior` alone. That split
is what separates the cost tiers: routing a bounded issue to `implementer`
stays on Sonnet regardless of how the orchestrator judges its difficulty,
and the only way work reaches Opus is through `senior`'s `Escalation:`
gate below -- a process control (stating a reason before an
escalation-shaped change proceeds), not a per-call judgment call. Before
every implementation launch the orchestrator triages: is this doable as
a bounded change, or does it go deep architectural? Doable goes to
`implementer`; deep architectural goes to `senior`, with a stated reason.

`fixer` covers a trivial fix whose brief already carries file:line,
excerpt and failure scenario, confined to one file or system. It is
pinned to the same Sonnet set as reader, implementer and verifier, so an
Opus or haiku override on it is refused: the choice between `fixer` and
`implementer` is made by picking the subagent_type, never by a per-call
model field.

Five project agents exist under .claude/agents/ (reader/sonnet,
fixer/sonnet, implementer/sonnet, verifier/sonnet, senior/opus), each
pinned to one model set in its own frontmatter. claude-code-guide is a
built-in agent this project also allows, at haiku or sonnet. Nothing else
is a valid subagent_type for the Agent tool: general-purpose, Explore,
Plan, claude, and any typo or omission are all refused, because each is a
way to land back on the orchestrator's own model instead of the pinned
one. A model override on tool_input is refused for the same reason even
when the subagent_type is one of the five -- the whole point of a pinned
agent is that its model is not a per-call decision. isolation: "remote"
is refused because a remote launch is not covered by this hook's own
visibility into what ran.

The only Opus this project allows is exactly `claude-opus-5-5`, and it is
`senior`'s alone -- `implementer` is refused on `claude-opus-5-5`, the
bare `opus` alias, and `haiku`, the same way any model string containing
"opus" that isn't the exact id is refused by name for anyone else, the
same way a Fable model is.

`senior` also requires its brief to carry a line `Escalation: <criterion>`
naming one of two-failed-cycles, architecture, or cross-layer -- this hook
checks that line is present and well-formed before the call is allowed
through. The check exists here, in code, for the same reason the whole
hook exists: a written criterion is only as strong as whoever is reading
the brief, so the tool checks it instead of leaving it to whoever launches
or reviews the agent.

`unknown-cause` was removed on 2026-09-23: a generic "difficult" criterion
reopens exactly the judgment call this hook exists to close. `architecture`
and `cross-layer` are decided by triage before launch; `two-failed-cycles`
is the fallback when an implementer already tried and failed twice.

A model value containing "fable" is refused outright, regardless of
subagent_type: the owner decided on 2026-09-23 that Fable is not used on
this project, and a hook is the only way that decision survives whoever is
driving the session that day.

The Workflow tool is refused outright: it is a second way to run arbitrary
code outside the five pinned agents, and this hook has no per-workflow
routing table to check it against.

Deny mechanism chosen: the JSON hookSpecificOutput.permissionDecision shape
documented for PreToolUse (code.claude.com/docs/en/hooks.md), not the exit-2
alternative that page also documents. deny_broad_staging.py in this same
directory already uses this shape, so the two hooks agree on how a denial
looks on stdout; picking the other mechanism here for no reason would just
be a second shape to remember.

Malformed or missing input is allowed through rather than erroring, same
posture as deny_broad_staging.py: a hook that can crash a tool call on bad
input is a liability, not a safeguard.
"""

import json
import re
import sys

# subagent_type values the Agent tool may be launched with, each pinned to
# the model(s) named here. reader/fixer/implementer/verifier/senior are this
# project's own agents (.claude/agents/*.md); claude-code-guide is the one
# built-in agent this project also allows, since it answers questions about
# Claude Code itself rather than doing project work.
PINNED_MODELS = {
    "reader": {"sonnet", "claude-sonnet-5-5"},
    "fixer": {"sonnet", "claude-sonnet-5-5"},
    "implementer": {"sonnet", "claude-sonnet-5-5"},
    "verifier": {"sonnet", "claude-sonnet-5-5"},
    "senior": {"claude-opus-5-5"},
    "claude-code-guide": {"haiku", "sonnet"},
}

# The three criteria a `senior` brief's "Escalation: <criterion>" line may
# name. Kept exactly these three on purpose -- see module docstring: a
# generic "difficult" criterion would reopen the judgment call this hook
# exists to close.
ESCALATION_CRITERIA = (
    "two-failed-cycles",
    "architecture",
    "cross-layer",
)

ESCALATION_LINE_RE = re.compile(
    r"^Escalation:\s*(" + "|".join(ESCALATION_CRITERIA) + r")\b",
    re.MULTILINE,
)

ROUTING_ADVICE = (
    "Use the Agent tool with subagent_type set to one of: "
    + ", ".join(sorted(PINNED_MODELS))
    + "."
)


def deny(reason):
    return {
        "hookSpecificOutput": {
            "hookEventName": "PreToolUse",
            "permissionDecision": "deny",
            "permissionDecisionReason": reason,
        }
    }


def check_agent(tool_input):
    """Return a deny reason for an Agent call, or None to allow it."""
    subagent_type = tool_input.get("subagent_type")
    if not isinstance(subagent_type, str) or subagent_type not in PINNED_MODELS:
        seen = subagent_type if isinstance(subagent_type, str) and subagent_type else "(none given)"
        return (
            "subagent_type '{}' is not a routed agent. {}".format(seen, ROUTING_ADVICE)
        )

    model = tool_input.get("model")
    if model is not None and model != "":
        if "fable" in model.lower():
            return (
                "Fable is not used on this project (owner decision, 2026-09-23). "
                "Drop the model field and let '{}' use its own pinned "
                "model.".format(subagent_type)
            )
        if "opus" in model.lower() and model != "claude-opus-5-5":
            return (
                "'{}' is not claude-opus-5-5, and no other Opus is allowed on "
                "this project (owner decision, 2026-09-24). Drop the model "
                "field and let '{}' use its own pinned model.".format(
                    model, subagent_type
                )
            )
        allowed = PINNED_MODELS[subagent_type]
        if model not in allowed:
            return (
                "'{}' is pinned to {} and its model is not a per-call choice. "
                "Drop the model field from this Agent call and let it use its "
                "own pinned model.".format(subagent_type, " or ".join(sorted(allowed)))
            )

    isolation = tool_input.get("isolation")
    if isolation == "remote":
        return (
            "isolation: \"remote\" is not allowed for routed agents. Launch "
            "'{}' without isolation instead.".format(subagent_type)
        )

    if subagent_type == "senior":
        prompt = tool_input.get("prompt")
        if not isinstance(prompt, str) or not ESCALATION_LINE_RE.search(prompt):
            return (
                "'senior' requires a brief with a line 'Escalation: <criterion>' "
                "naming one of: " + ", ".join(ESCALATION_CRITERIA) + ". Add that "
                "line to the prompt, or launch 'implementer' instead."
            )

    return None


def check(tool_name, tool_input):
    """Return a deny reason for this tool call, or None to allow it."""
    if tool_name == "Agent":
        return check_agent(tool_input)

    if tool_name == "Workflow":
        return (
            "Workflow is not a routed path for delegation. "
            + ROUTING_ADVICE
        )

    return None


def main():
    try:
        payload = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        return 0

    if not isinstance(payload, dict):
        return 0

    tool_name = payload.get("tool_name")
    tool_input = payload.get("tool_input")
    if not isinstance(tool_input, dict):
        tool_input = {}

    if not isinstance(tool_name, str) or not tool_name:
        return 0

    reason = check(tool_name, tool_input)
    if reason:
        json.dump(deny(reason), sys.stdout)

    return 0


if __name__ == "__main__":
    sys.exit(main())
