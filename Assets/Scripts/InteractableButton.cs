using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Кнопка на сцене: анимация нажатия → запуск песни и танцора через DanceManager.</summary>
public class InteractableButton : Interactable
{
    [Header("Animation Settings")]
    [SerializeField] private Animation buttonAnimation;
    [SerializeField] private string animationName = "ButtonPress";
    [SerializeField] private bool waitForAnimation = true;

    [Header("Sound Settings")]
    [SerializeField] private AudioSource buttonAudioSource;
    [SerializeField] private AudioClip pressSound;
    [SerializeField] private float soundVolume = 0.8f;

    [Header("Dance Settings")]
    [Tooltip("Номер песни/танцора (0 — первая песня).")]
    [SerializeField] private int buttonIndex = 0;
    [SerializeField] private UnityEvent onButtonPressed;

    private bool _busy;

    private void Awake()
    {
        if (buttonAnimation == null) buttonAnimation = GetComponent<Animation>();
        if (buttonAnimation != null) buttonAnimation.playAutomatically = false;

        if (buttonAudioSource == null)
        {
            buttonAudioSource = gameObject.AddComponent<AudioSource>();
            buttonAudioSource.spatialBlend = 1f;
            buttonAudioSource.maxDistance = 10f;
            buttonAudioSource.playOnAwake = false;
        }
    }

    private void OnValidate()
    {
        if (buttonIndex < 0) buttonIndex = 0;
    }

    private void OnDisable()
    {
        _busy = false; // иначе кнопка «залипнет», если её выключили посреди нажатия
    }

    public int GetButtonIndex()
    {
        return buttonIndex;
    }

    public override bool CanInteract()
    {
        return !_busy && base.CanInteract();
    }

    public override void Interact()
    {
        if (!CanInteract()) return;
        StartCoroutine(PressRoutine());
    }

    private IEnumerator PressRoutine()
    {
        _busy = true;

        PlaySound(pressSound, soundVolume);
        if (interactionSound != null) PlaySound(interactionSound, interactionSoundVolume);

        if (buttonAnimation != null && !string.IsNullOrEmpty(animationName))
        {
            AnimationClip clip = buttonAnimation.GetClip(animationName);
            if (clip != null)
            {
                buttonAnimation.Play(animationName);
                if (waitForAnimation)
                {
                    // Таймаут — на случай, если анимацию кто-то прервал.
                    float t = 0f;
                    while (t < clip.length + 0.1f && buttonAnimation.IsPlaying(animationName))
                    {
                        t += Time.deltaTime;
                        yield return null;
                    }
                }
            }
            else
            {
                Debug.LogWarning("Анимация '" + animationName + "' не найдена на " + name, this);
            }
        }

        if (DanceManager.Instance != null) DanceManager.Instance.PlayDance(buttonIndex);
        else Debug.LogError("DanceManager не найден в сцене!", this);

        onButtonPressed.Invoke();

        if (oneTimeInteraction) _hasInteracted = true;
        _busy = false;
    }

    // Можно вызывать событием из анимации.
    public void PlayPressSoundFromAnimation()
    {
        PlaySound(pressSound, soundVolume);
    }

    private void PlaySound(AudioClip clip, float volume)
    {
        if (clip != null && buttonAudioSource != null)
            buttonAudioSource.PlayOneShot(clip, volume * GameSettings.SfxVolume);
    }
}
