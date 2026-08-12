using UnityEngine;
using DG.Tweening;

public class SwordController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private Transform swordPoint;
    [SerializeField] private float lerpSpeed = 10f;
    [SerializeField] private float maxDistanceX = 2f;
    [SerializeField] private float maxDistanceY = 2f;

    [Header("Input Preview")]
    [SerializeField, Range(-1f, 1f)] private float horiz;
    [SerializeField, Range(-1f, 1f)] private float vert;

    [Header("Aim Rotation")]
    [SerializeField] private Transform rotationParent;
    [SerializeField] private float rotationBlendSpeed = 10f;
    [SerializeField] private float rotationLeft = -20f;
    [SerializeField] private float rotationRight = 20f;
    [SerializeField] private float rotationUp = -15f;
    [SerializeField] private float rotationDown = 15f;

    [Header("Slash")]
    [SerializeField] private Transform slashRotationParent;
    [SerializeField] private Transform slashOffsetTarget;
    [SerializeField] private float slashRollOffset = 0f;
    [SerializeField] private float slashRotationToMouseLerpSpeed = 12f;
    [SerializeField] private float slashWindupDuration = 0.08f;
    [SerializeField] private float slashTravelDuration = 0.12f;
    [SerializeField] private float minimumSlashDistance = 1f;
    [SerializeField] private float followThroughRecoveryDuration = 0.12f;
    [SerializeField] private float windupRotationX;
    [SerializeField] private float followThroughRotationX;
    [SerializeField] private Ease slashOffsetEase = Ease.OutSine;

    private Camera mainCamera;
    private Quaternion initialRotationParentLocalRotation;
    private Quaternion initialSlashRotationParentLocalRotation;
    private Quaternion initialSlashOffsetTargetLocalRotation;
    private bool isHoldingSlash;
    private bool isExecutingSlash;
    private bool isRecoveringSlash;
    private float lockedSlashRoll;
    private Vector3 slashReleaseTargetPosition;
    private Vector3 holdStartTargetPosition;
    private float holdChargeWeight;
    private float windupWeightAtRelease;
    private float slashTravelTimer;
    private float slashTravelProgress;
    private Vector3 slashTravelStartPosition;
    private float slashRecoveryTimer;
    private Quaternion heldRotationParentLocalRotation;
    private Quaternion slashRecoveryStartRotation;
    private Tween slashOffsetRotationTween;
    private Quaternion lastSlashOffsetLocalRotationTarget;
    private bool hasSlashOffsetTweenTarget;

    private void Awake()
    {
        mainCamera = Camera.main;
        //Cursor.visible = false;

        if (rotationParent != null)
        {
            initialRotationParentLocalRotation = rotationParent.localRotation;
        }

        if (slashRotationParent != null)
        {
            initialSlashRotationParentLocalRotation = slashRotationParent.localRotation;
        }

        if (slashOffsetTarget != null)
        {
            initialSlashOffsetTargetLocalRotation = slashOffsetTarget.localRotation;
        }
    }

    private void Update()
    {
        if (swordPoint == null || mainCamera == null)
        {
            return;
        }

        Vector3 mouseScreenPosition = Input.mousePosition;
        mouseScreenPosition.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);

        Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);
        Vector3 localOffset = mouseWorldPosition - transform.position;

        float clampedX = Mathf.Clamp(localOffset.x, -maxDistanceX, maxDistanceX);
        float clampedY = Mathf.Clamp(localOffset.y, -maxDistanceY, maxDistanceY);

        horiz = maxDistanceX > Mathf.Epsilon ? Mathf.Clamp(clampedX / maxDistanceX, -1f, 1f) : 0f;
        vert = maxDistanceY > Mathf.Epsilon ? Mathf.Clamp(clampedY / maxDistanceY, -1f, 1f) : 0f;

        Vector3 targetPosition = transform.position + new Vector3(horiz * maxDistanceX, vert * maxDistanceY, 0f);
        float mouseFacingRoll = GetMouseFacingSlashRoll(mouseWorldPosition, lockedSlashRoll);

        if (Input.GetMouseButtonDown(0))
        {
            isHoldingSlash = true;
            isExecutingSlash = false;
            isRecoveringSlash = false;
            lockedSlashRoll = mouseFacingRoll;
            holdStartTargetPosition = targetPosition;
            holdChargeWeight = 0f;

            if (rotationParent != null)
            {
                heldRotationParentLocalRotation = rotationParent.localRotation;
            }
        }

        if (Input.GetMouseButtonUp(0) && isHoldingSlash)
        {
            isHoldingSlash = false;
            isExecutingSlash = true;
            lockedSlashRoll = GetCurrentSlashRoll();
            slashReleaseTargetPosition = targetPosition;
            windupWeightAtRelease = holdChargeWeight;
            slashTravelStartPosition = swordPoint.position;
            slashTravelTimer = 0f;
            slashTravelProgress = 0f;
        }

        if (isHoldingSlash)
        {
            float chargeDistance = Vector3.Distance(targetPosition, holdStartTargetPosition);
            holdChargeWeight = GetChargeWeight(chargeDistance);
        }
        else if (isExecutingSlash)
        {
            if (slashTravelDuration <= Mathf.Epsilon)
            {
                swordPoint.position = slashReleaseTargetPosition;
                slashTravelProgress = 1f;
                isExecutingSlash = false;
                isRecoveringSlash = true;
                slashRecoveryTimer = 0f;
                if (slashRotationParent != null)
                {
                    slashRecoveryStartRotation = slashRotationParent.localRotation;
                }
            }
            else
            {
                slashTravelTimer += Time.deltaTime;
                slashTravelProgress = Mathf.Clamp01(slashTravelTimer / slashTravelDuration);
                swordPoint.position = Vector3.Lerp(slashTravelStartPosition, slashReleaseTargetPosition, slashTravelProgress);

                if (slashTravelProgress >= 1f)
                {
                    swordPoint.position = slashReleaseTargetPosition;
                    isExecutingSlash = false;
                    isRecoveringSlash = true;
                    slashRecoveryTimer = 0f;
                    if (slashRotationParent != null)
                    {
                        slashRecoveryStartRotation = slashRotationParent.localRotation;
                    }
                }
            }
        }
        else
        {
            swordPoint.position = Vector3.Lerp(swordPoint.position, targetPosition, lerpSpeed * Time.deltaTime);
        }

        if (isRecoveringSlash)
        {
            slashRecoveryTimer += Time.deltaTime;
            if (slashRecoveryTimer >= followThroughRecoveryDuration)
            {
                isRecoveringSlash = false;
            }
        }

        float targetRoll = isExecutingSlash ? lockedSlashRoll : 0f;

        if (rotationParent != null)
        {
            if (isHoldingSlash)
            {
                rotationParent.localRotation = heldRotationParentLocalRotation;
            }
            else
            {
                float targetYaw = horiz < 0f
                    ? Mathf.Lerp(0f, rotationLeft, -horiz)
                    : Mathf.Lerp(0f, rotationRight, horiz);

                float targetPitch = vert < 0f
                    ? Mathf.Lerp(0f, rotationDown, -vert)
                    : Mathf.Lerp(0f, rotationUp, vert);

                Quaternion targetRotation = initialRotationParentLocalRotation * Quaternion.Euler(targetPitch, targetYaw, 0f);
                rotationParent.localRotation = Quaternion.Lerp(
                    rotationParent.localRotation,
                    targetRotation,
                    rotationBlendSpeed * Time.deltaTime);
            }
        }

        if (slashRotationParent != null)
        {
            if (isHoldingSlash)
            {
                lockedSlashRoll = GetMouseFacingSlashRoll(mouseWorldPosition, lockedSlashRoll);
                Quaternion holdSlashRotation = initialSlashRotationParentLocalRotation * Quaternion.Euler(0f, 0f, lockedSlashRoll);
                slashRotationParent.localRotation = Quaternion.Slerp(
                    slashRotationParent.localRotation,
                    holdSlashRotation,
                    Mathf.Clamp01(slashRotationToMouseLerpSpeed * Time.deltaTime));
            }
            else
            {
                Quaternion targetSlashRotation = initialSlashRotationParentLocalRotation * Quaternion.Euler(0f, 0f, targetRoll);
                if (isRecoveringSlash)
                {
                    float recoveryT = followThroughRecoveryDuration <= Mathf.Epsilon
                        ? 1f
                        : Mathf.Clamp01(slashRecoveryTimer / followThroughRecoveryDuration);
                    slashRotationParent.localRotation = Quaternion.Slerp(
                        slashRecoveryStartRotation,
                        targetSlashRotation,
                        recoveryT);
                }
                else
                {
                    slashRotationParent.localRotation = Quaternion.Lerp(
                        slashRotationParent.localRotation,
                        targetSlashRotation,
                        Mathf.Clamp01(rotationBlendSpeed * Time.deltaTime));
                }
            }
        }

        if (slashOffsetTarget != null)
        {
            float slashProgress = GetSlashProgress();
            float windupWeight = GetWindupWeight(slashProgress);
            float followThroughWeight = GetFollowThroughWeight(slashProgress);

            float offsetRotationX = (windupRotationX * windupWeight) + (followThroughRotationX * followThroughWeight);
            float offsetTweenDuration = isRecoveringSlash
                ? followThroughRecoveryDuration
                : slashWindupDuration;
            TweenSlashOffsetTarget(offsetRotationX, offsetTweenDuration);
        }
    }

    private void OnDisable()
    {
        slashOffsetRotationTween?.Kill();
        hasSlashOffsetTweenTarget = false;
    }

    private float GetMouseFacingSlashRoll(Vector3 mouseWorldPosition, float fallbackRoll)
    {
        Vector2 toMouse = mouseWorldPosition - swordPoint.position;
        if (toMouse.sqrMagnitude <= Mathf.Epsilon)
        {
            return fallbackRoll;
        }

        return Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg + slashRollOffset;
    }

    private float GetCurrentSlashRoll()
    {
        if (slashRotationParent == null)
        {
            return 0f;
        }

        Quaternion relativeRotation = Quaternion.Inverse(initialSlashRotationParentLocalRotation) * slashRotationParent.localRotation;
        float roll = relativeRotation.eulerAngles.z;
        if (roll > 180f)
        {
            roll -= 360f;
        }

        return roll;
    }

    private float GetChargeWeight(float chargeDistance)
    {
        if (minimumSlashDistance <= Mathf.Epsilon)
        {
            return 1f;
        }

        float normalizedCharge = Mathf.Clamp01(chargeDistance / minimumSlashDistance);
        return Mathf.SmoothStep(0f, 1f, normalizedCharge);
    }

    private float GetSlashProgress()
    {
        if (!isExecutingSlash)
        {
            return 0f;
        }

        return slashTravelProgress;
    }

    private float GetWindupWeight(float slashProgress)
    {
        if (isHoldingSlash)
        {
            return holdChargeWeight;
        }

        if (isExecutingSlash)
        {
            return windupWeightAtRelease * (1f - slashProgress);
        }

        return 0f;
    }

    private float GetFollowThroughWeight(float slashProgress)
    {
        if (isExecutingSlash)
        {
            return slashProgress;
        }

        if (isRecoveringSlash)
        {
            if (followThroughRecoveryDuration <= Mathf.Epsilon)
            {
                return 0f;
            }

            float recoveryT = Mathf.Clamp01(slashRecoveryTimer / followThroughRecoveryDuration);
            return 1f - recoveryT;
        }

        return 0f;
    }

    private float GetDurationBlendFactor(float duration)
    {
        if (duration <= Mathf.Epsilon)
        {
            return 1f;
        }

        return Mathf.Clamp01(Time.deltaTime / duration);
    }

    private void TweenSlashOffsetTarget(float offsetRotationX, float duration)
    {
        if (slashOffsetTarget == null)
        {
            return;
        }

        Quaternion targetLocalRotation = initialSlashOffsetTargetLocalRotation * Quaternion.Euler(offsetRotationX, 0f, 0f);

        if (duration <= Mathf.Epsilon)
        {
            slashOffsetRotationTween?.Kill();
            slashOffsetTarget.localRotation = targetLocalRotation;
            lastSlashOffsetLocalRotationTarget = targetLocalRotation;
            hasSlashOffsetTweenTarget = true;
            return;
        }

        bool rotationTargetChanged = !hasSlashOffsetTweenTarget ||
            Quaternion.Angle(lastSlashOffsetLocalRotationTarget, targetLocalRotation) > 0.01f;

        if (rotationTargetChanged)
        {
            slashOffsetRotationTween?.Kill();
            slashOffsetRotationTween = slashOffsetTarget
                .DOLocalRotateQuaternion(targetLocalRotation, duration)
                .SetEase(slashOffsetEase);
            lastSlashOffsetLocalRotationTarget = targetLocalRotation;
        }

        hasSlashOffsetTweenTarget = true;
    }
}
