using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Собирает предметы комнаты (письмо, рамку с картинкой, красную кнопку).
/// Один и тот же код используется:
///  • редактором — меню Tools/Stage/Create Maze Item Prefabs сохраняет результат в префабы;
///  • генератором комнаты — как запасной вариант, если префаб не назначен.
/// Материалы создаются из материала по умолчанию текущего пайплайна (URP/Built-in),
/// Образец материала берётся у ваших префабов (SetTemplateMaterial) — поэтому ничего не «розовеет» в билде.
/// Лицевая сторона предметов смотрит в -Z (как у Quad).
/// </summary>
public static class MazeItemFactory
{
    public const float PaperWidth = 0.24f;
    public const float PaperHeight = 0.34f;
    private const float CanvasWidth = 480f;
    private const float CanvasHeight = 680f;

    private static Material _template;
    private static Material _templateOverride;

    /// <summary>
    /// Образец материала с гарантированно рабочим шейдером — берётся у ваших префабов кита (стол, пол, стены).
    /// ВАЖНО: в собранной игре на URP GameObject.CreatePrimitive выдаёт материал со старым шейдером Standard,
    /// который URP не рисует (розовые предметы), поэтому полагаться на «материал примитива» нельзя.
    /// </summary>
    public static void SetTemplateMaterial(Material source)
    {
        if (source == null || IsBroken(source)) return;
        _templateOverride = source;
        _template = null;
    }

    private static Material Template
    {
        get
        {
            if (_template != null) return _template;

            if (_templateOverride != null)
            {
                _template = _templateOverride;
                return _template;
            }

            // Редактор (создание префабов): здесь материал примитива — настоящий материал URP.
            GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material primitiveMaterial = probe.GetComponent<Renderer>().sharedMaterial;
            DestroyObject(probe);
            if (!IsBroken(primitiveMaterial))
            {
                _template = primitiveMaterial;
                return _template;
            }

            // Последний шанс: шейдеры URP по имени (сработает, если они попали в сборку).
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            _template = shader != null ? new Material(shader) : primitiveMaterial;
            return _template;
        }
    }

    public static void DestroyObject(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Object.Destroy(obj);
        else Object.DestroyImmediate(obj);
    }

    /// <summary>Материал, который не нарисуется (нет шейдера, он не поддерживается, ошибочный или Standard под URP).</summary>
    public static bool IsBroken(Material m)
    {
        if (m == null || m.shader == null) return true;
        if (!m.shader.isSupported) return true;

        string shaderName = m.shader.name;
        if (shaderName.StartsWith("Hidden/InternalErrorShader")) return true;
        if (shaderName == "Standard" && UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null) return true;
        return false;
    }

    /// <summary>Сколько текстур назначено материалу (чем меньше — тем лучше образец).</summary>
    public static int TextureCount(Material m)
    {
        if (m == null) return int.MaxValue;
        int count = 0;
        string[] names = m.GetTexturePropertyNames();
        for (int i = 0; i < names.Length; i++)
            if (m.GetTexture(names[i]) != null) count++;
        return count;
    }

    /// <summary>Задаёт цвет независимо от шейдера (URP: _BaseColor, встроенные: _Color).</summary>
    public static void SetMaterialColor(Material m, Color color)
    {
        if (m == null) return;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
    }

    // Убирает текстуры образца (кирпич, нормали…) и ключевые слова, которые на них завязаны.
    private static void ClearTextures(Material m)
    {
        string[] names = m.GetTexturePropertyNames();
        for (int i = 0; i < names.Length; i++) m.SetTexture(names[i], null);

        string[] keywords =
        {
            "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_SPECGLOSSMAP", "_OCCLUSIONMAP", "_PARALLAXMAP",
            "_EMISSION", "_DETAIL_MULX2", "_DETAIL_SCALED"
        };
        for (int i = 0; i < keywords.Length; i++) m.DisableKeyword(keywords[i]);

        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
    }

    private static Material Make(string name, Color color)
    {
        Material m = new Material(Template);
        m.name = name;
        ClearTextures(m);
        SetMaterialColor(m, color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.2f);
        return m;
    }

    /// <summary>
    /// Проверяет все рендереры объекта и заменяет «розовые» материалы рабочими (цвет сохраняется).
    /// Вызывается для всего, что создаётся кодом или берётся из префабов предметов.
    /// </summary>
    public static void RepairMaterials(GameObject root)
    {
        if (root == null) return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || r is SpriteRenderer) continue;

