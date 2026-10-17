using UnityEngine;

/// <summary>
/// УСТАРЕЛ И ОТКЛЮЧЁН. Раньше эта заплатка на лету отключала raycastTarget у «панелей» — из-за неё
/// переставали нажиматься кнопки экранного управления на телефоне (джойстик, «Действие», зона камеры).
/// Файл оставлен пустым только затем, чтобы в префабе PauseMenuCanvas не появился «Missing Script».
/// Удалите компонент с префаба (Tools/Stage/4. Remove Legacy Components), после этого можно удалить и сам файл.
/// </summary>
[AddComponentMenu("")]
public class UIClickableFixer : MonoBehaviour
{
}
