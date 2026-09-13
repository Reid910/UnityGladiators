# Combat Identity + Camera/Controls Redesign

_Living design doc, not a finished spec — captures where the design
conversation is, including still-open questions. Shared here so any
concurrent session has the same context._

_Merged with a parallel Mac-session brainstorm (was `MAC_SESSION_IDEAS.md`,
now folded in below) — that session came at combat identity from "how do we
make finishers feel flashy" rather than from a playtesting pass, so treat the
two halves as complementary angles on the same problem, not a single linear
plan. Where the two disagreed, the resolution is noted inline._

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

The Mac-session brainstorm (folded in below) independently arrived at the
same "disable-focused, not stat-stick" identity from the finisher/flashiness
angle — Stagger → Broken → finisher is exactly the loop both halves of this
doc are trying to make feel good, just approached from opposite ends.

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
- **Enemy tiers**: `EnemyController.Tier` (T1/T2/T3) drives loot rarity and
  now tints the enemy on spawn (`EnemyTierColor` — white/orange/red
  placeholder, added in the Mac session, not real per-tier art) but still
  doesn't auto-scale stats. No tier variant *prefabs* exist yet beyond the
  single baseline Enemy.prefab. M5's sketch: Retiarius (fast/fragile),
  Legionary (slow/tanky/stagger-resistant — the "champion"), Ranged/beast
  (long stoppingDistance).
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
  **Resolved (cross-referenced with the Mac session's stagger placeholder
  below): decay slowing down is the primary lever, not raising
  `damageToStaggerMultiplier`** — that stays at 1 for now. Doing both at
  once risked overcorrecting into stagger being trivial to trigger; pick one
  lever, retune later if decay alone isn't enough.
- **Player HP/regen (500 / 10 per sec) is an acknowledged testing crutch**,
  not an intentional design target — the user doesn't feel survivable
  without it *because* the real survivability tools (readable enemy
  telegraphs, enemy spacing, saner wave density) aren't there yet. Plan is
  to fix those root causes first, then bring HP/regen back down — not to
  leave the crutch in place permanently. Exact new numbers: playtest-tuning
  call, not pinned here.
- **Enemy AI needs spacing/surrounding behavior** instead of every enemy
  independently pathing straight at the player — should feel like enemies
  are trying to flank/surround rather than clump into one stack. (See
  "Enemy AI" below for the Mac session's concrete take on this — NavMesh +
  standoff distance, independently arrived at the same conclusion.)
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
  **Clarified: this is about implementation/polish order, not a ban on
  designing itemization ideas early** — the Mac session's gear/itemization
  brainstorm (including Deflect being Gloves-granted) can coexist with this
  without conflict; it just means building it out is lower priority than
  nailing movement/telegraphs/spacing first.
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

---

## Combat mechanics (from the Mac session brainstorm)

Original framing: how to make combat feel more flashy/finisher-driven,
Sekiro-style, without losing what's specific to this game (wave-survival
arena combat against groups, not 1v1 duels).

### Deflect (timed block) + baseline Block fallback
- Deflect is defensive only — negates incoming HP damage. Deliberately does
  NOT build stagger on the enemy; keeps "stagger" and "defense" as separate
  concerns instead of one mechanic doing both jobs.
- Needs its own reward so it's not strictly worse than just dashing away.
  Resolution: successful deflects feed the Ultimate meter fast. Landing
  normal hits should still feed it at a reasonable (slower) rate too —
  otherwise the best strategy becomes "turtle until Ultimate, then dump it,"
  which fights the always-decaying, aggression-rewarding stagger design.
- Granted by the **Gloves** slot (see Gear as build identity below) as the
  *upgrade* on the same input, not a separate button. Thematically fits —
  parrying is a hand/weapon-catching motion. Different glove items could
  grant different defensive utilities later (a damage-reduction stance, a
  counter-shove), with Deflect as just the first one.
- **Resolved: missing the perfect timing (or not having a Gloves item that
  grants Deflect at all) falls back to a baseline Block** — same input,
  negates HP damage from the hit, but the player takes stagger instead of
  the enemy taking none. This is exactly how Sekiro's actual posture system
  works: holding block chips your own posture per hit, only a perfectly
  -timed deflect avoids that cost entirely. Block does NOT feed the Ultimate
  meter — only a genuine Deflect does.
- This means **baseline defense is always available regardless of gear** —
  a build without a good Gloves item still has real defense (just the lower
  Block tier, not the free/rewarded Deflect tier). Resolves the earlier open
  question about Dash needing to compensate for Gloves-less builds — it
  doesn't need to, Block already covers that gap.
