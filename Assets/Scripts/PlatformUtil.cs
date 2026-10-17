using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Определение платформы и общие настройки телефона (60 FPS, ландшафт, экран не гаснет).
/// UseTouchControls = true на телефонах/планшетах (и в редакторе, если включён пункт меню
/// Tools/Stage/Mobile/Touch Controls In Editor — удобно проверять управление мышью).
/// </summary>
public static class PlatformUtil
{
    public const string EditorTouchKey = "Stage.TouchControlsInEditor";

    private static bool? _touch;

    public static bool IsMobile
    {
        get { return Application.isMobilePlatform; }
    }

    public static bool UseTouchControls
    {
        get
        {
            if (!_touch.HasValue) _touch = Compute();
            return _touch.Value;
        }
    }

    /// <summary>Сбросить кэш (после смены пункта меню в редакторе).</summary>
    public static void Refresh()
    {
        _touch = null;
    }

    private static bool Compute()
    {
#if UNITY_EDITOR
        if (UnityEditor.EditorPrefs.GetBool(EditorTouchKey, false)) return true;
#endif
        if (Application.isMobilePlatform) return true;

        // Планшет/ноутбук с тачскрином, но без клавиатуры и мыши.
        return Touchscreen.current != null && Keyboard.current == null && Mouse.current == null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        _touch = null;
        if (!Application.isMobilePlatform) return;

        // По умолчанию на телефонах 30 FPS, а экран гаснет через минуту бездействия.
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        // Игра — только в ландшафте.
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.AutoRotation;
    }

    /// <summary>
    /// Подсказки в коде написаны для клавиатуры («E — взять»). На телефоне подставляем название кнопки на экране.
    /// </summary>
    public static string AdaptHint(string hint)
    {
        if (string.IsNullOrEmpty(hint) || !UseTouchControls) return hint;

        string button = GameSettings.Tr("Действие", "Use");
        string s = hint;
        s = s.Replace("Нажмите E", "Нажмите «" + button + "»");
        s = s.Replace("Нажмите Е", "Нажмите «" + button + "»");   // кириллическая «Е»
        s = s.Replace("Press E", "Press «" + button + "»");
        s = s.Replace("E / Q", button);
        s = s.Replace("E — ", button + " — ");
        s = s.Replace("E - ", button + " - ");
        return s;
    }
}
