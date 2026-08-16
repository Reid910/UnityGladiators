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
