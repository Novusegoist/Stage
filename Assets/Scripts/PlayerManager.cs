using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Держит ОДНОГО игрока на все игровые сцены (Stage → MazeScene).
/// Чистит дубликаты (в MazeScene лежал свой экземпляр Player — получалось два игрока),
/// а при входе в MainMenu уничтожает игрока и себя, чтобы новая игра начиналась с нуля.
/// </summary>
public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    [Header("Player Settings")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Vector3 stageSpawnPosition = new Vector3(0f, 1f, 0f);
    [Tooltip("Запасная позиция в лабиринте. Если в сцене есть MazeRoomGenerator, он ставит игрока сам.")]
    [SerializeField] private Vector3 mazeSpawnPosition = new Vector3(2f, 1f, 2f);

    private GameObject _currentPlayer;
    private int _handledSceneHandle = int.MinValue;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        // Для первой сцены sceneLoaded мог сработать раньше — HandleScene идемпотентен.
        HandleScene(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) HandleScene(scene);
    }

    private void HandleScene(Scene scene)
    {
        if (scene.handle == _handledSceneHandle) return;
        _handledSceneHandle = scene.handle;

        if (scene.name == SceneFlow.MainMenuScene)
        {
            ShutDown();
            return;
        }

        bool isStage = scene.name == SceneFlow.StageScene;
        bool mazeHasGenerator = Object.FindFirstObjectByType<MazeRoomGenerator>() != null;

        Vector3 spawn = isStage ? stageSpawnPosition : mazeSpawnPosition;
        EnsurePlayer(spawn);
        RemoveDuplicatePlayers();

        if (_currentPlayer == null) return;

        if (isStage) Teleport(_currentPlayer, stageSpawnPosition, _currentPlayer.transform.rotation);
        else if (!mazeHasGenerator) Teleport(_currentPlayer, mazeSpawnPosition, _currentPlayer.transform.rotation);
    }

    private void EnsurePlayer(Vector3 spawn)
    {
        if (_currentPlayer != null) return;

        GameObject existing = GameObject.FindGameObjectWithTag("Player");
        if (existing != null)
        {
            _currentPlayer = existing;
        }
        else if (playerPrefab != null)
        {
            _currentPlayer = Instantiate(playerPrefab, spawn, Quaternion.identity);
            _currentPlayer.name = "Player";
        }
        else
        {
            Debug.LogError("[PlayerManager] Player prefab не назначен!", this);
            return;
        }

        DontDestroyOnLoad(_currentPlayer);
    }

    private void RemoveDuplicatePlayers()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == _currentPlayer) continue;
            players[i].SetActive(false); // Destroy отложен до конца кадра — сразу прячем, чтобы поиск по тегу его не находил
            Destroy(players[i]);
        }
    }

    private void ShutDown()
    {
        if (_currentPlayer != null) Destroy(_currentPlayer);
        _currentPlayer = null;
        Destroy(gameObject);
    }

    // ---------- Публичный API ----------

    public GameObject GetCurrentPlayer()
    {
        return _currentPlayer;
    }

    public void TeleportToPosition(Vector3 position)
    {
        if (_currentPlayer != null) Teleport(_currentPlayer, position, _currentPlayer.transform.rotation);
    }

    /// <summary>
    /// Безопасный телепорт: CharacterController перебивает прямое присвоение transform.position,
    /// поэтому его нужно выключить на время перемещения.
    /// </summary>
    public static void Teleport(GameObject player, Vector3 position, Quaternion rotation)
    {
        if (player == null) return;

        CharacterController cc = player.GetComponent<CharacterController>();
        bool wasEnabled = cc != null && cc.enabled;
        if (wasEnabled) cc.enabled = false;

        player.transform.SetPositionAndRotation(position, rotation);

        if (wasEnabled) cc.enabled = true;
        Physics.SyncTransforms();

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement != null) movement.ResetMotion();
    }

    /// <summary>Расстояние от pivot игрока до подошв капсулы (с учётом масштаба префаба).</summary>
    public static float GetFeetOffset(GameObject player)
    {
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc == null) return 0f;
        float scaleY = Mathf.Abs(player.transform.lossyScale.y);
        return (cc.height * 0.5f - cc.center.y) * scaleY + cc.skinWidth;
    }

    /// <summary>Ставит игрока ногами на заданную точку пола (y — высота пола).</summary>
    public static void PlaceOnGround(GameObject player, Vector3 groundPoint, float yawDegrees)
    {
        if (player == null) return;
        Vector3 pos = groundPoint + Vector3.up * (GetFeetOffset(player) + 0.02f);
        Teleport(player, pos, Quaternion.Euler(0f, yawDegrees, 0f));
    }
}
