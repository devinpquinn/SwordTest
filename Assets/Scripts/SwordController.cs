using UnityEngine;

public class SwordController : MonoBehaviour
{
    [SerializeField] private Transform swordPoint;
    [SerializeField] private float lerpSpeed = 10f;
    [SerializeField] private float maxDistanceX = 2f;
    [SerializeField] private float maxDistanceY = 2f;
    [SerializeField, Range(-1f, 1f)] private float horiz;
    [SerializeField, Range(-1f, 1f)] private float vert;
    [SerializeField] private Transform rotationParent;
    [SerializeField] private float rotationBlendSpeed = 10f;
    [SerializeField] private float rotationLeft = -20f;
    [SerializeField] private float rotationRight = 20f;
    [SerializeField] private float rotationUp = -15f;
    [SerializeField] private float rotationDown = 15f;
    [SerializeField] private Transform slashRotationParent;
    [SerializeField] private float slashRollOffset = 0f;
    [SerializeField] private float slashWindupDuration = 0.08f;
    [SerializeField] private float slashTravelLerpSpeed = 30f;
    [SerializeField] private float slashCompleteDistance = 0.05f;

    private Camera mainCamera;
    private Quaternion initialRotationParentLocalRotation;
    private Quaternion initialSlashRotationParentLocalRotation;
    private bool isHoldingSlash;
    private bool isExecutingSlash;
    private float lockedSlashRoll;
    private Vector3 slashReleaseTargetPosition;
    private Quaternion heldRotationParentLocalRotation;
    private Quaternion heldSlashRotationParentLocalRotation;
    private Quaternion slashWindupStartRotation;
    private float slashWindupTimer;

    private void Awake()
    {
        mainCamera = Camera.main;
        Cursor.visible = false;

        if (rotationParent != null)
        {
            initialRotationParentLocalRotation = rotationParent.localRotation;
        }

        if (slashRotationParent != null)
        {
            initialSlashRotationParentLocalRotation = slashRotationParent.localRotation;
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
        float liveRoll = GetLiveSlashRoll();

        if (Input.GetMouseButtonDown(0))
        {
            isHoldingSlash = true;
            isExecutingSlash = false;
            lockedSlashRoll = liveRoll;

            if (rotationParent != null)
            {
                heldRotationParentLocalRotation = rotationParent.localRotation;
            }

            if (slashRotationParent != null)
            {
                slashWindupStartRotation = slashRotationParent.localRotation;
                slashWindupTimer = 0f;
                heldSlashRotationParentLocalRotation =
                    initialSlashRotationParentLocalRotation * Quaternion.Euler(0f, 0f, lockedSlashRoll);
            }
        }

        if (Input.GetMouseButtonUp(0) && isHoldingSlash)
        {
            isHoldingSlash = false;
            isExecutingSlash = true;
            slashReleaseTargetPosition = targetPosition;
        }

        if (isHoldingSlash)
        {
            // Freeze sword tip while drag is held to define a slash line.
        }
        else if (isExecutingSlash)
        {
            swordPoint.position = Vector3.Lerp(
                swordPoint.position,
                slashReleaseTargetPosition,
                slashTravelLerpSpeed * Time.deltaTime);

            if (Vector3.Distance(swordPoint.position, slashReleaseTargetPosition) <= slashCompleteDistance)
            {
                swordPoint.position = slashReleaseTargetPosition;
                isExecutingSlash = false;
            }
        }
        else
        {
            swordPoint.position = Vector3.Lerp(swordPoint.position, targetPosition, lerpSpeed * Time.deltaTime);
        }

        float targetRoll = (isHoldingSlash || isExecutingSlash) ? lockedSlashRoll : 0f;

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
                if (slashWindupDuration <= Mathf.Epsilon)
                {
                    slashRotationParent.localRotation = heldSlashRotationParentLocalRotation;
                }
                else
                {
                    slashWindupTimer += Time.deltaTime;
                    float windupT = Mathf.Clamp01(slashWindupTimer / slashWindupDuration);
                    slashRotationParent.localRotation = Quaternion.Slerp(
                        slashWindupStartRotation,
                        heldSlashRotationParentLocalRotation,
                        windupT);
                }
            }
            else
            {
                Quaternion targetSlashRotation = initialSlashRotationParentLocalRotation * Quaternion.Euler(0f, 0f, targetRoll);
                slashRotationParent.localRotation = Quaternion.Lerp(
                    slashRotationParent.localRotation,
                    targetSlashRotation,
                    rotationBlendSpeed * Time.deltaTime);
            }
        }
    }

    private float GetLiveSlashRoll()
    {
        Vector2 toCenter = new Vector2(-horiz, -vert);
        if (toCenter.sqrMagnitude <= Mathf.Epsilon)
        {
            return 0f;
        }

        return Mathf.Atan2(toCenter.y, toCenter.x) * Mathf.Rad2Deg + slashRollOffset;
    }
}