- **Placeholder numbers, both untested — expect to retune after playing
  them:**
  - Deflect timing window: **0.5 seconds**
  - Block's stagger cost: **75%** of the hit's normal damage-based stagger
    value (i.e. `AddStaggerFromDamage`'s usual result for that hit, scaled
    by 0.75) — not a flat percentage of max stagger.

### Perilous attacks
- **Resolved: two concrete moves — Downslam and Side swing.** Both are
  unblockable/undeflectable; the required reaction is simply **being out of
  the affected area when it lands** — no per-type counter-move (no jump
  -over vs dash-through distinction needed, which is simpler now that jump
  is cut). Any repositioning tool works: dash, slide, sprint away, or just
  walking clear in time.
  - **Downslam** — big overhead impact, presumably a ground-radius AoE
    around the enemy (or a targeted point). Get outside the radius before
    it lands.
  - **Side swing** — wide horizontal arc to the enemy's side/front. Get
    clear of the arc's path.
- Should be visually telegraphed differently from a normal swing (distinct
  color/VFX on the windup) so the read is teachable, not a guess.
- **Placeholder: long shared cooldown, ~10 seconds**, covering both Perilous
  moves together (not tracked separately per-type) — keeps them as rare,
  high-impact spikes rather than a constant threat. Rough starting point,
  expect to retune.
- Even 1-2 enemy movesets using this raises the skill ceiling a lot for very
  little added content.
- **Resolved: Perilous attacks don't appear until around wave 3-4**, not
  from wave 1. Gives the player time to learn basic combat and pick up some
  gear before facing the higher-skill-ceiling mechanic. Mirrors how
  `EnemyTier` T2/T3 already gate in by wave number (`WaveManager`'s
  `t2UnlockWave`/`t3UnlockWave`) — same pattern, just for a moveset instead
  of a tier.

### Directional / crowd-aware attacks — SUPERSEDED
- Original idea: attack direction/input picks the move — a forward-input
  heavy as a lunge/thrust for a single tough target, a stationary/side-input
  heavy as a sweep hitting everyone in an arc. The "make it feel like *this*
  game, not a Sekiro clone" reasoning still stands (crowd-survival arena,
  players regularly surrounded), but the *mechanism* below replaced true
  directional input.
