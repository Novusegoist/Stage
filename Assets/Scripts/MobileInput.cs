using UnityEngine;

/// <summary>
/// Общее состояние экранного управления. HUD (джойстик, зона камеры, кнопки) пишет сюда,
/// PlayerMovement / PlayerInteractor / HoldableItem читают — поэтому управление не зависит от
/// настройки Input Actions и работает вместе с клавиатурой, мышью и геймпадом.
/// Нажатия «забираются» (Consume…), чтобы их нельзя было пропустить из-за порядка обновления скриптов.
/// </summary>
public static class MobileInput
{
    /// <summary>Джойстик, −1..1.</summary>
    public static Vector2 Move;

    /// <summary>Джойстик отклонён «до упора» — бег.</summary>
    public static bool Run;

    /// <summary>Предмет в руках: перетаскивание по экрану вращает предмет, а не камеру.</summary>
    public static bool InspectMode;

    private static Vector2 _look;      // пиксели
    private static Vector2 _inspect;   // пиксели
    private static bool _interact;
    private static bool _jump;

    public static void AddLook(Vector2 pixels) { _look += pixels; }
    public static void AddInspect(Vector2 pixels) { _inspect += pixels; }
    public static void PressInteract() { _interact = true; }
    public static void PressJump() { _jump = true; }

    public static Vector2 ConsumeLook()
    {
        Vector2 v = _look;
        _look = Vector2.zero;
        return v;
    }

    public static Vector2 ConsumeInspect()
    {
        Vector2 v = _inspect;
        _inspect = Vector2.zero;
        return v;
    }

    public static bool ConsumeInteract()
    {
        bool v = _interact;
        _interact = false;
        return v;
    }

    public static bool ConsumeJump()
    {
        bool v = _jump;
        _jump = false;
        return v;
    }

    /// <summary>Пикселей на дюйм (на старых устройствах dpi может быть 0).</summary>
    public static float Dpi
    {
        get { return Screen.dpi > 1f ? Screen.dpi : 160f; }
    }

    public static void ResetAll()
    {
        Move = Vector2.zero;
        Run = false;
        _look = Vector2.zero;
        _inspect = Vector2.zero;
        _interact = false;
        _jump = false;
    }
}
