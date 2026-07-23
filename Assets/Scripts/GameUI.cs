using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private Health playerHealth;

    [Header("HUD")]
    [SerializeField] private TextMeshProUGUI playerHealthText;
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private TextMeshProUGUI enemiesRemainingText;

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

        if (waveText != null && waveManager != null)
        {
            waveText.text = "Wave: " + waveManager.CurrentWave + " / " + waveManager.TotalWaves;
        }

        if (enemiesRemainingText != null && waveManager != null)
        {
            enemiesRemainingText.text = "Enemies: " + waveManager.EnemiesAlive;
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