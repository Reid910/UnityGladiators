using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    public System.Action GameWon;
    public System.Action GameLost;
    
    [Header("Waves")]
    [Tooltip("All enemy prefabs must have an EnemyController so their Tier can gate which waves they're allowed to spawn in.")]
    [SerializeField] private GameObject[] enemyPrefabs;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private int totalWaves = 3;
    [Tooltip("If true, waves keep scaling past totalWaves forever instead of ending the run — fits a farming loop better than a hard win-at-wave-3 cap.")]
    [SerializeField] private bool endlessMode = true;
    [SerializeField] private int startingEnemyCount = 2;
    [SerializeField] private int enemiesAddedPerWave = 2;
    [SerializeField] private float timeBetweenSpawns = 0.5f;
    [SerializeField] private float timeBetweenWaves = 2f;

    [Header("Tier Unlocks")]
    [Tooltip("T2 enemy prefabs won't be picked before this wave number.")]
    [SerializeField] private int t2UnlockWave = 2;
    [Tooltip("T3 enemy prefabs won't be picked before this wave number.")]
    [SerializeField] private int t3UnlockWave = 3;

    [Header("State")]
    [SerializeField] private int currentWave = 0;
    [SerializeField] private int enemiesAlive = 0;

    private readonly List<GameObject> spawnedEnemies = new List<GameObject>();
    private readonly List<GameObject> activePickups = new List<GameObject>();
    private bool isSpawningWave;
    private bool gameEnded;

    public int CurrentWave => currentWave;
    public int EnemiesAlive => enemiesAlive;
    public int TotalWaves => totalWaves;
    public bool EndlessMode => endlessMode;

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

        if (!endlessMode && currentWave+1 > totalWaves)
        {
            WinGame();
            return;
        }

        // Corpses from the previous wave are cleared right as the next wave
        // starts — the inter-wave gap is the last chance to loot them.
        ClearCorpses();

        currentWave++;

        int enemyCount = startingEnemyCount + ((currentWave - 1) * enemiesAddedPerWave);

        StartCoroutine(SpawnWave(enemyCount));
    }

    // Called by LootableCorpse when it spawns a dropped item, so WaveManager
    // can clean it up on the same wave-boundary cadence as corpses.
    public void RegisterPickup(GameObject pickupObject)
    {
        activePickups.Add(pickupObject);
    }

    private void ClearCorpses()
    {
        foreach (GameObject enemyObject in spawnedEnemies)
        {
            if (enemyObject != null)
            {
                Destroy(enemyObject);
            }
        }

        spawnedEnemies.Clear();
    }

    // Items get one full wave of grace before they're cleared, unlike
    // corpses which clear at the very next wave start.
    private void ClearPickups()
    {
        foreach (GameObject pickupObject in activePickups)
        {
            if (pickupObject != null)
            {
                Destroy(pickupObject);
            }
        }

        activePickups.Clear();
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

    // T1 is always eligible; T2/T3 only join the pool once their unlock wave
    // is reached, so early waves stay easy and later waves mix in tougher
    // (better-looting) enemies instead of just adding more of the same one.
    private GameObject ChooseEnemyPrefab()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            return null;
        }

        List<GameObject> eligiblePrefabs = new List<GameObject>();

        foreach (GameObject prefab in enemyPrefabs)
        {
            if (prefab == null)
            {
                continue;
            }

            EnemyController enemyController = prefab.GetComponent<EnemyController>();
            EnemyTier prefabTier = enemyController != null ? enemyController.Tier : EnemyTier.T1;

            bool isEligible = prefabTier switch
            {
                EnemyTier.T1 => true,
                EnemyTier.T2 => currentWave >= t2UnlockWave,
                EnemyTier.T3 => currentWave >= t3UnlockWave,
                _ => true,
            };

            if (isEligible)
            {
                eligiblePrefabs.Add(prefab);
            }
        }

        if (eligiblePrefabs.Count == 0)
        {
            // Fall back to whatever exists rather than spawning nothing.
            return enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        }

        return eligiblePrefabs[Random.Range(0, eligiblePrefabs.Count)];
    }

    private void SpawnEnemy()
    {
        GameObject enemyPrefab = ChooseEnemyPrefab();

        if (enemyPrefab == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("WaveManager is missing enemy prefabs or spawn points.");
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
            ClearPickups();
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