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

        EnsureRunner();
        runner.RunFreeze(duration);
    }

    // Bigger beat for a finisher/Execute kill (see Health.Execute()): a hard
    // freeze-frame, then a slow-motion ramp back up to normal speed instead
    // of snapping back instantly — reads as a deliberate cinematic beat,
    // not just a longer version of a normal hit's stop. Pure Time.timeScale
    // sequencing, no camera/VFX work — camera zoom/framing is explicitly
    // deferred (see docs/combat-redesign-plan.md's Open Questions).
    public static void TriggerFinisher(float freezeDuration, float rampDuration, float rampStartTimeScale)
    {
        EnsureRunner();
        runner.RunFinisher(freezeDuration, rampDuration, rampStartTimeScale);
    }

    private static void EnsureRunner()
    {
        if (runner == null)
        {
            GameObject runnerObject = new GameObject("HitStopRunner");
            Object.DontDestroyOnLoad(runnerObject);
            runner = runnerObject.AddComponent<HitStopRunner>();
        }
    }
}

internal class HitStopRunner : MonoBehaviour
{
    private Coroutine activeRoutine;
    private float originalTimeScale = 1f;

    public void RunFreeze(float duration)
    {
        BeginNewSequence();
        activeRoutine = StartCoroutine(FreezeRoutine(duration));
    }

    public void RunFinisher(float freezeDuration, float rampDuration, float rampStartTimeScale)
    {
        BeginNewSequence();
        activeRoutine = StartCoroutine(FinisherRoutine(freezeDuration, rampDuration, rampStartTimeScale));
    }

    // A new hit/finisher landing mid-sequence restarts rather than stacking
    // on top of whatever's already running — only the very first call in a
    // chain gets to remember the "real" time scale to restore to.
    private void BeginNewSequence()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }
        else
        {
            originalTimeScale = Time.timeScale;
        }
    }

    private IEnumerator FreezeRoutine(float duration)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = originalTimeScale;
        activeRoutine = null;
    }

    private IEnumerator FinisherRoutine(float freezeDuration, float rampDuration, float rampStartTimeScale)
    {
        Time.timeScale = 0f;

        if (freezeDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(freezeDuration);
        }

        float elapsed = 0f;

        while (elapsed < rampDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(rampStartTimeScale, originalTimeScale, elapsed / Mathf.Max(0.01f, rampDuration));
            yield return null;
        }

        Time.timeScale = originalTimeScale;
        activeRoutine = null;
    }
}
