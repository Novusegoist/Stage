using UnityEngine;

/// <summary>
/// Танцующий персонаж. Включается/выключается DanceManager'ом.
/// Анимация — состояние по умолчанию в Animator Controller (у всех шести это «Dance»).
/// Поворот к игроку выполняется ТОЛЬКО здесь (раньше этим одновременно занимались
/// DancingNPC, NPCRotation и DanceManager и перебивали друг друга).
/// </summary>
[DisallowMultipleComponent]
public class DancingNPC : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private Animator npcAnimator;
    [SerializeField] private float danceSpeed = 1f;
    [Tooltip("Имя состояния. Если пусто — играет состояние по умолчанию.")]
    [SerializeField] private string danceState = "";
    [Tooltip("Танцор не должен «уезжать» со сцены из-за root motion.")]
    [SerializeField] private bool disableRootMotion = true;

    [Header("Movement")]
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private bool facePlayer = true;

    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem danceParticles;
    [SerializeField] private Light danceLight;
    [SerializeField] private float lightIntensity = 2f;
    [SerializeField] private Color lightColor = Color.blue;

    private Transform _target;
    private bool _dancing;
    private float _lightTimer;
    private Transform _animTransform;
    private Vector3 _animLocalPos;
    private Quaternion _animLocalRot;

    private void Awake()
    {
        if (npcAnimator == null) npcAnimator = GetComponentInChildren<Animator>(true);

        if (npcAnimator != null)
        {
            _animTransform = npcAnimator.transform;
            _animLocalPos = _animTransform.localPosition;
            _animLocalRot = _animTransform.localRotation;
            if (disableRootMotion) npcAnimator.applyRootMotion = false;
        }

        if (danceLight != null)
        {
            danceLight.color = lightColor;
            danceLight.intensity = 0f;
        }
    }

    public void StartDancing(Transform lookTarget)
    {
        _target = lookTarget;
        gameObject.SetActive(true);

        // Вернуть дочерний аниматор на место (на случай дрейфа от root motion при повторных запусках).
        if (_animTransform != null && _animTransform != transform)
        {
            _animTransform.localPosition = _animLocalPos;
            _animTransform.localRotation = _animLocalRot;
        }

        if (npcAnimator != null)
        {
            npcAnimator.speed = danceSpeed;
            npcAnimator.Rebind(); // танец всегда начинается с первого кадра — в такт музыке
            if (!string.IsNullOrEmpty(danceState)) npcAnimator.Play(danceState, 0, 0f);
            npcAnimator.Update(0f);
        }

        if (danceParticles != null) danceParticles.Play();

        _lightTimer = 0f;
        _dancing = true;
    }

    public void StopDancing()
    {
        _dancing = false;

        if (danceParticles != null) danceParticles.Stop();
        if (danceLight != null) danceLight.intensity = 0f;

        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!_dancing) return;

        if (facePlayer && _target != null)
        {
            Vector3 dir = _target.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);
            }
        }

        if (danceLight != null)
        {
            _lightTimer += Time.deltaTime;
            danceLight.intensity = Mathf.Abs(Mathf.Sin(_lightTimer * 3f)) * lightIntensity;
        }
    }
}
