using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Только движение и обзор. Взаимодействие вынесено в PlayerInteractor
/// (раньше оно дублировалось в двух скриптах и срабатывало дважды за нажатие).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    // Градусов на пиксель мыши при Sensitivity = 1 и ползунке настроек = 2 (значение по умолчанию).
    // С _sensitivity = 20 из префаба это ≈ 0.12°/px — обычная «комфортная» скорость.
    // Не зависит от FPS (раньше дельта мыши умножалась на deltaTime, и скорость плавала вместе с FPS).
    private const float LookScale = 0.006f;

    // Поворот камеры пальцем: градусов на дюйм перемещения при ползунке чувствительности = 2.
    // Считаем в дюймах (по dpi), чтобы управление ощущалось одинаково на экранах разной плотности.
    private const float TouchDegreesPerInch = 70f;

    [Header("References")]
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private Transform _checkGround;
    [SerializeField] private LayerMask _groundMask;
    [SerializeField] private PlayerInput _playerInput;

    [Header("Movement Settings")]
    [SerializeField] private float _checkRadiusSphere = 0.4f;
    [SerializeField] private float _checkDistance = 0.5f;
    [SerializeField] private float _gravity = -14f;
    [SerializeField] private float _speed = 4f;
    [SerializeField] private float _speedRun = 7f;
    [SerializeField] private float _jumpHeight = 2f;
    [Range(1, 100)]
    [SerializeField] private float _sensitivity = 50f;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip[] _footstepSounds;
    [SerializeField] private AudioClip _jumpSound;
    [SerializeField] private AudioClip _landSound;
    [SerializeField] private float _footstepIntervalWalk = 0.5f;
    [SerializeField] private float _footstepIntervalRun = 0.3f;
    [SerializeField] private float _minMoveThreshold = 0.1f;

    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _jumpAction;
    private InputAction _runAction;

    private float _pitch;
    private float _velocityY;
    private bool _isGrounded;
    private bool _wasGrounded;
    private bool _isMoving;
    private bool _isRunning;
    private float _footstepTimer;
    private bool _lookEnabled = true;
    private bool _moveEnabled = true;
    private Vector2 _lastMoveInput;
    private float _sensitivityScale = 1f;
    private float _nextSensitivityRead;

    private void Awake()
    {
        if (_characterController == null) _characterController = GetComponent<CharacterController>();
        if (_playerInput == null) _playerInput = GetComponent<PlayerInput>();
        if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
        if (_cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) _cameraTransform = cam.transform;
        }
    }

    private void Start()
    {
        InputActionAsset actions = _playerInput != null ? _playerInput.actions : null;
        if (actions == null || _cameraTransform == null)
        {
            Debug.LogError("[PlayerMovement] Не назначены PlayerInput/камера — движение отключено.", this);
            enabled = false;
            return;
        }

        _moveAction = actions.FindAction("Move", true);
        _lookAction = actions.FindAction("Look", true);
        _jumpAction = actions.FindAction("Jump", false);
        _runAction = actions.FindAction("Run", false);

        float x = _cameraTransform.localEulerAngles.x;
        _pitch = x > 180f ? x - 360f : x;

        SetCursorLocked(true);

        // Телефон: экранное управление живёт на игроке (джойстик, камера, кнопки).
        if (PlatformUtil.UseTouchControls && GetComponent<MobileControlsHud>() == null)
            gameObject.AddComponent<MobileControlsHud>();

        Camera cam = _cameraTransform.GetComponent<Camera>();
        if (cam != null) GraphicsQuality.ApplyToCamera(cam);
    }

    private void Update()
    {
        // Земля: состояние после прошлого Move() + (необязательно) SphereCast по маске.
        _isGrounded = _characterController.isGrounded || ProbeGround();
        if (_isGrounded && !_wasGrounded) PlayLandSound();
        _wasGrounded = _isGrounded;

        // Касания накапливаются между кадрами — забираем всегда, чтобы они не «выстреливали» позже.
        Vector2 touchLook = MobileInput.ConsumeLook();

        // Камера крутится при захваченном курсоре (ПК) или при экранном управлении (телефон).
        bool lookAllowed = Cursor.lockState == CursorLockMode.Locked || PlatformUtil.UseTouchControls;
        if (_lookEnabled && lookAllowed) Look(touchLook);

        Move();
        UpdateFootsteps();
    }

    private bool ProbeGround()
    {
        if (_groundMask.value == 0) return false; // маска пуста — SphereCast бессмысленен
        Vector3 start = transform.position + Vector3.up * 0.1f;
        return Physics.SphereCast(start, _checkRadiusSphere, Vector3.down, out RaycastHit _, _checkDistance, _groundMask);
    }

    private void Look(Vector2 touchPixels)
    {
        Vector2 look = _lookAction.ReadValue<Vector2>();

        // Мышь отдаёт смещение за кадр (FPS-независимо), стик — скорость в секунду (нужен deltaTime).
        InputControl control = _lookAction.activeControl;
        bool isDelta = control == null || control.device is Pointer;

        // PlayerPrefs читаем 4 раза в секунду, а не каждый кадр.
        if (Time.unscaledTime >= _nextSensitivityRead)
        {
            _nextSensitivityRead = Time.unscaledTime + 0.25f;
            _sensitivityScale = GameSettings.MouseSensitivity / 2f;
        }

        float sens = _sensitivity * _sensitivityScale;
        float k = isDelta ? sens * LookScale : sens * 4.5f * Time.deltaTime;

        float yaw = look.x * k;
        float pitchDelta = look.y * k;

        if (touchPixels.sqrMagnitude > 0f)
        {
            float degPerPixel = TouchDegreesPerInch / MobileInput.Dpi * _sensitivityScale;
            yaw += touchPixels.x * degPerPixel;
            pitchDelta += touchPixels.y * degPerPixel;
        }

        _pitch = Mathf.Clamp(_pitch - pitchDelta, -90f, 90f);
        _cameraTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        transform.Rotate(Vector3.up * yaw);
    }

    private void Move()
    {
        bool canMove = _moveEnabled && Time.timeScale > 0f;

        bool touchJump = MobileInput.ConsumeJump();   // забираем всегда, чтобы нажатие не «залипало»

        Vector2 input = canMove ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
        bool run = canMove && _runAction != null && _runAction.IsPressed();
        bool jump = canMove && _jumpAction != null && _jumpAction.WasPressedThisFrame();

        if (canMove && PlatformUtil.UseTouchControls)
        {
            input += MobileInput.Move;
            if (input.sqrMagnitude > 1f) input.Normalize();
            run = run || MobileInput.Run;
            jump = jump || touchJump;
        }

        _lastMoveInput = input;
        _isRunning = run;
        _isMoving = input.sqrMagnitude > _minMoveThreshold * _minMoveThreshold && _isGrounded;

        if (_isGrounded && _velocityY < 0f) _velocityY = -2f;

        if (jump && _isGrounded)
        {
            _velocityY = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
            PlayJumpSound();
        }

        _velocityY = Mathf.Max(_velocityY + _gravity * Time.deltaTime, -50f);

        Vector3 horizontal = transform.right * input.x + transform.forward * input.y;
        if (horizontal.sqrMagnitude > 1f) horizontal.Normalize();
        horizontal *= run ? _speedRun : _speed;

        // Один вызов Move на кадр (раньше было два) — корректнее определяется isGrounded на склонах.
        _characterController.Move((horizontal + Vector3.up * _velocityY) * Time.deltaTime);
    }

    private void UpdateFootsteps()
    {
        if (!_isMoving)
        {
            _footstepTimer = 0f;
            return;
        }

        _footstepTimer -= Time.deltaTime;
        if (_footstepTimer > 0f) return;

        PlayFootstep();
        _footstepTimer = _isRunning ? _footstepIntervalRun : _footstepIntervalWalk;
    }

    private void PlayFootstep()
    {
        if (_audioSource == null || _footstepSounds == null || _footstepSounds.Length == 0) return;
        AudioClip clip = _footstepSounds[Random.Range(0, _footstepSounds.Length)];
        if (clip != null) _audioSource.PlayOneShot(clip, GameSettings.SfxVolume);
    }

    private void PlayJumpSound()
    {
        if (_audioSource != null && _jumpSound != null) _audioSource.PlayOneShot(_jumpSound, GameSettings.SfxVolume);
    }

    private void PlayLandSound()
    {
        if (_audioSource != null && _landSound != null && _lastMoveInput.sqrMagnitude > 0f)
            _audioSource.PlayOneShot(_landSound, GameSettings.SfxVolume);
    }

    // ---------- Публичный API ----------

    /// <summary>Выключает поворот камеры (например, пока игрок вращает предмет в руках).</summary>
    public void SetLookEnabled(bool enabledLook) { _lookEnabled = enabledLook; }

    public void SetMoveEnabled(bool enabledMove) { _moveEnabled = enabledMove; }

    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    /// <summary>Вызывать после телепорта: сбрасывает накопленную скорость падения.</summary>
    public void ResetMotion()
    {
        _velocityY = 0f;
        _footstepTimer = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Vector3 start = transform.position + Vector3.up * 0.1f;
        Gizmos.DrawWireSphere(start + Vector3.down * _checkDistance, _checkRadiusSphere);
    }
}
