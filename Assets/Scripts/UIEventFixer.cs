using UnityEngine;

/// <summary>
/// УСТАРЕЛ И ОТКЛЮЧЁН. Раньше добавлял StandaloneInputModule к EventSystem с новым Input System — они конфликтовали.
/// EventSystem теперь создаётся через UIFactory.EnsureEventSystem(). Файл оставлен пустым, чтобы в префабе
/// PauseMenuCanvas не появился «Missing Script»; удалите компонент (Tools/Stage/4. Remove Legacy Components) и файл.
/// </summary>
[AddComponentMenu("")]
public class UIEventFixer : MonoBehaviour
{
}
