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
- [ ] `ItemDefinition` ScriptableObject: slot (Head/Chest/Pants/Boots/Weapon —
      5 slots, one weapon wielded in both hands, no separate left/right), rarity
      tier (Common/Rare/Super Rare), and a flat damage roll range (armor slots can
      just roll 0 or a small damage bonus if you don't want them contributing
      offense).
- [ ] `Weapon` items reference an `AbilityDefinition` (which move it grants for the
      `Ability` input) in addition to their damage/affix.
- [ ] `Boots` items reference a `DashDefinition` (which dash variant it grants for
      the `Dash` input). No boots equipped means no dash available.
- [ ] Head/Chest/Pants stay passive (no active-input grant) — themed stat carriers,
      keeping the input scheme at 4 buttons total (light, heavy, ability, dash):
      - Chest: survivability-flavored (max HP, armor/damage reduction, stagger
        resistance — ties into the finisher-death stakes from M1).
      - Head: accuracy/detection-flavored (crit chance, bonus finisher damage, or
        loot-related utility like revealing corpse rarity before hitting it).
      - Pants: mobility/stamina-flavored (move speed, dash cooldown reduction, extra
        dash charge — synergizes with boots).
      Final kit: Weapon = skill + damage, Boots = dash, Chest/Head/Pants = passive
      stats that keep you alive/effective (survivability, accuracy, mobility).
- [ ] `Affix` ScriptableObject or struct: stat type (attack speed, crit chance,
      ability cooldown, move speed, max health, armor — pick ~5-6 total) + min/max
      roll range. Consider splitting the affix pool per slot theme above rather
      than one pool shared across all 5 slots, so drops feel distinct by slot.
- [ ] Roll logic on drop: every item gets one flat damage roll. Common/Rare get
      exactly one affix rolled from the pool; Super Rare (low drop chance) gets
      two distinct affixes. Rarity also widens/raises the roll ranges (a Rare rolls
      higher numbers than a Common) on top of the extra affix.
- [ ] `EquippedItem` runtime instance = definition + rolled damage value + rolled
      affix (type + value) + rarity, distinct from the ScriptableObject template.

## M3 — Corpse looting, pickup & cleanup
- [ ] On enemy death (`Health.Die()` in `Health.cs`), leave the corpse in the scene
      (already the default when `destroyOnDeath` is false) but re-enable a small
      trigger/hitbox on a distinct "Corpse" layer so it can still be hit — the
      combat collider gets disabled on death, so this needs to be separate from it.
- [ ] `LootableCorpse` component: tracks whether it's already been looted, rolls
      whether it has loot at all (not every corpse needs to drop something) and
      which item, and doesn't drop until the player actually attacks it.
- [ ] Loot table gated by enemy tier (see M5): T1 enemies only roll Common items,
      T2 only roll Rare, T3 rolls from Rare+Super Rare (the one deliberate overlap,
      so the toughest enemies are the place to farm for the best gear, but it's not
      a guaranteed drop). Keeps low-tier farming useful without trivializing the
      top tier.
- [ ] On player attack hitting a corpse (extend `PlayerCombat.Attack()`'s
      `OverlapSphere` to also check the Corpse layer): pop the rolled item out as a
      world pickup at the corpse's position, mark the corpse looted so it can't be
      hit again for more loot.
- [ ] `ItemPickup` component: on player trigger enter, compare to currently equipped
      item in that slot — equip the new one, drop the old one on the ground in the
      player's place.
- [ ] Visual distinction by rarity (outline/glow color or a floating icon) so drops
      read at a glance without opening any UI.
- [ ] Cleanup, tied to `WaveManager` instead of a timer: when a new wave starts
      (`StartNextWave()`), destroy all remaining corpses (looted or not — last
      chance to loot is before you commit to the next wave). When that new wave
      finishes (`enemiesAlive` hits 0 again), destroy any items still sitting on
      the ground from the previous loot window — gives the player one full wave
      to grab drops before they're gone.

## M4 — Stats integration
- [ ] A `PlayerStats` aggregator that sums base stats + all equipped item affixes,
      recalculated on equip/unequip.
- [ ] Wire `PlayerCombat.cs` damage/cooldown and `Health.cs` max HP to read from
      `PlayerStats` instead of their own hardcoded serialized fields.
- [ ] Same treatment isn't needed for enemies unless you want elite/rare enemies later.
- [ ] Wire the equipped weapon's `AbilityDefinition` into `PlayerCombat.TryUseAbility()`
      (currently a fixed placeholder trigger) and the equipped boots'
      `DashDefinition` into `TryDash()` (currently always available with a fixed
      distance) — gate dash behind boots being equipped.

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
