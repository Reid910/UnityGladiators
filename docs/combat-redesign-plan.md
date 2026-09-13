# Combat Identity + Camera/Controls Redesign

_Living design doc, not a finished spec — captures where the design
conversation is, including still-open questions. Shared here so any
concurrent session has the same context._

## Context

Playtesting the merged combat-feel build surfaced a bigger question than "fix
the camera": the game currently *plays* like a stat-stick RPG (500 HP, 10
regen/sec on the player — you can tank chip damage and out-heal it) when the
intended fantasy is a fast, disable-focused ninja:

- Fast and mobile, not a slow tank-and-spank
- Wins fights by *disabling* enemies (Stagger → Broken → finisher), not by
  out-statting them
- Crits are big, satisfying damage/stagger spikes, not just a DPS multiplier
- The real challenge is taking down tanky "champion" enemies (M5's sketched
  Legionary-tier archetype), not grinding through trash with bigger numbers
- Loot still exists and still matters, even though the "ninja loots gladiator
  gear" framing is a little silly (acknowledged, not something to solve here)

This plan is the camera/controls work *and* the combat-identity tuning that
motivated revisiting it — they're being designed together because dash feel,
camera framing during fights, and how "disable-focused" combat reads are all
the same underlying problem: does the game feel like the fantasy above?

## Current mechanics (verified in code)

- **Health/Stagger formula** (`Assets/Scripts/Stagger.cs`,
  `Assets/Scripts/Health.cs`): `AddStaggerFromDamage` fills stagger by
  `(damage / targetMaxHealth) * maxStagger * damageToStaggerMultiplier` — so
  a hit dealing X% of a target's max HP fills X% of their stagger bar
  (multiplier currently 1 on both prefabs). Stagger decays at a flat
  `decayPerSecond` regardless of time-since-hit (always-active, not
  out-of-combat-gated — same model as Health regen).
- **Current tuning**: Player = 500 HP / 10 regen/sec / 200 maxStagger / 5
  decay/sec. Enemy (T1 baseline) = 100 HP / 0 regen / 100 maxStagger / 5
  decay/sec. `damageToStaggerMultiplier` is untouched (1) on both — TODO.md
  already flags this as the intended knob for a future tankier tier
  (lower multiplier = stagger-resistant) rather than just raising maxStagger.
- **Dash** (`PlayerCombat.TryDash()`): `transform.forward * distance` —
  always fires in current facing direction, ignores movement input entirely.
  Has real i-frames (`IsInvulnerable`, gated by `DashDefinition.InvulnerabilityDuration`).
- **Abilities**: omnidirectional sphere-cast at the player's position, not
  directional — camera/facing changes don't affect these.
- **Camera** (`ThirdPersonCamera.cs`): already migrated to the new Input
  System (`Look` action), mouse-orbit with yaw/pitch clamp, no collision/
  occlusion handling (can clip through arena geometry), `mouseSensitivity`
  is an untuned guess (0.12).
- **Enemy tiers**: `EnemyController.Tier` (T1/T2/T3) is a passive label only
  — drives loot rarity, does not auto-scale stats. No tier variant prefabs
  exist yet beyond the single baseline Enemy.prefab. M5's sketch: Retiarius
  (fast/fragile), Legionary (slow/tanky/stagger-resistant — the "champion"),
  Ranged/beast (long stoppingDistance).
- **Itemization**: 6 `StatType`s (Attack Speed, Crit Chance, Ability
  Cooldown Reduction, Move Speed, Max Health, Armor), all flat/percentage
  roll ranges on gear (`Assets/Definitions/Affixes/*.asset`). This is the
  "stat stick" system in question.
- **Enemy attacks are already windup-gated, not instant** —
  `EnemyController.AttackTarget()` → `PerformAttack()` coroutine: fires the
  `Attack` animator trigger, waits `attackWindup` (0.4s default/configured),
  then only resolves damage if the target is still in `attackRange` during
  the active window (`attackActiveDuration`, 0.15s). Dodging away during
  windup, or breaking the enemy mid-windup, avoids the hit. This already
  shipped (`TODO.md` confirms, all sub-items checked). So the player's "can't
  dodge in and hit them without getting hit" feeling is real, but the cause
  isn't missing windup logic. Most likely causes instead:
  1. **No visual telegraph.** Many attack animations are still generic
     filler clips, not built to visibly show a wind-up pose before the
     swing — so the 0.4s mechanical delay exists but isn't *readable*.
  2. **Hyper armor makes trading intentional, not accidental.** While the
     player is mid-attack (`PlayerCombat.IsAttacking`), they can't be
     stunned out of their combo, but they still take damage/stagger
     normally — so attacking into an enemy's attack window is a designed
     trade, not a bug, which conflicts with a "dodge and punish" fantasy
     unless there's a real safe window to approach in first.
  3. **Density** — each enemy runs its own independent 0.4s-windup attack
     with zero awareness of other enemies (`EnemyController` has no
     separation/surrounding/avoidance logic at all, confirmed absent). With
     multiple enemies, there's rarely a moment when *no* attack is active
     nearby, even if each individual one is technically fair.
