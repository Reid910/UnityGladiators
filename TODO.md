# UnityGladiators TODO

Scope: keep the existing wave-survival arena loop (`WaveManager.cs`). Add combo-based
combat (M1/M2 + ability + stagger/finishers) and button-swap ground loot with
POE2-lite affixes. No inventory UI, no crafting, no procedural levels — standing
near a dropped item and pressing Interact (E) swaps it into that slot and drops
whatever was equipped there, no pickup/equip menu step in between. (Originally
walk-over-to-instantly-swap; changed to a button press so brushing past loot
mid-fight can't accidentally swap out good gear — see M6.)

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
- [x] Add an `Ability` input (new action in `InputSystem_Actions`) with a cooldown.
      Elden-Ring-style weapon-granted behavior landed in M4 — no longer a
      placeholder, see there.
- [x] Add a `Dash` input, granted by the equipped boots (Risk of Rain shift-style).
      Boots-gating and per-boots dash variants landed in M4 — no longer a
      placeholder, see there.
- [x] Animator: rather than building new states/transitions for `AttackCombo1/2/3`,
      `AttackHeavy`, `AbilityCast`, `Dash` (which would need real animation clips
      the project doesn't have), light combo and heavy now reuse the existing
      `Attack` trigger/state — every attack plays the same swing for now.
      Ability/dash fire no animator trigger at all; both still fully function
      mechanically. Distinct animations per move are a future polish item, not
      required for MVP.
- [x] Combat feedback: hit-stop and damage number popups now both land in
      `Health.cs`, the single choke point every hit already passes through
      (`TakeDamage()`/`Execute()`), so this covers player-dealt and
      enemy-dealt damage in one place rather than duplicating it in
      `PlayerCombat.cs` and `EnemyController.cs`:
      - `DamageNumber.cs` (new): a floating `TextMeshPro` that rises and
        fades out over its lifetime, spawned via a new
        `Assets/Prefabs/DamageNumber.prefab`. Finishers show whatever health
        remained as the "damage" number.
      - `HitStop.cs` (new): a brief global `Time.timeScale` freeze
        (`HitStop.Trigger(duration)`) on every landed hit — no scene wiring
        needed, it lazily spins up its own persistent runner object.
        Finishers hold the freeze twice as long as a normal hit.
      - `Health.TakeDamage()` still also fires the animator `Hit` trigger for
        flinch reactions, unchanged.
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
      Player-side is partially done: `GameUI.staggerText` shows the player's own
      live `Stagger` value (or "BROKEN") under the health readout — enemy-side
      (a bar over each enemy, or a break cue) is still unbuilt.
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
      is false) — separate from `objectCollider`, which still gets disabled, so a
      corpse can still be hit after death via an optional `corpseHitbox` Collider.
      That hitbox isn't enabled the instant the enemy dies, though —
      `Health.EnableCorpseHitbox()` is called by `WaveManager` only once the wave
      that enemy died in fully clears (`OnEnemyDied()` → `EnableCorpseLooting()`),
      so corpses aren't lootable mid-fight while more enemies from the same wave
      are still incoming. Needs an actual child hitbox object created and wired
      per prefab, see `SETUP.md`.
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
- [x] `ItemPickup.cs`: on player trigger enter/exit, registers/unregisters
      itself as the player's nearby pickup (`PlayerEquipment.RegisterNearby()`/
      `UnregisterNearby()`). The actual swap happens on the Interact input via
      `PlayerEquipment.TrySwapWithNearby()` (see M6), which calls the existing
      `Equip()` (tracks which `EquippedItem` is in each slot) and either
      destroys the pickup (slot was empty) or turns it into the
      previously-equipped item (drops it in the same spot) — no menu step,
      just a button press instead of instant-on-touch.
- [x] Visual distinction by rarity: new `RarityColor.cs` maps rarity to a color
      (white/blue/orange), and `ItemPickup.cs` colors both its mesh (via
      `MaterialPropertyBlock`) and a world-space name label by it — the
      `nameLabel` TextMeshPro child object now exists on `ItemPickup.prefab`.
      No icon/outline yet, just mesh tint + text label.
