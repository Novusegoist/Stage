using UnityEngine;

/// <summary>
/// УСТАРЕЛ. Поворотом танцора теперь занимается DancingNPC (поле Face Player).
/// Файл оставлен только затем, чтобы не появились «Missing Script» в шести NPC-префабах.
/// Удалите компонент с префабов (Tools/Stage/Remove Legacy Components) — после этого файл можно удалить.
/// </summary>
[AddComponentMenu("")]
public class NPCRotation : MonoBehaviour
{
    private void Awake()
    {
        enabled = false;
    }
}
