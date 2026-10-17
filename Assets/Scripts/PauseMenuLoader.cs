using UnityEngine;

/// <summary>
/// Кладётся в игровую сцену: создаёт меню паузы, если его ещё нет.
/// (Раньше проверялся несуществующий PauseMenu, поэтому меню создавалось заново при каждой загрузке Stage.)
/// </summary>
public class PauseMenuLoader : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenuPrefab;

    private void Start()
    {
        if (SimplePauseMenu.Instance == null)
        {
            if (pauseMenuPrefab != null)
            {
                GameObject menu = Instantiate(pauseMenuPrefab);
                menu.name = "PauseMenuCanvas";
            }
            else
            {
                Debug.LogError("[PauseMenuLoader] Префаб меню паузы не назначен!", this);
            }
        }

        Destroy(gameObject); // загрузчик своё дело сделал
    }
}
