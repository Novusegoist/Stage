using UnityEngine;

/// <summary>
/// Ограничение частоты кадров на ПК. Без него игра рисует столько кадров, сколько способна, — в простом
/// главном меню это тысячи кадров в секунду: видеокарта и процессор работают на 100%, вентиляторы воют.
/// По умолчанию — 60 FPS. Выбор: настройки → «Лимит кадров» (VSync / 30 / 60 / 120 / без лимита).
/// На телефонах лимит задаёт GraphicsQuality (30 или 60). Окно в фоне — игра приостанавливается (в сборке).
/// </summary>
public static class PerformanceSettings
{
    public const int VSync = 0;
    public const int Fps30 = 1;
    public const int Fps60 = 2;
    public const int Fps120 = 3;
    public const int Unlimited = 4;

    private const string Key = "FrameLimit";

    public static int FrameLimit
    {
        get { return PlayerPrefs.GetInt(Key, Fps60); }
        set
        {
            PlayerPrefs.SetInt(Key, Mathf.Clamp(value, VSync, Unlimited));
            Apply();
        }
    }

    public static string[] Labels()
    {
        return new[] { "VSync", "30", "60", "120", GameSettings.Tr("Без лимита", "Unlimited") };
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        // Свёрнутое/неактивное окно не должно грузить компьютер. В редакторе не трогаем — это мешало бы разработке.
        if (!Application.isEditor) Application.runInBackground = false;
        Apply();
    }

    public static void Apply()
    {
        if (Application.isMobilePlatform) return;   // на телефоне — GraphicsQuality

        switch (FrameLimit)
        {
            case VSync:
                QualitySettings.vSyncCount = 1;
                Application.targetFrameRate = -1;
                break;
            case Fps30:
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 30;
                break;
            case Fps60:
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 60;
                break;
            case Fps120:
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 120;
                break;
            default:
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
                break;
        }
    }
}
