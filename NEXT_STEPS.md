# Next Steps

Short, prioritized "what's next" list — not a duplicate of `TODO.md` (granular
per-feature checklist) or `SETUP.md` (exact Editor-wiring steps). Just the
thread to pick back up so it doesn't get lost between sessions. Update/reorder
this whenever priorities shift; it should stay short.

All 6 build-order steps in `docs/combat-redesign-plan.md`'s "Implementation
approach" are done (hyper armor/tuning, dash direction, enemy AI, movement/
Slide, Deflect/Block/Ultimate meter, gear itemization). Audio is wired too
(see below) — nothing left on the active list right now.

Everything else (camera, Champion enemy, the numbers/tuning pass,
multiplayer) is deferred — see below for the order.

## Recently done
- **Audio hookup** — every `AudioClip` field (attacks/hit/death/break/
  dash/slide/deflect/block/ability/ultimate/music) assigned real clips from
  the Kenney RPG Audio, Kenney Interface Sounds, and Alexander Ehlers music
  packs. Best-fit placeholder picks, not custom SFX — untuned volumes, swap
  any that don't land right. See `SETUP.md`.
- **Full UI pass** — main menu, pause menu, ultimate gauge, enemy stagger
  nameplates, stat shard/gloves gear slots, result screens. Merged (PR #19).
- **NavMeshAgent switch** — no longer blocked. `Enemy.prefab` has a real
  `NavMeshAgent`, `SampleScene`'s NavMesh is baked, and a follow-up
  separation pass (`EnemyController.GetSeparationVector()`) fixed enemies
  jamming into each other near the player. `SETUP.md`'s old "blocked on
  manual Editor steps" note for this is stale — already done.
- **Perilous attacks** (Downslam / Side swing) — implemented, gated to
  wave 3+, unblockable/undeflectable, distinct orange telegraph. Untuned.
- **Gear assets** — Gloves item + Deflect, 3 armor passives (Lifesteal/
  DamageMitigation/AutoDodge) wired onto the existing Head/Chest/Pants
  items, 1 Stat Shard item. All droppable in play now. Untuned.
- **Finisher presentation** — `HitStop.TriggerFinisher()`: longer freeze +
  slow-mo ramp back to speed on an Execute kill. Code-only, no camera/VFX
  (camera work stays deferred, see below). Untuned.
- **Dedicated finisher action + narrower Light Attack range** — Light
  attack pre-empts the normal combo with its own action when a Broken
  enemy is in range, instead of the execute happening buried inside a
  normal swing. Untuned.
- **Deflect/AutoDodge feedback** — both now flash a distinct tint on
  proc (gold/cyan) since neither had any visible feedback before.
- **Corpse loot pulse glow** — fixed the rarity glow being invisible on
  T1/Common (both plain white) by pulsing any lootable corpse holding an
  item, independent of its actual color.

## Explicitly deferred (don't start until asked) — in priority order
1. Camera work: collision/occlusion, combat-assist framing, zoom
2. Champion/Legionary "beefy" enemy archetype
3. **Full numbers/tuning pass** (`TODO.md`'s M7) — deliberately last before
   multiplayer, not next: most combat numbers (windups, stagger, shuffle
   timing, lunge distance, Perilous/gear values, etc.) are first-guess
   placeholders, and the two items above would each add their own new
   numbers to tune too — better to do one real pass after those land than
   repeat it.
4. Peer-to-peer multiplayer (`TODO.md`'s M8, last priority)
