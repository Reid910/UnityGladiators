# Next Steps

Short, prioritized "what's next" list — not a duplicate of `TODO.md` (granular
per-feature checklist) or `SETUP.md` (exact Editor-wiring steps). Just the
thread to pick back up so it doesn't get lost between sessions. Update/reorder
this whenever priorities shift; it should stay short.

All 6 build-order steps in `docs/combat-redesign-plan.md`'s "Implementation
approach" are done (hyper armor/tuning, dash direction, enemy AI, movement/
Slide, Deflect/Block/Ultimate meter, gear itemization). What's left:

1. **Full UI pass** — main menu, pause menu, HUD visual refresh. This was the
   stated next phase once audio plumbing was hooked up.
2. **Assign real audio clips** — `AudioManager` plumbing exists everywhere
   (light/heavy/ultimate/hit/ability/dash/slide/deflect/block/break/music),
   but every `AudioClip` field is still empty.
3. **Create actual gear assets** — `ItemDefinition`/`DeflectDefinition`/
   `PassiveEffectDefinition` instances for the Gloves/Head/Chest/Pants slots
   and Stat Shards. Code-side plumbing only exists so far, no real items.
4. **Perilous attacks** (Downslam / Side swing) as real enemy moves — fully
   designed in `docs/combat-redesign-plan.md`, not implemented in code yet.
5. **Finisher (Execute) cinematic presentation** — camera/VFX for a
   stagger-break kill. Currently just an instant kill with no presentation.
6. **Full numbers/tuning pass** (`TODO.md`'s M7) — most combat numbers
   (windups, stagger, shuffle timing, lunge distance, etc.) are first-guess
   placeholders that need a real playtest pass.

## Explicitly deferred (don't start until asked)
- NavMeshAgent switch for enemy movement (no arena/obstacles yet to need it)
- Camera work: collision/occlusion, combat-assist framing, zoom
- Champion/Legionary "beefy" enemy archetype
- Peer-to-peer multiplayer (`TODO.md`'s M8, last priority)
