using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// HUD (distance + session best), pause menu and death screen.
// Call GameUI.PlayerDied() when the player dies.
public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }

    // Best distance of this session. Survives "Play Again" (scene reload), reset when going to the main menu.
    public static int SessionBest { get; private set; }

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Behaviour[] disableWhilePaused;

    [Header("HUD")]
    [SerializeField] private TMP_Text distanceText;
    [SerializeField] private TMP_Text bestText;

    [Header("Pause Menu")]
    [SerializeField] private CanvasGroup pauseMenu;
    [SerializeField] private RectTransform pausePanel;
    [Tooltip("Scene loaded by the Main Menu button (must be in Build Settings)")]
    [SerializeField] private string mainMenuScene = "";

    [Header("Death Screen")]
    [SerializeField] private CanvasGroup deathScreen;
    [SerializeField] private RectTransform deathPanel;
    [SerializeField] private TMP_Text deathDistanceText;
    [SerializeField] private TMP_Text deathBestText;
    [SerializeField] private GameObject newHighScore;

    [Header("Feel")]
    [SerializeField] private float panelAnimTime = 0.25f;
    [SerializeField] private float distancePop = 1.12f;

    private int distance;
    private int runBest;
    private int bestBeforeRun;
    private bool isPaused;
    private bool isDead;
    private RectTransform distanceRect;
    private float popTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SessionBest = 0;
        Instance = null;
    }

    // Call this when the player dies
    public static void PlayerDied()
    {
        if (Instance != null)
        {
            Instance.ShowDeathScreen();
        }
    }

    // Call this from your main menu if the best should be cleared there too
    public static void ResetSessionBest()
    {
        SessionBest = 0;
    }

    private void Awake()
    {
        Instance = this;
        distanceRect = distanceText.rectTransform;
        HideInstant(pauseMenu);
        HideInstant(deathScreen);
        Time.timeScale = 1f;
    }

    private void Start()
    {
        bestBeforeRun = SessionBest;
        distance = -1;
        UpdateHud(force: true);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (!isDead && Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused) Resume();
            else Pause();
        }

        if (!isDead)
        {
            UpdateHud(force: false);
        }

        // Little pop on the distance counter whenever it goes up
        if (popTimer > 0f)
        {
            popTimer = Mathf.Max(0f, popTimer - Time.unscaledDeltaTime);
            float t = popTimer / 0.2f;
            distanceRect.localScale = Vector3.one * Mathf.Lerp(1f, distancePop, t * t);
        }
    }

    private void UpdateHud(bool force)
    {
        int current = Mathf.Max(0, Mathf.FloorToInt(player.position.z));
        if (current == distance && !force)
        {
            return;
        }

        if (current > distance && !force)
        {
            popTimer = 0.2f;
        }

        distance = current;
        runBest = Mathf.Max(runBest, distance);
        SessionBest = Mathf.Max(SessionBest, runBest);
        distanceText.text = Meters(distance);
        bestText.text = Meters(SessionBest);
    }

    // ---------- Pause ----------

    public void Pause()
    {
        if (isPaused || isDead) return;
        isPaused = true;
        SetGameplayRunning(false);
        StartCoroutine(ShowPanel(pauseMenu, pausePanel));
    }

    public void Resume()
    {
        if (!isPaused) return;
        isPaused = false;
        HideInstant(pauseMenu);
        SetGameplayRunning(true);
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        ResetSessionBest();
        if (string.IsNullOrEmpty(mainMenuScene))
        {
            Debug.LogWarning("GameUI: set 'Main Menu Scene' on the GameUI component to load your main menu.");
            return;
        }
        SceneManager.LoadScene(mainMenuScene);
    }

    // ---------- Death ----------

    public void ShowDeathScreen()
    {
        if (isDead) return;
        isDead = true;
        if (isPaused)
        {
            isPaused = false;
            HideInstant(pauseMenu);
        }

        UpdateHud(force: true);
        bool beatBest = runBest > bestBeforeRun;
        deathDistanceText.text = Meters(runBest);
        deathBestText.text = Meters(SessionBest);
        newHighScore.SetActive(beatBest);

        SetGameplayRunning(false);
        StartCoroutine(ShowPanel(deathScreen, deathPanel));
        StartCoroutine(CountUp(deathDistanceText, runBest));
    }

    public void PlayAgain()
    {
        Time.timeScale = 1f;
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
    }

    // ---------- Helpers ----------

    private static string Meters(int value)
    {
        return $"{value}<size=60%> m</size>";
    }

    private void SetGameplayRunning(bool running)
    {
        Time.timeScale = running ? 1f : 0f;
        foreach (Behaviour b in disableWhilePaused)
        {
            if (b != null) b.enabled = running;
        }
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private static void HideInstant(CanvasGroup group)
    {
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        group.gameObject.SetActive(false);
    }

    private IEnumerator ShowPanel(CanvasGroup group, RectTransform panel)
    {
        group.gameObject.SetActive(true);
        group.interactable = true;
        group.blocksRaycasts = true;

        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / panelAnimTime);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            group.alpha = eased;
            // Slight overshoot so the panel "lands"
            float s = t < 1f ? Mathf.LerpUnclamped(0.85f, 1f, 1f + 2.2f * Mathf.Pow(t - 1f, 3f) + 1.2f * Mathf.Pow(t - 1f, 2f)) : 1f;
            panel.localScale = Vector3.one * s;
            yield return null;
        }
    }

    private IEnumerator CountUp(TMP_Text text, int target)
    {
        float duration = Mathf.Clamp(target / 60f, 0.3f, 1f);
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / duration);
            float eased = 1f - Mathf.Pow(1f - t, 2f);
            text.text = Meters(Mathf.RoundToInt(target * eased));
            yield return null;
        }
    }
}
