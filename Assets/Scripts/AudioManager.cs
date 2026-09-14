using UnityEngine;

// Global one-shot SFX + background music player — call AudioManager.PlaySfx(clip)
// or PlayMusic(clip) from anywhere, no scene wiring needed (same
// lazily-creates-its-own-persistent-runner pattern as HitStop.Trigger()).
// Individual scripts hold their own AudioClip fields (see Health's Hit/Death
// clips, PlayerCombat's per-move clips, etc.) the same way they already hold
// their own hitStopDuration — intentionally plumbing for now. A null clip is
// a no-op, so nothing breaks before real sound files are assigned.
public static class AudioManager
{
    private static AudioManagerRunner runner;

    public static void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (clip == null)
        {
            return;
        }

        GetRunner().PlaySfx(clip, volume);
    }

    // Re-calling with the same clip that's already playing is a no-op rather
    // than restarting it, so this can be called from Start() or a wave/scene
    // transition without the track stuttering.
    public static void PlayMusic(AudioClip clip, float volume = 0.5f)
    {
        if (clip == null)
        {
            return;
        }

        GetRunner().PlayMusic(clip, volume);
    }

    private static AudioManagerRunner GetRunner()
    {
        if (runner == null)
        {
            GameObject runnerObject = new GameObject("AudioManagerRunner");
            Object.DontDestroyOnLoad(runnerObject);
            runner = runnerObject.AddComponent<AudioManagerRunner>();
        }

        return runner;
    }
}

internal class AudioManagerRunner : MonoBehaviour
{
    private AudioSource sfxSource;
    private AudioSource musicSource;

    private void Awake()
    {
        sfxSource = gameObject.AddComponent<AudioSource>();

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
    }

    public void PlaySfx(AudioClip clip, float volume)
    {
        sfxSource.PlayOneShot(clip, volume);
    }

    public void PlayMusic(AudioClip clip, float volume)
    {
        if (musicSource.clip == clip && musicSource.isPlaying)
        {
            return;
        }

        musicSource.clip = clip;
        musicSource.volume = volume;
        musicSource.Play();
    }
}
