using UnityEngine;

public class SwordController : MonoBehaviour
{
    [SerializeField] private Transform swordPoint;
    [SerializeField] private float lerpSpeed = 10f;
    [SerializeField] private float maxDistanceX = 2f;
    [SerializeField] private float maxDistanceY = 2f;

    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
        Cursor.visible = false;
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

        localOffset.x = Mathf.Clamp(localOffset.x, -maxDistanceX, maxDistanceX);
        localOffset.y = Mathf.Clamp(localOffset.y, -maxDistanceY, maxDistanceY);
        localOffset.z = 0f;

        Vector3 targetPosition = transform.position + localOffset;
        swordPoint.position = Vector3.Lerp(swordPoint.position, targetPosition, lerpSpeed * Time.deltaTime);
    }
}
