using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] protected string interactionText = "Нажмите E";
    [SerializeField] protected bool oneTimeInteraction = false;
    [SerializeField] protected AudioClip interactionSound;
    [SerializeField] protected float interactionSoundVolume = 1.0f; // Добавлено!
    
    protected bool _hasInteracted = false;
    
    public abstract void Interact();
    
    public virtual string GetInteractionText()
    {
        return interactionText;
    }
    
    public virtual bool CanInteract()
    {
        return !(_hasInteracted && oneTimeInteraction);
    }
    
    public virtual bool IsOneTimeInteraction()
    {
        return oneTimeInteraction;
    }
    
    public virtual void ResetInteraction()
    {
        _hasInteracted = false;
    }
    
    // Метод для получения громкости звука взаимодействия
    public virtual float GetInteractionSoundVolume()
    {
        return interactionSoundVolume;
    }
    
    // Метод для установки громкости звука взаимодействия
    public virtual void SetInteractionSoundVolume(float volume)
    {
        interactionSoundVolume = Mathf.Clamp01(volume);
    }
}