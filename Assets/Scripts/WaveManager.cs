using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    public System.Action GameWon;
    public System.Action GameLost;
    
    [Header("Waves")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private int totalWaves = 3;
    [SerializeField] private int startingEnemyCount = 2;
    [SerializeField] private int enemiesAddedPerWave = 2;
    [SerializeField] private float timeBetweenSpawns = 0.5f;
    [SerializeField] private float timeBetweenWaves = 2f;

    [Header("State")]
    [SerializeField] private int currentWave = 0;
    [SerializeField] private int enemiesAlive = 0;

    private readonly List<GameObject> spawnedEnemies = new List<GameObject>();
    private bool isSpawningWave;
    private bool gameEnded;

    public int CurrentWave => currentWave;
    public int EnemiesAlive => enemiesAlive;
    public int TotalWaves => totalWaves;

    private void Start()
    {
        StartCoroutine(StartNextWaveAfterDelay(1f));
    }

    private IEnumerator StartNextWaveAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        StartNextWave();
    }

    private void StartNextWave()
    {
        if (gameEnded || isSpawningWave)
        {
            return;
        }

        if (currentWave+1 > totalWaves)
        {
            WinGame();
            return;
        }

        currentWave++;

        int enemyCount = startingEnemyCount + ((currentWave - 1) * enemiesAddedPerWave);

        StartCoroutine(SpawnWave(enemyCount));
    }

    private IEnumerator SpawnWave(int enemyCount)
    {
        isSpawningWave = true;

        for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
        {
            SpawnEnemy();
            yield return new WaitForSeconds(timeBetweenSpawns);
        }

        isSpawningWave = false;
    }

    private void SpawnEnemy()
    {
        if (enemyPrefab == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("WaveManager is missing enemy prefab or spawn points.");
            return;
        }

        Transform selectedSpawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

        GameObject enemyObject = Instantiate(
            enemyPrefab,
            selectedSpawnPoint.position,
            selectedSpawnPoint.rotation
        );

        spawnedEnemies.Add(enemyObject);
        enemiesAlive++;

        Health enemyHealth = enemyObject.GetComponent<Health>();

        if (enemyHealth == null)
        {
            enemyHealth = enemyObject.GetComponentInChildren<Health>();
        }

        if (enemyHealth != null)
        {
            enemyHealth.Died += OnEnemyDied;
        }
    }

    private void OnEnemyDied(Health enemyHealth)
    {
        enemyHealth.Died -= OnEnemyDied;

        enemiesAlive = Mathf.Max(0, enemiesAlive - 1);

        if (enemiesAlive <= 0 && !isSpawningWave)
        {
            StartCoroutine(StartNextWaveAfterDelay(timeBetweenWaves));
        }
    }

    public void LoseGame()
    {
        if (gameEnded)
        {
            return;
        }

        gameEnded = true;
        GameLost?.Invoke();
        Debug.Log("Game Over");
    }

    private void WinGame()
    {
        if (gameEnded)
        {
            return;
        }

        gameEnded = true;
        GameWon?.Invoke();
        Debug.Log("Victory");
    }
}