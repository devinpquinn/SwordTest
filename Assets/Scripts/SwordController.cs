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

    private Camera mainCamera;
    private Quaternion initialRotationParentLocalRotation;

    private void Awake()
    {
        mainCamera = Camera.main;
        Cursor.visible = false;

        if (rotationParent != null)
        {
            initialRotationParentLocalRotation = rotationParent.localRotation;
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
        swordPoint.position = Vector3.Lerp(swordPoint.position, targetPosition, lerpSpeed * Time.deltaTime);

        if (rotationParent != null)
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
}