            Material[] materials = r.sharedMaterials;
            bool changed = false;
            for (int j = 0; j < materials.Length; j++)
            {
                if (!IsBroken(materials[j])) continue;
                materials[j] = Replacement(materials[j]);
                changed = true;
            }
            if (changed) r.sharedMaterials = materials;
        }
    }

    private static Material Replacement(Material broken)
    {
        Material m = new Material(Template);
        ClearTextures(m);
        m.name = broken != null ? broken.name : "Repaired";

        Color color = new Color(0.6f, 0.6f, 0.6f);
        if (broken != null && broken.HasProperty("_Color")) color = broken.GetColor("_Color");
        else if (broken != null && broken.HasProperty("_BaseColor")) color = broken.GetColor("_BaseColor");
        SetMaterialColor(m, color);
        return m;
    }

    private static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.GetComponent<Renderer>().sharedMaterial = mat;

        // Физика — на корне предмета, а не на каждой детали.
        Collider c = go.GetComponent<Collider>();
        if (c != null) DestroyObject(c);
        return go;
    }

    // ------------------------------------------------------------------ Письмо

    public static GameObject CreateLetter()
    {
        GameObject root = new GameObject("Letter");

        Material paper = Make("Letter_Paper", new Color(0.96f, 0.93f, 0.84f));
        Part(PrimitiveType.Cube, "Paper", root.transform, Vector3.zero, new Vector3(PaperWidth, PaperHeight, 0.004f), paper);

        BoxCollider col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(PaperWidth, PaperHeight, 0.02f);

        // Текст на мировом Canvas, чуть перед бумагой (лицо — в -Z).
        GameObject canvasGo = new GameObject("TextCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(root.transform, false);
        canvasGo.transform.localPosition = new Vector3(0f, 0f, -0.0035f);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 4f; // резкий текст при поднесении к камере

        RectTransform canvasRt = (RectTransform)canvasGo.transform;
        canvasRt.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
        float s = PaperWidth / CanvasWidth;
        canvasRt.localScale = new Vector3(s, s, s);

        Color ink = new Color(0.12f, 0.1f, 0.08f);

        Text title = UIFactory.CreateText(canvasGo.transform, "", 34, TextAnchor.UpperCenter, ink);
        title.name = "Title";
        title.fontStyle = FontStyle.Bold;
        RectTransform titleRt = (RectTransform)title.transform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(-60f, 70f);
        titleRt.anchoredPosition = new Vector2(0f, -24f);

        Text content = UIFactory.CreateText(canvasGo.transform, "", 28, TextAnchor.UpperLeft, ink);
        content.name = "Content";
        content.horizontalOverflow = HorizontalWrapMode.Wrap;
        content.verticalOverflow = VerticalWrapMode.Truncate;
        content.resizeTextForBestFit = true;
        content.resizeTextMinSize = 14;
        content.resizeTextMaxSize = 30;
        RectTransform contentRt = (RectTransform)content.transform;
        contentRt.anchorMin = Vector2.zero;
        contentRt.anchorMax = Vector2.one;
        contentRt.offsetMin = new Vector2(30f, 30f);
        contentRt.offsetMax = new Vector2(-30f, -100f);

        LetterInteractable letter = root.AddComponent<LetterInteractable>();
        letter.ConfigureHold(0.5f, new Vector3(0f, -0.02f, 0f), Vector3.zero);
        return root;
    }

    // ------------------------------------------------------------------ Рамка с картиной

    public static GameObject CreatePictureFrame()
    {
        GameObject root = new GameObject("PictureFrame");

        Material wood = Make("Frame_Wood", new Color(0.36f, 0.22f, 0.1f));
        Material back = Make("Frame_Back", new Color(0.12f, 0.1f, 0.09f));
        Material canvasMat = Make("Frame_Canvas", Color.white);

        const float innerW = 0.44f;
        const float innerH = 0.33f;   // 4:3
        const float bar = 0.05f;
        const float depth = 0.03f;
        float outerW = innerW + bar * 2f;
        float outerH = innerH + bar * 2f;

        Part(PrimitiveType.Cube, "Top", root.transform, new Vector3(0f, innerH * 0.5f + bar * 0.5f, 0f), new Vector3(outerW, bar, depth), wood);
        Part(PrimitiveType.Cube, "Bottom", root.transform, new Vector3(0f, -(innerH * 0.5f + bar * 0.5f), 0f), new Vector3(outerW, bar, depth), wood);
        Part(PrimitiveType.Cube, "Left", root.transform, new Vector3(-(innerW * 0.5f + bar * 0.5f), 0f, 0f), new Vector3(bar, innerH, depth), wood);
        Part(PrimitiveType.Cube, "Right", root.transform, new Vector3(innerW * 0.5f + bar * 0.5f, 0f, 0f), new Vector3(bar, innerH, depth), wood);

        // Задник (виден сзади) и полотно (спереди, в -Z).
        Part(PrimitiveType.Cube, "Back", root.transform, new Vector3(0f, 0f, 0.01f), new Vector3(innerW, innerH, 0.008f), back);
        Part(PrimitiveType.Quad, "PictureCanvas", root.transform, new Vector3(0f, 0f, 0.004f), new Vector3(innerW, innerH, 1f), canvasMat);

        BoxCollider col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(outerW, outerH, depth + 0.01f);

        PictureInteractable picture = root.AddComponent<PictureInteractable>();
        picture.ConfigureHold(0.65f, new Vector3(0f, -0.02f, 0f), Vector3.zero);
        return root;
    }

    // ------------------------------------------------------------------ Бочка и сундук (запасной вариант без префабов)

    public static GameObject CreateBarrel()
    {
        GameObject root = new GameObject("Barrel");

        Material wood = Make("Barrel_Wood", new Color(0.4f, 0.25f, 0.12f));
        Material iron = Make("Barrel_Iron", new Color(0.15f, 0.15f, 0.16f));

        // У Cylinder высота = 2 * scale.y: корпус высотой 1.1 м и диаметром 0.9 м.
        Part(PrimitiveType.Cylinder, "Body", root.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.9f, 0.55f, 0.9f), wood);
        float[] bandHeights = { 0.15f, 0.55f, 0.95f };
        for (int i = 0; i < bandHeights.Length; i++)
            Part(PrimitiveType.Cylinder, "Band", root.transform, new Vector3(0f, bandHeights[i], 0f), new Vector3(0.94f, 0.03f, 0.94f), iron);

        BoxCollider col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.55f, 0f);
        col.size = new Vector3(0.9f, 1.1f, 0.9f);
        return root;
    }

    public static GameObject CreateChest()
    {
        GameObject root = new GameObject("Chest");

        Material wood = Make("Chest_Wood", new Color(0.35f, 0.2f, 0.1f));
        Material lid = Make("Chest_Lid", new Color(0.42f, 0.25f, 0.12f));
        Material iron = Make("Chest_Iron", new Color(0.16f, 0.16f, 0.18f));
        Material gold = Make("Chest_Gold", new Color(0.85f, 0.7f, 0.2f));

        // Лицевая сторона (замок) смотрит в +Z.
        Part(PrimitiveType.Cube, "Base", root.transform, new Vector3(0f, 0.25f, 0f), new Vector3(1.0f, 0.5f, 0.6f), wood);
        Part(PrimitiveType.Cube, "Lid", root.transform, new Vector3(0f, 0.58f, 0f), new Vector3(1.02f, 0.16f, 0.62f), lid);
        Part(PrimitiveType.Cube, "StrapLeft", root.transform, new Vector3(-0.32f, 0.38f, 0f), new Vector3(0.08f, 0.76f, 0.64f), iron);
        Part(PrimitiveType.Cube, "StrapRight", root.transform, new Vector3(0.32f, 0.38f, 0f), new Vector3(0.08f, 0.76f, 0.64f), iron);
        Part(PrimitiveType.Cube, "Lock", root.transform, new Vector3(0f, 0.5f, 0.31f), new Vector3(0.1f, 0.12f, 0.03f), gold);

        BoxCollider col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.33f, 0f);
        col.size = new Vector3(1.02f, 0.66f, 0.62f);
        return root;
    }

    // ------------------------------------------------------------------ Красная кнопка

    public static GameObject CreateExitButton()
    {
        GameObject root = new GameObject("ExitButton");

        Material baseMat = Make("Exit_Base", new Color(0.18f, 0.18f, 0.2f));
        Material redMat = Make("Exit_Red", new Color(0.85f, 0.08f, 0.08f));

        // У Cylinder высота = 2 * scale.y, диаметр = scale.x.
        Part(PrimitiveType.Cylinder, "Base", root.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.30f, 0.02f, 0.30f), baseMat);
        GameObject button = Part(PrimitiveType.Cylinder, "Button", root.transform, new Vector3(0f, 0.065f, 0f), new Vector3(0.20f, 0.025f, 0.20f), redMat);

        BoxCollider col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.045f, 0f);
        col.size = new Vector3(0.30f, 0.09f, 0.30f);

        GameObject glowGo = new GameObject("Glow");
        glowGo.transform.SetParent(root.transform, false);
        glowGo.transform.localPosition = new Vector3(0f, 0.18f, 0f);
        Light glow = glowGo.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.15f, 0.15f);
        glow.range = 1.5f;
        glow.intensity = 0.8f;
        glow.shadows = LightShadows.None;

        ExitButtonInteractable exit = root.AddComponent<ExitButtonInteractable>();
        exit.Configure(button.transform, button.GetComponent<Renderer>(), glow);
        return root;
    }
}
