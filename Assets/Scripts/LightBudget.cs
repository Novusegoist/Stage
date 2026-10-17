using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Бюджет освещения для слабых устройств. В сцене Stage около 12 динамических источников (прожекторы и точечные),
/// а телефонам и URP в режиме Forward больше нескольких на объект не нужно.
/// Когда «Графика» = Низкое или Среднее (на телефоне это по умолчанию) компонент:
///   • выключает тени у всех источников,
///   • оставляет включёнными только N ближайших источников, чьи зоны действия попадают в кадр (Низкое — 4, Среднее — 6).
/// На «Высоком» (ПК) ничего не делает и возвращает исходные настройки. Факелы лабиринта управляются TorchLightCuller.
/// </summary>
public class LightBudget : MonoBehaviour
{
    private sealed class Entry
    {
        public Light Light;
        public LightShadows Shadows;
    }

    private const float Interval = 0.4f;

    private readonly List<Entry> _entries = new List<Entry>();
    private readonly Plane[] _planes = new Plane[6];
    private float[] _keys = new float[0];
    private int[] _order = new int[0];
    private float _timer;
    private float _collectAt = -1f;
    private bool _managing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<LightBudget>() != null) return;
        GameObject go = new GameObject("LightBudget");
        DontDestroyOnLoad(go);
        go.AddComponent<LightBudget>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Collect();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _entries.Clear();
        _managing = false;
        _collectAt = Time.unscaledTime + 0.4f;   // даём сцене и генераторам создать свои объекты
    }

    private void Collect()
    {
        _entries.Clear();
        Light[] all = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Light l = all[i];
            if (l.type == LightType.Directional || !l.enabled) continue;
            if (l.GetComponentInParent<TorchLightCuller>() != null) continue;   // факелы и лампы лабиринта — отдельно
            _entries.Add(new Entry { Light = l, Shadows = l.shadows });
        }
    }

    private void Update()
    {
        if (_collectAt > 0f && Time.unscaledTime >= _collectAt)
        {
            _collectAt = -1f;
            Collect();
        }

        _timer -= Time.unscaledDeltaTime;
        if (_timer > 0f) return;
        _timer = Interval;

        int quality = GraphicsQuality.Effective;
        if (quality >= GraphicsQuality.High)
        {
            if (_managing) RestoreAll();
            return;
        }

        Camera cam = Camera.main;
        if (cam == null || _entries.Count == 0) return;

        _managing = true;
        int limit = quality == GraphicsQuality.Low ? 4 : 6;

        GeometryUtility.CalculateFrustumPlanes(cam, _planes);
        Vector3 camPos = cam.transform.position;

        int count = _entries.Count;
        if (_keys.Length != count)
        {
            _keys = new float[count];
            _order = new int[count];
        }

        for (int i = 0; i < count; i++)
        {
            _order[i] = i;
            Light l = _entries[i].Light;
            if (l == null)
            {
                _keys[i] = float.MaxValue;
                continue;
            }

            l.shadows = LightShadows.None;

            // Источник «виден», если его зона действия пересекает пирамиду камеры.
            Bounds area = new Bounds(l.transform.position, Vector3.one * (l.range * 2f));
            bool visible = GeometryUtility.TestPlanesAABB(_planes, area);
            _keys[i] = visible ? (l.transform.position - camPos).sqrMagnitude : float.MaxValue;
        }

        System.Array.Sort(_keys, _order, 0, count);   // ближайшие — первыми

        for (int rank = 0; rank < count; rank++)
        {
            Light l = _entries[_order[rank]].Light;
            if (l == null) continue;

            bool on = rank < limit && _keys[rank] < float.MaxValue;
            if (l.enabled != on) l.enabled = on;
        }
    }

    private void RestoreAll()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            Entry e = _entries[i];
            if (e.Light == null) continue;
            e.Light.enabled = true;
            e.Light.shadows = e.Shadows;
        }
        _managing = false;
    }
}
