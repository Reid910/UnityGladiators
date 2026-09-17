using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Owns presentation state and input suspension; combat components remain untouched.
[DefaultExecutionOrder(10000)]
public class ArenaMenuController : MonoBehaviour
{
    public GameObject mainMenu;
    public GameObject pauseMenu;
    public GameObject combatHUD;
    public Button playButton;
    public Button resumeButton;
    public ThirdPersonCamera orbitCamera;
    public PlayerCombat playerCombat;
    public PlayerController playerController;
    public GameUI gameUI;

    public bool IsMenuOpen => mainMenu.activeSelf || pauseMenu.activeSelf;
    public bool IsSuspended { get; private set; }
    private static bool startNextRun;
    private bool cameraWasEnabled;
    private bool audioWasPaused;
    private bool ending;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() { startNextRun = false; }

    private void Start()
    {
        mainMenu.SetActive(false);
        pauseMenu.SetActive(false);
        if (startNextRun)
        {
            startNextRun = false;
            combatHUD.SetActive(true);
        }
        else
        {
            Suspend();
            combatHUD.SetActive(false);
            mainMenu.SetActive(true);
            Select(playButton);
        }
    }

    private void Update()
    {
        if (!ending && !mainMenu.activeSelf &&
            ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
             (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame)))
        {
            if (pauseMenu.activeSelf) Resume();
            else Pause();
        }
        if (IsSuspended) Time.timeScale = 0f;
    }

    private void LateUpdate()
    {
        // Hit-stop uses realtime coroutines. Keep their temporary time-scale writes
        // from releasing a menu pause at the next simulation frame.
        if (!IsSuspended) return;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Play()
    {
        if (ending) return;
        mainMenu.SetActive(false);
        pauseMenu.SetActive(false);
        combatHUD.SetActive(true);
        Restore();
    }

    public void Pause()
    {
        if (ending || IsMenuOpen) return;
        Suspend();
        pauseMenu.SetActive(true);
        Select(resumeButton);
    }

    public void Resume()
    {
        if (!pauseMenu.activeSelf || ending) return;
        pauseMenu.SetActive(false);
        Restore();
    }

    public void ShowResult(GameObject panel)
    {
        ending = true;
        Suspend();
        mainMenu.SetActive(false);
        pauseMenu.SetActive(false);
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        Select(panel.GetComponentInChildren<Button>());
    }

    private void Suspend()
    {
        if (IsSuspended) return;
        IsSuspended = true;
        audioWasPaused = AudioListener.pause;
        // Each gameplay component owns its own generated InputSystem_Actions
        // wrapper instance, so suspension has to go through each one's own
        // Disable() rather than the global InputSystem action list — that
        // would desync the wrapper's own enable/disable bookkeeping from
        // the action's actual state and trip its leak-detection finalizer
        // ("Player.Disable() has not been called") the next time it's GC'd.
        if (playerCombat != null) playerCombat.SetGameplayInputEnabled(false);
        if (playerController != null) playerController.SetGameplayInputEnabled(false);
        cameraWasEnabled = orbitCamera != null && orbitCamera.enabled;
        if (orbitCamera != null) orbitCamera.enabled = false;
        AudioListener.pause = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Restore()
    {
        if (!IsSuspended) return;
        IsSuspended = false;
        Time.timeScale = 1f;
        AudioListener.pause = audioWasPaused;
        if (playerCombat != null) playerCombat.SetGameplayInputEnabled(true);
        if (playerController != null) playerController.SetGameplayInputEnabled(true);
        if (orbitCamera != null) orbitCamera.enabled = cameraWasEnabled;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    public void ReturnToMenu() { Reload(false); }
    public void RestartRun() { Reload(true); }

    private void Reload(bool playImmediately)
    {
        startNextRun = playImmediately;
        Restore();
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().path);
    }

    public void Quit()
    {
        Restore();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static void Select(Button button)
    {
        if (button != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(button.gameObject);
    }

    private void OnDestroy()
    {
        // Do not re-enable disposed input maps during scene destruction.
        if (!IsSuspended) return;
        Time.timeScale = 1f;
        AudioListener.pause = audioWasPaused;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