- **Replaced by the movement-state-conditional model** (see "No jump; sprint
  instead"): dodge-out and sprint attacks, modeled on how Elden Ring actually
  does it — not true 8-directional input, but state-conditional variants
  (running attack, roll attack). Cheaper to build, reuses state already
  tracked (moving/dashing) instead of new input detection.
- Heavy no longer changes shape based on direction — it's just the small-
  scale AoE stagger tool now (see Heavy and Ultimate below). The crowd-aware
  goal is served by Heavy/Ultimate both being AoE, not by direction-switching
  Heavy's animation.

### Heavy and Ultimate — same purpose, different scale
Both exist primarily to build **stagger**, not health damage, just at very
different scales and commitment levels — this replaces the earlier "Heavy
does disproportionate stagger" idea with a cleaner shared framing:
- **Light combo** → sustained health damage (unchanged from current)
- **Heavy** → small stagger bonus + moderate damage — frequent, low
  commitment, the routine posture-chip tool
- **Ultimate** → huge stagger bonus + good damage — rare, meter-gated, the
  big commitment payoff
- Everything still deals real damage so nothing feels wasted, but the
  *reason* to reach for each one shifts along the stagger axis as commitment
  goes up.
- **Both should be good AoE attacks**, not single-target — this is a
  crowd-survival arena, so a stagger-focused move that only hits one enemy
  out of a surrounding mob has much less practical value than one that
  staggers a cluster. Makes Ultimate specifically feel like a genuine
  "reset the fight" moment when surrounded, matching its rarity/meter-gate.
- **Resolved**: both Heavy and Ultimate landing on an already-staggered/
  broken enemy just kill it outright — no finisher cinematic plays, for
  either move, regardless of how many enemies they actually catch in a given
  swing. Applies to Heavy and Ultimate specifically *because* they're the
  AoE-focused tools, not based on how many targets happen to be hit — a
  simpler rule than trying to special-case "well this Heavy only clipped one
  guy." Their own presentation (impact VFX, hit-stop, AoE stagger burst) is
  the spectacle, not a chain of individual finishers.
- **Only Light attack triggers the single-target finisher cinematic** — see
  Finisher presentation and Ultimate meter below. This is now the full rule:
  Light on a broken enemy = full execute cinematic + feeds Ultimate meter;
  Heavy/Ultimate on a broken enemy = instant kill, no cinematic, no separate
  meter reward beyond their own hit already feeding the meter normally.

### Ultimate ability + meter
- Gated behind a meter — see Heavy/Ultimate above for what it's actually
  *for* (huge stagger + good damage, not just "big damage").
- Meter fills from three sources:
  - Landing hits (baseline rate)
  - Successful deflects (faster rate) — see the guardrail note under Deflect
    above
  - **Light-attack finishers** (landing Light on a broken enemy — the only
    move that triggers the full execute cinematic, see the "Resolved" note
    under Heavy and Ultimate above) — rewards the moment that already looks
    and feels special with more of the resource that creates more special
    moments
- This is the "big flashy moment" slot — where a lot of the visual budget
  (VFX, camera, sound) should go.
- **Placeholder numbers, meter cap 100 (rough, expect to retune)**:
  +5 per hit landed, +20 per successful deflect, +15 per Light-attack
  finisher — roughly ~20 hits, ~5 deflects, or ~7 finishers to fill from
  empty, so it feels earned without being a marathon.

### Finisher (Execute) presentation
- Mechanically already special (instant kill on a broken enemy via
  `Stagger.IsBroken` + `Health.Execute()`), but currently doesn't *look*
  special — reads as a slightly bigger regular hit.
- **Only Light attack triggers this full cinematic treatment.** Heavy and
  Ultimate killing a broken enemy is an instant kill with no cinematic (see
  Heavy and Ultimate above) — keeps the presentation from becoming chaotic
  when an AoE move catches several broken enemies in one swing.
- Ideas to sell the moment:
  - Dedicated finisher animation, not a reused combo/heavy clip
  - Camera punch-in or brief cut on the killing blow
  - Longer hit-stop than a normal hit (already have `HitStop.Trigger` —
    just a bigger duration specifically for finishers)
  - Screen flash / slash VFX, distinct "kill" sound cue
  - Brief `Time.timeScale` dip (slow-mo) right as the blow lands
- General flourish scaling: hit-stop/screen-shake intensity could scale with
  combo step, so hit 3 reads as heavier than hit 1.

### Stagger tuning
- Keep the always-constant-decay model — it's the right fit for an
  aggression-rewarding design (confirmed: it only felt bad during testing
  because of a debug-inflated player max health diluting the
  relative-to-max-health math, not the decay model itself).
- **Placeholder numbers (rough starting points, expect to retune once
  playable)**, all built on the same existing `AddStaggerFromDamage` formula
  rather than separate systems per move:
  - `damageToStaggerMultiplier` baseline: **stays at 1**, unchanged — see
    the reconciled decision under "Decisions so far" above (decay slowing
    down is the primary lever, not raising build-up).
  - Heavy stagger bonus: **×2** on top of the baseline (a Heavy hit gives
    twice the stagger a same-damage Light hit would)
  - Ultimate stagger bonus: **×5** on top of the baseline

## Movement (from the Mac session brainstorm)

### No jump; sprint instead
- Decided against adding jump — no verticality anywhere else in the design,
  and it costs a real amount (new vertical physics state, mid-air attack
  handling) for not much payoff given how much move variety already exists
  (Light/Heavy/Ultimate/Deflect/Dash).
- Instead, lean further into the Elden Ring movement-state-conditional model
  already noted above: a **dodge-out attack** (light attack thrown right
  after a dash finishes) and a **sprint attack** (light attack while
  sprinting = a forward lunge), both cheap to build since they're just
  checking existing movement state, not new mechanics.
- **Resolved (placeholder, untested): 0.25 second window** for both the
  dodge-out and sprint-out attack triggers.
- **Add unlimited sprint, no stamina meter.** Already stacking enough
  resource systems (Stagger, Ultimate meter, Dash/Ability/Deflect
  cooldowns) — stamina would be one more bar for little added depth. Dash's
  cooldown already does the "can't spam mobility forever" job; keep base
  movement itself frictionless.
- **Cross-reference**: Dash should also respect movement-input direction
  (see "Decisions so far" above) — that's an orthogonal fix to *which kind*
  of dash triggers (normal vs. Slide below); both should get it.

### Slide — the grounded answer to Perilous sweeps, and the "mechanical depth" tool
- Goal: something simple to use but with a high skill ceiling when chained,
  like Warframe's bullet jump — without adding verticality (no jump).
- **Slide directly resolves the earlier gap**: Perilous attacks were defined
  as needing "jump over a sweep, dash through a thrust," but jump got cut.
  Sliding under a sweep is the natural grounded equivalent of jumping over
  it — same thematic/mechanical function, zero verticality needed.
- Mechanical depth comes from chaining, not from the slide itself: sprint →
  slide → cancel into dash → cancel into a slide-out attack (same
  movement-state-conditional pattern as the dodge-out/sprint attacks).
  Trivial to use once, real tech to chain well.
- **Resolved: Dash while sprinting triggers a Slide** instead of the normal
  short dash — same input, state-conditional result, no new button (matches
  how Block/Deflect and dodge-out/sprint attacks already work).
- **Resolved: a little residual momentum carries over after the slide
  ends**, rather than snapping straight back to normal move speed. This is
  the actual ingredient that makes chaining feel good instead of just being
  a slide animation — carried speed into the next action (another dash,
  a slide-out attack) is what creates the Warframe-style "chain moves while
  still fast" feel, not the slide alone.

### Mobility & player moveset
- Move variety should come from each move having a distinct *job*
  (Light = damage, Heavy = stagger, Ultimate = payoff), not from stacking
  more moves that all just do "damage, but different numbers."

## Enemy AI (from the Mac session brainstorm)

Independently arrived at the same "enemies need spacing/surrounding
behavior" conclusion as the playtesting-driven decision above — this is the
concrete mechanism proposed for it.

### Distance-based movement
First real idea for enemy AI/moveset (previously just flagged as needed).
- **Three-phase approach instead of one constant chase speed**: Sprint
  while far from the player, **Shuffle** once close but not yet in attack
  range, then Attack once in range.
- The Shuffle phase is a soft telegraph — it reads as "this one's about to
  commit to something" before the actual attack windup even starts, and
  makes the approach feel less robotic than a flat charge-in at one speed.
- Maps onto `EnemyController`'s existing distance-based state (it already
  has `stoppingDistance`) — Shuffle would be a new distance band between
  "still closing" and "in range," not a wholly new system.