- [x] Cleanup, tied to `WaveManager` instead of a timer, both with a grace
      period so nothing vanishes the instant a wave ends:
      `AdvanceCorpseAndPickupGenerations()` (called from `StartNextWave()`)
      destroys the corpse batch from two transitions ago and the pickup batch
      from three transitions ago, then promotes each wave's freshly-finished
      batch to await the next one — corpses get one full wave of grace
      (a body from wave N survives all of wave N+1, cleared when wave N+2
      starts), pickups get one wave more than that (survives wave N+1 *and*
      N+2, cleared when N+3 starts) — populated via
      `WaveManager.RegisterPickup()`, called from `LootableCorpse` when it
      spawns a drop.

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
- [x] Crit Chance now rolls in `PlayerCombat.DealDamage()` — one roll per
      swing (not per enemy hit), multiplying total damage by
      `critDamageMultiplier` (1.5x default) on a hit.
- [x] Armor now mitigates incoming damage too — `Health.SetArmor()`, called
      by `PlayerStats` alongside the existing `SetMaxHealthBonus()`, stores
      the equipped total and `TakeDamage()` subtracts it as a flat reduction
      (matching how the affix is already surfaced as flat points in
      `GameUI`), floored at 1 damage so armor can't make the player
      unkillable. This was the other affix silently unconsumed since M2 —
      it rolled and displayed fine, it just never affected anything.

## M5 — Content pass (make waves feel different, not just numerous)
- [ ] At least 2-3 gladiator-themed enemy variants — as it turns out this needs
      little to no new code: `EnemyController`'s existing fields already produce
      distinct archetypes just by tuning values per prefab (see `SETUP.md`):
      - Retiarius — fast `movementSpeed`, low `Health.maxHealth`, high
        `attackHitstunDuration` (reads as a brief "root" even though it's just
        an extended hitstun, no new system needed)
      - Heavily-armored legionary — slow `movementSpeed`, high `maxHealth`,
        high `attackDamage`, high `Stagger.maxStagger` (stagger-resistant)
      - Ranged/beast — a large `stoppingDistance` already produces an
        instant-hit "ranged" attacker with the existing code (no projectile
        visual yet, would need real Editor/animation work to add one — noted
        as a later polish item, not blocking)
- [x] Tier now lives on `EnemyController` (`Tier` property, new field) instead
      of being duplicated on `LootableCorpse`, which now reads it via
      `GetComponent<EnemyController>()` — one source of truth per prefab, drives
      both loot rarity (see M3) and is a label for tuning that prefab's stats
      (tier doesn't auto-scale stats itself, each prefab's fields do).
- [x] `WaveManager.cs` now spawns from an `enemyPrefabs[]` array instead of a
      single prefab (`ChooseEnemyPrefab()`), gated by tier: T1 always eligible,
      T2 eligible from `t2UnlockWave` (default wave 2), T3 from `t3UnlockWave`
      (default wave 3) — so composition actually shifts over time instead of
      just adding more of the same enemy. Needs the actual prefab variants
      created to have any effect (see `SETUP.md`).
- [x] Endless mode: new `WaveManager.endlessMode` bool (default true) — when
      set, waves keep scaling past `totalWaves` forever instead of calling
      `WinGame()`. Fits the farming loop much better than a hard win-at-wave-3
      cap, since the loot/tier system assumes ongoing play. `CurrentWave` is
      already exposed for a "highest wave reached" score display (M6).
- [x] Enough affix variety (5-8 stat types) and rarity color coding that loot
      decisions feel meaningful — both done: 6 `StatType`s (M2), and
      `RarityColor.cs` colors both the pickup mesh and its name label (M3),
      plus the equipped-items HUD readout (M6).
- [ ] Tune `WaveManager.cs` scaling (`enemiesAddedPerWave`, `t2UnlockWave`,
      `t3UnlockWave`) against the actual combat/loot power curve — genuinely
      needs playtesting, can't be tuned further from code alone.
- [ ] Dodge with brief i-frames and telegraphed enemy attacks (a wind-up tell
      before each swing) were discussed as good additions — dodge, but boots
      already grew into the dash serving that defensive-mobility role, so a
      separate dodge may be redundant now (worth revisiting once dash is
      playtested). Telegraphs are real Animator/timing work tied to whatever
      attack animations each enemy variant gets — not started.

## M6 — UI/feedback
- [x] `GameUI.cs`: shows currently equipped item per slot, text-colored by
      rarity (`UpdateEquippedItemsText()` — text only, no icons yet, would need
      actual item icon assets), ability cooldown, and dash cooldown (added
      since dash is now a real gated resource, not always-available). Now also
      shows each equipped item's actual stats underneath its name — rolled
      damage, every affix formatted by type (percentages for Attack Speed/
      Crit Chance/Ability Cooldown Reduction/Move Speed, flat points for Max
      Health/Armor), and the weapon's/boots' ability/dash name if it has one.
