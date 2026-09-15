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
3. **Full numbers/tuning pass** (`TODO.md`'s M7) — most combat numbers
   (windups, stagger, shuffle timing, lunge distance, Perilous/gear values,
   etc.) are first-guess placeholders that need a real playtest pass.

## In progress
- **NavMeshAgent switch** — no longer deferred. Filler arena obstacles are
  in `SampleScene`, `EnemyController`'s Sprint phase now paths via
  `NavMeshAgent` when one's present, and everything falls back safely
  until it is. **Blocked on manual Editor steps** — see `SETUP.md`: add a
  `NavMeshSurface` + Bake, then add `NavMeshAgent` to `Enemy.prefab`.

## Recently done
- **Perilous attacks** (Downslam / Side swing) — implemented, gated to
  wave 3+, unblockable/undeflectable, distinct orange telegraph. Untuned.
- **Gear assets** — Gloves item + Deflect, 3 armor passives (Lifesteal/
  DamageMitigation/AutoDodge) wired onto the existing Head/Chest/Pants
  items, 1 Stat Shard item. All droppable in play now. Untuned.
- **Finisher presentation** — `HitStop.TriggerFinisher()`: longer freeze +
  slow-mo ramp back to speed on an Execute kill. Code-only, no camera/VFX
  (camera work stays deferred, see below). Untuned.

## Explicitly deferred (don't start until asked)
- Camera work: collision/occlusion, combat-assist framing, zoom
- Champion/Legionary "beefy" enemy archetype
- Peer-to-peer multiplayer (`TODO.md`'s M8, last priority)
