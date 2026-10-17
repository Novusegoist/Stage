using UnityEngine;

/// <summary>Вписывает RectTransform в «безопасную область» экрана (без вырезов, скруглённых углов и жестовой панели).</summary>
public class SafeAreaFitter : MonoBehaviour
{
    private RectTransform _rt;
    private Rect _lastArea;
    private int _lastWidth;
    private int _lastHeight;

    private void Awake()
    {
        _rt = (RectTransform)transform;
        Apply();
    }

    private void Update()
    {
        if (Screen.safeArea != _lastArea || Screen.width != _lastWidth || Screen.height != _lastHeight) Apply();
    }

    private void Apply()
    {
        _lastArea = Screen.safeArea;
        _lastWidth = Screen.width;
        _lastHeight = Screen.height;
        if (Screen.width <= 0 || Screen.height <= 0) return;

        Vector2 min = _lastArea.position;
        Vector2 max = _lastArea.position + _lastArea.size;
        min.x /= Screen.width;
        min.y /= Screen.height;
        max.x /= Screen.width;
        max.y /= Screen.height;

        _rt.anchorMin = min;
        _rt.anchorMax = max;
        _rt.offsetMin = Vector2.zero;
        _rt.offsetMax = Vector2.zero;
    }
}