### Local avoidance + simple obstacle pathfinding
- Goal: enemies path around simple static obstacles in the arena, and don't
  run directly into each other while approaching.
- **Recommended: Unity's built-in NavMesh system solves both at once.** Bake
  a NavMesh for the arena (handles routing around whatever static
  obstacles/geometry get added automatically), and switch enemies from their
  current direct CharacterController-toward-player movement to a
  `NavMeshAgent`.
- `NavMeshAgent` has built-in avoidance (agents steer around each other at a
  configurable quality level) — this mostly solves "don't run into each
  other" for free once adopted for obstacle pathfinding, without needing
  separate custom boid/flocking code.
- **Real architecture note for later**: this replaces the current movement
  approach in `EnemyController`, it doesn't just layer on top of it —
  `NavMeshAgent` drives its own position, so this is a genuine movement
  -system swap, not a small add-on script.

### Standoff distance from the player
- Enemies shouldn't all close to the exact same minimum `stoppingDistance`
  and hug the player's position — give them a preferred orbit distance
  instead.
- Combined with agent-agent avoidance above, this should naturally produce
  enemies spreading into a **ring around the player** instead of clumping
  into one overlapping blob — a much more readable, cinematic shape for a
  crowd encounter.

### Attack variety: normal + Perilous only, for now
- Deliberately minimal scope: just two enemy attack types, not a full
  moveset per enemy. See Perilous attacks above for what makes an attack
  Perilous and how it should be telegraphed.

### Attack timing: random, no coordination
- **Resolved**: no coordinator system to prevent enemies swinging at once —
  just random per-enemy timing. Simplest possible answer, avoids building
  coordination logic for a problem that mostly just needs enemies to not
  accidentally sync up.
- **Placeholder: ±20% random jitter** on each enemy's attack cooldown timer,
  applied per-instance, so a group reaching attack range together doesn't
  volley in lockstep.

## Progression & itemization (from the Mac session brainstorm)

Per the sequencing clarification under "Decisions so far" above, this is
early design brainstorming, not a claim that itemization should be *built*
before the core movement/telegraph/spacing feel is solid — just captured
here so the ideas aren't lost.

### Gear as build identity (expands the M2 item model)
Three progression axes, kept deliberately separate so each one has a clear
job:

