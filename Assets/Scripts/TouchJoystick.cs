using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// «Плавающий» джойстик: появляется там, где вы коснулись левой части экрана.
/// Отклонение до упора — бег. Несколько пальцев работают одновременно (джойстик + камера).
/// </summary>
public class TouchJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private const float IdleAlpha = 0.3f;
    private const float ActiveAlpha = 0.85f;

    private Canvas _canvas;
    private RectTransform _container;
    private RectTransform _base;
    private RectTransform _knob;
    private CanvasGroup _visual;
    private float _radius;

    private bool _active;
    private Vector2 _originScreen;
    private Vector2 _originLocal;

    public void Init(Canvas canvas, RectTransform container, RectTransform baseRect, RectTransform knobRect, CanvasGroup visual, float radius)
    {
        _canvas = canvas;
        _container = container;
        _base = baseRect;
        _knob = knobRect;
        _visual = visual;
        _radius = radius;
        ShowIdle();
    }

    private void LateUpdate()
    {
        if (!_active) ShowIdle();   // держим джойстик в углу, даже если изменился размер/ориентация экрана
    }

    private void ShowIdle()
    {
        if (_container == null) return;
        Rect r = _container.rect;
        Vector2 idle = new Vector2(-r.width * 0.5f + _radius + 90f, -r.height * 0.5f + _radius + 90f);
        _base.anchoredPosition = idle;
        _knob.anchoredPosition = idle;
        _visual.alpha = IdleAlpha;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _active = true;
        _originScreen = eventData.position;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_container, eventData.position, null, out _originLocal);

        _base.anchoredPosition = _originLocal;
        _knob.anchoredPosition = _originLocal;
        _visual.alpha = ActiveAlpha;
        Apply(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_active) Apply(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _active = false;
        MobileInput.Move = Vector2.zero;
        MobileInput.Run = false;
        ShowIdle();
    }

    private void OnDisable()
    {
        _active = false;
        MobileInput.Move = Vector2.zero;
        MobileInput.Run = false;
    }

    private void Apply(Vector2 screenPosition)
    {
        float pixelsPerUnit = _canvas != null ? _canvas.scaleFactor : 1f;
        Vector2 delta = (screenPosition - _originScreen) / (_radius * pixelsPerUnit);
        Vector2 v = Vector2.ClampMagnitude(delta, 1f);

        MobileInput.Move = v;
        MobileInput.Run = v.magnitude > 0.92f;
        _knob.anchoredPosition = _originLocal + v * _radius;
    }
}
