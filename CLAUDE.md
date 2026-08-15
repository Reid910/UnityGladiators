# Working on UnityGladiators

A Unity project. See `TODO.md` for the feature roadmap and design decisions.

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
