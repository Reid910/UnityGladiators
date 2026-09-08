# UI filler assets needed

Derived from what `GameUI.cs`, `ItemPickup.cs`, and the item/ability/dash
definitions currently only render as plain text. Everything below is a gap
between "text label" and "readable at a glance" — rarity color already
exists in code (`RarityColor.cs`), so icons don't need per-rarity variants,
just a colored tint/border applied at runtime.

## Item slot icons (5)
One generic icon per equip slot, silhouette-style so a rarity-colored tint
or border reads clearly on top of it.
- **Head** — helmet silhouette
- **Chest** — chestplate/torso armor silhouette
- **Pants** — leg armor silhouette
- **Boots** — boot silhouette
- **Weapon** — generic blade/sword silhouette (doesn't need to match any
  specific weapon model since `ItemDefinition` doesn't distinguish weapon
  shapes yet)

## Ability / dash icons (2)
- **Ability icon** — generic "special attack" burst/impact icon, shown next
  to the ability cooldown readout. Only one weapon ability exists right
  now (`AbilityDefinition`), so one generic icon covers it.
- **Dash icon** — generic forward-motion/streak icon for the dash cooldown
  readout (`DashDefinition`).

## HUD frame/panel art
- **Health bar frame** — currently text-only ("Health: X / Y"); a filled
  bar + frame reads faster mid-combat.
- **Stagger/posture bar frame** — same idea for the stagger meter
  (`staggerText`), ideally visually distinct from the health bar (different
  color or shape) so a "BROKEN" state pops.
- **Ability/dash cooldown radial or bar frame** — small circular or bar
  fill-frame to sit behind each icon above, so cooldown reads as a
  fill/wipe instead of a countdown number.
- **HUD backing panel** — one semi-transparent panel/background texture to
  group the health/stagger/wave/combo readouts so they don't float
  directly on the game world.

## Combo / wave readout icons
- **Combo step icon(s)** — small marker (e.g. a pip or number badge) shown
  per combo hit landed (`PlayerCombat.ComboStep`), optional beyond the
  existing text but helps combo feedback read faster than text alone.
- **Wave/enemies-remaining icon** — small skull or enemy-count icon next to
  the wave/enemies-remaining text.

## World-space pickup label art
- **Pickup label background** — small backing plate/panel behind
  `ItemPickup.nameLabel` so item name + rarity color is legible against any
  background, and so the `[E] <item>` swap prompt stands out from the
  plain name state.

## End screens (2 panels)
- **Victory panel background/art** — currently just an empty `GameObject`
  toggled active (`victoryPanel`).
- **Game Over panel background/art** — same, for `gameOverPanel`.
- A **restart button** graphic if the end screens should offer a click
  target instead of only a key/menu action (not currently wired either way
  in `GameUI.cs`).

## Rarity indicator (optional, small)
- **Rarity border/frame overlay** (3 states: Common, Rare, Super Rare) if
  you want a border around item icons in addition to (or instead of) tinting
  the icon itself via `RarityColor.Get()`.
