using UnityEngine;
using System.Collections;

public class AudioSyncController : MonoBehaviour
{
    [Header("Settings")]
    
    
    private AudioSource[] audioSources;
    private bool[] isPlaying;
    
    void Start()
    {
        audioSources = GetComponents<AudioSource>();
        isPlaying = new bool[audioSources.Length];
    }
    
    public void PlaySynced(int sourceIndex, float duration)
    {
        if (sourceIndex < 0 || sourceIndex >= audioSources.Length)
        {
            Debug.LogError($"Неверный индекс источника: {sourceIndex}");
            return;
        }
        
        StartCoroutine(PlayWithSync(sourceIndex, duration));
    }
    
    private IEnumerator PlayWithSync(int sourceIndex, float duration)
    {
        AudioSource source = audioSources[sourceIndex];
        
        if (source == null || source.clip == null)
        {
            Debug.LogError($"Источник {sourceIndex} не настроен");
            yield break;
        }
        
        // Запускаем воспроизведение
        source.Play();
        isPlaying[sourceIndex] = true;
        
        // Ждем указанную длительность
        yield return new WaitForSeconds(duration);
        
        // Плавно останавливаем
        float startTime = Time.time;
        float startVolume = source.volume;
        
        while (source.volume > 0 && Time.time - startTime < 1f)
        {
            source.volume = Mathf.Lerp(startVolume, 0f, (Time.time - startTime) / 1f);
            yield return null;
        }
        
        source.Stop();
        source.volume = startVolume;
        isPlaying[sourceIndex] = false;
        
        Debug.Log($"Источник {sourceIndex} остановлен синхронно");
    }
    
    public bool IsPlaying(int sourceIndex)
    {
        if (sourceIndex >= 0 && sourceIndex < isPlaying.Length)
        {
            return isPlaying[sourceIndex];
        }
        return false;
    }
}