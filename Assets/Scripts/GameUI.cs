using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    }

    private void ShowGameOver()
    {
        if (gameEnded)
        {
            return;
        }

        gameEnded = true;
        gameOverPanel.SetActive(true);
    }

    public void RestartGame()
    {
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