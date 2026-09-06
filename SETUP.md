# Setup

Manual Unity Editor steps needed to make the current code playable. Updated after
each feature.

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

## UI/feedback — M6, needs Canvas/TextMeshProUGUI creation

`GameUI.cs` has new optional fields — none are required (all null-checked), so
existing HUD keeps working untouched if you skip this. To actually see the new
info:

1. **On the GameUI object**: assign `Player Combat` and `Player Equipment`
   (drag the Player object in) — needed for the new readouts below.
2. **Create new TextMeshProUGUI elements** on your HUD Canvas (duplicate an
   existing HUD text element and reposition, same as how `Wave Text`/
   `Enemies Remaining Text` were presumably set up) for whichever of these you
   want, then assign them on `GameUI`:
   - `Equipped Items Text` — multi-line, shows all 5 slots colored by rarity
     (uses TextMeshPro's `<color>` rich text tag, so make sure Rich Text is
     enabled on that text object, which is the TMP default).
   - `Ability Cooldown Text`, `Dash Cooldown Text` — simple "Ready" / "Xs"
     readouts.
   - `Combo Text` — shows current combo chain step.
3. **`ItemPickup`'s new `Name Label` field** (on the pickup prefab from the M3
   step): add a child `TextMeshPro` (3D, not UGUI — it's a floating
   world-space label, not screen-space) above the pickup mesh, assign it.
   Without it, pickups still work, they just don't show a name/rarity label.
