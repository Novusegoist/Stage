using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Пресеты графики: Авто / Низкое / Среднее / Высокое (пункт «Графика» в настройках).
/// Работает через настройки камер URP и разрешение экрана — файлы проекта (URP-ассет) не изменяются,
/// поэтому в редакторе ничего не «залипает» после тестов.
///   Низкое : 30 FPS на телефоне, рендер ~720p, без теней, пост-обработки и сглаживания, текстуры вдвое меньше.
///   Среднее: 60 FPS, рендер ~900p, тени включены, без пост-обработки.
///   Высокое: как настроено в проекте.
/// Авто: на телефоне — Среднее (от 5.5 ГБ ОЗУ) или Низкое, на ПК — Высокое.
/// </summary>
public static class GraphicsQuality
{
    public const int Auto = 0;
    public const int Low = 1;
    public const int Medium = 2;
    public const int High = 3;

    private const string Key = "GraphicsQuality";

    private struct CameraState
    {
        public bool PostProcessing;
        public bool Shadows;
        public AntialiasingMode Antialiasing;
    }

    private static readonly Dictionary<int, CameraState> CameraStates = new Dictionary<int, CameraState>();
    private static bool _captured;
    private static AnisotropicFiltering _originalAniso;
    private static int _originalMipLimit;
    private static int _nativeWidth;
    private static int _nativeHeight;
    private static bool _resolutionChanged;
    private static bool _assetCaptured;
    private static float _originalRenderScale = 1f;
    private static int _originalMsaa = 1;
    private static bool _originalHdr = true;
    private static int _appliedShortSide = -1;

    public static int Setting
    {
        get { return PlayerPrefs.GetInt(Key, Auto); }
        set
        {
            PlayerPrefs.SetInt(Key, Mathf.Clamp(value, Auto, High));
            Apply();
        }
    }

    public static int Effective
    {
        get
        {
            int s = Setting;
            if (s != Auto) return s;
            if (!Application.isMobilePlatform) return SystemInfo.graphicsMemorySize >= 3500 ? High : Medium;   // слабая видеокарта — Среднее
            return SystemInfo.systemMemorySize >= 5500 ? Medium : Low;
        }
    }

    public static string[] Labels()
    {
        return new[]
        {
            GameSettings.Tr("Авто", "Auto"),
            GameSettings.Tr("Низкое", "Low"),
            GameSettings.Tr("Среднее", "Medium"),
            GameSettings.Tr("Высокое", "High")
        };
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        Apply();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyToCameras();
    }

    private static void Capture()
    {
        if (_captured) return;
        _captured = true;
        _originalAniso = QualitySettings.anisotropicFiltering;
        _originalMipLimit = QualitySettings.globalTextureMipmapLimit;
        _nativeWidth = Display.main.systemWidth;
        _nativeHeight = Display.main.systemHeight;
    }

    public static void Apply()
    {
        Capture();
        int q = Effective;
        bool mobile = Application.isMobilePlatform;

        // Текстуры и фильтрация: на «Низком» — вдвое меньше разрешение mip-уровней.
        QualitySettings.anisotropicFiltering = q <= Low ? AnisotropicFiltering.Disable : _originalAniso;
        QualitySettings.globalTextureMipmapLimit = q <= Low ? Mathf.Max(_originalMipLimit, 1) : _originalMipLimit;

        if (mobile)
        {
            Application.targetFrameRate = q <= Low ? 30 : 60;
            ApplyRenderResolution(q <= Low ? 720 : (q == Medium ? 900 : 1440));
        }

        ApplyPipelineAsset(q, mobile);
        ApplyToCameras();
    }

    // Масштаб рендера, MSAA и HDR живут в URP-ассете. В редакторе его не трогаем — изменения сохранились бы
    // в файле проекта; в собранной игре ассет существует только в памяти, поэтому это безопасно.
    private static void ApplyPipelineAsset(int q, bool mobile)
    {
        if (Application.isEditor) return;

        UniversalRenderPipelineAsset asset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (asset == null) return;

        if (!_assetCaptured)
        {
            _assetCaptured = true;
            _originalRenderScale = asset.renderScale;
            _originalMsaa = asset.msaaSampleCount;
            _originalHdr = asset.supportsHDR;
        }

        // На телефоне разрешение уже уменьшено через Screen.SetResolution — масштаб рендера там не трогаем.
        if (!mobile) asset.renderScale = q <= Low ? 0.75f : (q == Medium ? 0.9f : _originalRenderScale);
        asset.msaaSampleCount = q <= Low ? 1 : (q == Medium ? Mathf.Min(2, _originalMsaa) : _originalMsaa);
        asset.supportsHDR = q >= High ? _originalHdr : false;
    }

    // Рендерим в меньшем разрешении — самый надёжный способ ускорить игру на слабом телефоне.
    private static void ApplyRenderResolution(int targetShortSide)
    {
        int shortSide = Mathf.Min(_nativeWidth, _nativeHeight);
        int longSide = Mathf.Max(_nativeWidth, _nativeHeight);
        if (shortSide <= 0) return;

        float scale = Mathf.Min(1f, targetShortSide / (float)shortSide);
        int h = Mathf.RoundToInt(shortSide * scale);
        int w = Mathf.RoundToInt(longSide * scale);

        if (scale >= 0.999f && !_resolutionChanged) return;   // уже родное разрешение
        if (_appliedShortSide == h) return;

        _appliedShortSide = h;
        _resolutionChanged = scale < 0.999f;

        bool landscape = Screen.width >= Screen.height;
        Screen.SetResolution(landscape ? w : h, landscape ? h : w, true);
    }

    public static void ApplyToCameras()
    {
        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++) ApplyToCamera(cameras[i]);
    }

    public static void ApplyToCamera(Camera cam)
    {
        if (cam == null) return;
        Capture();

        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        if (data == null) return;

        int id = cam.GetInstanceID();
        if (!CameraStates.ContainsKey(id))
        {
            CameraStates[id] = new CameraState
            {
                PostProcessing = data.renderPostProcessing,
                Shadows = data.renderShadows,
                Antialiasing = data.antialiasing
            };
        }

        CameraState original = CameraStates[id];
        int q = Effective;
        data.renderPostProcessing = q >= High ? original.PostProcessing : false;
        data.renderShadows = q >= Medium ? original.Shadows : false;
        data.antialiasing = q >= High ? original.Antialiasing : AntialiasingMode.None;
    }
}
