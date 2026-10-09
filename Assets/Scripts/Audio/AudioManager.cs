using System.Collections;
using UnityEngine;

// Music + sound effects. Lives across scenes (DontDestroyOnLoad).
// Put the AudioManager prefab in every scene: the first one survives, later copies only make sure the
// music is running (so starting a scene directly in the editor also has music) and then remove themselves.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Music")]
    [SerializeField] private AudioClip music;
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.5f;
    [SerializeField] private float deathFadeTime = 1.5f;

    [Header("Sound Effects")]
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioClip landSound;
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private AudioClip clickSound;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.8f;
    [SerializeField] private int sfxVoices = 4;

    public AudioClip JumpSound => jumpSound;
    public AudioClip LandSound => landSound;
    public float SfxVolume => sfxVolume;

    private AudioSource musicSource;
    private AudioSource[] sfxSources;
    private int nextSfx;
    private Coroutine fade;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Instance.EnsureMusicPlaying();
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.clip = music;
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;

        sfxSources = new AudioSource[Mathf.Max(1, sfxVoices)];
        for (int i = 0; i < sfxSources.Length; i++)
        {
            sfxSources[i] = gameObject.AddComponent<AudioSource>();
            sfxSources[i].playOnAwake = false;
            sfxSources[i].spatialBlend = 0f;
        }

        PlayMusicFromStart();
    }

    // ---------- Music ----------

    public void PlayMusicFromStart()
    {
        StopFade();
        musicSource.volume = musicVolume;
        musicSource.time = 0f;
        musicSource.Play();
    }

    // Keeps the music going as it is; restarts it from the beginning only if it was stopped / fading out
    public void EnsureMusicPlaying()
    {
        if (fade != null || !musicSource.isPlaying)
        {
            PlayMusicFromStart();
        }
    }

    public void FadeOutMusic()
    {
        StopFade();
        fade = StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        float start = musicSource.volume;
        float t = 0f;
        while (t < 1f)
        {
            // Unscaled so it works while the game is paused; step capped so a frame hitch can't skip the fade
            t += Mathf.Min(Time.unscaledDeltaTime, 0.05f) / Mathf.Max(deathFadeTime, 0.01f);
            musicSource.volume = Mathf.Lerp(start, 0f, t);
            yield return null;
        }
        musicSource.Stop();
        musicSource.volume = musicVolume;
        fade = null;
    }

    private void StopFade()
    {
        if (fade != null)
        {
            StopCoroutine(fade);
            fade = null;
        }
    }

    // ---------- Sound effects (static so callers don't need a reference) ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    public static void PlayJump(float pitch = 1f)
    {
        if (Instance != null) Instance.PlaySfx(Instance.jumpSound, 1f, pitch);
    }

    public static void PlayLand()
    {
        if (Instance != null) Instance.PlaySfx(Instance.landSound, 0.8f, Random.Range(0.94f, 1.06f));
    }

    public static void PlayClick()
    {
        if (Instance != null) Instance.PlaySfx(Instance.clickSound, 0.7f, Random.Range(0.97f, 1.03f));
    }

    // Death sound + music fades out and stops
    public static void OnPlayerDied()
    {
        if (Instance == null) return;
        Instance.PlaySfx(Instance.deathSound, 1f, 1f);
        Instance.FadeOutMusic();
    }

    private void PlaySfx(AudioClip clip, float volume, float pitch)
    {
        if (clip == null) return;
        AudioSource source = sfxSources[nextSfx];
        nextSfx = (nextSfx + 1) % sfxSources.Length;
        source.pitch = pitch;
        source.PlayOneShot(clip, volume * sfxVolume);
    }
}
