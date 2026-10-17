using System.Collections;
using UnityEngine;

/// <summary>Красная кнопка в комнате: нажатие → выход в главное меню (через SceneFlow).</summary>
public class ExitButtonInteractable : Interactable
{
    [Header("Кнопка выхода")]
    [SerializeField] private string sceneToLoad = SceneFlow.MainMenuScene;
    [SerializeField] private Transform movingPart;       // подвижная «шляпка» кнопки (если пусто — сам объект)
    [SerializeField] private Renderer colorRenderer;     // чей цвет менять при нажатии
    [SerializeField] private Light buttonLight;
    [SerializeField] private float pressDepth = 0.02f;
    [SerializeField] private float pressDuration = 0.15f;
    [SerializeField] private float loadDelay = 0.4f;
    [SerializeField] private Color pressedColor = new Color(0.1f, 0.85f, 0.25f);

    private bool _pressed;

    public override string GetInteractionText()
    {
        return GameSettings.Tr("E — выйти в главное меню", "E — exit to main menu");
    }

    public override bool CanInteract()
    {
        return !_pressed && base.CanInteract();
    }

    public void Configure(Transform moving, Renderer coloredRenderer, Light glow)
    {
        movingPart = moving;
        colorRenderer = coloredRenderer;
        buttonLight = glow;
    }

    public override void Interact()
    {
        if (!CanInteract()) return;

        if (!Application.CanStreamedLevelBeLoaded(sceneToLoad))
        {
            Debug.LogError("[ExitButton] Сцена '" + sceneToLoad + "' не добавлена в Build Settings.", this);
            return;
        }

        _pressed = true;
        StartCoroutine(PressRoutine());
    }

    private IEnumerator PressRoutine()
    {
        if (interactionSound != null)
            AudioSource.PlayClipAtPoint(interactionSound, transform.position, interactionSoundVolume * GameSettings.SfxVolume);

        Transform part = movingPart != null ? movingPart : transform;
        Vector3 start = part.localPosition;
        Vector3 end = start + Vector3.down * pressDepth;

        float duration = Mathf.Max(0.01f, pressDuration);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            part.localPosition = Vector3.Lerp(start, end, t / duration);
            yield return null;
        }
        part.localPosition = end;

        if (colorRenderer != null) MazeItemFactory.SetMaterialColor(colorRenderer.material, pressedColor);
        if (buttonLight != null) buttonLight.color = pressedColor;

        yield return new WaitForSeconds(loadDelay);
        SceneFlow.Load(sceneToLoad);
    }
}