- **Wave pacing**: `WaveManager` spawns enemies one at a time
  (`timeBetweenSpawns` = 0.5s stagger, not all at once), picking a random
  spawn point per enemy from 4 points spaced ~6–7 units apart in a rough
  arc. `enemyCount = startingEnemyCount(2) + (wave-1) * enemiesAddedPerWave(2)`
  — so wave 5 already spawns 10 enemies. This is the "+2 each wave is too
  much" the user flagged, combined with the density problem above (more
  simultaneous independent attackers compounds the telegraph/hyper-armor
  issues, not just "harder").

## Decisions so far

- **Dash** should respect movement input direction instead of always firing
  in the current facing direction (`transform.forward`).
- **Stagger decay** should be significantly slower than the current flat
  5/sec — exact number is a playtest-tuning call, not pinned down here.
- **Player HP/regen (500 / 10 per sec) is an acknowledged testing crutch**,
  not an intentional design target — the user doesn't feel survivable
  without it *because* the real survivability tools (readable enemy
  telegraphs, enemy spacing, saner wave density) aren't there yet. Plan is
  to fix those root causes first, then bring HP/regen back down — not to
  leave the crutch in place permanently. Exact new numbers: playtest-tuning
  call, not pinned here.
- **Enemy AI needs spacing/surrounding behavior** instead of every enemy
  independently pathing straight at the player — should feel like enemies
  are trying to flank/surround rather than clump into one stack.
- **Wave density needs to come down**: lower `enemiesAddedPerWave` from the
  current 2, and increase spacing between spawn points (current ~6–7 units
  apart apparently isn't enough separation for how this is meant to feel).
- **Enemy attack telegraphs need to actually be visible** — not just
  mechanically fair (the windup already exists) but readable, so the
  player can learn to dodge/punish rather than trade blindly.
- **Sequencing/itemization philosophy**: figure out how the game should
  play *first* (movement, dodging, telegraphs, spacing — all baseline,
  gear-independent feel), and design itemization *after*, as the thing that
  fills the gap needed to beat tougher/"champion" enemies — not as a
  constant background power-creep loop. Itemization specifics (which
  stats, whether hyper armor becomes a gear grant, etc.) are deferred until
  the core feel above is settled, not designed in parallel with it.
- **Hyper armor confirmed to apply to ALL basic attacks**, not just heavy —
  `PlayerCombat.BeginAttack()` (which sets the `IsAttacking`/hyper-armor
  window) is called from both `TryLightAttack()` and `TryHeavyAttack()`;
  only the ability is exempt by design. Every swing currently grants
  hitstun immunity for its full windup+active+recovery window (~0.35–0.9s),
  meaning there's no way to safely poke right now — every attack is a
  designed trade. Proposed direction: cut or drastically shrink baseline
  hyper armor so basic combat is genuinely risk/reward (must find a real
  opening), and revisit it later as a possible gear-granted effect (fits
  the "items help you beat bigger guys" philosophy above) rather than a
  free baseline. **Awaiting user confirmation on this specific cut.**

## Open questions (still being worked through)

- Is designing the "beefy champion" enemy archetype (M5's Legionary) in
  scope for this plan, or is that a separate future pass once the core feel
  is retuned? (Leaning toward: separate/later, given the itemization
  sequencing decision above — champions are downstream of core feel too.)
- Camera collision/occlusion, combat-assist framing (nudge toward nearest
  enemy / zoom out with multiple targets), zoom — still unconfirmed which
  of these are wanted.
- Confirm: cut/shrink baseline hyper armor (see above), or keep it and see
  if fixing telegraphs/density already solves the feel first?

## Implementation approach

_To be filled in once the open questions above are resolved — this doc will
be updated as the design conversation continues._
