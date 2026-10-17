using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Проигрывает песню на двух колонках и сообщает о начале/конце событиями —
/// по ним DanceManager включает и выключает танцора, поэтому музыка и танец больше не расходятся.
///
/// Что исправлено по сравнению со старой версией:
///  • все корутины теперь хранятся и останавливаются (раньше currentFadeCoroutine всегда был null,
///    и fade-in/fade-out одновременно спорили за громкость);
///  • обе колонки стартуют сэмпл-точно через PlayScheduled (без фазового сдвига);
///  • конец песни определяется по времени (корректно работает с паузой), а не гадается таймерами снаружи;
///  • удалён мёртвый код (PlaySongWithCallback и т.п.) и бесполезный panStereo при spatialBlend = 1.
/// </summary>
public class MusicPlayer : MonoBehaviour
{
    [Header("Music Settings")]
    [SerializeField] private AudioClip[] songs;           // по одной песне на кнопку
    [SerializeField] private AudioSource leftSpeaker;
    [SerializeField] private AudioSource rightSpeaker;
    [SerializeField] private bool playOnStart = false;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.7f;
    [SerializeField] private float fadeInDuration = 1f;
    [SerializeField] private float fadeOutDuration = 1f;
    [SerializeField] private float startLeadTime = 0.1f;

    /// <summary>Песня реально зазвучала (индекс песни).</summary>
    public event Action<int> SongStarted;

    /// <summary>Песня закончилась: (индекс, true = доиграла сама, false = её остановили/перебили).</summary>
    public event Action<int, bool> SongEnded;

    public int SongCount { get { return songs != null ? songs.Length : 0; } }
    public int CurrentIndex { get; private set; }
    public bool IsPlaying { get { return CurrentIndex >= 0; } }

    private AudioSource[] _sources = new AudioSource[0];
    private Coroutine _routine;
    private float _gain;              // 0..1 — текущий fade
    private float _musicSetting = 1f; // кэш GameSettings.MusicVolume
    private float _nextSettingRead;

    private void Awake()
    {
        CurrentIndex = -1;

        if (leftSpeaker == null || rightSpeaker == null)
        {
            AudioSource[] found = GetComponentsInChildren<AudioSource>();
            if (leftSpeaker == null && found.Length > 0) leftSpeaker = found[0];
            if (rightSpeaker == null) rightSpeaker = found.Length > 1 ? found[1] : leftSpeaker;
        }

        if (leftSpeaker == null)
        {
            Debug.LogError("[MusicPlayer] Не найдены AudioSource колонок!", this);
            return;
        }
        if (rightSpeaker == null) rightSpeaker = leftSpeaker;

        _sources = leftSpeaker == rightSpeaker
            ? new AudioSource[] { leftSpeaker }
            : new AudioSource[] { leftSpeaker, rightSpeaker };

        for (int i = 0; i < _sources.Length; i++)
        {
            _sources[i].loop = false;
            _sources[i].playOnAwake = false;
            _sources[i].spatialBlend = 1f;
            _sources[i].volume = 0f;
        }
    }

    private void Start()
    {
        if (playOnStart && SongCount > 0) Play(0);
    }

    private void Update()
    {
        if (!IsPlaying) return;

        // Применяем fade × громкость × настройку игрока (слайдер «Музыка»), настройку читаем 4 раза в секунду.
        if (Time.unscaledTime >= _nextSettingRead)
        {
            _nextSettingRead = Time.unscaledTime + 0.25f;
            _musicSetting = GameSettings.MusicVolume;
        }
        ApplyVolume();
    }

    private void OnDisable()
    {
        StopImmediately();
    }

    // ------------------------------------------------------------------ API

    public void Play(int index)
    {
        if (_sources.Length == 0) return;
        if (songs == null || index < 0 || index >= songs.Length || songs[index] == null)
        {
            Debug.LogError("[MusicPlayer] Нет песни с индексом " + index + ". Доступно: " + SongCount, this);
            return;
        }

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(PlayRoutine(index));
    }

    public void Stop()
    {
        if (!IsPlaying) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(StopRoutine());
    }

    public float GetSongDuration(int index)
    {
        return (songs != null && index >= 0 && index < songs.Length && songs[index] != null) ? songs[index].length : 0f;
    }

    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        ApplyVolume();
    }

    // ------------------------------------------------------------------ Корутины

    private IEnumerator PlayRoutine(int index)
    {
        // 1) Если что-то играет — плавно гасим (SongEnded(старая, false) придёт после затухания).
        if (IsPlaying) yield return FadeOutAndStop();

        AudioClip clip = songs[index];
        CurrentIndex = index;
        _gain = 0f;

        // 2) Обе колонки стартуют в один и тот же DSP-момент.
        double startTime = AudioSettings.dspTime + Mathf.Max(0.05f, startLeadTime);
        for (int i = 0; i < _sources.Length; i++)
        {
            _sources[i].clip = clip;
            _sources[i].loop = false;
            _sources[i].volume = 0f;
            _sources[i].PlayScheduled(startTime);
        }
        while (AudioSettings.dspTime < startTime) yield return null;

        if (SongStarted != null) SongStarted(index);

        // 3) Играем: fade-in в начале и fade-out в конце клипа. Время считаем по Time.deltaTime —
        //    при паузе (timeScale = 0) оно стоит вместе со звуком.
        float length = clip.length;
        float t = 0f;
        while (t < length)
        {
            t += Time.deltaTime;
            float fadeIn = fadeInDuration > 0f ? Mathf.Clamp01(t / fadeInDuration) : 1f;
            float fadeOut = fadeOutDuration > 0f ? Mathf.Clamp01((length - t) / fadeOutDuration) : 1f;
            _gain = Mathf.Min(fadeIn, fadeOut);
            yield return null;
        }

        int finished = CurrentIndex;
        StopSources();
        CurrentIndex = -1;
        _routine = null;
        if (SongEnded != null) SongEnded(finished, true);
    }

    private IEnumerator StopRoutine()
    {
        yield return FadeOutAndStop();
        _routine = null;
    }

    private IEnumerator FadeOutAndStop()
    {
        int ending = CurrentIndex;
        float startGain = _gain;
        float duration = Mathf.Max(0.01f, fadeOutDuration);
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            _gain = Mathf.Lerp(startGain, 0f, t / duration);
            yield return null;
        }

        StopSources();
        CurrentIndex = -1;
        if (SongEnded != null) SongEnded(ending, false);
    }

    // ------------------------------------------------------------------ Внутреннее

    private void ApplyVolume()
    {
        float v = volume * _musicSetting * _gain;
        for (int i = 0; i < _sources.Length; i++) _sources[i].volume = v;
    }

    private void StopSources()
    {
        for (int i = 0; i < _sources.Length; i++)
        {
            _sources[i].Stop();
            _sources[i].volume = 0f;
        }
        _gain = 0f;
    }

    private void StopImmediately()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        StopSources();
        CurrentIndex = -1;
    }
}
