using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Главное меню. Сцена MainMenu пустая (камера и свет), поэтому весь UI строится кодом — и этот контроллер
/// сам появляется в сцене (см. Bootstrap), так что ничего расставлять вручную не нужно.
/// Фон полностью процедурный (без ассетов): градиент, качающиеся лучи прожекторов, «эквалайзер» внизу и виньетка —
/// в тему сцены с музыкой и танцами. Кнопки: Играть / Настройки / Выход.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string gameScene = SceneFlow.StageScene;

    private const int BeamCount = 5;
    private const int BarCount = 48;

    private readonly List<Texture2D> _textures = new List<Texture2D>();

    private CanvasGroup _group;
    private RectTransform[] _beams;
    private float[] _beamBase;
    private float[] _beamSpeed;
    private float[] _beamPhase;
    private RectTransform[] _bars;
    private float[] _barRight;
    private Text _title;
    private GameObject _settingsOverlay;
    private Button _playButton;
    private float _age;
    private float _nextAnimation;

    // ------------------------------------------------------------------ Автозапуск в сцене MainMenu

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureForScene(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureForScene(scene);
    }

    private static void EnsureForScene(Scene scene)
    {
        if (scene.name != SceneFlow.MainMenuScene) return;
        if (FindFirstObjectByType<MainMenuController>() != null) return;
        new GameObject("MainMenuController").AddComponent<MainMenuController>();
    }

    // ------------------------------------------------------------------ Жизненный цикл

    private void Start()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        AudioListener.volume = GameSettings.MasterVolume;   // настройки громкости действуют уже в меню
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        UIFactory.EnsureEventSystem();
        BuildUI();
        if (_playButton != null) _playButton.Select();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _textures.Count; i++)
            if (_textures[i] != null) Destroy(_textures[i]);
    }

    private void Update()
    {
        _age += Time.unscaledDeltaTime;
        float t = Time.unscaledTime;

        if (_group != null) _group.alpha = Mathf.Clamp01(_age / 0.9f);   // плавное появление меню

        // Фон анимируется 30 раз в секунду: каждое изменение UI-элемента пересобирает холст, а глаз разницы не заметит.
        bool animate = Time.unscaledTime >= _nextAnimation;
        if (animate) _nextAnimation = Time.unscaledTime + 1f / 30f;

        if (animate && _beams != null)
        {
            for (int i = 0; i < _beams.Length; i++)
            {
                float angle = _beamBase[i] + Mathf.Sin(t * _beamSpeed[i] + _beamPhase[i]) * 16f;
                _beams[i].localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        if (animate && _bars != null)
        {
            for (int i = 0; i < _bars.Length; i++)
            {
                float noise = Mathf.PerlinNoise(i * 0.23f, t * 0.9f);
                float wave = 0.5f + 0.5f * Mathf.Sin(t * 2.3f + i * 0.55f);
                float h = 0.03f + 0.24f * (0.35f + 0.65f * noise) * (0.4f + 0.6f * wave);
                _bars[i].anchorMax = new Vector2(_barRight[i], h);
            }
        }

        if (animate && _title != null)
        {
            float pulse = 1f + 0.025f * Mathf.Sin(t * 2f);
            _title.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
        }

        Keyboard kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame && _settingsOverlay != null && _settingsOverlay.activeSelf)
            CloseSettings();
    }

    // ------------------------------------------------------------------ Построение UI

    private void BuildUI()
    {
        Canvas canvas = UIFactory.CreateCanvas("MainMenuCanvas", 200);   // выше холстов, переживших смену сцены (пауза — 100)
        canvas.transform.SetParent(transform, false);
        _group = canvas.gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        Transform root = canvas.transform;

        // 1. Фон: вертикальный градиент
        RawImage bg = CreateRaw(root, "Background", MakeGradientTexture(), Color.white);
        UIFactory.Stretch(bg.rectTransform);

        // 2. Лучи прожекторов
        Texture2D beamTexture = MakeBeamTexture();
        Color[] beamColors =
        {
            new Color(1f, 0.25f, 0.8f, 0.30f),
            new Color(0.25f, 0.85f, 1f, 0.26f),
            new Color(1f, 0.85f, 0.35f, 0.22f),
            new Color(0.6f, 0.4f, 1f, 0.28f),
            new Color(1f, 0.4f, 0.55f, 0.24f)
        };
        _beams = new RectTransform[BeamCount];
        _beamBase = new float[BeamCount];
        _beamSpeed = new float[BeamCount];
        _beamPhase = new float[BeamCount];
        for (int i = 0; i < BeamCount; i++)
        {
            RawImage beam = CreateRaw(root, "Beam" + i, beamTexture, beamColors[i]);
            RectTransform rt = beam.rectTransform;
            float ax = 0.12f + i * 0.19f;
            rt.anchorMin = new Vector2(ax, 1.05f);
            rt.anchorMax = new Vector2(ax, 1.05f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(260f, 1500f);
            _beams[i] = rt;
            _beamBase[i] = (i - 2) * 9f;
            _beamSpeed[i] = 0.35f + i * 0.09f;
            _beamPhase[i] = i * 1.7f;
        }

        // 3. «Эквалайзер» внизу экрана
        _bars = new RectTransform[BarCount];
        _barRight = new float[BarCount];
        for (int i = 0; i < BarCount; i++)
        {
            float k = i / (float)(BarCount - 1);
            Color c = Color.Lerp(new Color(1f, 0.2f, 0.75f, 0.55f), new Color(0.2f, 0.85f, 1f, 0.55f), k);
            Image bar = UIFactory.CreateImage(root, "Bar" + i, c);
            bar.raycastTarget = false;
            RectTransform rt = bar.rectTransform;
            float x0 = i / (float)BarCount + 0.15f / BarCount;
            float x1 = (i + 1) / (float)BarCount - 0.15f / BarCount;
            rt.anchorMin = new Vector2(x0, 0f);
            rt.anchorMax = new Vector2(x1, 0.05f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _bars[i] = rt;
            _barRight[i] = x1;
        }

        // 4. Виньетка
        RawImage vignette = CreateRaw(root, "Vignette", MakeVignetteTexture(), Color.white);
        UIFactory.Stretch(vignette.rectTransform);

        // Всё содержимое меню — в безопасной области экрана (вырез камеры, скруглённые углы телефона).
        RectTransform content = UIFactory.CreateRect("SafeContent", root);
        UIFactory.Stretch(content);
        content.gameObject.AddComponent<SafeAreaFitter>();

        // 5. Заголовок
        _title = UIFactory.CreateText(content, "STAGE", 160, TextAnchor.MiddleCenter, Color.white);
        _title.fontStyle = FontStyle.Bold;
        RectTransform titleRt = _title.rectTransform;
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta = new Vector2(1400f, 240f);
        titleRt.anchoredPosition = new Vector2(0f, 270f);
        Outline glow = _title.gameObject.AddComponent<Outline>();
        glow.effectColor = new Color(0.75f, 0.3f, 1f, 0.55f);
        glow.effectDistance = new Vector2(5f, -5f);

        Text subtitle = UIFactory.CreateText(content, "D A N C E   P A R T Y", 40, TextAnchor.MiddleCenter, new Color(0.82f, 0.78f, 1f, 0.9f));
        RectTransform subRt = subtitle.rectTransform;
        subRt.anchorMin = new Vector2(0.5f, 0.5f);
        subRt.anchorMax = new Vector2(0.5f, 0.5f);
        subRt.sizeDelta = new Vector2(1000f, 60f);
        subRt.anchoredPosition = new Vector2(0f, 150f);

        // 6. Кнопки
        Vector2 size = new Vector2(580f, 96f);
        _playButton = AddButton(content, GameSettings.Tr("Играть", "Play"), size, new Vector2(0f, 10f), Play);
        AddButton(content, GameSettings.Tr("Настройки", "Settings"), size, new Vector2(0f, -110f), OpenSettings);

        // На iOS приложение закрывают жестом (правила Apple), на WebGL закрывать нечего.
        bool canQuit = Application.platform != RuntimePlatform.IPhonePlayer && Application.platform != RuntimePlatform.WebGLPlayer;
        if (canQuit) AddButton(content, GameSettings.Tr("Выход", "Quit"), size, new Vector2(0f, -230f), Quit);

        // 7. Версия в углу
        Text version = UIFactory.CreateText(content, "v" + Application.version, 24, TextAnchor.LowerLeft, new Color(1f, 1f, 1f, 0.35f));
        RectTransform vRt = version.rectTransform;
        vRt.anchorMin = new Vector2(0f, 0f);
        vRt.anchorMax = new Vector2(0f, 0f);
        vRt.pivot = new Vector2(0f, 0f);
        vRt.sizeDelta = new Vector2(300f, 40f);
        vRt.anchoredPosition = new Vector2(24f, 16f);

        // 8. Панель настроек (поверх всего, скрыта)
        BuildSettings(root);
    }

    private void BuildSettings(Transform root)
    {
        Image dim = UIFactory.CreateImage(root, "SettingsOverlay", new Color(0f, 0f, 0f, 0.65f));
        UIFactory.Stretch(dim.rectTransform);
        dim.raycastTarget = true;   // клики не должны проходить сквозь затемнение на кнопки меню
        _settingsOverlay = dim.gameObject;

        Image panel = UIFactory.CreateRoundedImage(dim.transform, "SettingsPanel", new Color(0.09f, 0.07f, 0.16f, 0.97f));
        RectTransform prt = panel.rectTransform;
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(900f, 720f);
        prt.anchoredPosition = Vector2.zero;

        Text title = UIFactory.CreateText(panel.transform, GameSettings.Tr("Настройки", "Settings"), 56, TextAnchor.UpperCenter, Color.white);
        RectTransform trt = title.rectTransform;
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.sizeDelta = new Vector2(0f, 90f);
        trt.anchoredPosition = new Vector2(0f, -28f);

        SettingsPanelBuilder.Build(panel.transform, CloseSettings);
        _settingsOverlay.SetActive(false);
    }

    private Button AddButton(Transform root, string label, Vector2 size, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        Button b = UIFactory.CreateButton(root, label, size, onClick);
        RectTransform rt = (RectTransform)b.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        return b;
    }

    private static RawImage CreateRaw(Transform parent, string name, Texture texture, Color color)
    {
        RectTransform rt = UIFactory.CreateRect(name, parent);
        RawImage raw = rt.gameObject.AddComponent<RawImage>();
        raw.texture = texture;
        raw.color = color;
        raw.raycastTarget = false;
        return raw;
    }

    // ------------------------------------------------------------------ Кнопки

    private void Play()
    {
        SceneFlow.Load(gameScene);
    }

    private void OpenSettings()
    {
        if (_settingsOverlay != null) _settingsOverlay.SetActive(true);
    }

    private void CloseSettings()
    {
        PlayerPrefs.Save();
        if (_settingsOverlay != null) _settingsOverlay.SetActive(false);
        if (_playButton != null) _playButton.Select();
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------------ Процедурные текстуры

    private Texture2D NewTexture(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        _textures.Add(tex);
        return tex;
    }

    private Texture2D MakeGradientTexture()
    {
        const int h = 256;
        Texture2D tex = NewTexture(2, h);
        Color top = new Color(0.07f, 0.03f, 0.16f);
        Color mid = new Color(0.22f, 0.06f, 0.34f);
        Color bottom = new Color(0.02f, 0.015f, 0.05f);

        for (int y = 0; y < h; y++)
        {
            float v = y / (h - 1f);   // 0 — низ, 1 — верх
            Color c = v > 0.5f ? Color.Lerp(mid, top, (v - 0.5f) * 2f) : Color.Lerp(bottom, mid, v * 2f);
            tex.SetPixel(0, y, c);
            tex.SetPixel(1, y, c);
        }
        tex.Apply();
        return tex;
    }

    // Мягкий луч: ярче у источника (верх) и у центральной оси.
    private Texture2D MakeBeamTexture()
    {
        const int w = 64;
        const int h = 256;
        Texture2D tex = NewTexture(w, h);
        Color32[] pixels = new Color32[w * h];

        for (int y = 0; y < h; y++)
        {
            float along = (y + 0.5f) / h;   // 0 — низ, 1 — верх
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f;
                float a = Mathf.Pow(1f - Mathf.Abs(u), 2f) * Mathf.Pow(along, 1.2f);
                pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    // Затемнение к краям экрана.
    private Texture2D MakeVignetteTexture()
    {
        const int size = 128;
        Texture2D tex = NewTexture(size, size);
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / 1.2f;
                float s = Mathf.Clamp01((r - 0.35f) / 0.65f);
                s = s * s * (3f - 2f * s);
                pixels[y * size + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(s * 0.85f * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }
}
