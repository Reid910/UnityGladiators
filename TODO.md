# UnityGladiators TODO

Scope: keep the existing wave-survival arena loop (`WaveManager.cs`). Add combo-based
combat (M1/M2 + ability + stagger/finishers) and Fortnite-style instant-swap ground
loot with POE2-lite affixes. No inventory UI, no crafting, no procedural levels —
walking over an item instantly swaps it into that slot and drops whatever was
equipped there, no pickup/equip menu step in between.

Theme: player as a fast assassin, enemies as Roman gladiators (and whatever else
gets thrown into the arena) — fast, blow-trading combat building toward stagger
breaks and finishers, not a slow tank-and-spank.

## M1 — Combat overhaul (do this first, it's the core feel)
- [x] Replace single `Attack()` trigger in `PlayerCombat.cs` with a combo state machine:
      track combo step (0-2), a window after each hit during which the next input
      advances the combo instead of resetting it, and a timeout that resets to step 0.
- [x] M1 (light): 3-hit chain, each hit slightly faster/weaker or building toward the
      3rd hit doing more damage / knockback / stagger.
- [x] M2 (heavy): separate attack, either a single big hit or its own 2-hit chain;
      slower windup, more damage, maybe brief poise/armor while swinging. (Shipped
      as a single big hit for v1.)
- [x] Add an `Ability` input (new action in `InputSystem_Actions`) with a cooldown —
      input wired and firing an `AbilityCast` trigger placeholder.
  - [ ] Elden-Ring-style weapon-granted behavior (different weapons give a
        different skill move) — depends on the item system, moved to M4.
- [x] Add a `Dash` input, granted by the equipped boots (Risk of Rain shift-style) —
      input wired, does a basic forward burst via `CharacterController.Move`.
  - [ ] Gate dash behind boots being equipped (no boots = no dash) and support
        different dash variants (distance/speed/damaging) — depends on the item
        system, moved to M4.
- [ ] Animator: add params/states for combo step and ability so animations can react
      (`AttackCombo1/2/3`, `AttackHeavy`, `AbilityCast`, `Dash` triggers already set
      from code in `PlayerCombat.cs` — the Animator Controller states/transitions
      for them still need to be built in the Editor, see `SETUP.md`).
- [ ] Combat feedback: hit-stop/flinch on enemies, a damage number popup or flash —
      cheap juice that makes combos feel worth building. Partial: `Health.TakeDamage()`
      now fires an animator `Hit` trigger for flinch reactions; hit-stop and damage
      number popups still pending (popup likely belongs with M6 UI work).
- [x] Hitstun: new `Hitstun.cs` component (`ApplyStun(duration)` / `IsStunned`),
      distinct from the stagger meter — this is what lets a combo actually chain,
      both player-on-enemy and enemy-on-player, since the target can't act while
      stunned. Wired into `PlayerCombat.DealDamage()` and
      `EnemyController.AttackTarget()`, both of which also feed the target's
      stagger meter on every hit that causes hitstun.
- [x] Stagger meter: new `Stagger.cs` component (`AddStagger(amount)` / `IsBroken`,
      decays over time if not recently hit). Heavy hits fill it faster than light
      combo hits (see `heavyStaggerAmount` vs. per-hit `staggerAmount` in
      `PlayerCombat.cs`).
- [x] When stagger meter fills, the target enters a "broken" state (`Stagger.IsBroken`)
      — `EnemyController`/`PlayerController`/`PlayerCombat` all check this (via an
      `IsIncapacitated` property) and skip movement/attack/input while broken,
      instead of literally disabling components.
- [x] Finisher: `Health.Execute()` instant-kills regardless of remaining health.
      Landing any hit on an already-broken target triggers it — wired in both
      `PlayerCombat.DealDamage()` (player → enemy) and
      `EnemyController.AttackTarget()` (enemy → player).
- [ ] Feedback for stagger: a UI bar over the enemy (or screen-space) and a visual/
      audio cue when it breaks, so the player can read "this one's about to go down."
      Not built yet — needs a UI prefab, see `SETUP.md`/M6.
- [x] Stagger meter applies to the player too, symmetrically: `EnemyController.AttackTarget()`
      fills the player's `Stagger` on every landed hit, same as the player does to
      enemies.
- [x] If any enemy attack connects while the player is broken, it's a finisher —
      instant death via `Health.Execute()`, regardless of remaining health, same
      as the player can do to enemies.

