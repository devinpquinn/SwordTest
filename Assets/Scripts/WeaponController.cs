using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class WeaponController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private bool isPlayer = true;
    [SerializeField] private Transform weaponTarget;
    [SerializeField] private float lerpSpeed = 50f;
    
    [Header("Slash")]
    [SerializeField] private Transform weaponObject;
    [SerializeField] private LineRenderer slashLineRenderer;
    [SerializeField] private TrailRenderer slashTrailRenderer;
    [SerializeField] private float slashDuration = 0.5f;
    [SerializeField] private Ease slashEaseType = Ease.OutSine;

    [Header("Block")]
    [SerializeField] private LayerMask blockLayers = ~0;
    [SerializeField] private float bounceBackDuration = 0.2f;
    [SerializeField] private float minBounceBackDistance = 1f;
    [SerializeField] private float maxBounceBackDistance = 3f;
    [SerializeField] private float minBounceBackSpeed = 0f;
    [SerializeField] private float maxBounceBackSpeed = 40f;
    [SerializeField] private Ease bounceBackEaseType = Ease.OutQuad;
    [SerializeField] private float blockSweepRadius = 0.1f;

    [Header("Block Object")]
    [SerializeField] private Transform blockObject;
    [SerializeField] private LineRenderer blockLineRenderer;
    [SerializeField] private float blockObjectThickness = 0.2f;
    [SerializeField] private float minBlockLength = 0.1f;

    [Header("Stamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float slashStaminaCost = 25f;
    [SerializeField] private float staminaRecoveryRate = 20f;
    [SerializeField] private Image staminaBar;
    private float fullStaminaDurationMultiplier = 1f;
    [SerializeField] private float emptyStaminaDurationMultiplier = 2f;

    private float currentStamina;
    private Camera mainCamera;
    private bool isChargingSlash;
    private bool isExecutingSlash;
    private bool isBouncingBack;
    private bool isBlocking;
    private Vector3 blockStartPosition;
    private Vector3 blockEndPosition;
    private Vector3 slashStartPosition;
    private Vector3 slashDirection;
    private Vector3 previousWeaponPosition;
    private float slashSpeed;
    private Tween slashTween;
    private readonly RaycastHit[] sweepHits = new RaycastHit[8];

    private void Awake()
    {
        mainCamera = Camera.main;
        currentStamina = maxStamina;
        UpdateStaminaBar();

        if (isPlayer)
        {
            Cursor.visible = false;
        }

        if (weaponTarget != null && weaponObject != null)
        {
            weaponObject.position = weaponTarget.position;
            weaponObject.gameObject.SetActive(false);
        }

        if (blockObject != null)
        {
            blockObject.gameObject.SetActive(false);
        }

        if (slashLineRenderer != null)
        {
            slashLineRenderer.positionCount = 2;
            slashLineRenderer.enabled = false;
        }

        if (blockLineRenderer != null)
        {
            blockLineRenderer.positionCount = 2;
            blockLineRenderer.enabled = false;
        }

        if (weaponObject != null)
        {
            WeaponBlockRelay relay = weaponObject.GetComponent<WeaponBlockRelay>();
            if (relay == null)
            {
                relay = weaponObject.gameObject.AddComponent<WeaponBlockRelay>();
            }

            relay.Initialize(NotifyBlocked);
        }
    }

    private void Update()
    {
        if (weaponTarget == null)
        {
            return;
        }

        if (isPlayer)
        {
            UpdateMouseInput();
        }

        if (weaponObject != null && !IsBusy)
        {
            weaponObject.position = weaponTarget.position;
            weaponObject.gameObject.SetActive(false);
        }

        RecoverStamina();
        UpdateSlashLine();
        UpdateBlockLine();
    }

    public float CurrentStamina => currentStamina;

    public bool HasStaminaForSlash => currentStamina >= slashStaminaCost;

    private float NormalizedStamina => maxStamina > Mathf.Epsilon ? Mathf.Clamp01(currentStamina / maxStamina) : 0f;

    private float StaminaDurationMultiplier => Mathf.Lerp(emptyStaminaDurationMultiplier, fullStaminaDurationMultiplier, NormalizedStamina);

    private void RecoverStamina()
    {
        if (IsBusy || isBlocking || currentStamina >= maxStamina)
        {
            return;
        }

        currentStamina = Mathf.Min(currentStamina + staminaRecoveryRate * Time.deltaTime, maxStamina);
        UpdateStaminaBar();
    }

    private void UpdateStaminaBar()
    {
        if (staminaBar == null)
        {
            return;
        }

        staminaBar.fillAmount = maxStamina > Mathf.Epsilon ? currentStamina / maxStamina : 0f;
    }

    private void UpdateBlockLine()
    {
        if (blockLineRenderer == null)
        {
            return;
        }

        if (!isBlocking)
        {
            blockLineRenderer.enabled = false;
            return;
        }

        blockLineRenderer.enabled = true;
        blockLineRenderer.positionCount = 2;
        blockLineRenderer.SetPosition(0, blockStartPosition);
        blockLineRenderer.SetPosition(1, blockEndPosition);
    }

    private void UpdateSlashLine()
    {
        if (slashLineRenderer == null)
        {
            return;
        }

        if (!isChargingSlash || weaponTarget == null)
        {
            slashLineRenderer.enabled = false;
            return;
        }

        slashLineRenderer.enabled = true;
        slashLineRenderer.positionCount = 2;
        slashLineRenderer.SetPosition(0, slashStartPosition);
        slashLineRenderer.SetPosition(1, weaponTarget.position);
    }

    private void UpdateMouseInput()
    {
        if (mainCamera == null)
        {
            return;
        }

        Vector3 mouseScreenPosition = Input.mousePosition;
        mouseScreenPosition.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);

        Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);
        Vector3 targetPosition = new Vector3(mouseWorldPosition.x, mouseWorldPosition.y, weaponTarget.position.z);
        MoveTargetTowards(targetPosition);

        UpdateBlockObject(targetPosition);

        if (weaponObject == null)
        {
            return;
        }

        if (Input.GetMouseButtonDown(1) && isChargingSlash)
        {
            CancelSlashCharge();
        }

        if (Input.GetMouseButtonDown(0))
        {
            BeginSlashCharge(weaponTarget.position);
        }

        if (Input.GetMouseButtonUp(0) && isChargingSlash)
        {
            ReleaseSlash(weaponTarget.position);
        }
    }

    public bool IsBusy => isChargingSlash || isExecutingSlash || isBouncingBack;

    public event System.Action SlashFinished;

    public void MoveTargetTowards(Vector3 position)
    {
        if (weaponTarget == null)
        {
            return;
        }

        weaponTarget.position = Vector3.Lerp(weaponTarget.position, position, lerpSpeed * Time.deltaTime);
    }

    public void SetTargetPosition(Vector3 position)
    {
        if (weaponTarget == null)
        {
            return;
        }

        weaponTarget.position = position;
    }

    public void BeginSlashCharge(Vector3 startPosition)
    {
        if (weaponObject == null)
        {
            return;
        }

        slashTween?.Kill();
        slashTween = null;
        isChargingSlash = true;
        isExecutingSlash = false;
        isBouncingBack = false;
        slashStartPosition = startPosition;
        weaponObject.position = startPosition;
        weaponObject.gameObject.SetActive(true);

        if (slashTrailRenderer != null)
        {
            slashTrailRenderer.Clear();
        }

        if (weaponTarget != null)
        {
            weaponTarget.position = startPosition;
        }
    }

    public void ReleaseSlash(Vector3 endPosition)
    {
        if (weaponObject == null || weaponTarget == null)
        {
            return;
        }

        if (!HasStaminaForSlash)
        {
            CancelSlashCharge();
            return;
        }

        currentStamina = Mathf.Max(currentStamina - slashStaminaCost, 0f);
        UpdateStaminaBar();

        isChargingSlash = false;
        weaponTarget.position = endPosition;
        UpdateSlashLine();
        StartSlashTween();
    }

    public void CancelSlashCharge()
    {
        if (!isChargingSlash)
        {
            return;
        }

        slashTween?.Kill();
        slashTween = null;
        isChargingSlash = false;
        UpdateSlashLine();

        if (weaponObject != null)
        {
            if (weaponTarget != null)
            {
                weaponObject.position = weaponTarget.position;
            }

            weaponObject.gameObject.SetActive(false);
        }

        SlashFinished?.Invoke();
    }

    private void StartSlashTween()
    {
        if (weaponObject == null || weaponTarget == null)
        {
            return;
        }

        isExecutingSlash = true;
        isBouncingBack = false;
        slashDirection = (weaponTarget.position - weaponObject.position).normalized;
        previousWeaponPosition = weaponObject.position;
        slashSpeed = 0f;
        slashTween?.Kill();
        slashTween = weaponObject
            .DOMove(weaponTarget.position, slashDuration * StaminaDurationMultiplier)
            .SetEase(slashEaseType)
            .OnComplete(() =>
            {
                isExecutingSlash = false;
                weaponObject.position = weaponTarget.position;
                weaponObject.gameObject.SetActive(false);
                slashTween = null;
                SlashFinished?.Invoke();
            });
    }

    // Block object is scaled along its local X, so its pivot must sit at the end that stays on the drag start point.
    private void UpdateBlockObject(Vector3 pointerPosition)
    {
        if (Input.GetMouseButtonDown(1))
        {
            isBlocking = true;
            blockStartPosition = pointerPosition;
            blockEndPosition = pointerPosition;
            if (blockObject != null)
            {
                blockObject.gameObject.SetActive(true);
            }
        }

        if (Input.GetMouseButtonUp(1))
        {
            isBlocking = false;
            if (blockObject != null)
            {
                blockObject.gameObject.SetActive(false);
            }
        }

        if (!isBlocking)
        {
            return;
        }

        blockEndPosition = pointerPosition;

        if (blockObject == null)
        {
            return;
        }

        Vector3 delta = pointerPosition - blockStartPosition;
        blockObject.position = blockStartPosition;

        if (delta.sqrMagnitude > Mathf.Epsilon)
        {
            blockObject.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        blockObject.localScale = new Vector3(Mathf.Max(delta.magnitude, minBlockLength), blockObjectThickness, blockObjectThickness);
    }

    // Tween-driven motion teleports the transform, so sweep between frames to catch blockers the collider skipped over.
    private void LateUpdate()
    {
        if (!isExecutingSlash || weaponObject == null)
        {
            return;
        }

        Vector3 startPosition = previousWeaponPosition;
        Vector3 currentPosition = weaponObject.position;
        Vector3 delta = currentPosition - startPosition;
        previousWeaponPosition = currentPosition;

        float distance = delta.magnitude;
        if (distance <= Mathf.Epsilon)
        {
            slashSpeed = 0f;
            return;
        }

        slashSpeed = Time.deltaTime > Mathf.Epsilon ? distance / Time.deltaTime : 0f;

        int hitCount = Physics.SphereCastNonAlloc(
            startPosition,
            Mathf.Max(blockSweepRadius, 0.001f),
            delta / distance,
            sweepHits,
            distance,
            blockLayers,
            QueryTriggerInteraction.Collide);

        int closestIndex = -1;
        float closestDistance = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            if (sweepHits[i].collider.transform.IsChildOf(weaponObject))
            {
                continue;
            }

            if (sweepHits[i].distance < closestDistance)
            {
                closestDistance = sweepHits[i].distance;
                closestIndex = i;
            }
        }

        if (closestIndex < 0)
        {
            return;
        }

        weaponObject.position = startPosition + delta.normalized * closestDistance;
        previousWeaponPosition = weaponObject.position;
        NotifyBlocked(sweepHits[closestIndex].collider.gameObject);
    }

    internal void NotifyBlocked(GameObject blocker)
    {
        if (!isExecutingSlash || isBouncingBack || weaponObject == null)
        {
            return;
        }

        if (blocker == null || (blockLayers.value & (1 << blocker.layer)) == 0)
        {
            return;
        }

        slashTween?.Kill();
        slashTween = null;
        isExecutingSlash = false;
        
        // Debug.Log("Weapon speed: " + slashSpeed + " units/sec");

        Vector3 bounceDirection = slashDirection.sqrMagnitude > Mathf.Epsilon ? -slashDirection : Vector3.zero;
        float speedWeight = Mathf.InverseLerp(minBounceBackSpeed, maxBounceBackSpeed, slashSpeed);
        float speedScaledDistance = Mathf.Lerp(minBounceBackDistance, maxBounceBackDistance, speedWeight);
        Vector3 bounceTarget = weaponObject.position + bounceDirection * speedScaledDistance;

        isBouncingBack = true;
        slashTween = weaponObject
            .DOMove(bounceTarget, bounceBackDuration * StaminaDurationMultiplier)
            .SetEase(bounceBackEaseType)
            .OnComplete(() =>
            {
                isBouncingBack = false;
                slashTween = null;
                if (weaponTarget != null)
                {
                    weaponObject.position = weaponTarget.position;
                }

                weaponObject.gameObject.SetActive(false);
                SlashFinished?.Invoke();
            });
    }

    private void OnDisable()
    {
        slashTween?.Kill();
        slashTween = null;
        isChargingSlash = false;
        isExecutingSlash = false;
        isBouncingBack = false;
        isBlocking = false;
        if (weaponObject != null)
        {
            weaponObject.gameObject.SetActive(false);
        }

        if (blockObject != null)
        {
            blockObject.gameObject.SetActive(false);
        }

        if (slashLineRenderer != null)
        {
            slashLineRenderer.enabled = false;
        }
    }
}

public class WeaponBlockRelay : MonoBehaviour
{
    private System.Action<GameObject> onBlocked;

    internal void Initialize(System.Action<GameObject> blockedCallback)
    {
        onBlocked = blockedCallback;
    }

    private void OnTriggerEnter(Collider other)
    {
        onBlocked?.Invoke(other.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        onBlocked?.Invoke(other.gameObject);
    }
}
