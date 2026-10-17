using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Экранное управление для телефона. Создаётся на игроке (PlayerMovement.Start) и живёт вместе с ним.
///   Левая часть экрана  — плавающий джойстик (до упора = бег)
///   Остальной экран      — поворот камеры (с предметом в руках — вращение предмета)
///   «Действие»           — взять / нажать / положить (то же, что E)
///   «Прыжок», пауза      — в углах
/// Элементы построены на UI-событиях, поэтому работают и мышью в редакторе (Tools/Stage/Mobile/Touch Controls In Editor).
/// </summary>
public class MobileControlsHud : MonoBehaviour
{
    private Canvas _canvas;
    private CanvasGroup _group;
    private Text _interactLabel;
    private PlayerInteractor _interactor;
    private float _nextEventSystemCheck;

    // Почти нулевая альфа вместо 0: так зона гарантированно принимает касания и при этом не видна.
    private static readonly Color HitZoneColor = new Color(0f, 0f, 0f, 0.004f);
    private static readonly Color ButtonNormal = new Color(0.16f, 0.12f, 0.28f, 0.7f);
    private static readonly Color ButtonPressed = new Color(0.55f, 0.32f, 0.95f, 0.95f);

    private void Awake()
    {
        if (!PlatformUtil.UseTouchControls)
        {
            Destroy(this);
            return;
        }

        _interactor = GetComponent<PlayerInteractor>();
        Build();
    }

    private void OnDestroy()
    {
        MobileInput.ResetAll();
        MobileInput.InspectMode = false;
    }

    private void Build()
    {
        EnsureEventSystem();

        _canvas = UIFactory.CreateCanvas("MobileControls", 20);
        _canvas.transform.SetParent(transform, false);
        _group = _canvas.gameObject.AddComponent<CanvasGroup>();

        RectTransform safe = UIFactory.CreateRect("Safe", _canvas.transform);
        UIFactory.Stretch(safe);
        safe.gameObject.AddComponent<SafeAreaFitter>();

        // 1. Зона камеры — на весь экран, самая нижняя в иерархии (получает касания последней).
        Image look = UIFactory.CreateImage(safe, "LookZone", HitZoneColor);
        UIFactory.Stretch(look.rectTransform);
        look.raycastTarget = true;
        look.canvasRenderer.cullTransparentMesh = false;
        look.gameObject.AddComponent<TouchLookZone>();

        // 2. Джойстик: левая половина экрана, нижние ¾.
        RectTransform joyZone = UIFactory.CreateRect("JoystickZone", safe);
        joyZone.anchorMin = new Vector2(0f, 0f);
        joyZone.anchorMax = new Vector2(0.5f, 0.75f);
        joyZone.offsetMin = Vector2.zero;
        joyZone.offsetMax = Vector2.zero;
        Image joyImage = joyZone.gameObject.AddComponent<Image>();
        joyImage.color = HitZoneColor;
        joyImage.raycastTarget = true;
        joyImage.canvasRenderer.cullTransparentMesh = false;

        RectTransform visualRoot = UIFactory.CreateRect("JoystickVisual", safe);
        UIFactory.Stretch(visualRoot);
        CanvasGroup visualGroup = visualRoot.gameObject.AddComponent<CanvasGroup>();
        visualGroup.blocksRaycasts = false;
        visualGroup.interactable = false;

        Image joyBase = UIFactory.CreateCircle(visualRoot, "Base", new Color(1f, 1f, 1f, 0.28f));
        joyBase.raycastTarget = false;
        joyBase.rectTransform.sizeDelta = new Vector2(260f, 260f);
        Image joyKnob = UIFactory.CreateCircle(visualRoot, "Knob", new Color(1f, 1f, 1f, 0.7f));
        joyKnob.raycastTarget = false;
        joyKnob.rectTransform.sizeDelta = new Vector2(110f, 110f);

        TouchJoystick joystick = joyZone.gameObject.AddComponent<TouchJoystick>();
        joystick.Init(_canvas, safe, joyBase.rectTransform, joyKnob.rectTransform, visualGroup, 110f);

        // 3. Кнопки справа внизу.
        TouchButton interact = CreateRoundButton(safe, "Interact", GameSettings.Tr("Действие", "Use"), 210f, new Vector2(-60f, 60f), out _interactLabel);
        interact.Pressed = MobileInput.PressInteract;

        Text unused;
        TouchButton jump = CreateRoundButton(safe, "Jump", GameSettings.Tr("Прыжок", "Jump"), 150f, new Vector2(-60f, 300f), out unused);
        jump.Pressed = MobileInput.PressJump;

        // 4. Пауза — справа вверху (две полоски).
        Image pause = UIFactory.CreateCircle(safe, "Pause", ButtonNormal);
        RectTransform pr = pause.rectTransform;
        pr.anchorMin = new Vector2(1f, 1f);
        pr.anchorMax = new Vector2(1f, 1f);
        pr.pivot = new Vector2(1f, 1f);
        pr.sizeDelta = new Vector2(96f, 96f);
        pr.anchoredPosition = new Vector2(-30f, -30f);
        pause.raycastTarget = true;

        for (int i = 0; i < 2; i++)
        {
            Image bar = UIFactory.CreateImage(pause.transform, "Bar" + i, new Color(1f, 1f, 1f, 0.9f));
            bar.raycastTarget = false;
            bar.rectTransform.sizeDelta = new Vector2(12f, 40f);
            bar.rectTransform.anchoredPosition = new Vector2(i == 0 ? -14f : 14f, 0f);
        }

        TouchButton pauseButton = pause.gameObject.AddComponent<TouchButton>();
        pauseButton.Init(pause, ButtonNormal, ButtonPressed);
        pauseButton.Released = TogglePause;
    }

