using UnityEngine;
using DG.Tweening;

public class WeaponController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private Transform weaponTarget;
    [SerializeField] private Transform weaponObject;
    [SerializeField] private float slashDuration = 0.5f;
    [SerializeField] private Ease slashEaseType = Ease.OutSine;
    [SerializeField] private float lerpSpeed = 50f;
    [SerializeField] private float maxDistanceX = 10f;
    [SerializeField] private float maxDistanceY = 10f;

    private Camera mainCamera;
    private bool isChargingSlash;
    private bool isExecutingSlash;
    private Tween slashTween;

    private void Awake()
    {
        mainCamera = Camera.main;
        Cursor.visible = false;

        if (weaponTarget != null && weaponObject != null)
        {
            weaponObject.position = weaponTarget.position;
            weaponObject.gameObject.SetActive(false);
        }
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

        if (weaponObject == null)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            slashTween?.Kill();
            slashTween = null;
            isChargingSlash = true;
            isExecutingSlash = false;
            weaponObject.gameObject.SetActive(true);
        }

        if (Input.GetMouseButtonUp(0) && isChargingSlash)
        {
            isChargingSlash = false;
            StartSlashTween();
        }

        if (!isChargingSlash && !isExecutingSlash)
        {
            weaponObject.position = weaponTarget.position;
            weaponObject.gameObject.SetActive(false);
        }
    }

    private void StartSlashTween()
    {
        if (weaponObject == null || weaponTarget == null)
        {
            return;
        }

        isExecutingSlash = true;
        slashTween?.Kill();
        slashTween = weaponObject
            .DOMove(weaponTarget.position, slashDuration)
            .SetEase(slashEaseType)
            .OnComplete(() =>
            {
                isExecutingSlash = false;
                weaponObject.position = weaponTarget.position;
                weaponObject.gameObject.SetActive(false);
                slashTween = null;
            });
    }

    private void OnDisable()
    {
        slashTween?.Kill();
        slashTween = null;
        isChargingSlash = false;
        isExecutingSlash = false;
        if (weaponObject != null)
        {
            weaponObject.gameObject.SetActive(false);
        }
    }
}