## M2 — Item data model
- [x] `ItemDefinition` ScriptableObject (`Assets/Scripts/Items/ItemDefinition.cs`):
      slot (`ItemSlot` enum: Head/Chest/Pants/Boots/Weapon — 5 slots, one weapon
      wielded in both hands), and a flat damage roll range (`minDamage`/`maxDamage`).
      Deviation from the original plan: rarity is NOT stored on the template — it's
      rolled per drop by `ItemRoller` (see below), since the same item template can
      drop at any rarity. Armor slots can just leave damage at 0 if you don't want
      them contributing offense.
- [x] `Weapon` items reference an `AbilityDefinition` (`AbilityDefinition.cs` —
      name, cooldown, animator trigger) via `ItemDefinition.AbilityDefinition`, only
      relevant when `Slot == Weapon`.
- [x] `Boots` items reference a `DashDefinition` (`DashDefinition.cs` — distance,
      cooldown, optional damage) via `ItemDefinition.DashDefinition`, only relevant
      when `Slot == Boots`. No boots equipped still means no dash — not wired yet,
      that gating happens in M4.
- [x] Head/Chest/Pants stay passive (no active-input grant) — themed stat carriers
      via slot-eligible affixes (see below), keeping the input scheme at 4 buttons
      total (light, heavy, ability, dash). Final kit: Weapon = skill + damage,
      Boots = dash, Chest/Head/Pants = passive stats (survivability, accuracy,
      mobility).
- [x] `AffixDefinition` ScriptableObject (`AffixDefinition.cs`): `StatType` enum
      (attack speed, crit chance, ability cooldown reduction, move speed, max
      health, armor — 6 total), min/max roll range, and an `eligibleSlots` array so
      the pool is naturally split by slot theme (empty array = eligible anywhere)
      rather than needing a fully separate pool per slot.
- [x] Roll logic on drop (`ItemRoller.cs`, static `Roll(definition, rarity)`):
      every item gets one flat damage roll. Common/Rare get exactly one affix
      rolled from the item's eligible pool; SuperRare gets two distinct affixes
      (no repeats). Rarity also multiplies both the damage roll and each affix's
      rolled value on top of the extra affix (`RarityRollMultiplier`).
- [x] `EquippedItem` runtime instance (`EquippedItem.cs`) = definition + rolled
      damage + rolled affixes (`RolledAffix.cs`: definition + value) + rarity,
      distinct from the `ItemDefinition` ScriptableObject template.

## M3 — Corpse looting, pickup & cleanup
- [x] On enemy death, the corpse stays in the scene (default when `destroyOnDeath`
      is false) and `Health.cs` now enables an optional `corpseHitbox` Collider on
      death — separate from `objectCollider`, which still gets disabled — so a
      corpse can still be hit after death. Needs an actual child hitbox object
      created and wired per prefab, see `SETUP.md`.
- [x] `LootableCorpse.cs`: tracks whether it's been looted (`TryLoot()` is a no-op
      after the first successful/attempted loot), rolls whether it has loot at all
      (`dropChance`) and which item from a per-corpse `possibleItems` pool, and
      doesn't drop until `PlayerCombat` actually hits it.
- [x] Loot table gated by enemy tier: new `EnemyTier` enum (T1/T2/T3) on
      `LootableCorpse`. T1 only rolls Common, T2 only Rare, T3 rolls Rare or
      SuperRare (weighted by `t3SuperRareChance`) — the one deliberate overlap so
      the toughest enemies are worth farming without a guaranteed top-tier drop.
      (Tier is set per-corpse for now since M5's enemy variants don't exist yet —
      once they do, each variant prefab just sets its own tier.)
- [x] `PlayerCombat.DealDamage()` now also runs a second `OverlapSphere` against a
      new `corpseLayer` (`LootCorpses()`) and calls `TryLoot()` on anything hit —
      separate from `enemyLayer` so corpses aren't also taking live damage.
- [x] `ItemPickup.cs`: on player trigger enter, calls `PlayerEquipment.Equip()`
      (new — tracks which `EquippedItem` is in each slot) and either destroys
      itself (slot was empty) or becomes the previously-equipped item (drops it in
      the same spot) — the Fortnite-style instant swap, no menu step.
- [ ] Visual distinction by rarity (outline/glow color or a floating icon) so drops
      read at a glance without opening any UI. Not built yet — needs an actual
      pickup prefab/material, see `SETUP.md`.