- [x] Combo counter readout (`comboText`, shows `PlayerCombat.ComboStep`).
- [x] Stagger readout (`staggerText`, shows the player's own `Stagger` value or
      "BROKEN" — added alongside the health readout).
- [x] World-space pickup name label (`ItemPickup.nameLabel`) is wired up — see
      M3's `SETUP.md` notes. Now also doubles as the swap-target prompt (see
      below) when the player's in range.
- [x] Swap-on-touch was replaced with swap-on-Interact: standing in an
      `ItemPickup`'s trigger no longer auto-equips it, it just marks it as
      the player's nearby pickup; pressing Interact (`E`, reusing the
      previously-unused stock `Interact` action — see
      `InputSystem_Actions.inputactions`) calls
      `PlayerEquipment.TrySwapWithNearby()` to actually swap it in. Reverses
      the earlier "press E is dropped, doesn't fit the design" call — walking
      past loot mid-fight no longer risks swapping out good gear for trash.
- [x] "Press E to swap" feedback added, on the world-space label rather than
      a HUD prompt: `ItemPickup`'s floating name label shows `[E] <item>` +
      a smaller `swaps <currently equipped item>` line while it's the
      player's swap target (`PlayerEquipment.RegisterNearby()`/
      `UnregisterNearby()` drive this via `ItemPickup.SetTargeted()`), and
      reverts to the plain name otherwise. Also scales the pickup up
      (`Visual Transform` + `Targeted Scale Multiplier`, optional) so the
      active target stands out if more than one pickup is nearby.

## Mouse-look camera and full Input System migration — combat-feel follow-up
- [x] `ThirdPersonCamera.cs` no longer uses the legacy Input Manager
      (`Input.GetAxis("Mouse X"/"Mouse Y")` — it was the only script left doing
      so, everything else already used the new Input System). It now reads the
      existing `Look` action (already bound to `<Pointer>/delta` in
      `InputSystem_Actions`, just never consumed anywhere before) via its own
      `InputSystem_Actions` instance, same pattern as `PlayerController`/
      `PlayerCombat`.
- [x] Cursor is locked and hidden by default so the mouse directly drives
      camera yaw/pitch instead of a free OS cursor wandering off the game
      window. Holding **Left or Right Alt** frees the cursor (`Cursor.lockState
      = None`, visible) and stops applying mouse movement to the camera while
      held — checked directly via `Keyboard.current`, no new input action
      needed for this part.
- [x] `ProjectSettings.asset`'s `activeInputHandler` switched from `2` (Both)
      to `1` (Input System Package only) — confirmed nothing else in the
      project (including third-party asset-pack scripts) still calls the
      legacy `Input.*` API, so this fully retires the old system and should
      clear the "Input Manager deprecation" Console warning for good.
- [ ] `mouseSensitivity` default (`0.12`) is a rough guess — raw pointer delta
      (pixels/frame) is a very different scale than the old Input Manager's
      smoothed axis value, so this needs real playtesting to tune, not just
      code review.

## Player poise/hyperarmor — combat-feel follow-up
- [x] Problem: any single enemy hit applies `Hitstun` to the player, and while
      stunned the player can't move or act at all (`IsIncapacitated`). With
      multiple enemies attacking on staggered cooldowns, this could chain into
      an unrecoverable stunlock just from standing near a few enemies — the
      player never gets to "win" the exchange through aggression.
- [x] Fix: `PlayerCombat.IsAttacking` (true while mid-swing, same window as
      the existing attack-recovery gate) grants poise/hyperarmor against
      hitstun specifically — `EnemyController.AttackTarget()` now skips
      calling `Hitstun.ApplyStun()` on the player while `IsAttacking` is true.
      Damage and Stagger still apply normally either way, so pressing forward
      recklessly can still get the player broken and finished (the real
      risk/consequence stays intact) — it just can't be chain-interrupted by
      every graze while already committed to a swing.
- [ ] Deliberately one-sided: enemies don't get equivalent poise against the
      player's hits — the player's stagger/hitstun/finisher tool against
      enemies is the intended asymmetry (player is the aggressor, gladiators
      are the ones meant to be broken). Revisit if a tougher (T2/T3) enemy
      ever needs its own poise resistance beyond a higher `Stagger.maxStagger`.

