using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Единственное меню паузы (раньше было два: PauseMenu и SimplePauseMenu).
/// Живёт между сценами вместе с PauseMenuCanvas.
///
/// Почему кнопки раньше «не нажимались» (и понадобились UIClickableFixer/UIEventFixer):
/// EventSystem лежал только в сцене Stage и исчезал при переходе в MazeScene.
/// Теперь EventSystem гарантируется при каждой паузе, а пауза в MainMenu отключена.
/// </summary>
public class SimplePauseMenu : MonoBehaviour
{
    public static SimplePauseMenu Instance { get; private set; }
    public static bool IsPaused { get; private set; }

    [Header("Ссылки")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button backFromSettingsButton;

    [Header("Настройки")]
    [SerializeField] private bool pauseTime = true;
    [SerializeField] private bool pauseAudio = true;

    private Canvas _canvas;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(transform.root.gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);
        SceneManager.activeSceneChanged += OnActiveSceneChanged;

        // Меню должно быть поверх всего и кликабельным — это больше не чинится «фиксерами» на лету.
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null)
        {
            _canvas.sortingOrder = Mathf.Max(_canvas.sortingOrder, 100);
            if (_canvas.GetComponent<GraphicRaycaster>() == null) _canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        ApplySceneState(SceneManager.GetActiveScene());
    }

    private void Start()
    {
        // Панель настроек в префабе пуста (ни одного слайдера, нет кнопки «Назад») — достраиваем её.
        if (settingsPanel != null && settingsPanel.GetComponentInChildren<Slider>(true) == null)
        {
            Button back = SettingsPanelBuilder.Build(settingsPanel.transform, null);
            if (backFromSettingsButton == null) backFromSettingsButton = back;
        }

        // Невидимые полноэкранные картинки (например BrightnessOverlay с альфой 0) принимали клики и закрывали
        // собой кнопки. Приём касаний нужен только самим элементам управления (кнопкам, ползункам и т.п.).
        DisableDecorativeRaycasts();
        UIFactory.EnsureEventSystem();

        if (continueButton != null) continueButton.onClick.AddListener(Resume);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(LoadMainMenu);
        if (backFromSettingsButton != null) backFromSettingsButton.onClick.AddListener(CloseSettings);

        HidePanels();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        Instance = null;
        if (IsPaused) ForceReset();
    }

    private void Update()
    {
        // Esc на ПК; на Android кнопка «Назад» тоже приходит как Esc.
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) Toggle();
    }

    /// <summary>Пауза / продолжить / назад из настроек. Вызывается Esc и кнопкой паузы на экране телефона.</summary>
    public void Toggle()
    {
        if (IsMainMenuScene() || SceneFlow.IsLoading) return;

        if (!IsPaused) Pause();
        else if (settingsPanel != null && settingsPanel.activeSelf) CloseSettings();
        else Resume();
    }

    // ------------------------------------------------------------------ Пауза

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;

        if (pauseTime) Time.timeScale = 0f;
        if (pauseAudio) AudioListener.pause = true;

        UIFactory.EnsureEventSystem();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        ShowPausePanel();
    }

    public void Resume()
    {
        if (!IsPaused) return;

        ForceReset();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>Сбрасывает состояние паузы, не трогая курсор (для выхода в меню курсор должен быть свободен).</summary>
    private void ForceReset()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        HidePanels();
    }

    public void OpenSettings()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
        if (backFromSettingsButton != null) backFromSettingsButton.Select();
    }

    public void CloseSettings()
    {
        if (SettingsManager.Instance != null) SettingsManager.Instance.SaveSettings();
        ShowPausePanel();
    }

    public void LoadMainMenu()
    {
        ForceReset();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneFlow.LoadMainMenu();
    }

    // ------------------------------------------------------------------ Помощники

    private void ShowPausePanel()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(true);
        if (continueButton != null) continueButton.Select();
    }

    private void HidePanels()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    private static bool IsMainMenuScene()
    {
        return SceneManager.GetActiveScene().name == SceneFlow.MainMenuScene;
    }

    private void OnActiveSceneChanged(Scene previous, Scene next)
    {
        // Попали в главное меню (в том числе из паузы) — полностью сбрасываем состояние паузы.
        bool isMenu = next.name == SceneFlow.MainMenuScene;
        if (isMenu) ForceReset();

        ApplySceneState(next);
        if (!isMenu) UIFactory.EnsureEventSystem();   // в MazeScene своего EventSystem нет
    }

    // Холст паузы переходит между сценами; в главном меню он выключен целиком, чтобы ничего не перекрывать.
    private void ApplySceneState(Scene scene)
    {
        if (_canvas != null) _canvas.enabled = scene.name != SceneFlow.MainMenuScene;
    }

    private void DisableDecorativeRaycasts()
    {
        if (_canvas == null) return;

        Graphic[] graphics = _canvas.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic g = graphics[i];
            if (!g.raycastTarget) continue;
            if (g.GetComponentInParent<Selectable>(true) != null) continue;   // кнопки, ползунки, списки и их части
            g.raycastTarget = false;
        }
    }
}
