using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Единственное место, откуда загружаются сцены: плавное затемнение, сброс паузы/звука,
/// звук перехода, который не обрывается при выгрузке сцены.
/// Создаётся автоматически при первом обращении.
/// </summary>
public class SceneFlow : MonoBehaviour
{
    public const string MainMenuScene = "MainMenu";
    public const string StageScene = "Stage";
    public const string MazeScene = "MazeScene";

    private static SceneFlow _instance;

    public static bool IsLoading { get; private set; }

    public static SceneFlow Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("SceneFlow");
                _instance = go.AddComponent<SceneFlow>();
            }
            return _instance;
        }
    }

    [SerializeField] private float fadeDuration = 0.35f;

    private CanvasGroup _fade;
    private AudioSource _audio;
    private Image _flash;

    public static void Load(string sceneName)
    {
        Instance.StartLoad(sceneName, null);
    }

    public static void Load(string sceneName, AudioClip transitionSound)
    {
        Instance.StartLoad(sceneName, transitionSound);
    }

    public static void LoadMainMenu()
    {
        Load(MainMenuScene);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
            IsLoading = false;
        }
    }

    private void BuildOverlay()
    {
        Canvas canvas = UIFactory.CreateCanvas("FadeCanvas", 5000);
        canvas.transform.SetParent(transform, false);

        Image black = UIFactory.CreateImage(canvas.transform, "Fade", Color.black);
        UIFactory.Stretch((RectTransform)black.transform);

        _fade = canvas.gameObject.AddComponent<CanvasGroup>();
        _fade.alpha = 0f;
        _fade.blocksRaycasts = false;
        _fade.interactable = false;

        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;
        _audio.ignoreListenerPause = true;

        // Отдельный слой для вспышек (скримеры): поверх игры, но не блокирует нажатия.
        Canvas flashCanvas = UIFactory.CreateCanvas("FlashCanvas", 4900);
        flashCanvas.transform.SetParent(transform, false);
        GraphicRaycaster flashRaycaster = flashCanvas.GetComponent<GraphicRaycaster>();
        if (flashRaycaster != null) flashRaycaster.enabled = false;
        _flash = UIFactory.CreateImage(flashCanvas.transform, "Flash", new Color(1f, 1f, 1f, 0f));
        UIFactory.Stretch(_flash.rectTransform);
        _flash.raycastTarget = false;
    }

    /// <summary>Короткая вспышка на весь экран (peakAlpha → 0 за duration секунд).</summary>
    public static void Flash(Color color, float peakAlpha, float duration)
    {
        SceneFlow flow = Instance;
        flow.StartCoroutine(flow.FlashRoutine(color, peakAlpha, duration));
    }

    private IEnumerator FlashRoutine(Color color, float peakAlpha, float duration)
    {
        float length = Mathf.Max(0.02f, duration);
        for (float t = 0f; t < length; t += Time.unscaledDeltaTime)
        {
            float k = 1f - t / length;
            color.a = peakAlpha * k * k;
            _flash.color = color;
            yield return null;
        }

        color.a = 0f;
        _flash.color = color;
    }

    private void StartLoad(string sceneName, AudioClip sound)
    {
        if (IsLoading) return;

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("[SceneFlow] Сцена '" + sceneName + "' не добавлена в Build Settings (меню Tools/Stage/Fix Build Settings).");
            return;
        }

        StartCoroutine(LoadRoutine(sceneName, sound));
    }

    private IEnumerator LoadRoutine(string sceneName, AudioClip sound)
    {
        IsLoading = true;

        if (sound != null) _audio.PlayOneShot(sound, GameSettings.SfxVolume);

        yield return Fade(1f);

        // Сбрасываем глобальное состояние, чтобы новая сцена не унаследовала паузу.
        Time.timeScale = 1f;
        AudioListener.pause = false;

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (op != null && !op.isDone) yield return null;

        yield return null; // даём новой сцене один кадр на инициализацию
        yield return Fade(0f);

        IsLoading = false;
    }

    private IEnumerator Fade(float target)
    {
        _fade.blocksRaycasts = true;
        float start = _fade.alpha;
        float duration = Mathf.Max(0.01f, fadeDuration);
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _fade.alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }

        _fade.alpha = target;
        _fade.blocksRaycasts = target > 0.5f;
    }
}
