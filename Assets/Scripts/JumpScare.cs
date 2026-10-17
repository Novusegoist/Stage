using System.Collections;
using UnityEngine;

/// <summary>
/// Скример в духе «картонных вырезов»: вырез прячется внутри стены, а когда игрок подходит, резко
/// выскакивает в коридор, разворачивается к нему и орёт — со вспышкой света и тряской камеры. Срабатывает один раз.
/// Создаётся генератором лабиринта (MazeRoomGenerator.PlaceJumpScares). Отключается пунктом «Скримеры» в настройках.
/// </summary>
public class JumpScare : MonoBehaviour
{
    private const float PopDistance = 1.9f;
    private const float PopTime = 0.14f;
    private const float HoldTime = 1.3f;
    private const float RetractTime = 0.3f;

    private Vector3 _inward;
    private Vector3 _hiddenPos;
    private Quaternion _hiddenRot;
    private Transform _cutout;
    private SpriteRenderer _renderer;
    private Light _flash;
    private AudioSource _audio;
    private BoxCollider _trigger;
    private AudioClip _clip;
    private int _variant;
    private int _clipVariant;
    private bool _fired;

    private Transform _shakeCamera;
    private Vector3 _shakeBase;
    private bool _shaking;

    /// <summary>
    /// Вызывать после того, как объект поставлен в точку на внутренней грани стены (на уровне пола).
    /// inward — нормаль внутрь коридора. Картинка и звук создаются чуть позже (loadDelayFrames),
    /// чтобы не собирать всё в один кадр при построении лабиринта.
    /// </summary>
    public void Setup(Vector3 inward, float wallThickness, int variant, int clipVariant, int loadDelayFrames)
    {
        _inward = inward;
        _variant = variant;
        _clipVariant = clipVariant;

        // Вырез — внутри стены, чуть за её гранью: из коридора его не видно.
        float tuck = Mathf.Clamp(wallThickness * 0.3f, 0.03f, 0.08f);
        _hiddenPos = transform.position - inward * tuck;
        _hiddenRot = Quaternion.LookRotation(-inward, Vector3.up);

        GameObject cutoutGo = new GameObject("Cutout");
        cutoutGo.transform.SetParent(transform, true);
        cutoutGo.transform.SetPositionAndRotation(_hiddenPos, _hiddenRot);
        _cutout = cutoutGo.transform;
        _renderer = cutoutGo.AddComponent<SpriteRenderer>();

        GameObject flashGo = new GameObject("Flash");
        flashGo.transform.SetParent(transform, true);
        flashGo.transform.position = transform.position + inward * 1.0f + Vector3.up * 2.2f;
        _flash = flashGo.AddComponent<Light>();
        _flash.type = LightType.Point;
        _flash.color = new Color(1f, 0.85f, 0.75f);
        _flash.range = 9f;
        _flash.intensity = 7f;
        _flash.shadows = LightShadows.None;
        _flash.enabled = false;

        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0.35f;
        _audio.minDistance = 5f;
        _audio.maxDistance = 60f;
        _audio.rolloffMode = AudioRolloffMode.Linear;

        // Зона срабатывания: перед стеной, ~3 м по глубине и ~3 м вдоль стены.
        _trigger = gameObject.AddComponent<BoxCollider>();
        _trigger.isTrigger = true;
        bool alongX = Mathf.Abs(inward.x) > 0.5f;
        _trigger.size = alongX ? new Vector3(3.4f, 4f, 3f) : new Vector3(3f, 4f, 3.4f);
        _trigger.center = inward * 1.9f + Vector3.up * 2f;

        StartCoroutine(LoadAssets(loadDelayFrames));
    }

    private IEnumerator LoadAssets(int delayFrames)
    {
        for (int i = 0; i < delayFrames; i++) yield return null;
        _renderer.sprite = ScareAssets.GetSprite(_variant);
        yield return null;
        _clip = ScareAssets.GetClip(_clipVariant);
    }

    private void OnDisable()
    {
        if (_shaking && _shakeCamera != null) _shakeCamera.localPosition = _shakeBase;
        _shaking = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_fired || !other.CompareTag("Player")) return;
        if (!GameSettings.JumpScaresEnabled) return;   // игрок мог выключить скримеры в настройках

        _fired = true;
        StartCoroutine(Run(other.transform));
    }

    private IEnumerator Run(Transform player)
    {
        // Если картинка ещё не успела создаться (почти невозможно) — делаем сейчас.
        if (_renderer.sprite == null) _renderer.sprite = ScareAssets.GetSprite(_variant);
        if (_clip == null) _clip = ScareAssets.GetClip(_clipVariant);

        Vector3 shown = _hiddenPos + _inward * PopDistance;

        _audio.PlayOneShot(_clip, GameSettings.SfxVolume);
        _flash.enabled = true;
        SceneFlow.Flash(new Color(1f, 0.95f, 0.9f), 0.55f, 0.18f);
        StartCoroutine(ShakeCamera(0.45f, 0.06f));

        // 1) Рывок из стены: быстро, с «перелётом», на ходу разворачиваясь к игроку.
        for (float t = 0f; t < PopTime; t += Time.deltaTime)
        {
            float k = EaseOutBack(t / PopTime);
            _cutout.position = Vector3.LerpUnclamped(_hiddenPos, shown, k);
            _cutout.rotation = Quaternion.Slerp(_hiddenRot, FaceRotation(shown, player), Mathf.Clamp01(k));
            yield return null;
        }

        // 2) Удержание: трясётся и следит за игроком.
        for (float t = 0f; t < HoldTime; t += Time.deltaTime)
        {
            Vector3 jitter = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * 0.025f;
            float wobble = Mathf.Sin(t * 32f) * 3f;
            _cutout.position = shown + jitter;
            _cutout.rotation = FaceRotation(shown, player) * Quaternion.Euler(0f, 0f, wobble);
            yield return null;
        }

        // 3) Обратно в стену.
        _flash.enabled = false;
        Quaternion from = _cutout.rotation;
        for (float t = 0f; t < RetractTime; t += Time.deltaTime)
        {
            float k = (t / RetractTime) * (t / RetractTime);
            _cutout.position = Vector3.Lerp(shown, _hiddenPos, k);
            _cutout.rotation = Quaternion.Slerp(from, _hiddenRot, k);
            yield return null;
        }

        _cutout.gameObject.SetActive(false);
        _trigger.enabled = false;
    }

    // Лицом к игроку (только по горизонтали): «вперёд» у спрайта направлен от зрителя.
    private Quaternion FaceRotation(Vector3 shown, Transform player)
    {
        Vector3 away = shown - player.position;
        away.y = 0f;
        return away.sqrMagnitude > 0.01f ? Quaternion.LookRotation(away.normalized, Vector3.up) : _hiddenRot;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = Mathf.Clamp01(t) - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    private IEnumerator ShakeCamera(float duration, float amplitude)
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;

        _shakeCamera = cam.transform;
        _shakeBase = _shakeCamera.localPosition;
        _shaking = true;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float fade = 1f - t / duration;
            _shakeCamera.localPosition = _shakeBase + Random.insideUnitSphere * (amplitude * fade);
            yield return null;
        }

        _shakeCamera.localPosition = _shakeBase;
        _shaking = false;
    }
}
