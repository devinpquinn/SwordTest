using UnityEngine;

public class WeaponController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private Transform weaponTarget;
    [SerializeField] private float lerpSpeed = 50f;
    [SerializeField] private float maxDistanceX = 10f;
    [SerializeField] private float maxDistanceY = 10f;

    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (weaponTarget == null || mainCamera == null)
        {
            return;
        }

        Vector3 mouseScreenPosition = Input.mousePosition;
        mouseScreenPosition.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);

        Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);
        float clampedX = Mathf.Clamp(mouseWorldPosition.x, -maxDistanceX, maxDistanceX);
        float clampedY = Mathf.Clamp(mouseWorldPosition.y, -maxDistanceY, maxDistanceY);

        Vector3 targetPosition = new Vector3(clampedX, clampedY, weaponTarget.position.z);
        weaponTarget.position = Vector3.Lerp(weaponTarget.position, targetPosition, lerpSpeed * Time.deltaTime);
    }
}
