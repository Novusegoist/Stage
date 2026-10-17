using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Плавное увеличение кнопки при наведении мыши или выборе с клавиатуры/геймпада.</summary>
public class UIButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float speed = 14f;

    private Vector3 _baseScale = Vector3.one;
    private float _current = 1f;
    private float _target = 1f;
    private bool _baseCaptured;

    private void Awake()
    {
        CaptureBase();
    }

    private void OnDisable()
    {
        _current = 1f;
        _target = 1f;
        if (_baseCaptured) transform.localScale = _baseScale;
    }

    private void CaptureBase()
    {
        if (_baseCaptured) return;
        _baseScale = transform.localScale;
        _baseCaptured = true;
    }

    public void OnPointerEnter(PointerEventData eventData) { _target = hoverScale; }
    public void OnPointerExit(PointerEventData eventData) { _target = 1f; }
    public void OnSelect(BaseEventData eventData) { _target = hoverScale; }
    public void OnDeselect(BaseEventData eventData) { _target = 1f; }

    private void Update()
    {
        if (Mathf.Abs(_current - _target) < 0.0005f) return;

        // unscaledDeltaTime — кнопки должны анимироваться и в паузе (timeScale = 0).
        _current = Mathf.Lerp(_current, _target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
        transform.localScale = _baseScale * _current;
    }
}
