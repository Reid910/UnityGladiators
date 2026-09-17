# Setup

Manual Unity Editor steps needed to make the current code playable. Updated after
each feature.

## Input-leak fix (ArenaMenuController) — verify before trusting

No Editor steps needed — wired directly in `SampleScene.unity`'s YAML
(new stripped reference for `PlayerController`, reusing the existing one
for `PlayerCombat`). Verify:

1. Play a run, pause (Escape), resume, then either die or clear enough
   waves to reach a result screen, then Return to Main Menu or Fight
   Again — the Console should show no more `This will cause a leak and
   performance issues, InputSystem_Actions.Player.Disable() has not been
   called` assert. Root cause: `ArenaMenuController.Suspend()`/`Restore()`
   used to disable/enable the "Player" input map via the low-level
   `InputSystem.ListEnabledActions()` API directly, bypassing
   `PlayerCombat`/`PlayerController`'s own `InputSystem_Actions` wrapper
   instances entirely — that desynced each wrapper's own lifecycle
   tracking from the action's real state. Fixed by adding a
   `SetGameplayInputEnabled(bool)` method to both scripts (routes through
   their own wrapper) and wiring `ArenaMenuController`'s two new
   `Player Combat`/`Player Controller` fields to call those instead.
   `ThirdPersonCamera` didn't need this — it was already toggled via whole
   component enable/disable, which correctly fires its own
   `OnEnable`/`OnDisable`.
