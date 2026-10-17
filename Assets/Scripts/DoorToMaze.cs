using UnityEngine;

/// <summary>Секретная дверь: касание игроком → переход в MazeScene (через SceneFlow, со звуком и затемнением).</summary>
[RequireComponent(typeof(Collider))]
public class DoorToMaze : MonoBehaviour
{
    [SerializeField] private string sceneToLoad = SceneFlow.MazeScene;
    [SerializeField] private AudioClip teleportSound;

    private bool _used;

    private void Reset()
    {
        Collider c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_used || !other.CompareTag("Player")) return;

        if (!Application.CanStreamedLevelBeLoaded(sceneToLoad))
        {
            Debug.LogError("[DoorToMaze] Сцена '" + sceneToLoad + "' не добавлена в Build Settings.", this);
            return;
        }

        _used = true;
        // Звук играет на постоянном объекте SceneFlow и не обрывается при выгрузке сцены.
        SceneFlow.Load(sceneToLoad, teleportSound);
    }
}
