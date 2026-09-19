"""Refuse unrouted delegation before it launches.

Wired as a PreToolUse hook on the Agent and Workflow tools; see
.claude/settings.json. Reads the hook payload on stdin, writes a deny
decision on stdout when the call would launch outside the four project
agents' pinned routing, and stays silent otherwise.

Why this exists as code rather than a line in CLAUDE.md: on 2026-09-19,
measuring the 14 days up to that point, Opus was requested on 121 of 377
agent launches and accounted for 59% of spend over the same window. The
CLAUDE.md wording that produced this -- "Prefer Sonnet; Opus only when a
mistake would be costly" -- reads as a judgment call, and "costly" turned
out to be whatever the orchestrator felt like in the moment. A written
preference is exactly as strong as whoever happens to be reading it; this
hook makes the routing a fact about the tool call instead.

Four project agents exist under .claude/agents/ (reader/haiku,
implementer/sonnet, verifier/sonnet, architect/opus), each pinned to one
model in its own frontmatter. claude-code-guide is a built-in agent this
project also allows, at haiku or sonnet. Nothing else is a valid
subagent_type for the Agent tool: general-purpose, Explore, Plan, claude,
and any typo or omission are all refused, because each is a way to land back
on the orchestrator's own model instead of the pinned one. A model override
on tool_input is refused for the same reason even when the subagent_type is
one of the four -- the whole point of a pinned agent is that its model is
not a per-call decision. isolation: "remote" is refused because a remote
launch is not covered by this hook's own visibility into what ran.

The Workflow tool is refused outright: it is a second way to run arbitrary
code outside the four pinned agents, and this hook has no per-workflow
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
import sys

# subagent_type values the Agent tool may be launched with, each pinned to
# the model(s) named here. reader/implementer/verifier/architect are this
# project's own agents (.claude/agents/*.md); claude-code-guide is the one
# built-in agent this project also allows, since it answers questions about
# Claude Code itself rather than doing project work.
PINNED_MODELS = {
    "reader": {"haiku"},
    "implementer": {"sonnet"},
    "verifier": {"sonnet"},
    "architect": {"opus"},
    "claude-code-guide": {"haiku", "sonnet"},
}

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
