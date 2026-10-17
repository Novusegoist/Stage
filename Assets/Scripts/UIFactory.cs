using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Небольшие помощники для UI, который строится кодом (главное меню, fade, подсказки).
/// Используется встроенный динамический шрифт — он поддерживает кириллицу без доп. ассетов.
/// </summary>
public static class UIFactory
{
    private static Font _font;

    public static Font DefaultFont
    {
        get
        {
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _font;
        }
    }

    public static Canvas CreateCanvas(string name, int sortingOrder)
    {
        GameObject go = new GameObject(name);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public static Image CreateImage(Transform parent, string name, Color color)
    {
        RectTransform rt = CreateRect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static Sprite _rounded;

    /// <summary>Скруглённый прямоугольник (9-slice), сгенерированный кодом — отдельных ассетов не нужно.</summary>
    public static Sprite RoundedSprite
    {
        get
        {
            if (_rounded == null) _rounded = BuildRoundedSprite(128, 36f, 44f);
            return _rounded;
        }
    }

    private static Sprite BuildRoundedSprite(int size, float radius, float border)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "RoundedRect";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Знаковое расстояние до скруглённого прямоугольника, 1 px сглаживания.
                float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                float ox = Mathf.Max(qx, 0f);
                float oy = Mathf.Max(qy, 0f);
                float d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - d) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }

    public static Image CreateRoundedImage(Transform parent, string name, Color color)
    {
        Image img = CreateImage(parent, name, color);
        img.sprite = RoundedSprite;
        img.type = Image.Type.Sliced;
        return img;
    }

    private static Sprite _circle;

    /// <summary>Круг (для джойстика и кнопок на экране), сгенерированный кодом.</summary>
    public static Sprite CircleSprite
    {
        get
        {
            if (_circle == null) _circle = BuildRoundedSprite(128, 64f, 0f);
            return _circle;
        }
    }

    public static Image CreateCircle(Transform parent, string name, Color color)
    {
        Image img = CreateImage(parent, name, color);
        img.sprite = CircleSprite;
        img.type = Image.Type.Simple;
        return img;
    }

    public static Text CreateText(Transform parent, string content, int fontSize, TextAnchor anchor, Color color)
    {
        RectTransform rt = CreateRect("Text", parent);
        Text text = rt.gameObject.AddComponent<Text>();
        text.font = DefaultFont;
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    public static Button CreateButton(Transform parent, string label, Vector2 size, UnityAction onClick)
    {
        Image img = CreateRoundedImage(parent, label + "Button", Color.white);
        ((RectTransform)img.transform).sizeDelta = size;

        Button button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.17f, 0.12f, 0.3f, 0.92f);
        colors.highlightedColor = new Color(0.52f, 0.28f, 0.85f, 1f);
        colors.selectedColor = new Color(0.52f, 0.28f, 0.85f, 1f);
        colors.pressedColor = new Color(0.32f, 0.16f, 0.55f, 1f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        Outline outline = img.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.14f);
        outline.effectDistance = new Vector2(2f, -2f);

        img.gameObject.AddComponent<UIButtonHover>();

        Text txt = CreateText(img.transform, label, 36, TextAnchor.MiddleCenter, Color.white);
        Stretch((RectTransform)txt.transform);

        if (onClick != null) button.onClick.AddListener(onClick);
        return button;
    }

    /// <summary>Слайдер из «голых» Image (без спрайтов и ассетов).</summary>
    public static Slider CreateSlider(Transform parent, float min, float max, float value, UnityAction<float> onChanged)
    {
        RectTransform root = CreateRect("Slider", parent);
        Slider slider = root.gameObject.AddComponent<Slider>();

        Image bg = CreateImage(root, "Background", new Color(0.22f, 0.22f, 0.28f, 1f));
        RectTransform bgRt = (RectTransform)bg.transform;
        bgRt.anchorMin = new Vector2(0f, 0.3f);
        bgRt.anchorMax = new Vector2(1f, 0.7f);
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        RectTransform fillArea = CreateRect("Fill Area", root);
        fillArea.anchorMin = new Vector2(0f, 0.3f);
        fillArea.anchorMax = new Vector2(1f, 0.7f);
        fillArea.offsetMin = new Vector2(8f, 0f);
        fillArea.offsetMax = new Vector2(-8f, 0f);

        Image fill = CreateImage(fillArea, "Fill", new Color(0.35f, 0.6f, 1f, 1f));
        RectTransform fillRt = (RectTransform)fill.transform;
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.sizeDelta = new Vector2(10f, 0f);

        RectTransform handleArea = CreateRect("Handle Slide Area", root);
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(10f, 0f);
        handleArea.offsetMax = new Vector2(-10f, 0f);

        Image handle = CreateImage(handleArea, "Handle", Color.white);
        RectTransform handleRt = (RectTransform)handle.transform;
        handleRt.anchorMin = new Vector2(0f, 0f);
        handleRt.anchorMax = new Vector2(0f, 1f);
        handleRt.sizeDelta = new Vector2(20f, 0f);

        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = Mathf.Clamp(value, min, max);

        if (onChanged != null) slider.onValueChanged.AddListener(onChanged);
        return slider;
    }

    /// <summary>
    /// Гарантирует наличие EventSystem с модулем нового Input System.
    /// (В MazeScene и MainMenu его нет, а в Stage он «умирает» при смене сцены.)
    /// </summary>
    public static EventSystem EnsureEventSystem()
    {
        EventSystem es = Object.FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            GameObject go = new GameObject("EventSystem");
            es = go.AddComponent<EventSystem>();
        }

        // Игра использует новый Input System. Старый StandaloneInputModule рядом с ним только мешает
        // (при режиме «только новый ввод» он ещё и сыплет исключениями), поэтому заменяем его.
        InputSystemUIInputModule module = es.GetComponent<InputSystemUIInputModule>();
        StandaloneInputModule legacy = es.GetComponent<StandaloneInputModule>();

        if (module == null)
        {
            if (legacy != null) Object.Destroy(legacy);
            module = es.gameObject.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }
        else
        {
            if (legacy != null) Object.Destroy(legacy);

            // Модуль без назначенных действий (указатель/клик) не реагирует на мышь и касания.
            if (module.point == null || module.leftClick == null) module.AssignDefaultActions();
        }
        return es;
    }
}