1. **Level** — passive baseline stat growth from character level, fully
   decoupled from gear. Frees gear from being the *only* source of raw
   power. **XP source resolved (for now): kills only**, no per-wave bonus.
   Simplest starting point, revisit if it doesn't feel right in practice.
   **Resolved: level grants MaxHealth + a flat base-damage scalar only** —
   the two universal power stats (survive more, hit harder). Deliberately
   NOT Attack Speed / Crit Chance / Move Speed / Ability Cooldown Reduction
   — those stay purely Stat-Shard-driven so shards keep a clear job (build
   identity/flavor) instead of competing with level-ups for relevance.
2. **Gear effects** — six equipment slots, each a fixed build-defining
   effect. Exactly three are active/button-pressed (kept to three
   deliberately — see Input budget below), three are passive:
   - **Weapon → Ability** (offense) — existing system
   - **Boots → Dash** (mobility) — existing system
   - **Gloves → Deflect / defensive utility** (defense) — new
   - **Head → passive combat trait** (lifesteal / crit / bleed / poison)
   - **Chest → passive sustain/utility** (reduce stagger buildup / heal /
     damage mitigation) — a reactive proc, not a manually-triggered skill
   - **Pants → passive mobility trait that modifies Boots' Dash** — good
     cross-slot synergy: Boots defines *what* the dash does, Pants defines
     *how often/reliably* you get to use it. **Default resolved: a
     cooldown-based automatic dodge** (a free evasive dodge triggers on its
     own on a cooldown, no input needed) — other Pants variants
     (bonus dash charges, reduced dash cooldown) can still exist as alternate
     rolls later, this is just the default. **Resolved: only one Pants
     passive active at a time** — same as every other slot, the player just
     equips whichever Pants item/variant they prefer, no stacking multiple
     at once.
   - Item **level** scales a gear piece's fixed effect numbers (a
     higher-level Lifesteal Helm heals for more).
3. **Stat shards** — a new item category that carries the *old* numeric
   affix system (`AffixDefinition`/`ItemRoller`: MaxHealth, Armor, Attack
   Speed, Crit Chance, Ability Cooldown Reduction, Move Speed) essentially
   unchanged, just decoupled from armor pieces now that those carry fixed
   effects instead. **Rarity's role narrows to just this category** — it
   keeps doing exactly what it does today (roll a magnitude from a range,
   scaled by `RarityRollMultiplier`), just applied to shards instead of
   armor.
   - **Resolved: Stat Shard is its own single equip slot**, swapped in/out
     like any other gear piece (not a socket on other items, not consumed).
     Simplest option — one more `ItemSlot` value, same swap-on-pickup flow
     everything else already uses.

**Input budget note**: deliberately capped at 3 itemized active abilities
(Weapon/Boots/Gloves) + Light/Heavy/Ultimate universal = 6 total
player-activated actions. Head/Chest/Pants are passive specifically to avoid
ballooning the control scheme in a game built around tight, readable,
Sekiro-style reflexes — more competing buttons mid-fight dilutes how sharp
any single one (like Deflect timing) gets to feel.

**Architecture note**: this isn't a new paradigm for the codebase — it's the
existing `AbilityDefinition`/`DashDefinition` pattern (one fixed effect +
tunable numeric fields owned by the definition) extended to Head/Chest/Pants/
Gloves, and the existing `ItemRoller` rarity-roll logic moved onto Stat
Shards essentially unchanged.

## Game loop / session structure (from the Mac session brainstorm)
Resolved while sanity-checking the full run loop start-to-finish (spawn →
fight → loot → level → next wave → win/lose):
- **Minimal/no starting gear is intentional**, not a gap to fix. Early-run
  weakness (little to no Dash/Ability/Deflect until the first few drops) is
  deliberate tension, Risk of Rain 2-style — you start weak and find power
  as you go, not a guaranteed baseline kit.
- **Level resets every run — no meta-progression**, players start fresh each
  time, same Risk of Rain 2-style loop. This isn't a new decision so much as
  a restatement of what `TODO.md` already lists under "Explicitly out of
  scope (for now)" (meta-progression between runs) — the new Level system
  should stay consistent with that, not quietly reintroduce persistence.
- **Gloves and Stat Shard join the existing loot table** alongside the
  original five slots — no separate drop mechanism, just extending what
  already exists to cover two more `ItemSlot` values.

## Implementation approach

_To be filled in once the open questions above are resolved — this doc will
be updated as the design conversation continues._
