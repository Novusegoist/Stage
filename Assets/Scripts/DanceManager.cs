using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Связывает кнопки, музыку и танцора. Танцор включается ровно тогда, когда MusicPlayer сообщил
/// о начале песни, и выключается, когда песня закончилась или её перебили — отдельные таймеры
/// (из-за которых танец и музыка расходились) больше не нужны.
/// NPC создаются один раз и переиспользуются (SetActive) — без Instantiate/Destroy на каждое нажатие.
/// </summary>
public class DanceManager : MonoBehaviour
{
    public static DanceManager Instance { get; private set; }

    [Header("Music Settings")]
    [SerializeField] private MusicPlayer musicPlayer;

    [Header("NPC Settings")]
    [Tooltip("Если точка не задана — NPC появляется на Fallback Spawn Offset от этого объекта.")]
    [SerializeField] private Transform[] npcSpawnPoints;
    [SerializeField] private GameObject[] npcPrefabs;
    [SerializeField] private Vector3 fallbackSpawnOffset = new Vector3(0f, 0f, 3f);

    [Header("Settings")]
    [Tooltip("Повторное нажатие той же кнопки останавливает песню.")]
    [SerializeField] private bool toggleOnSamePress = true;

    private readonly Dictionary<int, DancingNPC> _npcs = new Dictionary<int, DancingNPC>();
    private Transform _lookTarget;

    public int SongCount { get { return musicPlayer != null ? musicPlayer.SongCount : 0; } }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (musicPlayer == null) musicPlayer = FindFirstObjectByType<MusicPlayer>();
    }

    private void OnEnable()
    {
        if (Instance != this || musicPlayer == null) return;
        musicPlayer.SongStarted += HandleSongStarted;
        musicPlayer.SongEnded += HandleSongEnded;
    }

    private void OnDisable()
    {
        if (musicPlayer == null) return;
        musicPlayer.SongStarted -= HandleSongStarted;
        musicPlayer.SongEnded -= HandleSongEnded;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------ API (вызывается кнопками)

    public bool IsValidIndex(int index)
    {
        return index >= 0 && index < SongCount;
    }

    public void PlayDance(int index)
    {
        if (musicPlayer == null)
        {
            Debug.LogError("[DanceManager] MusicPlayer не назначен!", this);
            return;
        }
        if (!IsValidIndex(index))
        {
            Debug.LogError("[DanceManager] Неверный индекс песни: " + index + " (доступно " + SongCount + ")", this);
            return;
        }

        if (toggleOnSamePress && musicPlayer.CurrentIndex == index) musicPlayer.Stop();
        else musicPlayer.Play(index);
    }

    public void StopDance()
    {
        if (musicPlayer != null) musicPlayer.Stop();
    }

    // ------------------------------------------------------------------ События музыки

    private void HandleSongStarted(int index)
    {
        DancingNPC npc = GetOrCreateNpc(index);
        if (npc == null) return;

        Vector3 pos;
        Quaternion rot;
        Transform sp = (npcSpawnPoints != null && index < npcSpawnPoints.Length) ? npcSpawnPoints[index] : null;
        if (sp != null)
        {
            pos = sp.position;
            rot = sp.rotation;
        }
        else
        {
            pos = transform.position + fallbackSpawnOffset;
            rot = Quaternion.Euler(0f, 180f, 0f);
        }

        npc.transform.SetPositionAndRotation(pos, rot);
        npc.StartDancing(GetLookTarget());
    }

    private void HandleSongEnded(int index, bool finishedNaturally)
    {
        DancingNPC npc;
        if (_npcs.TryGetValue(index, out npc) && npc != null) npc.StopDancing();
    }

    // ------------------------------------------------------------------ Помощники

    private DancingNPC GetOrCreateNpc(int index)
    {
        DancingNPC npc;
        if (_npcs.TryGetValue(index, out npc) && npc != null) return npc;

        if (npcPrefabs == null || index < 0 || index >= npcPrefabs.Length || npcPrefabs[index] == null)
        {
            Debug.LogError("[DanceManager] Префаб NPC #" + index + " не назначен!", this);
            return null;
        }

        GameObject go = Instantiate(npcPrefabs[index]);
        go.name = "DancingNPC_" + index;
        go.SetActive(false);

        npc = go.GetComponent<DancingNPC>();
        if (npc == null) npc = go.AddComponent<DancingNPC>();

        _npcs[index] = npc;
        return npc;
    }

    private Transform GetLookTarget()
    {
        if (_lookTarget != null) return _lookTarget;

        GameObject player = PlayerManager.Instance != null ? PlayerManager.Instance.GetCurrentPlayer() : null;
        if (player == null) player = GameObject.FindGameObjectWithTag("Player");

        if (player != null) _lookTarget = player.transform;
        else if (Camera.main != null) _lookTarget = Camera.main.transform;
        return _lookTarget;
    }
}
