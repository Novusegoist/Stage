using UnityEngine;

/// <summary>
/// Рамка с картинкой. Берётся в руки, вращается ПКМ, обратная сторона тоже видна
/// (раньше картина была одиночным Quad без рамки и исчезала при повороте).
/// </summary>
public class PictureInteractable : HoldableItem
{
    [Header("Картина")]
    [SerializeField] private Texture2D pictureTexture;
    [SerializeField] private Renderer pictureRenderer;   // полотно; если не задано — ищется дочерний "PictureCanvas"
    [SerializeField] private float frameAspect = 4f / 3f;

    public override string GetInteractionText()
    {
        return GameSettings.Tr("E — взять картину", "E — pick up the picture");
    }

    private void Start()
    {
        ApplyTexture();
    }

    public void SetPictureTexture(Texture2D newTexture)
    {
        pictureTexture = newTexture;
        ApplyTexture();
    }

    private void ApplyTexture()
    {
        if (pictureTexture == null) return;

        if (pictureRenderer == null)
        {
            Transform canvas = transform.Find("PictureCanvas");
            if (canvas != null) pictureRenderer = canvas.GetComponent<Renderer>();
        }
        if (pictureRenderer == null) return;

        // Экземпляр материала нужен намеренно: текстура уникальна для этой картины.
        Material mat = pictureRenderer.material;
        mat.mainTexture = pictureTexture;
        MazeItemFactory.SetMaterialColor(mat, Color.white);

        // Центрированная обрезка под пропорции рамки — картинка не растягивается.
        float texAspect = (float)pictureTexture.width / pictureTexture.height;
        Vector2 scale = Vector2.one;
        Vector2 offset = Vector2.zero;
        if (texAspect > frameAspect)
        {
            scale.x = frameAspect / texAspect;
            offset.x = (1f - scale.x) * 0.5f;
        }
        else
        {
            scale.y = texAspect / frameAspect;
            offset.y = (1f - scale.y) * 0.5f;
        }
        mat.mainTextureScale = scale;
        mat.mainTextureOffset = offset;
    }
}
