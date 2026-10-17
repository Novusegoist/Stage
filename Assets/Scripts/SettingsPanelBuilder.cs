using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Достраивает панель настроек меню паузы. В префабе PauseMenuCanvas панель была пустой:
/// только заголовок, без единого слайдера и без кнопки «Назад» (поля SettingsManager не были привязаны).
/// Значения пишутся прямо в GameSettings, поэтому работают сразу и сохраняются между запусками.
/// </summary>
public static class SettingsPanelBuilder
{
    public static Button Build(Transform panel, UnityAction onBack)
    {
        // Пустой текст-заглушка из префаба больше не нужен.
        Transform placeholder = panel.Find("Text (TMP)");
        if (placeholder != null) placeholder.gameObject.SetActive(false);

        RectTransform list = UIFactory.CreateRect("Controls", panel);
        list.anchorMin = new Vector2(0.06f, 0.14f);
        list.anchorMax = new Vector2(0.94f, 0.82f);
        list.offsetMin = Vector2.zero;
        list.offsetMax = Vector2.zero;

        VerticalLayoutGroup vertical = list.gameObject.AddComponent<VerticalLayoutGroup>();
        vertical.spacing = 12f;
        vertical.childAlignment = TextAnchor.UpperCenter;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        AddSliderRow(list, GameSettings.Tr("Чувствительность камеры", "Look sensitivity"),
            0.1f, 10f, GameSettings.MouseSensitivity,
            v => v.ToString("F1"),
            v => GameSettings.MouseSensitivity = v);

        AddSliderRow(list, GameSettings.Tr("Общая громкость", "Master volume"),
            0f, 1f, GameSettings.MasterVolume,
            Percent,
            v =>
            {
                GameSettings.MasterVolume = v;
                AudioListener.volume = v;
            });

        AddSliderRow(list, GameSettings.Tr("Музыка", "Music"),
            0f, 1f, GameSettings.MusicVolume,
            Percent,
            v => GameSettings.MusicVolume = v);

        AddSliderRow(list, GameSettings.Tr("Эффекты", "Effects"),
            0f, 1f, GameSettings.SfxVolume,
            Percent,
            v => GameSettings.SfxVolume = v);

        AddCycleRow(list, GameSettings.Tr("Графика", "Graphics"), GraphicsQuality.Labels(), GraphicsQuality.Setting,
            i => GraphicsQuality.Setting = i);

        // На ПК: ограничение кадров (на телефоне его задаёт «Графика»).
        if (!Application.isMobilePlatform)
        {
            AddCycleRow(list, GameSettings.Tr("Лимит кадров", "Frame limit"), PerformanceSettings.Labels(), PerformanceSettings.FrameLimit,
                i => PerformanceSettings.FrameLimit = i);
        }

        AddCycleRow(list, GameSettings.Tr("Скримеры", "Jump scares"),
            new[] { GameSettings.Tr("Вкл", "On"), GameSettings.Tr("Выкл", "Off") },
            GameSettings.JumpScaresEnabled ? 0 : 1,
            i => GameSettings.JumpScaresEnabled = (i == 0));

        Button back = UIFactory.CreateButton(panel, GameSettings.Tr("Назад", "Back"), new Vector2(300f, 64f), onBack);
        RectTransform backRt = (RectTransform)back.transform;
        backRt.anchorMin = new Vector2(0.5f, 0f);
        backRt.anchorMax = new Vector2(0.5f, 0f);
        backRt.pivot = new Vector2(0.5f, 0f);
        backRt.anchoredPosition = new Vector2(0f, 36f);
        return back;
    }

    private static string Percent(float v)
    {
        return Mathf.RoundToInt(v * 100f) + "%";
    }

    // Строка «название — кнопка»: каждое нажатие переключает на следующий вариант.
    private static void AddCycleRow(Transform parent, string label, string[] options, int current, UnityAction<int> onChanged)
    {
        RectTransform row = UIFactory.CreateRect("Row", parent);
        LayoutElement rowElement = row.gameObject.AddComponent<LayoutElement>();
        rowElement.preferredHeight = 50f;

        HorizontalLayoutGroup horizontal = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        horizontal.spacing = 16f;
        horizontal.childAlignment = TextAnchor.MiddleLeft;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = true;

        Text labelText = UIFactory.CreateText(row, label, 26, TextAnchor.MiddleLeft, Color.white);
        LayoutElement labelElement = labelText.gameObject.AddComponent<LayoutElement>();
        labelElement.preferredWidth = 300f;

        int index = Mathf.Clamp(current, 0, options.Length - 1);
        Button button = null;
        button = UIFactory.CreateButton(row, options[index], new Vector2(300f, 44f), () =>
        {
            index = (index + 1) % options.Length;
            Text caption = button.GetComponentInChildren<Text>();
            if (caption != null) caption.text = options[index];
            onChanged(index);
        });
        LayoutElement buttonElement = button.gameObject.AddComponent<LayoutElement>();
        buttonElement.flexibleWidth = 1f;
        buttonElement.preferredHeight = 44f;
    }

    private static void AddSliderRow(Transform parent, string label, float min, float max, float value,
        Func<float, string> format, UnityAction<float> onChanged)
    {
        RectTransform row = UIFactory.CreateRect("Row", parent);
        LayoutElement rowElement = row.gameObject.AddComponent<LayoutElement>();
        rowElement.preferredHeight = 50f;

        HorizontalLayoutGroup horizontal = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        horizontal.spacing = 16f;
        horizontal.childAlignment = TextAnchor.MiddleLeft;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = true;

        Text labelText = UIFactory.CreateText(row, label, 26, TextAnchor.MiddleLeft, Color.white);
        LayoutElement labelElement = labelText.gameObject.AddComponent<LayoutElement>();
        labelElement.preferredWidth = 300f;

        Text valueText = null;
        Slider slider = UIFactory.CreateSlider(row, min, max, value, v =>
        {
            if (valueText != null) valueText.text = format(v);
            onChanged(v);
        });
        LayoutElement sliderElement = slider.gameObject.AddComponent<LayoutElement>();
        sliderElement.flexibleWidth = 1f;
        sliderElement.preferredHeight = 30f;

        valueText = UIFactory.CreateText(row, format(value), 26, TextAnchor.MiddleRight, Color.white);
        LayoutElement valueElement = valueText.gameObject.AddComponent<LayoutElement>();
        valueElement.preferredWidth = 90f;
    }
}
