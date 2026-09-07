using System.Collections;
using UnityEngine;

// Brief global freeze-frame on impactful hits. Call HitStop.Trigger(duration)
// from anywhere (see Health.TakeDamage()/Execute()) — no scene wiring
// needed, it lazily creates its own persistent runner object on first use.
public static class HitStop
{
    private static HitStopRunner runner;

    public static void Trigger(float duration)
    {
        if (duration <= 0f)
        {
            return;
        }

        if (runner == null)
        {
            GameObject runnerObject = new GameObject("HitStopRunner");
            Object.DontDestroyOnLoad(runnerObject);
            runner = runnerObject.AddComponent<HitStopRunner>();
        }

        runner.Run(duration);
    }
}

internal class HitStopRunner : MonoBehaviour
{
    private Coroutine activeRoutine;
    private float originalTimeScale = 1f;

    public void Run(float duration)
    {
        if (activeRoutine != null)
        {
            // A hit landed mid-freeze — restart with the new duration rather
            // than stacking freezes on top of each other.
            StopCoroutine(activeRoutine);
        }
        else
        {
            originalTimeScale = Time.timeScale;
        }

        activeRoutine = StartCoroutine(FreezeRoutine(duration));
    }

    private IEnumerator FreezeRoutine(float duration)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = originalTimeScale;
        activeRoutine = null;
    }
}
