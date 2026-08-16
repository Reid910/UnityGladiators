# Working on UnityGladiators

A Unity project. See `TODO.md` for the feature roadmap and design decisions.

## Git workflow

Branching/PR-per-feature policy lives in the global `~/.claude/CLAUDE.md` —
applies here too. Project-specific on top of that:

- Before pushing, sanity-check the diff (`git diff origin/main HEAD --stat` or
  `--summary`) for unintended file drops.
- PR description format:
  - `## Summary` — one paragraph on what changed and why
  - `## What's included` — bold subsection headers, bullet points underneath
  - `## Test plan` — what was actually verified, plainly stated, not padded.
    For this project that mostly means: does it compile in Unity without
    errors, and has it actually been played in the Editor — not just
    "logic looks right on paper."
  - `## Known limitations` — anything genuinely not done (not playtested,
    Editor wiring still pending in `SETUP.md`, numbers untuned, etc.)
  - No "Generated with Claude Code" footer or similar attribution line.
  - Post the whole description inside a single fenced code block, so it's
    copy-pasteable in one action.

## Setup instructions file

Maintain `SETUP.md` as a running, step-by-step list of manual Unity Editor work
needed to make the current code actually functional and playable — things code
alone can't do: creating prefabs, assigning serialized references in the
Inspector, setting up Animator states/transitions, creating layers, creating
ScriptableObject asset instances, wiring Input Actions, etc.

- After implementing any feature (finishing a TODO.md checklist item or milestone),
  update `SETUP.md` with the exact steps needed to wire that feature up in the
  Editor. Be concrete: which component, which field, what value/reference.
- Remove or check off steps once they're superseded or no longer relevant, so the
  file always reflects what's currently needed, not a historical log.
- The goal: at any point, the user can open `SETUP.md` and know exactly what's
  left to do in-editor to get the latest code running, without having to
  reverse-engineer it from the diff.
