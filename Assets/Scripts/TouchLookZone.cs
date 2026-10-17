using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Прозрачная зона на весь экран: перетаскивание пальцем поворачивает камеру
/// (или вращает предмет в руках, если включён MobileInput.InspectMode).
/// </summary>
public class TouchLookZone : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        // Нужен, чтобы EventSystem запомнил этот объект как цель перетаскивания.
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (MobileInput.InspectMode) MobileInput.AddInspect(eventData.delta);
        else MobileInput.AddLook(eventData.delta);
    }
}
