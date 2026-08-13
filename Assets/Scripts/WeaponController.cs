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

    [Header("Block")]
    [SerializeField] private LayerMask blockLayers = ~0;
    [SerializeField] private float bounceBackDuration = 0.2f;
    [SerializeField] private float bounceBackDistance = 1f;
    [SerializeField] private Ease bounceBackEaseType = Ease.OutQuad;

    private Camera mainCamera;
    private bool isChargingSlash;
    private bool isExecutingSlash;
    private bool isBouncingBack;
    private Vector3 slashDirection;
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

        if (weaponObject != null)
        {
            WeaponBlockRelay relay = weaponObject.GetComponent<WeaponBlockRelay>();
            if (relay == null)
            {
                relay = weaponObject.gameObject.AddComponent<WeaponBlockRelay>();
            }

            relay.Initialize(this);
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
            isBouncingBack = false;
            weaponObject.gameObject.SetActive(true);
        }

        if (Input.GetMouseButtonUp(0) && isChargingSlash)
        {
            isChargingSlash = false;
            StartSlashTween();
        }

        if (!isChargingSlash && !isExecutingSlash && !isBouncingBack)
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
        isBouncingBack = false;
        slashDirection = (weaponTarget.position - weaponObject.position).normalized;
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

        Vector3 bounceDirection = slashDirection.sqrMagnitude > Mathf.Epsilon ? -slashDirection : Vector3.zero;
        Vector3 bounceTarget = weaponObject.position + bounceDirection * bounceBackDistance;

        isBouncingBack = true;
        slashTween = weaponObject
            .DOMove(bounceTarget, bounceBackDuration)
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
            });
    }

    private void OnDisable()
    {
        slashTween?.Kill();
        slashTween = null;
        isChargingSlash = false;
        isExecutingSlash = false;
        isBouncingBack = false;
        if (weaponObject != null)
        {
            weaponObject.gameObject.SetActive(false);
        }
    }
}

public class WeaponBlockRelay : MonoBehaviour
{
    private WeaponController owner;

    internal void Initialize(WeaponController weaponController)
    {
        owner = weaponController;
    }

    private void OnTriggerEnter(Collider other)
    {
        owner?.NotifyBlocked(other.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        owner?.NotifyBlocked(other.gameObject);
    }
}
