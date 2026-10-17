using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Единственная система взаимодействия игрока:
/// луч из камеры → Interactable → подсказка → E. Также управляет предметом в руках.
/// Подсказка берётся из префаба игрока (Canvas/Panel/Text (TMP)); если её нет — создаётся запасная.
/// </summary>
[DisallowMultipleComponent]
public class PlayerInteractor : MonoBehaviour
{
    private static PlayerInteractor _current;

    /// <summary>
    /// Активный игрок. Ссылка перенаходится сама: при переходе Stage → MazeScene лишний игрок из сцены
    /// удаляется, и его OnDestroy не должен «обнулять» настоящего (который пришёл из Stage).
    /// </summary>
    public static PlayerInteractor Current
    {
        get
        {
            if (_current == null || !_current.isActiveAndEnabled)
                _current = FindFirstObjectByType<PlayerInteractor>();
            return _current;
        }
    }

    [Header("Взаимодействие")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private float checkInterval = 0.05f;

    [Header("Подсказка (находится автоматически)")]
    [SerializeField] private GameObject interactHint;
    [SerializeField] private TMP_Text interactText;

    [Header("Камера")]
    [SerializeField] private Camera playerCamera;

    [Header("Луч")]
    [Tooltip("Радиус «помощи прицеливания» на телефоне: пальцем трудно навестись точно в центр экрана.")]
    [SerializeField] private float touchAimAssistRadius = 0.15f;

    [Tooltip("Если включено — предмет за стеной/любым коллайдером нельзя нажать. " +
             "По умолчанию выключено: в Stage перед кнопками стоят посторонние коллайдеры (основания, стены), и они перехватывали бы луч.")]
    [SerializeField] private bool blockedByColliders = false;

    [Header("Диагностика")]
    [Tooltip("Показывает в углу экрана, во что упирается луч взаимодействия.")]
    [SerializeField] private bool debugOverlay = false;

    private Interactable _focused;
    private HoldableItem _held;
    private PlayerMovement _movement;
    private InputAction _interactAction;
    private Text _fallbackText;
    private string _shownText;
    private float _nextCheck;
    private readonly RaycastHit[] _hits = new RaycastHit[16];
    private string _debugInfo = "";
    private bool _touchInteract;

    public Camera PlayerCamera { get { return playerCamera; } }
    public HoldableItem HeldItem { get { return _held; } }
    public PlayerMovement Movement { get { return _movement; } }

    private void Awake()
    {
        _current = this;
        _movement = GetComponent<PlayerMovement>();
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();

        PlayerInput input = GetComponent<PlayerInput>();
        if (input != null && input.actions != null) _interactAction = input.actions.FindAction("Interact", false);

        ResolveHintUI();
        HideHint();
    }

    private void OnDestroy()
    {
        if (_current == this) _current = null;
    }

    private void Update()
    {
        // Нажатие кнопки «Действие» на экране; забираем до проверки паузы, чтобы оно не сработало позже.
        _touchInteract = MobileInput.ConsumeInteract();

        if (Time.timeScale <= 0f) return; // пауза

        if (_held != null)
        {
            if (InteractPressed() || DropPressed()) _held.Release();
            return;
        }

        if (Time.unscaledTime >= _nextCheck)
        {
            _nextCheck = Time.unscaledTime + checkInterval;
            UpdateFocus();
        }

        if (_focused != null && InteractPressed())
        {
            _focused.Interact();
            _nextCheck = 0f; // сразу перепроверить фокус и обновить подсказку
        }
    }

    private void UpdateFocus()
    {
        _focused = FindInteractable();

        if (_focused != null) ShowHint(_focused.GetInteractionText());
        else HideHint();
    }

    /// <summary>
    /// Ближайший Interactable на линии взгляда. Коллайдеры самого игрока пропускаются
    /// (капсула CharacterController не должна перехватывать луч из камеры).
    /// </summary>
    private Interactable FindInteractable()
    {
        if (playerCamera == null)
        {
            if (debugOverlay) _debugInfo = "нет камеры";
            return null;
        }

        Transform cam = playerCamera.transform;
        int count = (PlatformUtil.UseTouchControls && touchAimAssistRadius > 0f)
            ? Physics.SphereCastNonAlloc(cam.position, touchAimAssistRadius, cam.forward, _hits, interactDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            : Physics.RaycastNonAlloc(cam.position, cam.forward, _hits, interactDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        SortHitsByDistance(count);

        string firstOther = null;
        for (int i = 0; i < count; i++)
        {
            Collider col = _hits[i].collider;
            if (col == null || col.transform.IsChildOf(transform)) continue;

            Interactable found = col.GetComponentInParent<Interactable>();
            if (found != null)
            {
                bool ok = found.CanInteract();
                if (debugOverlay) _debugInfo = (ok ? "OK: " : "недоступно: ") + found.name + " (" + _hits[i].distance.ToString("F1") + " м)";
                return ok ? found : null;
            }

            if (firstOther == null) firstOther = col.name;
            if (blockedByColliders)
            {
                if (debugOverlay) _debugInfo = "перекрыто: " + col.name;
                return null;
            }
        }

        if (debugOverlay)
            _debugInfo = firstOther == null ? "луч ни во что не попал" : "нет Interactable (впереди: " + firstOther + ")";
        return null;
    }

    /// <summary>Луч для других систем (удержание предмета): не попадает в самого игрока.</summary>
    public bool RaycastIgnoringPlayer(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
    {
        int count = Physics.RaycastNonAlloc(origin, direction, _hits, distance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        SortHitsByDistance(count);

        for (int i = 0; i < count; i++)
        {
            Collider col = _hits[i].collider;
            if (col == null || col.transform.IsChildOf(transform)) continue;
            hit = _hits[i];
            return true;
        }

        hit = default(RaycastHit);
        return false;
    }

    private void SortHitsByDistance(int count)
    {
        for (int i = 1; i < count; i++)
        {
            RaycastHit key = _hits[i];
            int j = i - 1;
            while (j >= 0 && _hits[j].distance > key.distance)
            {
                _hits[j + 1] = _hits[j];
                j--;
            }
            _hits[j + 1] = key;
        }
    }

    private void OnGUI()
    {
        if (!debugOverlay) return;
        string held = _held != null ? " | в руках: " + _held.name : "";
        GUI.Label(new Rect(20f, 20f, 900f, 30f), "[Interact] " + _debugInfo + held);
    }

    /// <summary>Вызывается предметом, когда его берут в руки / кладут обратно.</summary>
    public void SetHeldItem(HoldableItem item)
    {
        _held = item;
        _focused = null;
        _nextCheck = 0f;

        if (item != null) ShowHint(item.GetHeldHint());
        else HideHint();
    }

    // ---------- Ввод ----------

    private bool InteractPressed()
    {
        if (_touchInteract) return true;
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame) return true;
        return _interactAction != null && _interactAction.WasPressedThisFrame();
    }

    private static bool DropPressed()
    {
        Keyboard kb = Keyboard.current;
        return kb != null && kb.qKey.wasPressedThisFrame;
    }

    // ---------- Подсказка ----------

    private void ResolveHintUI()
    {
        if (interactText == null) interactText = GetComponentInChildren<TMP_Text>(true);

        if (interactText != null && interactHint == null)
        {
            Transform parent = interactText.transform.parent;
            bool parentIsPanel = parent != null && parent.GetComponent<Canvas>() == null;
            interactHint = parentIsPanel ? parent.gameObject : interactText.gameObject;
        }

        if (interactText == null) BuildFallbackHint();
    }

    private void BuildFallbackHint()
    {
        Canvas canvas = UIFactory.CreateCanvas("InteractHintCanvas", 10);
        canvas.transform.SetParent(transform, false); // живёт и умирает вместе с игроком (раньше утекал между сценами)

        Image panel = UIFactory.CreateImage(canvas.transform, "InteractHint", new Color(0f, 0f, 0f, 0.65f));
        RectTransform rt = (RectTransform)panel.transform;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(620f, 64f);
        rt.anchoredPosition = new Vector2(0f, 120f);

        _fallbackText = UIFactory.CreateText(panel.transform, "", 28, TextAnchor.MiddleCenter, Color.white);
        UIFactory.Stretch((RectTransform)_fallbackText.transform);

        interactHint = panel.gameObject;
    }

    private void ShowHint(string message)
    {
        if (interactHint == null) return;
        message = PlatformUtil.AdaptHint(message);   // на телефоне «E — …» заменяется названием кнопки

        if (message != _shownText)
        {
            _shownText = message;
            if (interactText != null) interactText.text = message;
            else if (_fallbackText != null) _fallbackText.text = message;
        }

        if (!interactHint.activeSelf) interactHint.SetActive(true);
    }

    private void HideHint()
    {
        _shownText = null;
        if (interactHint != null && interactHint.activeSelf) interactHint.SetActive(false);
    }
}
