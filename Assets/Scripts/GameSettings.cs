using System;
using UnityEngine;

/// <summary>
/// Единая точка доступа к пользовательским настройкам (PlayerPrefs).
/// Ключи совпадают с теми, что пишет SettingsManager.
/// </summary>
public static class GameSettings
{
    private const string MouseSensitivityKey = "MouseSensitivity";
    private const string MasterVolumeKey = "MasterVolume";
    private const string MusicVolumeKey = "MusicVolume";
    private const string SfxVolumeKey = "SFXVolume";
    private const string LanguageKey = "Language";

    /// <summary>Вызывается при смене языка (чтобы письмо, подсказки и т.п. обновили текст).</summary>
    public static event Action LanguageChanged;

    /// <summary>Чувствительность мыши. 2 = «как в префабе»; диапазон слайдера 0.1–10.</summary>
    public static float MouseSensitivity
    {
        get { return PlayerPrefs.GetFloat(MouseSensitivityKey, 2f); }
        set { PlayerPrefs.SetFloat(MouseSensitivityKey, Mathf.Clamp(value, 0.1f, 10f)); }
    }

    public static float MasterVolume
    {
        get { return PlayerPrefs.GetFloat(MasterVolumeKey, 1f); }
        set { PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(value)); }
    }

    /// <summary>Громкость музыки (умножается на громкость MusicPlayer).</summary>
    public static float MusicVolume
    {
        get { return PlayerPrefs.GetFloat(MusicVolumeKey, 1f); }
        set { PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value)); }
    }

    /// <summary>Громкость эффектов (шаги, кнопки, предметы).</summary>
    public static float SfxVolume
    {
        get { return PlayerPrefs.GetFloat(SfxVolumeKey, 1f); }
        set { PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value)); }
    }

    /// <summary>0 = Русский, 1 = English.</summary>
    public static int LanguageIndex
    {
        get { return PlayerPrefs.GetInt(LanguageKey, 0); }
        set
        {
            PlayerPrefs.SetInt(LanguageKey, value);
            if (LanguageChanged != null) LanguageChanged();
        }
    }

    /// <summary>Скримеры в лабиринте (можно отключить в настройках).</summary>
    public static bool JumpScaresEnabled
    {
        get { return PlayerPrefs.GetInt("JumpScares", 1) == 1; }
        set { PlayerPrefs.SetInt("JumpScares", value ? 1 : 0); }
    }

    public static bool IsEnglish { get { return LanguageIndex == 1; } }

    public static SystemLanguage CurrentLanguage
    {
        get { return IsEnglish ? SystemLanguage.English : SystemLanguage.Russian; }
    }

    /// <summary>Короткий способ выбрать строку по текущему языку.</summary>
    public static string Tr(string ru, string en)
    {
        return IsEnglish ? en : ru;
    }
}
