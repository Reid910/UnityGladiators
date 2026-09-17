# Arena UI pass validation

Verified in Unity 6000.4.3f1, SampleScene, on 2026-09-16.

- Unity compilation and final Console check: no errors.
- Initial main-menu state freezes simulation, suspends gameplay actions, keeps UI input available, and unlocks the cursor.
- Play/Resume button callbacks restore gameplay input, camera control, audio, and normal time scale.
- Escape pause/resume exercised with queued keyboard states processed through the Input System's dynamic update. Initial attempts through its editor update did not produce gameplay button-edge events; the dynamic-update check passed.
- Pause remains at timeScale 0 across hit-stop completion. Main-menu and result buttons remain selectable.
- Result -> Main Menu reloads into a fresh menu; Fight Again reloads directly into gameplay. Quit exits Editor Play mode and restores time/audio.
- Ultimate gauge checked at empty, 50%, ready, and spent values using temporary Play-mode fixtures. Ready label and frame flash respond correctly.
- Enemy stagger plate checked against a live enemy's Stagger component, including visible BROKEN / FINISHER text and fill. Projection pivot corrected after visual inspection.
- Main menu, pause panel, and combat HUD visually inspected in Game view. The CanvasScaler uses Expand to keep at least the reference layout dimensions; separate device/aspect-ratio builds were not run.
- Scene object audit: no pre-existing serialized scene objects removed. Only UI components and UI transforms changed among the original objects. Original GameUI bindings remain assigned.
- PlayerCombat, EnemyController, Health, Stagger, WaveManager, and gameplay balance files have no changes in this pass.

Screenshots: [Main menu](../Captures/arena-main-menu.png), [Pause](../Captures/arena-pause-menu.png), [Combat HUD](../Captures/arena-combat-hud.png).

The combat screenshot uses temporary staged enemy/ultimate state for visibility. These fixtures were discarded on leaving Play mode. No external tooling is required to play or rebuild the UI; the installer is **Gladiators > Install Full Arena UI**.
