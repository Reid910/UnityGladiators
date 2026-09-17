using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private Health playerHealth;
    [SerializeField] private PlayerCombat playerCombat;
    [SerializeField] private PlayerEquipment playerEquipment;
    [SerializeField] private Stagger playerStagger;

    [Header("HUD")]
    [SerializeField] private TextMeshProUGUI playerHealthText;
    [Tooltip("Optional. Shows the player's current stagger meter so a break feels readable coming.")]
    [SerializeField] private TextMeshProUGUI staggerText;
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private TextMeshProUGUI enemiesRemainingText;
    [Tooltip("Optional. One line per slot, colored by rarity, e.g. 'Weapon: Rusted Blade'.")]
    [SerializeField] private TextMeshProUGUI equippedItemsText;
    [SerializeField] private TextMeshProUGUI abilityCooldownText;
    [SerializeField] private TextMeshProUGUI dashCooldownText;
    [Tooltip("Optional. Reinforces the combo system by showing the current chain step.")]
    [SerializeField] private TextMeshProUGUI comboText;

    [Header("End Screens")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private GameObject gameOverPanel;

    private bool gameEnded;

    [Header("Visual HUD")]
    [SerializeField] private Image healthFill;
    [SerializeField] private Image staggerFill;
    [SerializeField] private Image abilityIcon;
    [SerializeField] private Image dashIcon;
    [SerializeField] private Image abilityCooldownFill;
    [SerializeField] private Image dashCooldownFill;
    [SerializeField] private Image attackCooldownFill;
    [SerializeField] private TextMeshProUGUI attackCooldownText;
    [SerializeField] private Image heavyCooldownFill;
    [SerializeField] private TextMeshProUGUI heavyCooldownText;
    [SerializeField] private HUDFlash abilityReadyFlash;
    [SerializeField] private HUDFlash dashReadyFlash;
    private bool abilityWasCooling;
    private bool dashWasCooling;
    private AbilityDefinition previousAbility;
    private DashDefinition previousDash;
    private float abilityCooldownPeak;
    private float dashCooldownPeak;

    [Header("Arena UI")]
    [SerializeField] private ArenaMenuController menus;
    [SerializeField] private Image ultimateFill;
    [SerializeField] private TextMeshProUGUI ultimateText;
    [SerializeField] private HUDFlash ultimateReadyFlash;
    private bool ultimateWasReady;

    private void Start()
    {
        victoryPanel.SetActive(false);
        gameOverPanel.SetActive(false);

        if (waveManager != null)
        {
            waveManager.GameWon += ShowVictory;
            waveManager.GameLost += ShowGameOver;
        }

        if (playerHealth != null)
        {
            playerHealth.Died += OnPlayerDied;
        }
    }

    private void Update()
    {
        UpdateHUD();
    }

    private void UpdateHUD()
    {
        if (playerHealthText != null && playerHealth != null)
        {
            playerHealthText.text = "Health: " + playerHealth.CurrentHealth + " / " + playerHealth.MaxHealth;
        }

        if (staggerText != null && playerStagger != null)
        {
            staggerText.text = playerStagger.IsBroken
                ? "Stagger: BROKEN"
                : "Stagger: " + Mathf.RoundToInt(playerStagger.CurrentStagger) + " / " + Mathf.RoundToInt(playerStagger.MaxStagger);
        }

        if (waveText != null && waveManager != null)
        {
            waveText.text = waveManager.EndlessMode
                ? "Wave: " + waveManager.CurrentWave
                : "Wave: " + waveManager.CurrentWave + " / " + waveManager.TotalWaves;
        }

        if (enemiesRemainingText != null && waveManager != null)
        {
            enemiesRemainingText.text = "Enemies: " + waveManager.EnemiesAlive;
        }

        if (abilityCooldownText != null && playerCombat != null)
        {
            float remaining = playerCombat.AbilityCooldownRemaining;
            abilityCooldownText.text = remaining > 0f
                ? "Ability: " + remaining.ToString("0.0") + "s"
                : "Ability: Ready";
        }

        if (dashCooldownText != null && playerCombat != null)
        {
            float remaining = playerCombat.DashCooldownRemaining;
            dashCooldownText.text = remaining > 0f
                ? "Dash: " + remaining.ToString("0.0") + "s"
                : "Dash: Ready";
        }

        if (comboText != null && playerCombat != null)
        {
            comboText.text = "Combo: " + playerCombat.ComboStep;
        }

        UpdateEquippedItemsText();
        UpdateVisualHUD();
    }

    private void UpdateVisualHUD()
    {
        if (healthFill != null && playerHealth != null)
        {
            healthFill.fillAmount = Mathf.Clamp01((float)playerHealth.CurrentHealth / Mathf.Max(1, playerHealth.MaxHealth));
            healthFill.rectTransform.localScale = new Vector3(healthFill.fillAmount, 1f, 1f);
        }
        if (staggerFill != null && playerStagger != null)
        {
            staggerFill.fillAmount = playerStagger.IsBroken ? 1f : Mathf.Clamp01(playerStagger.CurrentStagger / Mathf.Max(1f, playerStagger.MaxStagger));
            staggerFill.rectTransform.localScale = new Vector3(staggerFill.fillAmount, 1f, 1f);
            staggerFill.color = playerStagger.IsBroken ? new Color(1f, .25f, .15f) : new Color(.9f, .65f, .23f);
        }
        if (playerCombat == null) return;
        if (ultimateFill != null)
        {
            float fraction = Mathf.Clamp01(playerCombat.UltimateMeter / Mathf.Max(1f, playerCombat.UltimateMeterMax));
            ultimateFill.rectTransform.localScale = new Vector3(fraction, 1f, 1f);
            ultimateFill.color = playerCombat.IsUltimateReady ? new Color(1f,.78f,.32f) : new Color(.64f,.37f,.16f);
            if (ultimateText != null)
            {
                ultimateText.text = playerCombat.IsUltimateReady ? "[C]  ULTIMATE READY" : "ULTIMATE  /  " + Mathf.FloorToInt(fraction * 100f) + "%";
                ultimateText.color = playerCombat.IsUltimateReady ? new Color(.12f,.08f,.035f) : new Color(.94f,.9f,.8f);
            }
            if (playerCombat.IsUltimateReady && !ultimateWasReady) ultimateReadyFlash?.Trigger();
            ultimateWasReady = playerCombat.IsUltimateReady;
        }
        float attackRemaining = playerCombat.AttackCooldownRemaining;
        if (attackCooldownFill != null)
            attackCooldownFill.fillAmount = playerCombat.AttackCooldownDuration > 0f
                ? Mathf.Clamp01(attackRemaining / playerCombat.AttackCooldownDuration) : 0f;
        if (attackCooldownText != null)
            attackCooldownText.text = attackRemaining > 0f ? attackRemaining.ToString("0.0") + "s" : "ATTACK";
        // Heavy shares the same underlying cooldown as Light (both gate on
        // PlayerCombat.nextAttackTime) — that's intentional, but showing two
        // separate fills/timers ticking in perfect lockstep read like a bug
        // in playtesting. Heavy's icon stays static rather than mirroring
        // Attack's countdown; the shared cooldown system itself is unchanged.
        if (heavyCooldownText != null)
            heavyCooldownText.text = "HEAVY";
        bool hasAbility = playerEquipment != null && playerEquipment.GetEquipped(ItemSlot.Weapon)?.Definition?.AbilityDefinition != null;
        bool hasDash = playerEquipment != null && playerEquipment.GetEquipped(ItemSlot.Boots)?.Definition?.DashDefinition != null;
        UpdateSkill(abilityIcon, abilityCooldownFill, abilityCooldownText, hasAbility, playerCombat.AbilityCooldownRemaining, ref abilityCooldownPeak);
        UpdateSkill(dashIcon, dashCooldownFill, dashCooldownText, hasDash, playerCombat.DashCooldownRemaining, ref dashCooldownPeak);
        var ability = playerEquipment != null ? playerEquipment.GetEquipped(ItemSlot.Weapon)?.Definition?.AbilityDefinition : null;
        var dash = playerEquipment != null ? playerEquipment.GetEquipped(ItemSlot.Boots)?.Definition?.DashDefinition : null;
        bool abilityCooling = hasAbility && playerCombat.AbilityCooldownRemaining > 0f;
        bool dashCooling = hasDash && playerCombat.DashCooldownRemaining > 0f;
        if (abilityWasCooling && !abilityCooling && hasAbility && ability == previousAbility)
            abilityReadyFlash?.Trigger();
        if (dashWasCooling && !dashCooling && hasDash && dash == previousDash)
            dashReadyFlash?.Trigger();
        abilityWasCooling = abilityCooling;
        dashWasCooling = dashCooling;
        previousAbility = ability;
        previousDash = dash;
    }

    private static void UpdateSkill(Image icon, Image overlay, TextMeshProUGUI label, bool equipped, float remaining, ref float peak)
    {
        // Capture the actual cooldown after equipment/stat modifiers, including reductions.
        peak = remaining > 0f ? Mathf.Max(peak, remaining) : 0f;
        if (icon != null) icon.color = equipped ? Color.white : new Color(.3f, .3f, .3f, 1f);
        if (overlay != null) overlay.fillAmount = equipped && peak > 0f ? Mathf.Clamp01(remaining / peak) : 0f;
        if (icon != null && label != null)
            label.text = !equipped ? "UNEQUIPPED" : remaining > 0f ? remaining.ToString("0.0") + "s" : "READY";
    }

    private void UpdateEquippedItemsText()
    {
        if (equippedItemsText == null || playerEquipment == null)
        {
            return;
        }

        StringBuilder builder = new StringBuilder();

        foreach (ItemSlot slot in (ItemSlot[])Enum.GetValues(typeof(ItemSlot)))
        {
            EquippedItem item = playerEquipment.GetEquipped(slot);
            string colorHex = ColorUtility.ToHtmlStringRGB(
                item != null ? RarityColor.Get(item.Rarity) : Color.gray
            );
            string itemLabel = item?.Definition != null ? item.Definition.ItemName : "(empty)";

            builder.AppendLine(slot + ": <color=#" + colorHex + ">" + itemLabel + "</color>");

            if (item == null)
            {
                continue;
            }

            if (item.Definition?.AbilityDefinition != null)
            {
                builder.AppendLine("  Ability: " + item.Definition.AbilityDefinition.AbilityName);
            }

            if (item.Definition?.DashDefinition != null)
            {
                builder.AppendLine("  Dash: " + item.Definition.DashDefinition.DashName);
            }

            if (item.RolledDamage > 0)
            {
                builder.AppendLine("  +" + item.RolledDamage + " Damage");
            }

            foreach (RolledAffix affix in item.Affixes)
            {
                if (affix.definition == null)
                {
                    continue;
                }

                builder.AppendLine("  " + FormatAffix(affix));
            }
        }

        equippedItemsText.text = builder.ToString();
    }

    // Fractional stats (attack speed, crit, cooldown reduction, move speed)
    // read as percentages; flat stats (max health, armor) read as plain points.
    private static string FormatAffix(RolledAffix affix)
    {
        StatType statType = affix.definition.StatType;
        string statName = FormatStatName(statType);

        bool isFractional =
            statType == StatType.AttackSpeed ||
            statType == StatType.CritChance ||
            statType == StatType.AbilityCooldownReduction ||
            statType == StatType.MoveSpeed;

        return isFractional
            ? "+" + Mathf.RoundToInt(affix.rolledValue * 100f) + "% " + statName
            : "+" + Mathf.RoundToInt(affix.rolledValue) + " " + statName;
    }

    private static string FormatStatName(StatType statType)
    {
        switch (statType)
        {
            case StatType.AttackSpeed:
                return "Attack Speed";
            case StatType.CritChance:
                return "Crit Chance";
            case StatType.AbilityCooldownReduction:
                return "Ability Cooldown Reduction";
            case StatType.MoveSpeed:
                return "Move Speed";
            case StatType.MaxHealth:
                return "Max Health";
            case StatType.Armor:
                return "Armor";
            default:
                return statType.ToString();
        }
    }

    private void OnPlayerDied(Health health)
    {
        ShowGameOver();
    }

    private void ShowVictory()
    {
        if (gameEnded)
        {
            return;
        }

        gameEnded = true;
        victoryPanel.SetActive(true);
        if (menus != null) menus.ShowResult(victoryPanel);
    }

    private void ShowGameOver()
    {
        if (gameEnded)
        {
            return;
        }

        gameEnded = true;
        gameOverPanel.SetActive(true);
        if (menus != null) menus.ShowResult(gameOverPanel);
    }

    public void RestartGame()
    {
        if (menus != null) { menus.RestartRun(); return; }
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnDestroy()
    {
        if (waveManager != null)
        {
            waveManager.GameWon -= ShowVictory;
            waveManager.GameLost -= ShowGameOver;
        }

        if (playerHealth != null)
        {
            playerHealth.Died -= OnPlayerDied;
        }
    }
}