    private static TouchButton CreateRoundButton(Transform parent, string name, string label, float size, Vector2 position, out Text labelText)
    {
        Image img = UIFactory.CreateCircle(parent, name, ButtonNormal);
        RectTransform rt = img.rectTransform;
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = position;
        img.raycastTarget = true;

        labelText = UIFactory.CreateText(img.transform, label, size > 180f ? 32 : 26, TextAnchor.MiddleCenter, Color.white);
        labelText.fontStyle = FontStyle.Bold;
        UIFactory.Stretch(labelText.rectTransform);

        TouchButton button = img.gameObject.AddComponent<TouchButton>();
        button.Init(img, ButtonNormal, ButtonPressed);
        return button;
    }

    private static void TogglePause()
    {
        if (SimplePauseMenu.Instance != null) SimplePauseMenu.Instance.Toggle();
    }

    private static void EnsureEventSystem()
    {
        EventSystem es = UIFactory.EnsureEventSystem();
        es.pixelDragThreshold = 1;   // стандартные 10 px «съедали» начало движения пальца
    }

    private void Update()
    {
        // Меню паузы и загрузка сцены закрывают экранное управление.
        bool visible = !SimplePauseMenu.IsPaused && !SceneFlow.IsLoading;
        float alpha = visible ? 1f : 0f;
        if (_group.alpha != alpha)
        {
            _group.alpha = alpha;
            _group.blocksRaycasts = visible;
            if (!visible) MobileInput.ResetAll();
        }

        // EventSystem из сцены пропадает при загрузке следующей — возвращаем его.
        if (Time.unscaledTime >= _nextEventSystemCheck)
        {
            _nextEventSystemCheck = Time.unscaledTime + 1f;
            if (EventSystem.current == null) EnsureEventSystem();
        }

        if (_interactLabel != null)
        {
            bool holding = _interactor != null && _interactor.HeldItem != null;
            string wanted = holding ? GameSettings.Tr("Положить", "Put back") : GameSettings.Tr("Действие", "Use");
            if (_interactLabel.text != wanted) _interactLabel.text = wanted;
        }
    }
}
