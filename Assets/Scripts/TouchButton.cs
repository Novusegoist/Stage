using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Кнопка экранного управления: реагирует на касание сразу (а не при отпускании) и подсвечивается.</summary>
public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public Action Pressed;    // при касании
    public Action Released;   // при отпускании

    private Image _image;
    private Color _normal;
    private Color _pressed;

    public void Init(Image image, Color normal, Color pressed)
    {
        _image = image;
        _normal = normal;
        _pressed = pressed;
        _image.color = normal;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_image != null) _image.color = _pressed;
        if (Pressed != null) Pressed();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_image != null) _image.color = _normal;
        if (Released != null) Released();
    }

    private void OnDisable()
    {
        if (_image != null) _image.color = _normal;
    }
}
