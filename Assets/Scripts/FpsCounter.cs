using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Счётчик кадров по клавише F3 (ПК): FPS, время кадра, качество графики и лимит.
/// Нужен, чтобы быстро понять, что именно нагружает компьютер. Выключен по умолчанию.
/// </summary>
public class FpsCounter : MonoBehaviour
{
    private bool _visible;
    private float _timer;
    private int _frames;
    private float _fps;
    private string _text = "";
    private GUIStyle _style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Application.isMobilePlatform) return;
        if (FindFirstObjectByType<FpsCounter>() != null) return;

        GameObject go = new GameObject("FpsCounter");
        DontDestroyOnLoad(go);
        go.AddComponent<FpsCounter>();
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.f3Key.wasPressedThisFrame) _visible = !_visible;
        if (!_visible) return;

        _frames++;
        _timer += Time.unscaledDeltaTime;
        if (_timer < 0.5f) return;

        _fps = _frames / _timer;
        _frames = 0;
        _timer = 0f;

        string[] quality = GraphicsQuality.Labels();
        string[] limit = PerformanceSettings.Labels();
        _text = _fps.ToString("F0") + " FPS  (" + (1000f / Mathf.Max(_fps, 0.01f)).ToString("F1") + " ms)\n" +
                GameSettings.Tr("Графика: ", "Graphics: ") + quality[GraphicsQuality.Effective] +
                "   " + GameSettings.Tr("Лимит: ", "Limit: ") + limit[PerformanceSettings.FrameLimit] + "\n" +
                Screen.width + "×" + Screen.height + "   " + SystemInfo.graphicsDeviceName;
    }

    private void OnGUI()
    {
        if (!_visible) return;

        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label);
            _style.fontSize = 18;
            _style.normal.textColor = Color.yellow;
        }
        GUI.Label(new Rect(12f, 10f, 700f, 90f), _text, _style);
    }
}
