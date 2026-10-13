"""PreToolUse hook (.claude/settings.json) that denies unrouted delegation.

Routing is a fact about the tool call, not a line of prose. Routing and
escalation rules: CLAUDE.md "Agent routing". Fixer trial: docs/WORKFLOW.md.

Denies an Agent call when:
- subagent_type is not one of PINNED_MODELS;
- `model` is set to anything outside that agent's pin;
- `model` names Fable, or an Opus other than exactly claude-opus-5-5;
- isolation is "remote";
- subagent_type is `senior` and the prompt lacks a valid `Escalation:` line.

Denies the Workflow tool outright. Allows everything else, including
malformed input to non-Agent tools (a hook must not crash a tool call).
Denials use the JSON permissionDecision shape, as deny_broad_staging.py does.
"""

import json
import re
import sys

# Allowed subagent_types and the models each may name. The first five are
# .claude/agents/*.md; claude-code-guide is the one allowed built-in.
PINNED_MODELS = {
    "reader": {"haiku", "claude-haiku-5-5"},
    "fixer": {"haiku", "claude-haiku-5-5"},
    "implementer": {"sonnet", "claude-sonnet-5-5"},
    "verifier": {"haiku", "claude-haiku-5-5"},
    "senior": {"claude-opus-5-5"},
    "claude-code-guide": {"haiku", "sonnet"},
}

# Valid `Escalation:` criteria. `judgment` needs a reason on the same line.
JUDGMENT = "judgment"
ESCALATION_CRITERIA = (
    "two-failed-cycles",
    "architecture",
    "cross-layer",
    JUDGMENT,
)

ESCALATION_LINE_RE = re.compile(
    r"^Escalation:(?:\s*(?:"
    + "|".join(c for c in ESCALATION_CRITERIA if c != JUDGMENT)
    + r")\b|[ \t]*"
    + JUDGMENT
    + r"[ \t]*[-\u2014:]+[ \t]*\w)",
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
        return "subagent_type '{}' is not routed. {}".format(seen, ROUTING_ADVICE)

    model = tool_input.get("model")
    if model is not None and model != "":
        if "fable" in model.lower():
            return "Fable is not used on this project. Drop the model field."
        if "opus" in model.lower() and model != "claude-opus-5-5":
            return (
                "'{}' is not claude-opus-5-5, the only Opus allowed. Drop the "
                "model field.".format(model)
            )
        allowed = PINNED_MODELS[subagent_type]
        if model not in allowed:
            return (
                "'{}' is pinned to {}. Drop the model field.".format(
                    subagent_type, " or ".join(sorted(allowed))
                )
            )

    if tool_input.get("isolation") == "remote":
        return "isolation \"remote\" is not allowed. Launch '{}' without isolation.".format(
            subagent_type
        )

    if subagent_type == "senior":
        prompt = tool_input.get("prompt")
        if not isinstance(prompt, str) or not ESCALATION_LINE_RE.search(prompt):
            return (
                "'senior' needs a line 'Escalation: <criterion>', one of: "
                + ", ".join(ESCALATION_CRITERIA)
                + ". 'judgment' needs a reason: 'Escalation: judgment - <reason>'. "
                "Or launch 'implementer'."
            )

    return None


def check(tool_name, tool_input):
    """Return a deny reason for this tool call, or None to allow it."""
    if tool_name == "Agent":
        return check_agent(tool_input)

    if tool_name == "Workflow":
        return "Workflow is not allowed. " + ROUTING_ADVICE

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