- [x] Cleanup, tied to `WaveManager` instead of a timer: `StartNextWave()` now
      destroys everything in `spawnedEnemies` (the previous wave's corpses) before
      spawning the new wave. `OnEnemyDied()` now calls `ClearPickups()` when a wave
      finishes, destroying anything in the new `activePickups` list (populated via
      `WaveManager.RegisterPickup()`, called from `LootableCorpse` when it spawns a
      drop) — gives dropped items one full wave of grace before they're cleared.

## M4 — Stats integration
- [x] `PlayerStats.cs` aggregator: sums base damage + every equipped item's
      rolled damage (`TotalDamage`) and every equipped item's affixes by
      `StatType` (`GetStat()`), recalculated whenever `PlayerEquipment` reports
      a change (subscribes to `ItemEquipped`).
- [x] `PlayerCombat.cs` now adds `PlayerStats.TotalDamage` on top of each
      combo/heavy/dash hit's base damage, applies the Attack Speed affix to
      shorten recovery time (`ApplyAttackSpeed()`), and applies Ability
      Cooldown Reduction to the equipped weapon's ability cooldown.
      `Health.cs` gained `SetMaxHealthBonus()`, called by `PlayerStats` so the
      Max Health affix total adjusts `MaxHealth` (healing through on a gain,
      clamping down current health only if it would exceed a lowered max).
      `PlayerController.cs` applies the Move Speed affix as a multiplier on
      base movement speed.
- [ ] Same treatment isn't needed for enemies unless you want elite/rare enemies later.
- [x] Weapon-granted ability and boots-granted dash are now fully live, not
      placeholders: `PlayerCombat.TryUseAbility()` reads the equipped weapon's
      `AbilityDefinition` (no weapon/no ability = button does nothing) and
      `TryDash()` reads the equipped boots' `DashDefinition` (no boots = no
      dash) — both fields removed from `PlayerCombat`'s own Inspector, now
      fully gear-driven.
- [ ] Crit Chance affix exists in the data model (`StatType.CritChance`) but
      isn't consumed by any damage calculation yet — no crit roll/multiplier
      implemented. Left for a later pass since it's a self-contained addition
      to `DealDamage()` whenever it's wanted.

## M5 — Content pass (make waves feel different, not just numerous)
- [ ] At least 2-3 gladiator-themed enemy variants by extending or subclassing
      `EnemyController.cs` — right now there's only one. Theme gives concrete
      starting points instead of generic archetypes, e.g.:
      - Retiarius (net/trident) — fast, low-hp, maybe briefly roots the player
      - Heavily-armored legionary type — slow, high-damage, high stagger resistance
      - Beast or ranged variant — the tougher/rarer encounter
- [ ] Assign each variant a tier (T1/T2/T3) that drives both its stats (health/
      damage scaling) and its loot table (see M3) — e.g. retiarius = T1, legionary =
      T2, beast/ranged = T3. Wave composition can then mix tiers instead of just
      adding more of the same enemy.
- [ ] Enough affix variety (5-8 stat types) and rarity color coding that loot
      decisions feel meaningful.
- [ ] Tune `WaveManager.cs` scaling (`enemiesAddedPerWave`, `totalWaves`) against the
      new combat/loot power curve — this only makes sense after M1-M4 exist.

## M6 — UI/feedback
- [ ] `GameUI.cs`: show currently equipped item per slot (icon + rarity color) and
      ability cooldown.
- [ ] Combo counter or hit-counter readout (optional, but reinforces the combo system).
- [ ] "Press E to pick up [Rare] Item Name" world-space prompt when near a drop.

## M7 — Polish / playtest
- [ ] Playtest the full loop (waves + combos + drops) end to end, tune numbers.
- [ ] Cut or simplify anything that isn't landing rather than adding more scope.

## M8 — Stretch: peer-to-peer multiplayer (last, only after M1-M7 are solid)
- [ ] Real-time netcode for a fast, hitstun/posture/finisher-driven combat game is
      a genuinely hard problem — hit resolution has to feel fair on both ends
      despite latency, and instant-death finishers make desync especially costly.
      Treat this as optional and don't start it until the single-player loop is
      fully working and fun.
- [ ] The one thing worth doing early (in M1) to keep this door open cheaply: route
      all damage/stagger/finisher resolution through one central function per
      entity rather than scattering hit logic across scripts — doesn't make
      multiplayer easy, but avoids making it architecturally painful later.

## Explicitly out of scope (for now)
- Inventory grid / stash / crafting / rerolling affixes
- Procedural level generation, multiple biomes, multiple playable characters
- Meta-progression between runs