2. Also worth cleaning up separately (not touched here, not the cause of
   this leak since it's never activated): a long-deactivated leftover
   `PlayerCombat`/`Health`/`Animator` still sits on an old, inactive
   duplicate Player-like object in `SampleScene.unity` (`m_IsActive: 0`,
   dating back to early in the project before `Player.prefab` was wired up
   correctly) — dead weight, safe to delete whenever convenient.

## First playtest fixes — verify before trusting

No new Inspector wiring — all hand-edited YAML/code. Verify:

1. **Sprint**: hold Sprint while moving for several seconds — the run
   animation should play smoothly, not visibly restart/stutter every
   fraction of a second like before.
2. **Block**: hold the Deflect/Block input (Space) for a few seconds —
   same check, `BlockingLoop` should hold steady, not restart.
3. **Broken/StunnedLoop**: get an enemy (or yourself) staggered to full and
   watch it for the ~2s broken duration — should hold the stunned pose
   steadily rather than flickering/restarting (this bug predates this
   session's Sprint/Block work, just wasn't noticed until now).
4. **Slide commitment**: sprint, Dash to trigger a Slide, and try
   attacking/dashing again immediately (mid-slide) — should be fully
   locked out until near the end of the slide, where a Light attack should
   land as a dodge-out attack (forward lunge) right as the lock lifts.
5. **HUD**: Heavy's cooldown icon should no longer count down in lockstep
   with the Attack (Light) icon.
6. **Ability commitment**: try pressing Ability mid-Light-combo or
   mid-slide — should be fully blocked (no cast) until the current action's
   lock clears, unlike before where it fired immediately regardless.
   Casting the Ability should also block Light/Heavy/Dash until its own
   windup+active+recovery finishes.

## Combat identity redesign, step 1 — verify before trusting

No new Inspector wiring needed — everything here is either a pure code
change or a numeric tweak to fields that already exist. Verify:

1. **Hyper armor**: get hit by an enemy mid-Light-combo — you should now
   flinch/hitstun (assuming no other immunity applies), unlike before. Get
   hit mid-Heavy — you should still be immune to the flinch (hyper armor
   still applies), only the animation/hitbox timing changed.
2. **Telegraph flicker**: watch an enemy about to attack — it should flicker
   between its tier color and a red/orange warning tint for the whole
   windup, then settle back to its tier color right as the hit resolves.
   `EnemyController`'s new `Telegraph Flicker Color`/`Telegraph Flicker
   Interval` fields are tunable on the prefab if the flicker reads as too
   fast/slow or the wrong color.
3. **Numbers**: confirm stagger visibly takes longer to decay from a partial
   fill, the player's health bar shows 120 max instead of 500, and wave 5
   spawns 6 enemies instead of 10 (`2 + 4×1` vs. the old `2 + 4×2`).
4. **Enemy Shuffle**: approach an enemy from far away — it should Sprint
   straight at you, then within `Shuffle Distance` (default 3.5) start
   moving side to side while still facing you, then close in and attack
   once within `Stopping Distance`. If it looks wrong, `Shuffle Distance`
   needs to stay bigger than `Stopping Distance` on `Enemy.prefab` or the
   Shuffle band collapses to nothing.

## Combat identity redesign, step 4 — verify before trusting

No new Inspector wiring needed — all new fields default sensibly, and
`Sprint` was already bound (Left Shift) in the Input Actions asset, just
unused until now. Verify:

1. **Sprint**: hold Left Shift while moving — should visibly speed up, no
   limit on duration (no stamina bar exists to drain).
2. **Slide**: hold Sprint and press Dash — should cover the dash distance
   over a short slide instead of an instant teleport-like burst, and you
   should feel a brief speed boost right after it ends (easiest to notice
   by immediately holding a movement direction after the slide finishes).
   Dash while NOT sprinting should behave exactly as before (instant).
3. **Dodge-out / sprint attacks**: attack immediately after a dash/slide
   ends, or while sprinting — you should see/feel an extra forward lunge on
   top of the normal Light hit. Attacking normally (not sprinting, not
   right after a dash) should be unaffected.

## Combat identity redesign, step 5 — verify before trusting

No new Inspector wiring needed — `Jump`/`Crouch` were already bound (Space/C)
in the Input Actions asset, just unused until now. Verify:

1. **Block**: hold Space while an enemy attacks — you should take no HP
   damage, but your Stagger meter should visibly climb instead (75% of what
   the hit's normal stagger contribution would be).
2. **Deflect**: press Space (don't hold) right as an enemy's hit would land
   — should take no damage AND cost no Stagger, and the (currently
   text-only, no dedicated UI yet) Ultimate meter should jump by 20.
   Pressing too early (more than 0.5s before the hit lands) should fall
   back to the Block behavior above once the hit actually lands, not a
   clean Deflect.
3. **Ultimate**: land enough hits/deflects to fill the meter (no HUD
   element for this yet either — check `PlayerCombat.UltimateMeter` in the
   Inspector during Play mode, or add a debug readout), then press C —
   should play the same big two-handed swing Heavy uses, hit a wide AoE,
   and reset the meter to 0.
4. Confirm a **Heavy or Ultimate landing on an already-broken enemy** kills
   it instantly with no special animation beyond the swing itself, while a
   **Light attack on a broken enemy** still plays the same instant-kill
   (no dedicated cinematic exists yet — that's still open, see `TODO.md`).

## Audio hookup — done, verify in play — no Inspector wiring needed

All `AudioClip` fields are now assigned directly in the prefab/scene YAML
(same hand-edit approach used elsewhere this project) using clips from the
Kenney RPG Audio, Kenney Interface Sounds, and Alexander Ehlers Free Music
Pack folders under `Assets/AssetPacks/`. Best-fit picks, not custom combat
SFX — swap any of these later if a better-fitting sound turns up:

- `PlayerCombat` (Player.prefab): Light = `knifeSlice`, Heavy = `chop`,
  Ultimate = `bong_001`, Hit Impact = `metalPot2`, Ability Cast =
  `glitch_001`, Dash = `cloth1`, Slide = `cloth3`, Deflect = `metalClick`,
  Block = `metalLatch`.
- `Health` (Player.prefab + Enemy.prefab): Hit = `tick_001` (short, quiet —
  played at explicit 0.4 volume in code since it fires on every hit taken),
  Death = `dropLeather`.
- `Stagger` (Player.prefab + Enemy.prefab): Break = `glass_001` (a
  "shatter" sound doubling as the Broken-state cue).
- `EnemyController` (Enemy.prefab): Attack Swing = `knifeSlice2`.
- `WaveManager` (SampleScene): Background Music = Alexander Ehlers -
  "Doomed", Music Volume set to 0.2 for testing (was 0.5).

Verify: play a wave and confirm each sound actually fires at the moment it
should (swing on attack, thud on getting hit, shatter on stagger break,
music starts on wave 1, etc.) and that nothing is jarringly loud/quiet
relative to the rest — none of these volumes have been tuned by ear yet.

## Slide animation — verify before trusting

Same hand-edit technique as every other Animator Controller change this
project — no new Inspector wiring needed. Verify:

1. Sprint, then Dash — should play a forward-roll animation (`RollForward`
   filler clip, no dedicated slide clip exists) for the slide's duration,
   then return to normal locomotion.
2. Dash while NOT sprinting should still play no special animation (same as
   before this change).

## GetHit / BlockingLoop / Sprint animations — verify before trusting

Same hand-edit technique, no new Inspector wiring. Verify:

1. Take a hit — should play `GetHit` briefly before returning to normal
   locomotion. Should NOT play while already in `StunnedLoop` (Broken) —
   structurally shouldn't be possible since broken targets get finished via
   `Execute()` instead of `TakeDamage()`, but worth confirming.
2. Hold the Deflect/Block input (Space) — should loop `BlockingLoop` the
   whole time held, and return to normal locomotion the instant it's
   released.
3. Hold Sprint while moving — should play `Sprint` instead of the normal
   run cycle, and return to Locomotion the instant Sprint is released or
   movement stops.
4. **Known mismatch, not fixed yet**: getting hit while hyper-armored
   (mid-Heavy-swing) still plays `GetHit` even though hitstun itself is
   suppressed — see the note in `TODO.md`. Not broken, just a visual
   inconsistency worth knowing about if it looks odd in play.

## Combat identity redesign — NavMeshAgent switch, Editor steps needed

Code side is done: `EnemyController` now has an optional `navMeshAgent`
field (`Awake()` also falls back to `GetComponent<NavMeshAgent>()` if left
empty) and, when one is present and on a baked NavMesh, uses it during the
Sprint phase only — Shuffle/Attack are untouched, still direct
`CharacterController` movement, per `docs/combat-redesign-plan.md`. Until
the steps below are done, there's no `NavMeshAgent` on `Enemy.prefab` yet,
so this silently falls back to the old straight-line movement — nothing
breaks in the meantime.

Also added: 5 filler "Obstacle" pillars (plain boxes, reusing the arena
floor's own material) in `SampleScene` under a new `Obstacles` GameObject,
scattered in the lane between the player start and the enemy spawn
cluster, each already marked **Navigation Static**. Placeholder geometry
only — replace/rearrange freely once real arena art exists.

This project is on Unity 6 with the `com.unity.ai.navigation` package
already installed, so baking goes through a `NavMeshSurface` component, not
the old Window → AI → Navigation panel (removed in Unity 6). Steps:

1. Select `Arena_Floor` in `SampleScene` (or create an empty "Navigation"
   GameObject) and **Add Component → Nav Mesh Surface**.
2. In its Inspector, set **Collect Objects** to **All** (simplest — bakes
   from every collider in the scene regardless of static flags, so the
   Navigation Static flag on the obstacles is a nice-to-have here, not
   required).
3. Click **Bake** at the bottom of the Nav Mesh Surface Inspector. Confirm
   the blue NavMesh overlay covers the floor and routes around the 5
   Obstacle pillars (visible in the Scene view once baked).
4. Select `Enemy.prefab` and **Add Component → Nav Mesh Agent**. Rough
   starting values to match the existing tuning (all in `EnemyController`):
   - **Speed**: match `Movement Speed` (2.5 by default)
   - **Radius**: ~0.4, **Height**: ~2 (roughly the character's size)
   - **Stopping Distance**: 0 — `EnemyController`'s own three-phase
     distance logic already handles stopping/Shuffle/Attack; the agent
     should never think it's "arrived" on its own before that.
   - Everything else can stay default.
5. No field wiring needed on `EnemyController` itself — leave `Nav Mesh
   Agent` empty in the Inspector and it self-finds via `GetComponent`.
6. Playtest: an enemy approaching from beyond Shuffle Distance should walk
   around an Obstacle pillar instead of clipping through it. If it ignores
   the pillars entirely, re-bake (step 3) — the NavMesh may predate the
   Obstacles being added.

## Corpse loot-rarity glow — verify Visual Renderer assignment

`LootableCorpse` now tints the enemy's renderer once the corpse becomes
lootable (post-wave-clear): rarity color if it holds an item (same
`RarityColor` mapping as item pickups — white/blue/orange), a dim grey if it
rolled empty, untinted while still not lootable.

1. On `Enemy.prefab`, check the `LootableCorpse` component's **Visual
   Renderer** field. It falls back to `GetComponentInChildren<Renderer>()` if
   left empty, which may grab the wrong renderer on a multi-part character
   rig (e.g. a weapon mesh instead of the body). Assign the actual body
   mesh renderer explicitly if the auto-picked one looks wrong in play.
2. Kill an enemy, wait for the wave to clear, and confirm the corpse tints
   before you attack it (not after) — the color should match what actually
   drops when you loot it, since the roll now happens once at wave-clear
   instead of on-hit.
3. Confirm a corpse that rolls no drop shows the dim grey tint, not white
   (white is reserved for an actual Common-rarity drop) — a same-color
   result here would make "empty" indistinguishable from "Common item."
4. **New**: a corpse holding an item now also pulses toward white and
   back — added because a Common drop on a T1 enemy (the only tier that
   exists as real content) is the exact same white as the enemy's own
   live tint, making it invisible otherwise. Confirm the pulse stops the
   instant you actually loot the corpse (settles to a static tint), and
   that an empty corpse never pulses.

## Enemy tier tint — verify Visual Renderer assignment

`EnemyController` now tints itself by `Tier` on spawn (white/orange/red for
T1/T2/T3 — see `EnemyTierColor`), a cheap placeholder tell until real
per-tier prefabs/models exist. Same caveat as the corpse glow above:

1. Check `EnemyController`'s **Visual Renderer** field on `Enemy.prefab` —
   falls back to `GetComponentInChildren<Renderer>()` if left empty, which
   can grab the wrong renderer on a multi-part rig. Assign the body mesh
   explicitly if the auto-picked one looks wrong.
2. Since only T1 enemies exist as actual content right now (no T2/T3 prefab
   built yet — see `TODO.md`), there's nothing to visually compare against
   yet; this is confirmed correct once a T2 or T3 prefab actually exists and
   spawns with a different tint than T1.

## Player health regen — verify before trusting

No Inspector wiring needed — `Player.prefab`'s `Health` component was
hand-edited directly to set `regenPerSecond: 10` (Enemy stays at the script
default of 0, i.e. no regen). Regen is always active, even mid-combat — same
constant-tick model as Stagger's decay, no out-of-combat delay. Verify:

1. Take damage as the player and confirm health climbs back up slowly on its
   own, including while still taking hits, not just after a pause.
2. Confirm it stops exactly at max health rather than overshooting.

## Death animation / stray camera / stale sensitivity fix — verify before trusting

No Inspector wiring needed — this only touched hand-edited YAML (both Animator
Controllers, `SampleScene.unity`) plus `Health.cs`. Verify:

1. Get an enemy staggered to broken, then land the killing blow (a finisher).
   It should play its actual `Death` pose and stay there — not flash through
   `StunnedLoop` and end up standing at idle.
2. Confirm no more `Parameter 'Hit' does not exist` / `Parameter 'IsDead' does
   not exist` console errors during normal combat (light/heavy/enemy attacks,
   any death).
3. Mouse-look should feel normal again (`ThirdPersonCamera.mouseSensitivity`
   was stuck at a stale `2` in `SampleScene.unity`, now `0.12` to match the
   script default). If it's still too fast/slow for your mouse, that field is
   the one to tune directly on the Main Camera.
4. The Directional Light no longer has an (accidental, non-functional)
   `ThirdPersonCamera` component on it — nothing to verify here beyond
   confirming lighting looks unchanged.

## Combat overhaul (combo/heavy/ability/dash inputs) — M1, partial

1. **Regenerate the Input Actions C# wrapper.** `InputSystem_Actions.inputactions`
   was edited directly (added `Heavy`, `Ability`, `Dash` actions/bindings) but the
   generated `InputSystem_Actions.cs` wrapper needs Unity to regenerate it:
   - Open the project in Unity (it should auto-reimport the changed asset), or
   - If `PlayerCombat.cs` shows compile errors about missing `Heavy`/`Ability`/
     `Dash` members, select `Assets/InputSystem_Actions.inputactions` in the
     Project window, open it (double-click), and click **Save Asset** in the
     Input Actions editor toolbar — this forces regeneration.
2. **Default bindings added** (keyboard/mouse only for now — no gamepad bindings
   yet, add later if needed):
   - `Heavy` → Right Mouse Button
   - `Ability` → Q
   - `Dash` → Left Ctrl
   You can rebind these in the Input Actions editor if you'd rather use
   different keys.
3. ~~Animator Controller: `PlayerCombat.cs` fired trigger parameters
   (`AttackCombo1/2/3`, `AttackHeavy`, `AbilityCast`, `Dash`) that didn't
   exist~~ — resolved without new assets: light combo and the heavy attack now
   all reuse the existing `Attack` trigger/state (the only one either Animator
   Controller actually has), so every attack plays the same swing animation
   for now instead of needing new states built. Ability and dash fire no
   animator trigger at all — they still fully function (cooldowns, damage,
   movement), they just don't animate yet. Give each move its own trigger
   name in `PlayerCombat.cs` (and matching Controller states) once real
   animations exist; not needed for MVP.
4. **No new Inspector references needed** — `PlayerCombat` still uses the same
   `attackPoint`/`enemyLayer`/`animator`/`health` fields as before. It also now
   auto-fills a `CharacterController` reference via `GetComponent` on Awake if
   left unset, needed for the dash to move the player — the Player prefab
   should already have one (`PlayerController` uses it too), so this should
   need no action, but double check the field isn't pointing at the wrong
   object if you had one manually assigned before.
5. **Combo/heavy numbers are placeholder starting values** (see the
   `lightComboHits` array and `heavyDamage`/`heavyStaggerAmount`/
   `heavyHitstunDuration`/`heavyRecoveryTime` fields in the Inspector) — tune
   to taste once you can playtest. (Ability cooldown and dash distance/cooldown
   are no longer separate fields here — they come from the equipped weapon's
   `AbilityDefinition`/boots' `DashDefinition` instead, see M4 below.)

## Stagger / hitstun / finishers — M1, core logic done

1. ~~Add `Stagger` and `Hitstun` components to both the Player prefab and the
   Enemy prefab~~ — done, both prefabs already have both components.
2. **No new Animator params required for stagger/hitstun logic itself** — being
   staggered/stunned currently just freezes movement/attack via code, it
   doesn't play a dedicated animation yet. If you want a visible "broken" pose,
   that'd need its own Animator work later (not blocking).
3. **Tune stagger numbers per prefab** once you can playtest — `Stagger.cs`'s
   `maxStagger`, `decayPerSecond`, `decayDelayAfterHit`, and `brokenDuration`
   are all serialized fields, so a tankier enemy (e.g. a future "legionary"
   variant from M5) can just get a higher `maxStagger` on its own component
   instance without any code changes.
4. **Test carefully**: the player can now die instantly from a finisher if hit
   while broken — this is intentional (see TODO.md), but means iterating on
   `Stagger`'s numbers matters for whether the game feels fair vs. cheap. Watch
   for the player's stagger meter filling too fast against multiple enemies at
   once early on, before there's any gear to offset it.

## Item data model — M2, code done, no assets created yet

Nothing plays differently yet — this is just the data model (`ItemDefinition`,
`AffixDefinition`, `AbilityDefinition`, `DashDefinition`, all under
`Assets/Scripts/Items/`). Nothing in the game creates or equips items yet
(that's M3/M4). To actually have items to work with once that lands, you'll
need to create asset instances in the Editor:

1. **Create `AffixDefinition` assets first** — right-click in the Project
   window → **Create → UnityGladiators → Affix**, one per stat you want
   available (attack speed, crit chance, ability cooldown reduction, move
   speed, max health, armor). Set `Stat Type`, `Min Value`/`Max Value`, and
   optionally `Eligible Slots` (leave empty for "any slot," or restrict e.g.
   armor-flavored affixes to Chest/Head/Pants).
2. **Create `AbilityDefinition` assets** (**Create → UnityGladiators →
   Ability**) — one per weapon-granted skill. Just needs a name/cooldown for
   now; the actual gameplay effect isn't implemented yet (M4).
3. **Create `DashDefinition` assets** (**Create → UnityGladiators → Dash**) —
   one per boots-granted dash variant. Same caveat, data only for now.
4. **Create `ItemDefinition` assets** (**Create → UnityGladiators → Item**) —
   one per droppable item. Set `Slot`, damage range, and drag in the
   `AffixDefinition`s this item is allowed to roll (`Possible Affixes`). For
   Weapon-slot items, also assign an `AbilityDefinition`; for Boots-slot items,
   assign a `DashDefinition`.
5. No specific count needed yet — just enough to have something to test with,
   now that M3 (below) actually wires `ItemRoller.Roll()` into gameplay.

## Corpse looting, pickup, cleanup — M3, done (wired directly in the asset files)

All of this was wired up by editing the prefab/project/scene YAML directly
(this project uses Force Text asset serialization, so it's plain text) rather
than through the Editor UI, then **confirmed working end-to-end in a live
playtest** — corpses persist, get hit, roll loot, and a visible pickup spawns.

1. ~~Create `Corpse`/`Pickup` physics layers~~ — done (`Corpse` = layer 7,
   `Pickup` = layer 8, in Project Settings → Tags and Layers).
2. ~~Enemy prefab: `CorpseHitbox` child + `LootableCorpse` component~~ — done.
   The Enemy prefab now has a child GameObject `CorpseHitbox` (Capsule
   Collider, `Corpse` layer, disabled by default) wired into `Health`'s
   `Corpse Hitbox` field, and a `LootableCorpse` component (`Drop Chance`
   0.5, `T3 Super Rare Chance` 0.3, `Possible Items` = the one
   `NewItem.asset` from the M2 step, `Item Pickup Prefab` = the new
   `ItemPickup.prefab`, see below).
3. ~~Create an `ItemPickup` prefab~~ — done: `Assets/Prefabs/ItemPickup.prefab`,
   a small sphere (builtin mesh, no new mesh asset) with a trigger
   `SphereCollider` on the `Pickup` layer and an `ItemPickup` component.
4. ~~Player prefab: `PlayerEquipment` + `PlayerCombat.Corpse Layer`~~ — done.
   `PlayerEquipment` and `PlayerStats` components were both added to the
   Player prefab, and `PlayerCombat`'s `Corpse Layer` field now points at the
   `Corpse` layer.
5. ~~Visual rarity distinction~~ — done: `ItemPickup.cs` has a `visualRenderer`
   field (wired to the pickup's `MeshRenderer`) tinted by rarity via a
   `MaterialPropertyBlock`, plus a world-space name label (`nameLabel`) —
   `ItemPickup.prefab` now has a child `NameLabel` object (3D `TextMeshPro`,
   reusing the same `LiberationSans SDF` font and `FaceCamera` billboard
   pattern already used by the Enemy's health text) floating above the pickup,
   colored/text-set by `ItemPickup.Initialize()`/`OnTriggerEnter()`.
6. ~~`NewItem.asset` unconfigured stub~~ — resolved: it's now "Gladius" (a real
   Weapon item, 8-14 damage, references `NewAbility.asset`). Four more
   `ItemDefinition` assets were added — "Worn Sandals" (Boots, references
   `NewDash.asset`), "Leather Cap" (Head), "Leather Chestplate" (Chest),
   "Leather Greaves" (Pants) — all five wired into the Enemy prefab's
   `LootableCorpse.Possible Items`, so drops now cover every slot and equipping
   one actually swaps something visible. `NewAbility`/`NewDash` were given
   display names ("Reserved Strike" / "Sprint Step") but still don't do
   anything mechanically — equipping different weapons/boots just changes
   which named-but-inert ability/dash you're nominally carrying, as intended
   for this pass.
7. **Found and fixed a real bug while wiring this up**: four of the five
   `AffixDefinition` assets (`Cooldown Reduction`, `Critical Hit Chance`,
   `Max Health`, `Movement Speed`) had `statType: 0` regardless of their name
   — i.e. they were all secretly "Attack Speed" affixes — and every affix had
   `minValue`/`maxValue` both `0`, so every roll would've been worth nothing.
   Fixed all four `statType` indices to match their names, gave all five (plus
   a newly-created sixth, `Armor.asset` — `StatType.Armor` had no asset at
   all) real roll ranges. All six now also explicitly declare `eligibleSlots`
   as empty (any slot) rather than leaving the field ambiguous.
7. `WaveManager` needs no new references — cleanup is automatic once the
   above prefabs exist, since `LootableCorpse` finds it via
   `FindFirstObjectByType<WaveManager>()`.
8. ~~Enemy prefab's `Health.destroyOnDeath`/`disableObjectOnDeath` were still
   `true`~~ — fixed, both now `false` (matching the Player prefab). These
   predate corpse looting: left `true`, the corpse (and its loot window) got
   destroyed 2.5s after death regardless of the hitbox/`LootableCorpse` setup
   above. Now the corpse persists until `WaveManager.ClearCorpses()` clears it
   at the next wave, as `TODO.md`'s M3 notes describe.
9. `WaveManager`'s scene component also had a stale `enemyPrefab` (singular)
   field left over from before the M5 `enemyPrefabs[]` array refactor, which
   silently made the array empty (`Debug.LogWarning` on spawn) — fixed by
   moving that same Enemy prefab reference into the new array field directly
   in `Assets/Scenes/SampleScene.unity`.
10. ~~Corpse/pickup cleanup timing didn't match the intended "grace period"
    feel~~ — reworked in `WaveManager.cs`: corpses now survive one full wave
    before clearing (destroyed at the start of the wave *after* the one
    following their death), and dropped pickups get one wave more than that.
    See `TODO.md`'s M3 note for the exact mechanism
    (`AdvanceCorpseAndPickupGenerations()`).
11. **The biggest find: the Player actually running in the scene was not
    `Assets/Prefabs/Player.prefab` at all.** It was a leftover, disconnected
    setup — an instance of the imported `HumanMale_Character_FREE.prefab`
    (from the Blink asset pack) with gameplay scripts bolted on directly in
    the scene, missing `corpseLayer`, `Stagger`, `Hitstun`, `PlayerEquipment`,
    and `PlayerStats` entirely. That's the actual reason loot never spawned —
    the player's corpse-hit query was using an empty layer mask, nothing to
    do with drop chance or the pickup's mesh. Fixed by replacing it in
    `Assets/Scenes/SampleScene.unity` with a real instance of
    `Assets/Prefabs/Player.prefab` (which *is* the correct, intended object —
    it just had never been placed in the scene), re-pointing `GameUI` and
    both `ThirdPersonCamera` instances at the new instance, and deactivating
    (not deleting) the old object so it's trivially reversible. Confirmed
    working in-editor: movement, camera-follow, combat, and looting all run
    on the real prefab now.
12. `Drop Chance` was bumped to 1.0 temporarily to isolate the above bug and
    has been set back to 0.5 now that the pipeline is confirmed working.
13. **Found and fixed a real bug via live playtesting**: `CorpseHitbox`'s
    `CapsuleCollider` had `Is Trigger` unchecked. `Health.EnableCorpseHitbox()`
    just re-enables the collider once a wave clears — with `Is Trigger` off,
    that made a solid capsule that physically blocked the player from walking
    past/through cleared corpses instead of just being loot-hittable. Fixed by
    setting `m_IsTrigger: 1` on `Assets/Prefabs/Enemy.prefab`'s `CorpseHitbox`
    directly in the prefab YAML. Doesn't affect loot-hitting, since
    `PlayerCombat.LootCorpses()` already uses `Physics.OverlapSphere`, which
    detects trigger colliders fine.

## Stats integration — M4, done

1. ~~Add a `PlayerStats` component to the Player prefab~~ — done (added
   alongside `PlayerEquipment` in the M3 pass above; `equipment`/`health`
   auto-fill via `GetComponent` on Awake, no other wiring needed).
2. **`PlayerCombat`'s old `Ability Cooldown`/`Dash Distance`/`Dash Cooldown`
   Inspector fields are gone** — they're fully replaced by whatever
   `AbilityDefinition`/`DashDefinition` the equipped Weapon/Boots reference
   (see the M2/M3 steps above for creating those assets). This means the
   player now has **no ability and no dash until you equip a Weapon/Boots
   item** — either drop one in the scene for the player to walk over, or
   temporarily pre-populate `PlayerEquipment` for testing (there's no
   in-Inspector way to pre-equip yet, would need a small test-only script or
   waiting for actual pickups to exist in the scene).
3. **Crit Chance affix does nothing yet** — it rolls and aggregates fine, just
   isn't consumed by any damage math. Not blocking, noted in TODO.md.

## Enemy variants and wave composition — M5, needs prefab creation

The existing Enemy prefab has an `EnemyController` with a new `Tier` field
(defaults to T1) — no existing prefab breaks. To actually get variety:

1. **Set the current/only Enemy prefab's `Tier`** to whatever makes sense (T1
   is fine as the baseline). Also make sure it has `Stagger`, `Hitstun`, and
   `LootableCorpse` components (from the M1/M3 steps above) — `LootableCorpse`
   now requires an `EnemyController` on the same object (`[RequireComponent]`),
   which the existing prefab already has.
2. **Duplicate the Enemy prefab 1-2 times** to create variants (e.g.
   "Enemy_Retiarius", "Enemy_Legionary"), then just change values — no new
   scripts needed:
   - Retiarius (T1): higher `Movement Speed`, lower `Health.Max Health`, higher
     `Attack Hitstun Duration`.
   - Legionary (T2): lower `Movement Speed`, higher `Max Health`, higher
     `Attack Damage`, higher `Stagger.Max Stagger`.
   - Ranged/beast (T3): much higher `Stopping Distance` on `EnemyController`
     (makes it attack from range using the existing instant-hit logic — no
     projectile visual, that'd need real animation/VFX work later).
   Give each its own `LootableCorpse.Possible Items` pool if you want
   different variants to drop different gear.
3. **On `WaveManager`**: replace the single `Enemy Prefab` reference (field is
   now `Enemy Prefabs`, an array) with all your variant prefabs. Set
   `T2 Unlock Wave`/`T3 Unlock Wave` if you don't like the defaults (2 and 3).
4. **`Endless Mode` defaults to on** — the game no longer ends at wave 3, it
   just keeps scaling. Uncheck it on `WaveManager` if you want the old
   win-at-wave-3 behavior back for testing.

## UI/feedback — M6, done (wired directly in the scene YAML)

Same approach as the M3 corpse-looting pass: edited `Assets/Scenes/SampleScene.unity`
directly (Force Text serialization) rather than through the Editor UI.

1. ~~On the GameUI object: assign `Player Combat` and `Player Equipment`~~ —
   done. Added stripped `MonoBehaviour` references (fileIDs `700000005`/
   `700000006`) into the existing Player `PrefabInstance` block and wired them
   into `GameUI`'s `Player Combat`/`Player Equipment` fields.
2. ~~Create new TextMeshProUGUI elements for Equipped Items Text, Ability
   Cooldown Text, Dash Cooldown Text, Combo Text~~ — done, all four created
   under the `HUD` RectTransform and assigned on `GameUI`:
   - `Equipped Items Text` — anchored bottom-left (unlike the other HUD
     readouts, which are top-left), smaller font (24pt) to fit multi-line
     per-slot detail without overflowing.
   - `Ability Cooldown Text`, `Dash Cooldown Text`, `Combo Text` — same
     top-left column as the existing readouts, stacked below them.
3. **Found and fixed a real bug while wiring this up**: the existing top-left
   readouts (`PlayerHealthText`, `WaveText`, `EnemiesRemainingText`,
   `StaggerText`) had never been given distinct Y positions —
   `StaggerText` was anchored at the same `y: -50` as `WaveText`, so the two
   overlapped. Respaced the whole top-left column to `0, -50, -100, -150,
   -200, -250, -300` (Health, Wave, Enemies, Stagger, Ability, Dash, Combo).
4. ~~`ItemPickup`'s new `Name Label` field~~ — already done, see M3 notes above.

No manual Editor steps remain for M6. As always, open the project in Unity
once after a direct-YAML pass like this to let it re-serialize and confirm
nothing reports a missing reference before playtesting.

## Crit Chance and Armor stats — M4 follow-up, no Editor steps needed

Both affixes already rolled and displayed correctly (M2/M6) but neither
affected gameplay. Now wired in code only:

- Crit Chance: `PlayerCombat.DealDamage()` rolls it once per swing and
  multiplies total damage by the new `Crit Damage Multiplier` field (Attack
  header, defaults to 1.5x — tune to taste once you can playtest).
- Armor: `Health.SetArmor()`, called by `PlayerStats` alongside the existing
  `SetMaxHealthBonus()`, and `TakeDamage()` subtracts it as a flat reduction
  (floored at 1 damage).

No new Inspector references required — both read from the same
`PlayerStats`/`Health` wiring already in place from M4. Open the project once
so Unity picks up the new `critDamageMultiplier` serialized field with its
default value.

## Item swap now button-triggered, not instant-on-touch — no Editor steps needed

Standing near an `ItemPickup` no longer auto-equips it — it just marks itself
as the player's nearby pickup (`OnTriggerEnter`/`OnTriggerExit`, same trigger
collider as before). Pressing Interact calls
`PlayerEquipment.TrySwapWithNearby()` to actually swap it in.

- Reused the stock `Interact` action that already existed in
  `InputSystem_Actions.inputactions` (bound to `E` / gamepad North) but
  wasn't used anywhere — changed its interaction from `Hold` to a plain
  press (edited both the `.inputactions` asset and the matching embedded
  JSON in the generated `InputSystem_Actions.cs`, so no Unity regeneration
  step is needed, same as the direct-edit approach used for
  Heavy/Ability/Dash).
- No new prefab/Inspector wiring — `ItemPickup` and `PlayerEquipment` are
  the same components already on `Player.prefab`/`ItemPickup.prefab`.
- **Swap-target feedback added**: `ItemPickup.nameLabel` now shows `[E] <item
  name>` plus a smaller `swaps <currently equipped item name>` second line
  while it's the player's active swap target, reverting to the plain name
  when it isn't. There's also an optional `Visual Transform` field that
  scales up (1.25x default, `Targeted Scale Multiplier`) while targeted, so
  it visually pops if several pickups are near each other.
- **`Visual Transform` still needs wiring on `ItemPickup.prefab`** — assign
  it to the mesh's own child transform, *not* the prefab root, so the
  trigger collider doesn't grow along with the scale-up. Leaving it
  unassigned is safe — the label-only feedback still works, it just won't
  scale.

## Damage numbers and hit-stop — M1 follow-up

New `Assets/Prefabs/DamageNumber.prefab` (world-space `TextMeshPro`, same
font/billboard setup as the enemy health text — reuses the existing
`FaceCamera.cs`) plus two new scripts, `DamageNumber.cs` and `HitStop.cs`.
Both are wired into `Health.cs` itself (`TakeDamage()`/`Execute()`), so they
cover player-dealt and enemy-dealt damage from one place — nothing new to
wire in `PlayerCombat.cs` or `EnemyController.cs`.

1. ~~`Enemy.prefab`'s `Health` component: `Damage Number Prefab`~~ — done,
   wired directly in the prefab YAML, pointing at the new
   `DamageNumber.prefab`. `Damage Number Color` defaults to white, `Hit Stop
   Duration` to 0.05s (0.1s on a finisher) — tune both to taste.
2. ~~`Player.prefab`'s `Health` component: same wiring~~ — done now that
   this branch has `Player.prefab` (merged in from
   `chore/m1-m5-setup-and-fixes`). `Damage Number Color` set to red on the
   Player so damage taken reads differently from damage dealt.
3. **No Editor steps needed for `HitStop`** — it has no Inspector fields and
   creates its own runner object on first use.

## Mouse-look camera and Input System migration — no Editor steps needed

`ThirdPersonCamera.cs` was rewritten in code only — no new Inspector fields,
no `.inputactions` changes (it reuses the `Look` action that already existed
and was already bound to mouse delta, just never read anywhere), no
regeneration needed. `ProjectSettings.asset`'s Active Input Handling was
switched to "Input System Package (New)" only.

1. **Open the project once after pulling** so Unity re-reads the changed
   Active Input Handling setting — this one, like Tags & Layers, is read at
   Editor/Player startup, not through the normal asset-reimport pipeline.
2. **Tune `mouseSensitivity`** on the camera object once you can playtest —
   `0.12` is a rough starting guess for raw pointer-delta scale, not measured
   against real play.
3. **Hold Left or Right Alt** to free the cursor; release to re-lock and
   resume camera control. No settings menu or pause state yet — this is just
   a raw escape hatch so the cursor isn't trapped if you need to click
   elsewhere.

## Real combo/ability animations — verify before trusting

No new Inspector fields or asset assignment needed — `LowPolyHumanAnimator.controller`
was edited directly to add the new states/transitions/parameters, and
`PlayerCombat.cs` already fires the matching trigger names. That said, this is
the most complex hand-edited asset in the project so far (way more
interdependent fields than something like `TagManager.asset`), so please
actually verify it rather than assuming it's right:

1. **Open the project and open `LowPolyHumanAnimator.controller`** in the
   Animator window — you should see states `AttackCombo1` (MeleeAttack_OneHanded),
   `AttackCombo2` (PunchLeft), `AttackCombo3` (PunchRight), `AttackHeavy`
   (MeleeAttack_TwoHanded), and `AbilityCast` (SpellCast), each with an arrow
   in from "Any State" (on its own like-named Trigger parameter) and out to
   the Locomotion blend tree.
2. **Play and check the Console** for any Animator-related errors on the
   Player specifically (separate from the pre-existing, unrelated animation
   import warnings on `RollRight`/`RunLeft`/etc. — those aren't from this
   change).
3. **Try light combo (3 hits), heavy, and the ability in play mode** — you
   should see 4 different-looking motions: `MeleeAttack_OneHanded` then
   `PunchLeft` then `PunchRight` for the 3 combo hits, `MeleeAttack_TwoHanded`
   for heavy, `SpellCast` for the ability.
4. If anything looks wrong (T-pose flash, snapping, a state stuck), that's a
   sign something in the hand-edit doesn't match what the Editor would have
   produced — flag it rather than trying to hand-fix the controller further;
   easiest recovery is redoing the affected states through the Editor UI
   directly using the same clips (`MeleeAttack_OneHanded`/`PunchLeft`/
   `PunchRight`/`MeleeAttack_TwoHanded`/`SpellCast`) referenced in `TODO.md`.
5. Trigger names are deliberately generic (`AttackCombo1/2/3`, not
   left/right-specific) since the current clips are filler — when real combo
   animations replace them, only the clip assigned to each existing state
   needs to change, not `PlayerCombat.cs` or any trigger name.

## Broken/stagger animation (StunnedLoop) — verify before trusting, same as above

Same hand-edit technique, same "please actually check it" caveat — this one
touched **both** `LowPolyHumanAnimator.controller` and
`EnemyAnimatorController.controller` (a new `Broken` bool + `StunnedLoop`
state in each), plus `Stagger.cs` (no new required field — its `Animator`
reference auto-fills via `GetComponentInChildren`, same as `Health`/`Hitstun`
already do). No Inspector wiring needed on either prefab.

1. Get an enemy (or yourself) staggered to full in play mode and confirm it
   visibly plays a stunned pose instead of just freezing mid-animation.
2. Confirm it snaps back to normal movement/idle when the broken window ends,
   not stuck in the pose.
3. Same red flags as the combo/ability check above (T-pose flash, stuck
   state) mean something doesn't match what the Editor would have produced.

## Stagger redesign — no Editor steps needed, but a heads-up

No new Inspector wiring required — `damageToStaggerMultiplier` defaults to
`1` on both prefabs and everything else is code-side.

**Worth knowing**: while wiring this up, found that `Player.prefab` had a
serialized snapshot of `PlayerCombat`'s combo/heavy values baked in from
*before* the windup/active-window work — meaning the actual in-game numbers
had been silently different from whatever the script defaults said, this
entire session, without any error or warning. It's fixed now, but it's worth
occasionally spot-checking a prefab's Inspector values against the script
defaults if a number ever seems to not match what a `TODO.md` note says it
should be — Unity prefabs freeze field values at the time they're saved, and
a hand-edited script default only take effect for *new* instances or fields
that never existed on the prefab before.

## Dedicated finisher action — no Editor steps needed, but verify

No new Inspector wiring — pure code, all new fields (`Light Attack Range`,
`Finisher Animator Trigger`/`Windup`/`Recovery Time`) default sensibly.

1. Stagger an enemy to Broken (Heavy/Ultimate build stagger fastest), then
   press Light Attack while it's in range — should play a distinct,
   heavier-looking swing (reusing `AttackHeavy`'s animation for now) instead
   of the normal light-combo swing, then the enemy dies with the finisher
   slow-mo presentation.
2. With multiple enemies around, only a Broken one directly in front
   should trigger this — a Broken enemy off to the side (outside the new,
   smaller `Light Attack Range`) shouldn't.
3. A normal Light attack (no Broken enemy nearby) should feel unchanged
   apart from a slightly smaller hit range — it should now mostly only
   catch the one enemy directly in front instead of also clipping ones
   just off to the side.

## Perilous attacks, gear assets, finisher presentation — verify before trusting

No new Inspector wiring required — all three are code + hand-authored
`.asset`/`.meta`/prefab-array YAML, no new components or scene objects.

1. **Perilous attacks**: get to wave 3+ and let an enemy attack you a few
   times. Occasionally it should flicker **orange** (not the usual red)
   during windup, then either slam down (Downslam) or sweep a wide arc
   (Side swing) instead of the normal single-target swing — and it should
   land even if you're mid-Deflect/Block (unblockable by design). Below
   wave 3, enemies should never do this.
2. **Gear assets**: open `Assets/Definitions/Item/Worn Wraps.asset` and
   `Tempering Shard.asset` in the Inspector — confirm they show as a real
   `ItemDefinition` (not "missing script"), correct Slot dropdown (Gloves /
   Stat Shard), and `Worn Wraps` has a `Deflect Definition` reference
   assigned. Same check for `Leather Cap`/`Chestplate`/`Greaves` — each
   should now show a `Passive Effect Definition` assigned. In play: kill
   enough enemies that a corpse drops one of the 2 new items and confirm
   it's equippable and behaves (Gloves grants Deflect timing, Head/Chest/
   Pants passives actually proc — heal on hit, reduced damage taken, or
   periodic brief invulnerability).
3. **Finisher presentation**: break an enemy's stagger and land the
   killing blow — should freeze briefly then visibly ease back up to
   normal speed over about half a second, not snap back instantly like a
   normal hit's freeze does.

## Combat HUD (installed)
- SampleScene Canvas/Combat HUD contains framed vitals, wave status, centered skill slots, and five compact gear slots.
- Original eight HUD text objects are preserved and inactive; GameUI now references replacement labels.
- Skill icons use LMB (basic attack), Q (Ability), and Left Ctrl (Dash). The basic attack box is smaller. Equip a weapon with AbilityDefinition and boots with DashDefinition to enable those skills.
- Basic attack has a radial cooldown and centered countdown tied to actual windup, hit window, and recovery, including attack-speed modifiers and the shared heavy-attack recovery. GameUI Attack Cooldown Fill/Text are wired; use Gladiators > Upgrade Attack Cooldown for an older HUD.
- RMB Heavy has its own static box beside LMB; only the basic-attack box displays the shared recovery timer. Ability/dash borders flash white for 0.65 seconds when their cooldown completes, then return to gold.
- Equipping gear flashes the matching rarity frame and shows a fading "Equipped: item name" message above the skills for 2.2 seconds. EquipmentHUD listens to PlayerEquipment.ItemEquipped; no swap input changes are needed.
- Feedback is wired in SampleScene. Use Gladiators > Upgrade HUD Feedback for an older HUD; HUDFlash components control flash duration on the skill/gear borders.
- Art and layout can be adjusted directly on Canvas/Combat HUD. GameUI Visual HUD fields drive bars and cooldown overlays.
- Gear slots show item names and rarity borders; the previous Equipment text panel is preserved and inactive.
- Swap comparison appears only for the nearby pickup targeted by PlayerEquipment. It shows current and incoming item stats and [E] Equip/Swap. No inventory screen is needed.
- EquipmentHUD on Canvas/Combat HUD/Gear slots references PlayerEquipment, five slot views, and the Swap comparison panel. These references are already wired in SampleScene.
- For another scene with an existing GameUI Canvas, use Gladiators > Build Combat HUD. For the original HUD, use Gladiators > Upgrade HUD Gear Slots. Both skip the gear upgrade if Gear slots already exists.

### Full arena UI pass (installed in SampleScene)

- **Stat Shard display:** Combat HUD/Stat shard shows the equipped shard name, rarity, and rolled bonuses. EquipmentHUD's Slots array includes StatShard and Shard Bonuses references Stat shard/Bonuses. The card uses the existing rarity flash and equip notification; shard pickups use the existing [E] swap comparison below the card. Install on an older HUD with **Gladiators > Install Shard Display**.
- **Gloves display:** the gear row now fits six slots, including GLOVES. Its name, rarity, and flash use EquipmentHUD's Gloves slot binding. The shard installer adds Gloves too, or use **Gladiators > Install Gloves Display** independently.

- Enter Play mode to open the main menu. **Enter the Arena** starts the run; **Quit Game** exits the player (or stops Play mode in the Editor).
- **Escape / gamepad Start** toggles pause. Resume continues the run; Leave Run / Main Menu reloads a fresh menu state. The menu freezes time, suspends enabled gameplay input actions, releases the camera cursor, and pauses audio while keeping UI input active. It also holds the pause through realtime hit-stop completion.
- `ArenaMenuController` on Canvas references the main/pause panels, Play/Resume buttons, Combat HUD, GameUI, and ThirdPersonCamera. Existing combat scripts and tuning are unchanged. This is a menu state in SampleScene, so no additional build scene is needed.
- `GameUI` **Arena UI** fields: Menus = Canvas's ArenaMenuController; Ultimate Fill = Combat HUD/Ultimate/Track/Fill; Ultimate Text = Ultimate/Charge; Ultimate Ready Flash = Ultimate/Frame. The gauge reads PlayerCombat's existing UltimateMeter, UltimateMeterMax, and IsUltimateReady. At full charge it says **[C] ULTIMATE READY** and flashes the frame; spending charge empties it.
- The skill strip includes a **SPACE / DEFLECT / BLOCK** hint. Tap/hold behavior is unchanged. Equip notifications sit above the ultimate gauge.
- `EnemyStaggerHUD` on Canvas/Enemy stagger bars discovers spawned EnemyController objects and reads their Health/Stagger. Its inactive Template contains Status and Track/Fill. Bars project above each living enemy within 28 units, hide behind geometry/offscreen/in menus, and show **BROKEN - FINISHER** while broken. No enemy prefab wiring is needed.
- Enemy nameplates now show numeric health above a red health bar, followed by numeric stagger above its amber bar. Template adds Health value and Health track/Fill. The old matching world-space health text is hidden (not deleted) while the nameplate system is active. Use **Gladiators > Install Enemy Health Display** to upgrade older scene templates.
- VictoryPanel and DefeatPanel retain their original children, hidden. New Arena result cards provide Fight Again and Return to Main Menu, with cursor and keyboard selection enabled.
- CanvasScaler uses **Scale With Screen Size / Expand / 1920 x 1080** to retain layout space on narrow and wide views. HUDSafeArea on Combat HUD respects the device safe area.
- To install on another copy of the existing HUD, use **Gladiators > Install Full Arena UI** in Edit mode, then save the scene. The installer preserves the original GameUI bindings and skips scenes that already have ArenaMenuController. Panels, labels, colors, and offsets remain editable in the Canvas hierarchy.
