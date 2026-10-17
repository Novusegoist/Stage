using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Письмо на столе. Берётся в руки (E), текст нарисован прямо на бумаге (World Space Canvas),
/// поэтому его можно читать, поднося к камере и поворачивая правой кнопкой мыши.
/// Раньше письмо открывало экранный UI и отключало ВСЕ скрипты игрока — и не включало их обратно.
/// </summary>
public class LetterInteractable : HoldableItem
{
    [Header("Письмо")]
    [SerializeField] private string letterTitle = "Письмо";
    [TextArea(8, 20)]
    [SerializeField] private string russianText = "Дорогой друг,\n\nЭто пример текста письма.\n\nС уважением,\nАвтор";
    [TextArea(8, 20)]
    [SerializeField] private string englishText = "Dear friend,\n\nThis is an example letter text.\n\nBest regards,\nAuthor";

    [Header("Текстовые поля (находятся по имени Title/Content)")]
    [SerializeField] private Text titleText;
    [SerializeField] private Text contentText;

    public override string GetInteractionText()
    {
        return GameSettings.Tr("E — взять письмо", "E — pick up the letter");
    }

    private void OnEnable()
    {
        GameSettings.LanguageChanged += RefreshText;
        RefreshText();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        GameSettings.LanguageChanged -= RefreshText;
    }

    public void SetLetterText(string newRussianText, string newEnglishText)
    {
        russianText = newRussianText;
        englishText = newEnglishText;
        RefreshText();
    }

    public void SetLetterTitle(string newTitle)
    {
        letterTitle = newTitle;
        RefreshText();
    }

    private void RefreshText()
    {
        if (titleText == null || contentText == null) ResolveTexts();
        if (titleText != null) titleText.text = letterTitle;
        if (contentText != null) contentText.text = GameSettings.IsEnglish ? englishText : russianText;
    }

    private void ResolveTexts()
    {
        Text[] texts = GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name == "Title") titleText = texts[i];
            else if (texts[i].name == "Content") contentText = texts[i];
        }
    }
}
