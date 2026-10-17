using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Предмет, который можно взять в руки и рассмотреть (письмо, картина).
///  E          — взять / положить обратно (также Q)
///  ПКМ + мышь — вращать предмет (камера на это время не поворачивается)
/// Предмет не становится дочерним камеры: у префаба игрока неравномерный масштаб,
/// который искажал бы вложенные объекты. Вместо этого он плавно следует за камерой.
/// </summary>
public abstract class HoldableItem : Interactable
{
    private enum State { Resting, Held, Returning }

    private const float TouchRotateDegreesPerInch = 160f;

    [Header("Удержание в руках")]
    [SerializeField] protected float holdDistance = 0.6f;
    [SerializeField] protected Vector3 holdOffset = new Vector3(0f, -0.03f, 0f);
    [SerializeField] protected Vector3 heldEuler = Vector3.zero;
    [SerializeField] private float followSpeed = 14f;
    [SerializeField] private float returnDuration = 0.3f;
    [SerializeField] private float rotateSensitivity = 0.25f;
    [SerializeField] private AudioClip putDownSound;

    private State _state = State.Resting;
    private Transform _cam;
    private PlayerInteractor _owner;
    private Vector3 _homePos;
    private Quaternion _homeRot;
    private Vector3 _fromPos;
    private Quaternion _fromRot;
    private float _returnT;
    private float _yaw;
    private float _pitch;

    public bool IsHeld { get { return _state == State.Held; } }

    public void ConfigureHold(float distance, Vector3 offset, Vector3 extraEuler)
    {
        holdDistance = distance;
        holdOffset = offset;
        heldEuler = extraEuler;
    }

    public virtual string GetHeldHint()
    {
        if (PlatformUtil.UseTouchControls)
            return GameSettings.Tr("«Положить» — убрать    Тяните по экрану — вращать", "«Put back» — release    Drag the screen — rotate");
        return GameSettings.Tr("E — положить    ПКМ + мышь — вращать", "E — put back    RMB + mouse — rotate");
    }

    public override bool CanInteract()
    {
        return _state == State.Resting && base.CanInteract();
    }

    public override void Interact()
    {
        if (_state != State.Resting) return;

        PlayerInteractor who = PlayerInteractor.Current;
        if (who == null || who.PlayerCamera == null || who.HeldItem != null) return;

        _owner = who;
        _cam = who.PlayerCamera.transform;
        _homePos = transform.position;   // запоминаем позу покоя (на столе)
        _homeRot = transform.rotation;
        _yaw = 0f;
        _pitch = 0f;

        SetCollidersEnabled(false);      // в руках предмет не должен ни во что упираться и перекрывать луч
        _state = State.Held;
        MobileInput.InspectMode = true;  // телефон: перетаскивание по экрану вращает предмет
        MobileInput.ConsumeInspect();
        who.SetHeldItem(this);

        PlayClip(interactionSound, interactionSoundVolume);
        if (oneTimeInteraction) _hasInteracted = true;
        OnPickedUp();
    }

    public void Release()
    {
        if (_state != State.Held) return;

        _state = State.Returning;
        MobileInput.InspectMode = false;
        _returnT = 0f;
        _fromPos = transform.position;
        _fromRot = transform.rotation;

        if (_owner != null)
        {
            if (_owner.Movement != null) _owner.Movement.SetLookEnabled(true);
            _owner.SetHeldItem(null);
        }

        PlayClip(putDownSound, interactionSoundVolume);
        OnReleased();
    }

    protected virtual void OnPickedUp() { }
    protected virtual void OnReleased() { }

    protected virtual void OnDisable()
    {
        if (_state == State.Resting) return;

        // Если предмет выключили/уничтожили в руках — аккуратно отдаём игроку управление.
        if (_state == State.Held) MobileInput.InspectMode = false;
        if (_state == State.Held && _owner != null)
        {
            if (_owner.Movement != null) _owner.Movement.SetLookEnabled(true);
            _owner.SetHeldItem(null);
        }

        transform.SetPositionAndRotation(_homePos, _homeRot);
        SetCollidersEnabled(true);
        _state = State.Resting;
    }

    private void LateUpdate()
    {
        if (_state == State.Held) UpdateHeld();
        else if (_state == State.Returning) UpdateReturn();
    }

    private void UpdateHeld()
    {
        if (_cam == null)
        {
            Release();
            return;
        }

        bool rotating = false;
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed && Time.timeScale > 0f)
        {
            rotating = true;
            Vector2 d = mouse.delta.ReadValue();
            _yaw -= d.x * rotateSensitivity;
            _pitch = Mathf.Clamp(_pitch + d.y * rotateSensitivity, -85f, 85f);
        }
        // Телефон: перетаскивание по экрану (в градусах на дюйм, по dpi).
        Vector2 touch = MobileInput.ConsumeInspect();
        if (touch.sqrMagnitude > 0f && Time.timeScale > 0f)
        {
            float degPerPixel = TouchRotateDegreesPerInch / MobileInput.Dpi;
            _yaw -= touch.x * degPerPixel;
            _pitch = Mathf.Clamp(_pitch + touch.y * degPerPixel, -85f, 85f);
        }

        if (_owner != null && _owner.Movement != null) _owner.Movement.SetLookEnabled(!rotating);

        // Не даём предмету проваливаться в стену, если игрок стоит вплотную.
        float dist = holdDistance;
        RaycastHit hit;
        if (_owner != null && _owner.RaycastIgnoringPlayer(_cam.position, _cam.forward, holdDistance + 0.15f, out hit))
        {
            dist = Mathf.Max(0.3f, hit.distance - 0.15f);
        }

        Vector3 targetPos = _cam.position + _cam.forward * dist + _cam.TransformDirection(holdOffset);

        // Лицевая сторона предметов — -Z (как у Quad), то есть нормаль смотрит на игрока, когда
        // предмет повёрнут так же, как камера. Раньше здесь был лишний разворот на 180° — в руки брали «спиной».
        Quaternion targetRot = _cam.rotation
                               * Quaternion.Euler(_pitch, _yaw, 0f)
                               * Quaternion.Euler(heldEuler);

        float k = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPos, k);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, k);
    }

    private void UpdateReturn()
    {
        _returnT += Time.deltaTime / Mathf.Max(0.01f, returnDuration);
        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_returnT));
        transform.position = Vector3.Lerp(_fromPos, _homePos, t);
        transform.rotation = Quaternion.Slerp(_fromRot, _homeRot, t);

        if (_returnT >= 1f)
        {
            transform.SetPositionAndRotation(_homePos, _homeRot);
            SetCollidersEnabled(true);
            _state = State.Resting;
        }
    }

    private void SetCollidersEnabled(bool value)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = value;
    }

    private void PlayClip(AudioClip clip, float volume)
    {
        if (clip == null) return;
        Vector3 at = _cam != null ? _cam.position : transform.position;
        AudioSource.PlayClipAtPoint(clip, at, volume * GameSettings.SfxVolume);
    }
}
