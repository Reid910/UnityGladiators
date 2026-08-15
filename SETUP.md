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
