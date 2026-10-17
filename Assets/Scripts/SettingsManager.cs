using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Настройки игры (экран, звук, язык, управление). Имена сериализуемых полей сохранены,
/// поэтому ссылки в префабе PauseMenuCanvas не потеряются.
///
/// Исправлено:
///  • на первом запуске игра ставила САМОЕ МАЛЕНЬКОЕ разрешение (индекс 0 по умолчанию) — теперь текущее;
///  • слайдеры «Музыка» и «Эффекты» ничего не делали — теперь работают через GameSettings;
///  • «?.» на UnityEngine.Object обходил проверку «уничтоженного» объекта — убрано;
///  • «Сбросить» делал PlayerPrefs.DeleteAll() и стирал вообще всё — теперь только ключи настроек;
///  • язык: доступны только реализованные (Русский/English) и об изменении сообщается подписчикам.
/// </summary>
public class SettingsManager : MonoBehaviour
{
    [Header("Настройки экрана")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private TMP_Text resolutionLabel;
    [SerializeField] private TMP_Text fullscreenLabel;
    [SerializeField] private TMP_Text qualityLabel;

    [Header("Настройки звука")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TMP_Text masterVolumeValueText;
    [SerializeField] private TMP_Text musicVolumeValueText;
    [SerializeField] private TMP_Text sfxVolumeValueText;

    [Header("Настройки языка")]
    [SerializeField] private TMP_Dropdown languageDropdown;
    [SerializeField] private TMP_Text languageLabel;

    [Header("Настройки графики")]
    [SerializeField] private Slider brightnessSlider;
    [SerializeField] private Image brightnessOverlay;
    [SerializeField] private Toggle colorblindToggle;
    [SerializeField] private TMP_Dropdown colorblindModeDropdown;
    [SerializeField] private TMP_Text brightnessValueText;

    [Header("Настройки управления")]
    [SerializeField] private Slider mouseSensitivitySlider;
    [SerializeField] private TMP_InputField mouseSensitivityInput;
    [SerializeField] private TMP_Text mouseSensitivityLabel;

    [Header("Кнопки настроек")]
    [SerializeField] private Button saveButton;
    [SerializeField] private Button defaultsButton;
    [SerializeField] private Button backButton;
    [SerializeField] private TMP_Text saveButtonText;
    [SerializeField] private TMP_Text defaultsButtonText;
    [SerializeField] private TMP_Text backButtonText;

    [Header("Заголовки секций")]
    [SerializeField] private TMP_Text displaySectionTitle;
    [SerializeField] private TMP_Text audioSectionTitle;
    [SerializeField] private TMP_Text languageSectionTitle;
    [SerializeField] private TMP_Text graphicsSectionTitle;
    [SerializeField] private TMP_Text controlsSectionTitle;

    private static readonly string[] SettingKeys =
    {
        "Resolution", "Fullscreen", "Quality", "MasterVolume", "MusicVolume", "SFXVolume",
        "Language", "Brightness", "ColorblindMode", "ColorblindEnabled", "MouseSensitivity"
    };

    private Resolution[] _resolutions;
    private int _currentResolutionIndex;

    public static SettingsManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(transform.root.gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);
    }

    private void Start()
    {
        InitializeResolutionDropdown();
        InitializeQualityDropdown();
        InitializeLanguageDropdown();
        LoadSettings();
        SetupEventListeners();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------ Инициализация

    private void InitializeResolutionDropdown()
    {
        _resolutions = Screen.resolutions;
        _currentResolutionIndex = 0;
        if (resolutionDropdown == null) return;

        resolutionDropdown.ClearOptions();
        List<string> options = new List<string>();
        Resolution current = Screen.currentResolution;

        for (int i = 0; i < _resolutions.Length; i++)
        {
            int hz = Mathf.RoundToInt((float)_resolutions[i].refreshRateRatio.value);
            options.Add(_resolutions[i].width + " × " + _resolutions[i].height + " (" + hz + "Hz)");

            if (_resolutions[i].width == current.width && _resolutions[i].height == current.height &&
                Mathf.Approximately((float)_resolutions[i].refreshRateRatio.value, (float)current.refreshRateRatio.value))
            {
                _currentResolutionIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = _currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();
    }

    private void InitializeQualityDropdown()
    {
        if (qualityDropdown == null) return;

        qualityDropdown.ClearOptions();
        qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
        qualityDropdown.value = QualitySettings.GetQualityLevel();
    }

    private void InitializeLanguageDropdown()
    {
        if (languageDropdown == null) return;

        languageDropdown.ClearOptions();
        languageDropdown.AddOptions(new List<string> { "Русский", "English" });
    }

    private void SetupEventListeners()
    {
        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(SetResolution);
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        if (qualityDropdown != null) qualityDropdown.onValueChanged.AddListener(SetQuality);

        if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
        if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(SetSFXVolume);

        if (languageDropdown != null) languageDropdown.onValueChanged.AddListener(SetLanguage);

        if (brightnessSlider != null) brightnessSlider.onValueChanged.AddListener(SetBrightness);
        if (colorblindToggle != null) colorblindToggle.onValueChanged.AddListener(ToggleColorblindMode);
        if (colorblindModeDropdown != null) colorblindModeDropdown.onValueChanged.AddListener(SetColorblindMode);

        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.onValueChanged.AddListener(SetMouseSensitivity);

            if (mouseSensitivityInput != null)
            {
                mouseSensitivityInput.onEndEdit.AddListener(text =>
                {
                    float value;
                    if (float.TryParse(text, out value))
                        mouseSensitivitySlider.value = Mathf.Clamp(value, 0.1f, 10f);
                });
            }
        }

        if (saveButton != null) saveButton.onClick.AddListener(SaveSettings);
        if (defaultsButton != null) defaultsButton.onClick.AddListener(ResetToDefaults);
    }

    // ------------------------------------------------------------------ Применение настроек

    public void SetResolution(int resolutionIndex)
    {
        if (_resolutions == null || resolutionIndex < 0 || resolutionIndex >= _resolutions.Length) return;

        Resolution r = _resolutions[resolutionIndex];
        FullScreenMode mode = fullscreenToggle != null && !fullscreenToggle.isOn
            ? FullScreenMode.Windowed
            : FullScreenMode.FullScreenWindow;

        Screen.SetResolution(r.width, r.height, mode, r.refreshRateRatio);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        // Меняем только режим — разрешение, выбранное в списке, не трогаем.
        Screen.fullScreenMode = isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }

    public void SetQuality(int qualityIndex)
    {
        QualitySettings.SetQualityLevel(qualityIndex);
    }

    public void SetMasterVolume(float volume)
    {
        AudioListener.volume = volume;
        GameSettings.MasterVolume = volume;
        SetValueText(masterVolumeValueText, volume);
    }

    public void SetMusicVolume(float volume)
    {
        GameSettings.MusicVolume = volume;   // MusicPlayer подхватывает автоматически
        SetValueText(musicVolumeValueText, volume);
    }

    public void SetSFXVolume(float volume)
    {
        GameSettings.SfxVolume = volume;     // шаги, кнопки, предметы, переходы
        SetValueText(sfxVolumeValueText, volume);
    }

    public void SetLanguage(int languageIndex)
    {
        GameSettings.LanguageIndex = languageIndex;
    }

    public void SetBrightness(float brightness)
    {
        if (brightnessOverlay != null)
        {
            Color c = brightnessOverlay.color;
            c.a = 1f - brightness;
            brightnessOverlay.color = c;
        }
        SetValueText(brightnessValueText, brightness);
    }

    public void SetMouseSensitivity(float sensitivity)
    {
        GameSettings.MouseSensitivity = sensitivity;
        if (mouseSensitivityInput != null) mouseSensitivityInput.text = sensitivity.ToString("F1");
    }

    // Режим для дальтоников пока НЕ реализован: прежний код писал в глобальный шейдерный параметр,
    // который никакой шейдер не читает. Реализуйте через Volume (Color Adjustments / LUT) или уберите UI.
    public void ToggleColorblindMode(bool enabled)
    {
        if (colorblindModeDropdown != null) colorblindModeDropdown.gameObject.SetActive(enabled);
    }

    public void SetColorblindMode(int mode)
    {
    }

    private static void SetValueText(TMP_Text label, float value01)
    {
        if (label != null) label.text = Mathf.RoundToInt(value01 * 100f) + "%";
    }

    // ------------------------------------------------------------------ Сохранение / загрузка

    public void SaveSettings()
    {
        if (resolutionDropdown != null) PlayerPrefs.SetInt("Resolution", resolutionDropdown.value);
        if (fullscreenToggle != null) PlayerPrefs.SetInt("Fullscreen", fullscreenToggle.isOn ? 1 : 0);
        if (qualityDropdown != null) PlayerPrefs.SetInt("Quality", qualityDropdown.value);
        if (brightnessSlider != null) PlayerPrefs.SetFloat("Brightness", brightnessSlider.value);
        if (colorblindModeDropdown != null) PlayerPrefs.SetInt("ColorblindMode", colorblindModeDropdown.value);
        if (colorblindToggle != null) PlayerPrefs.SetInt("ColorblindEnabled", colorblindToggle.isOn ? 1 : 0);

        // Громкости, язык и чувствительность записываются в GameSettings сразу при изменении.
        PlayerPrefs.Save();
    }

    private void LoadSettings()
    {
        // Разрешение применяем только если игрок его уже сохранял (иначе остаётся текущее).
        int savedRes = PlayerPrefs.GetInt("Resolution", -1);
        bool hasSavedRes = _resolutions != null && savedRes >= 0 && savedRes < _resolutions.Length;
        if (resolutionDropdown != null)
        {
            resolutionDropdown.value = hasSavedRes ? savedRes : _currentResolutionIndex;
            resolutionDropdown.RefreshShownValue();
        }

        if (fullscreenToggle != null) fullscreenToggle.isOn = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        if (qualityDropdown != null) qualityDropdown.value = PlayerPrefs.GetInt("Quality", QualitySettings.GetQualityLevel());

        if (masterVolumeSlider != null) masterVolumeSlider.value = GameSettings.MasterVolume;
        if (musicVolumeSlider != null) musicVolumeSlider.value = GameSettings.MusicVolume;
        if (sfxVolumeSlider != null) sfxVolumeSlider.value = GameSettings.SfxVolume;

        if (languageDropdown != null) languageDropdown.value = Mathf.Clamp(GameSettings.LanguageIndex, 0, 1);

        if (brightnessSlider != null) brightnessSlider.value = PlayerPrefs.GetFloat("Brightness", 1f);
        if (colorblindModeDropdown != null) colorblindModeDropdown.value = PlayerPrefs.GetInt("ColorblindMode", 0);
        if (colorblindToggle != null) colorblindToggle.isOn = PlayerPrefs.GetInt("ColorblindEnabled", 0) == 1;

        if (mouseSensitivitySlider != null) mouseSensitivitySlider.value = GameSettings.MouseSensitivity;

        ApplyLoadedSettings(hasSavedRes);
    }

    private void ApplyLoadedSettings(bool applyResolution)
    {
        if (fullscreenToggle != null) SetFullscreen(fullscreenToggle.isOn);
        if (applyResolution && resolutionDropdown != null) SetResolution(resolutionDropdown.value);
        if (qualityDropdown != null) SetQuality(qualityDropdown.value);

        AudioListener.volume = GameSettings.MasterVolume;
        SetValueText(masterVolumeValueText, GameSettings.MasterVolume);
        SetValueText(musicVolumeValueText, GameSettings.MusicVolume);
        SetValueText(sfxVolumeValueText, GameSettings.SfxVolume);

        if (brightnessSlider != null) SetBrightness(brightnessSlider.value);
        if (colorblindToggle != null) ToggleColorblindMode(colorblindToggle.isOn);
        if (mouseSensitivityInput != null) mouseSensitivityInput.text = GameSettings.MouseSensitivity.ToString("F1");
    }

    public void ResetToDefaults()
    {
        // Раньше здесь был PlayerPrefs.DeleteAll() — он стирал и любые другие данные игры.
        for (int i = 0; i < SettingKeys.Length; i++) PlayerPrefs.DeleteKey(SettingKeys[i]);
        LoadSettings();
        SaveSettings();
    }
}
