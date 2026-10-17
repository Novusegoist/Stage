using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// В лабиринте полсотни факелов, и у каждого точечный свет — рендерить их все дорого (и URP всё равно
/// использует лишь несколько ближайших на объект). Компонент оставляет включёнными свет у N ближайших
/// к игроку факелов, а пламя/дым — у факелов в пределах effectRadius. Остальное выключено.
/// </summary>
public class TorchLightCuller : MonoBehaviour
{
    private sealed class Entry
    {
        public Transform Anchor;
        public Light[] Lights;
        public GameObject[] Effects;
        public bool LightsOn;
        public bool EffectsOn;
    }

    private float _lightRadius = 26f;
    private float _effectRadius = 40f;
    private int _maxLights = 10;
    private const float Interval = 0.25f;

    private readonly List<Entry> _entries = new List<Entry>();
    private float[] _keys = new float[0];
    private int[] _order = new int[0];
    private float _timer;
    private Transform _target;

    public void Configure(float lightRadius, int maxLights)
    {
        _lightRadius = Mathf.Max(1f, lightRadius);
        _effectRadius = _lightRadius * 1.5f;
        _maxLights = Mathf.Max(1, maxLights);
    }

    public void Register(GameObject torch)
    {
        Entry e = new Entry();
        e.Anchor = torch.transform;
        e.Lights = torch.GetComponentsInChildren<Light>(true);

        // Только «верхние» системы частиц: дым вложен в пламя и выключится вместе с ним.
        List<GameObject> effects = new List<GameObject>();
        ParticleSystem[] systems = torch.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            Transform parent = systems[i].transform.parent;
            if (parent != null && parent.GetComponentInParent<ParticleSystem>() != null) continue;
            effects.Add(systems[i].gameObject);
        }
        e.Effects = effects.ToArray();

        // До первой проверки всё выключено.
        for (int i = 0; i < e.Lights.Length; i++) e.Lights[i].enabled = false;
        for (int i = 0; i < e.Effects.Length; i++) e.Effects[i].SetActive(false);

        _entries.Add(e);
        _timer = 0f;
    }

    private void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = Interval;

        if (_target == null)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            _target = cam.transform;
        }

        int count = _entries.Count;
        if (count == 0) return;
        if (_keys.Length != count)
        {
            _keys = new float[count];
            _order = new int[count];
        }

        Vector3 p = _target.position;
        for (int i = 0; i < count; i++)
        {
            _keys[i] = (_entries[i].Anchor.position - p).sqrMagnitude;
            _order[i] = i;
        }
        System.Array.Sort(_keys, _order, 0, count);   // ближайшие — первыми

        float lightSqr = _lightRadius * _lightRadius;
        float effectSqr = _effectRadius * _effectRadius;
        for (int rank = 0; rank < count; rank++)
        {
            Entry e = _entries[_order[rank]];
            bool lightOn = rank < _maxLights && _keys[rank] <= lightSqr;
            bool effectsOn = _keys[rank] <= effectSqr;

            if (e.LightsOn != lightOn)
            {
                for (int i = 0; i < e.Lights.Length; i++) e.Lights[i].enabled = lightOn;
                e.LightsOn = lightOn;
            }
            if (e.EffectsOn != effectsOn)
            {
                for (int i = 0; i < e.Effects.Length; i++) e.Effects[i].SetActive(effectsOn);
                e.EffectsOn = effectsOn;
            }
        }
    }
}
