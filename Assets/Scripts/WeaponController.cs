using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class WeaponController : MonoBehaviour
{
    private const float DefaultBlockedStaminaSplit = 0.5f;

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

    [Header("Hit")]
    [SerializeField] private LayerMask heartLayers;
    [SerializeField] private float minHeartHitSpeed = 10f;

    [Header("Heart Movement")]
    [SerializeField] private Transform heartObject;
    [SerializeField] private Transform heartBoundsCenter;
    [SerializeField] private float heartMaxDistanceX = 5f;
    [SerializeField] private float heartMaxDistanceY = 3f;
    [SerializeField] private float heartMoveSpeed = 6f;
    [SerializeField] private float minHeartMoveSpeedMult = 0.5f;
    [SerializeField] private float heartSmoothTime = 0.1f;

    [Header("Block Object")]
    [SerializeField] private Transform blockObject;
    [SerializeField] private LineRenderer blockLineRenderer;
    [SerializeField] private float blockObjectThickness = 0.2f;
    [SerializeField] private float minBlockLength = 0.1f;

    [Header("Stamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float slashStaminaCost = 25f;
    [SerializeField] private float staminaRecoveryRate = 20f;
    [SerializeField] private float windupStaminaRecoveryMult = 0.25f;
    [SerializeField] private float blockStaminaRecoveryMult = 0.5f;
    [SerializeField] private float movementStaminaRecoveryMult = 0.1f;
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
    private float maxSlashSpeed;
    private float slashTravelDistance;
    private bool hasHitHeartThisSlash;
    private bool isMovingBackward;
    private bool isMoving;
    private Vector3 heartVelocity;
    private Tween slashTween;
    private readonly RaycastHit[] sweepHits = new RaycastHit[8];
    private float pendingSlashStaminaCost;
    private bool hasResolvedSlashStamina;

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
            BlockOwnerRelay owner = blockObject.GetComponent<BlockOwnerRelay>();
            if (owner == null)
            {
                owner = blockObject.gameObject.AddComponent<BlockOwnerRelay>();
            }

            owner.Initialize(this);
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
            UpdateHeartMovement();
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
        if (isExecutingSlash || isBouncingBack || currentStamina >= maxStamina)
        {
            return;
        }

        float recoveryMultiplier = 1f;
        if (isChargingSlash)
        {
            recoveryMultiplier *= windupStaminaRecoveryMult;
        }

        if (isBlocking)
        {
            recoveryMultiplier *= blockStaminaRecoveryMult;
        }

        if (isMoving)
        {
            recoveryMultiplier *= movementStaminaRecoveryMult;
        }

        currentStamina = Mathf.Min(currentStamina + staminaRecoveryRate * recoveryMultiplier * Time.deltaTime, maxStamina);
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

    // Heart position inside its bounds, mapped per axis to -1 (min edge) .. 1 (max edge).
    public Vector2 NormalizedHeartOffset
    {
        get
        {
            if (heartObject == null)
            {
                return Vector2.zero;
            }

            Vector3 center = heartBoundsCenter != null ? heartBoundsCenter.position : transform.position;
            return new Vector2(
                heartMaxDistanceX > Mathf.Epsilon ? Mathf.Clamp((heartObject.position.x - center.x) / heartMaxDistanceX, -1f, 1f) : 0f,
                heartMaxDistanceY > Mathf.Epsilon ? Mathf.Clamp((heartObject.position.y - center.y) / heartMaxDistanceY, -1f, 1f) : 0f);
        }
    }

    private void UpdateHeartMovement()
    {
        MoveHeart(new Vector3(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"), 0f));
    }

    public void MoveHeart(Vector3 input)
    {
        if (heartObject == null)
        {
            return;
        }

        input.z = 0f;
        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        isMoving = input.sqrMagnitude > Mathf.Epsilon;

        float moveSpeed = heartMoveSpeed * Mathf.Lerp(minHeartMoveSpeedMult, 1f, NormalizedStamina);
        Vector3 desiredPosition = heartObject.position + input * moveSpeed * heartSmoothTime;
        Vector3 position = Vector3.SmoothDamp(
            heartObject.position,
            desiredPosition,
            ref heartVelocity,
            heartSmoothTime,
            moveSpeed);

        Vector3 center = heartBoundsCenter != null ? heartBoundsCenter.position : transform.position;
        heartObject.position = new Vector3(
            Mathf.Clamp(position.x, center.x - heartMaxDistanceX, center.x + heartMaxDistanceX),
            Mathf.Clamp(position.y, center.y - heartMaxDistanceY, center.y + heartMaxDistanceY),
            heartObject.position.z);
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

    public bool IsChargingSlash => isChargingSlash;

    public bool IsBlocking => isBlocking;

    public Vector3 SlashStartPosition => slashStartPosition;

    public Vector3 BlockStartPosition => blockStartPosition;

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

        pendingSlashStaminaCost = slashStaminaCost;
        hasResolvedSlashStamina = false;

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
        maxSlashSpeed = 0f;
        slashTravelDistance = 0f;
        hasHitHeartThisSlash = false;
        isMovingBackward = false;
        slashTween?.Kill();
        slashTween = weaponObject
            .DOMove(weaponTarget.position, slashDuration * StaminaDurationMultiplier)
            .SetEase(slashEaseType)
            .OnComplete(() =>
            {
                ResolveSlashStaminaToSelf();
                isExecutingSlash = false;
                weaponObject.position = weaponTarget.position;
                weaponObject.gameObject.SetActive(false);
                slashTween = null;
                LogSlashStats();
                SlashFinished?.Invoke();
            });
    }

    private void LogSlashStats()
    {
        // Debug.Log($"{name} slash length: {slashTravelDistance:F2} units, max speed: {maxSlashSpeed:F2} units/sec");
    }

    private void UpdateBlockObject(Vector3 pointerPosition)
    {
        if (Input.GetMouseButtonDown(1))
        {
            BeginBlock(pointerPosition);
        }

        if (Input.GetMouseButtonUp(1))
        {
            EndBlock();
        }

        UpdateBlockDrag(pointerPosition);
    }

    public void BeginBlock(Vector3 startPosition)
    {
        isBlocking = true;
        blockStartPosition = startPosition;
        blockEndPosition = startPosition;

        if (blockObject != null)
        {
            blockObject.position = startPosition;
            blockObject.localScale = new Vector3(minBlockLength, blockObjectThickness, blockObjectThickness);
            blockObject.gameObject.SetActive(true);
        }
    }

    // Block object is scaled along its local X, so its pivot must sit at the end that stays on the drag start point.
    public void UpdateBlockDrag(Vector3 pointerPosition)
    {
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

    public void EndBlock()
    {
        if (!isBlocking)
        {
            return;
        }

        isBlocking = false;
        if (blockObject != null)
        {
            blockObject.gameObject.SetActive(false);
        }

        UpdateBlockLine();
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
        maxSlashSpeed = Mathf.Max(maxSlashSpeed, slashSpeed);
        slashTravelDistance += distance;
        isMovingBackward = Vector3.Dot(delta, slashDirection) < 0f;

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
            CheckHeartHit(startPosition, delta / distance, distance);
            return;
        }

        slashTravelDistance -= distance - closestDistance;
        // Cached because the heart sweep reuses the shared hit buffer.
        GameObject blocker = sweepHits[closestIndex].collider.gameObject;
        if (CheckHeartHit(startPosition, delta / distance, closestDistance))
        {
            return;
        }

        // Easing can drag the weapon backwards past a block; that contact shatters the block instead of stopping the slash.
        if (isMovingBackward)
        {
            slashTravelDistance += distance - closestDistance;
            BreakBlock(blocker);
            return;
        }

        weaponObject.position = startPosition + delta.normalized * closestDistance;
        previousWeaponPosition = weaponObject.position;
        NotifyBlocked(blocker, sweepHits[closestIndex].point);
    }

    private void BreakBlock(GameObject blocker)
    {
        if (blocker == null)
        {
            return;
        }

        WeaponController blockerOwner = blocker.GetComponentInParent<BlockOwnerRelay>()?.Owner;
        if (blockerOwner != null)
        {
            blockerOwner.EndBlock();
        }
        else
        {
            blocker.SetActive(false);
        }
    }

    // Returns true when a too-slow contact bounced the weapon off the heart instead of scoring a hit.
    private bool CheckHeartHit(Vector3 startPosition, Vector3 direction, float distance)
    {
        if (hasHitHeartThisSlash || heartLayers.value == 0 || distance <= Mathf.Epsilon)
        {
            return false;
        }

        int hitCount = Physics.SphereCastNonAlloc(
            startPosition,
            Mathf.Max(blockSweepRadius, 0.001f),
            direction,
            sweepHits,
            distance,
            heartLayers,
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
            return false;
        }

        if (slashSpeed < minHeartHitSpeed && !isMovingBackward)
        {
            slashTravelDistance -= distance - closestDistance;
            weaponObject.position = startPosition + direction * closestDistance;
            previousWeaponPosition = weaponObject.position;
            ResolveSlashStaminaToSelf();
            BounceBack();
            return true;
        }

        hasHitHeartThisSlash = true;
        Debug.Log($"{name} hit heart {sweepHits[closestIndex].collider.name} at {slashSpeed:F2} units/sec");
        return false;
    }

    internal void NotifyBlocked(GameObject blocker, Vector3 collisionPoint, bool hasCollisionPoint = true)
    {
        if (!isExecutingSlash || isBouncingBack || weaponObject == null)
        {
            return;
        }

        if (blocker == null || (blockLayers.value & (1 << blocker.layer)) == 0)
        {
            return;
        }

        if (isMovingBackward)
        {
            BreakBlock(blocker);
            return;
        }

        WeaponController blockerOwner = blocker.GetComponentInParent<BlockOwnerRelay>()?.Owner;
        ResolveBlockedSlashStamina(blockerOwner, hasCollisionPoint ? collisionPoint : weaponObject.position);
        if (blockerOwner != null)
        {
            blockerOwner.EndBlock();
        }

        BounceBack();
    }

    private void ResolveSlashStaminaToSelf()
    {
        if (hasResolvedSlashStamina)
        {
            return;
        }

        SpendStamina(pendingSlashStaminaCost);
        hasResolvedSlashStamina = true;
        pendingSlashStaminaCost = 0f;
    }

    private void ResolveBlockedSlashStamina(WeaponController blockerOwner, Vector3 collisionPoint)
    {
        if (hasResolvedSlashStamina)
        {
            return;
        }

        if (blockerOwner == null)
        {
            float splitCost = pendingSlashStaminaCost * DefaultBlockedStaminaSplit;
            SpendStamina(splitCost);
            hasResolvedSlashStamina = true;
            pendingSlashStaminaCost = 0f;
            return;
        }

        float attackDistance = Vector3.Distance(collisionPoint, slashStartPosition);
        float blockDistance = Vector3.Distance(collisionPoint, blockerOwner.BlockStartPosition);
        float fartherDistance = Mathf.Max(attackDistance, blockDistance);
        float attackerShare = fartherDistance > Mathf.Epsilon
            ? Mathf.Clamp01(0.5f + (blockDistance - attackDistance) / (2f * fartherDistance))
            : DefaultBlockedStaminaSplit;
        float attackerCost = pendingSlashStaminaCost * attackerShare;
        float defenderCost = pendingSlashStaminaCost - attackerCost;

        SpendStamina(attackerCost);
        blockerOwner.SpendStamina(defenderCost);

        hasResolvedSlashStamina = true;
        pendingSlashStaminaCost = 0f;
    }

    private void SpendStamina(float amount)
    {
        if (amount <= Mathf.Epsilon)
        {
            return;
        }

        currentStamina = Mathf.Max(currentStamina - amount, 0f);
        UpdateStaminaBar();
    }

    private void BounceBack()
    {
        slashTween?.Kill();
        slashTween = null;
        isExecutingSlash = false;
        LogSlashStats();

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
        pendingSlashStaminaCost = 0f;
        hasResolvedSlashStamina = false;
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
    private System.Action<GameObject, Vector3, bool> onBlocked;

    internal void Initialize(System.Action<GameObject, Vector3, bool> blockedCallback)
    {
        onBlocked = blockedCallback;
    }

    private void OnTriggerEnter(Collider other)
    {
        onBlocked?.Invoke(other.gameObject, transform.position, false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        onBlocked?.Invoke(other.gameObject, transform.position, false);
    }
}

// The block object lives outside the weapon's hierarchy, so it carries a back-reference to its owner.
public class BlockOwnerRelay : MonoBehaviour
{
    internal WeaponController Owner { get; private set; }

    internal void Initialize(WeaponController owner)
    {
        Owner = owner;
    }
}