## Attack windup + active-hitbox windows — combat-feel follow-up
- [x] Problem: every attack (player and enemy) resolved instantly the moment
      it was triggered — no telegraph, so there was nothing to actually dodge.
      Getting hit was purely about positioning at input time, not reaction.
- [x] `PlayerCombat`'s light combo and heavy attack now run through a real
      windup → active-hitbox-window → recovery sequence (`BeginAttack()` /
      `PerformAttack()` coroutine) instead of hitting on the same frame the
      button is pressed. The hit query (`CheckHit()`, still the existing
      `Physics.OverlapSphere` — there's no animated weapon collider without
      real character assets, so this is a time-gated approximation of a Souls
      hitbox, not a literal one) runs every frame across the active window
      rather than once, so a target only needs to be in range at some point
      during that window, not the exact instant the swing started. A
      `HashSet<Health>` prevents hitting the same target more than once per
      swing. windup+active+recovery sums match the old flat recovery values,
      so overall combo pacing is unchanged — this only carves out an explicit
      telegraph instead of an instant hit.
- [x] `EnemyController.AttackTarget()` gets the same treatment — a
      configurable `Attack Windup` (default 0.4s) before the hit is even
      checked, then an `Attack Active Duration` (0.15s) window checking
      `Attack Range` (separate from `Stopping Distance`, which only decides
      when the enemy stops closing in to swing). The enemy fully commits
      during this sequence (`isAttacking` freezes movement/re-triggering,
      mirroring how the player can't cancel their own combo mid-swing) — this
      is the actual dodging mechanic: see the tell, move out of `Attack
      Range` before the active window ends, take nothing.
- [x] Getting broken (`Stagger.IsBroken`) mid-windup cancels the attack for
      both sides — a fully-interrupted swing shouldn't still land. Plain
      hitstun doesn't cancel it — poise (see above) already covers that case
      for the player.
- [ ] All new windup/active/range numbers are first-pass guesses tuned only
      to preserve old total attack duration where a prior number existed —
      genuinely needs real playtesting, especially `EnemyController`'s new
      `Attack Windup`/`Attack Range`, which have no prior value to anchor to.

## Weapon ability now does something — combat-feel follow-up
- [x] Problem: pressing the `Ability` button just started a cooldown timer —
      no damage, no effect, nothing. Item types had it as a deliberate blank
      stub (see M2), but with real combat feel now the focus, an inert button
      was wasted design space.
- [x] `AbilityDefinition` (`Assets/Scripts/Items/AbilityDefinition.cs`) gained
      real effect fields: `Damage`, `Stagger Amount`, `Hitstun Duration`,
      `Windup`, `Active Duration`, `Range` — an ability is modeled as a
      bigger, rarer hit than a normal swing, not a bespoke new system.
- [x] `PlayerCombat.TryUseAbility()` now fires the shared `PerformAttack()`
      windup/active-window coroutine (same one light/heavy/attack windows
      use — see the windup follow-up above) with the equipped weapon's
      `AbilityDefinition` values. `BeginAttack()`/`PerformAttack()`/
      `CheckHit()` all gained an optional/threaded `range` parameter so an
      ability's hit radius can differ from the weapon's normal `attackRange`
      (defaults to a wider 2.5, vs. 1.5 for a regular swing).
- [x] Deliberately independent of the light/heavy combo state: the ability
      doesn't touch `nextAttackTime`/`comboStep`, and runs on its own
      untracked coroutine rather than the shared `attackCoroutine` field — so
      it can be weaved between combo hits instead of interrupting/resetting
      the chain, gated only by its own cooldown (`nextAbilityTime`).
- [x] `AbilityDefinition.AnimatorTrigger` (`AbilityCast` by default) is now
      actually fired via the shared pipeline — previously deliberately
      skipped. `Animator.SetTrigger` on a parameter that doesn't exist in the
      Controller is a silent no-op (confirmed safe, unlike playing a missing
      state by name), so this is harmless now and will just start animating
      once a matching state exists.
- [ ] Default ability numbers (30 damage, 25 stagger, 2.5 range) are a first
      guess with nothing to anchor to — needs playtesting like the rest of
      the windup follow-up above.

## Dash i-frames — combat-feel follow-up
- [x] `DashDefinition` gained `Invulnerability Duration` (default 0.2s) —
      `PlayerCombat.TryDash()` sets a new `invulnerableUntilTime` window when
      dashing, exposed as `IsInvulnerable`.
- [x] Deliberately stronger than poise: `EnemyController.ResolveHit()` checks
      `IsInvulnerable` first, before even the already-broken/finisher check —
      a correctly-timed dash blocks damage, Stagger, hitstun, and finishers
      entirely, not just the flinch like poise does. Missing an attack
      because the target dashed through it should always mean nothing
      happens, not "reduced consequences."
- [x] Deliberately scoped to dash only, not a separate dodge move — no boots
      equipped still means no dash and no i-frames, consistent with boots
      already being the sole source of that defensive-mobility option.
      Explicitly deferred: more dash *variants* (differing invulnerability
      windows, distances, etc.) until there's a visual to distinguish them by
      — a second dash that's mechanically different but looks identical
      wouldn't read as a real choice.
- [ ] `0.2s` is a first guess — needs playtesting to know if that's generous
      enough to reward a well-timed dash through a windup, or too forgiving.

## Real combo/ability animations — combat-feel follow-up
- [x] Discovered the project already owns an unused Blink asset pack
      (`Assets/Blink/Art/Animations/Animations_Starter_Pack/`) built for the
      exact same rig the Player and Enemy models already share
      (`HumanMale_Character.fbx` — Enemy just layers armor meshes on top), so
      these clips need zero retargeting work.
- [x] Wired 3 new states/triggers directly into
      `LowPolyHumanAnimator.controller` (Player's Animator Controller) by
      hand-editing the asset YAML (same technique used elsewhere in this
      project) rather than through the Editor: `AttackComboLeft` (`PunchLeft`
      clip), `AttackComboRight` (`PunchRight`), `AbilityCast` (`SpellCast`).
      Each is a plain Any State → state → exit-to-Locomotion transition,
      cloned from the existing working `Attack` state's pattern. Verified by
      parsing the resulting file back with PyYAML and checking every
      fileID/GUID cross-reference resolves — didn't just eyeball it.
- [x] `PlayerCombat.cs`'s light combo now alternates `AttackComboLeft`/
      `AttackComboRight`/`AttackComboLeft` instead of all 3 hits sharing the
      generic `Attack` trigger — each combo hit now visually reads as
      distinct. Heavy deliberately keeps using the pre-existing `Attack`
      state (already `MeleeAttack_OneHanded`), which is already a bigger,
      different motion from either punch, so it needed no new state.
      `AbilityCast` was already being fired by `TryUseAbility()` (see the
      ability follow-up above) — it just went from a safe no-op to an
      actually-playing, deliberately different-looking cast motion the
      moment a real state existed for it.
- [ ] `PunchLeft`/`PunchRight`/`SpellCast`/`MeleeAttack_OneHanded` are the
      same filler clips noted earlier as having Console import warnings —
      not diagnosed, worth checking Animation Import Settings in the Editor.
- [ ] Weapon visual gap is still open and deliberately deferred (per
      discussion) — these are bare-handed animations, so combat will look
      unarmed even with a Weapon equipped until a model is attached to the
      hand, real or filler.
- [ ] Enemy Animator Controller untouched — enemies only ever fire the one
      generic `Attack` trigger (no combo/ability concept), so there was
      nothing to distinguish for them in this pass.

## Broken/stagger visual (StunnedLoop) — combat-feel follow-up
- [x] Problem: `Stagger.IsBroken` had zero visual tell besides the player's
      own HUD bar — an enemy (or the player, to an onlooker) about to be
      finished looked identical to normal.
- [x] `Stagger.cs` now drives a `Broken` bool on its own `Animator` every
      frame (`IsBroken`, auto-filled via `GetComponentInChildren` like the
      rest of this project's optional-Animator components) — self-correcting
      on both the rising and falling edge, no need to hook the existing
      (still otherwise-unused) `Broken` C# event.
- [x] Wired a `StunnedLoop` state (same Blink filler pack) into both
      `LowPolyHumanAnimator.controller` and `EnemyAnimatorController.controller`
      — Any State → StunnedLoop while `Broken == true`, back to Locomotion/
      Idle while `Broken == false`. Both hand-edited and verified the same way
      as the combo/ability pass (parsed back with PyYAML, checked every
      cross-reference resolves) — see `SETUP.md` for the same
      verify-before-trusting checklist, now covering this too.
- [ ] Since this is shared by `Stagger.cs` (one component, used by both
      Player and Enemy prefabs), it applies to enemies automatically the
      moment they have both a `Stagger` and an `Animator` — no separate
      enemy-specific work needed, but not yet confirmed in play.

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
