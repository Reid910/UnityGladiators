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
3. **Animator Controller** — `PlayerCombat.cs` now fires these trigger
   parameters that don't exist in the Player's Animator Controller yet:
   - `AttackCombo1`, `AttackCombo2`, `AttackCombo3` (light combo, one per hit)
   - `AttackHeavy` (heavy attack)
   - `AbilityCast` (ability placeholder)
   - `Dash` (dash placeholder)
   For each: add a `Trigger` parameter with that exact name in the Animator
   Controller, then add a state + transition from wherever attacks currently
   trigger (look at how the existing single `Attack` trigger/state was wired,
   since that pattern still applies — you're just adding more of them). Until
   these exist, the moves will function (damage/cooldowns work, dash actually
   moves you) but won't visibly animate.
4. **No new Inspector references needed** — `PlayerCombat` still uses the same
   `attackPoint`/`enemyLayer`/`animator`/`health` fields as before. It also now
   auto-fills a `CharacterController` reference via `GetComponent` on Awake if
   left unset, needed for the dash to move the player — the Player prefab
   should already have one (`PlayerController` uses it too), so this should
   need no action, but double check the field isn't pointing at the wrong
   object if you had one manually assigned before.
5. **Combo/heavy numbers are placeholder starting values** (see the
   `lightComboHits` array, `heavyDamage`, `abilityCooldown`, `dashDistance`,
   `dashCooldown` fields in the Inspector) — these reset to script defaults
   since the old `attackDamage`/`attackCooldown` fields were replaced. Tune to
   taste once you can playtest.

## Stagger / hitstun / finishers — M1, core logic done

1. **Add `Stagger` and `Hitstun` components to both the Player prefab and the
   Enemy prefab.** These are plain `MonoBehaviour`s with no required Inspector
   wiring (all their fields have sane defaults) — just add the components via
   **Add Component → Stagger** and **Add Component → Hitstun** on each prefab.
   Without them, combat still works but nothing staggers/stuns — the
   `IsIncapacitated` checks in `PlayerController`/`PlayerCombat`/`EnemyController`
   just no-op if the components are missing (`GetComponent` returns null), so
   this won't break anything if skipped, it just won't do anything either.
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

## Corpse looting, pickup, cleanup — M3, mostly done, needs prefab/layer work

This is the one with the most Editor setup so far — several new pieces need
actual scene/prefab objects, not just component references.

1. **Create two new physics layers**: `Corpse` and `Pickup` (Project Settings →
   Tags and Layers, or Edit → Project Settings → Tags and Layers). `Corpse` is
   for dead-enemy loot hitboxes; `Pickup` is for the dropped-item trigger
   colliders (keeps them from colliding with the `enemyLayer`/`corpseLayer`
   OverlapSphere checks or with each other).
2. **On the Enemy prefab**: add a child GameObject (e.g. "CorpseHitbox") with
   its own `Collider` (a simple capsule/box is fine), set to the `Corpse`
   layer, **disabled by default**. Drag it into `Health`'s new `Corpse Hitbox`
   field. Also add a `LootableCorpse` component to the Enemy prefab (root is
   fine) and set: `Tier` (T1 for now, until M5 adds real variants),
   `Drop Chance`, `Possible Items` (drag in `ItemDefinition` assets from the
   M2 step above), and `Item Pickup Prefab` (see next step).
3. **Create an ItemPickup prefab**: any visible mesh (a placeholder cube/sphere
   is fine per the earlier placeholder-assets discussion) with a trigger
   `Collider` set to the `Pickup` layer, plus an `ItemPickup` component. This
   is the prefab you drag into `LootableCorpse.Item Pickup Prefab`.
4. **On the Player prefab**: add a `PlayerEquipment` component (no Inspector
   wiring needed — it's just a runtime dictionary). Also set
   `PlayerCombat`'s new `Corpse Layer` field to the `Corpse` layer you just
   created (separate from the existing `Enemy Layer` field).
5. **Visual rarity distinction is still unbuilt** — `ItemPickup`/`EquippedItem`
   expose `Rarity` in code, but nothing colors/highlights the pickup by it yet.
   A simple version: swap the pickup's material color based on
   `item.Rarity` in `ItemPickup.Initialize()` — not done since there's no
   pickup prefab/material to attach it to until you do step 3.
6. **`WaveManager`** needs no new references — cleanup is automatic once the
   above prefabs exist, since `LootableCorpse` finds it via
   `FindFirstObjectByType<WaveManager>()`.

## Stats integration — M4, done, one required component add

1. **Add a `PlayerStats` component to the Player prefab.** Like
   `PlayerEquipment`, it needs no Inspector wiring (`equipment`/`health` both
   auto-fill via `GetComponent` on Awake) — just **Add Component →
   PlayerStats**. Without it, `PlayerCombat`/`PlayerController`'s stat lookups
   all no-op to 0, so combat/movement still work, just with zero gear bonus.
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
