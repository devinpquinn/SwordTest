using UnityEngine;

[RequireComponent(typeof(WeaponController))]
public class NpcWeaponController : MonoBehaviour
{
    [Header("Bounds")]
    [SerializeField] private Transform playCenter;
    [SerializeField] private float maxDistanceX = 10f;
    [SerializeField] private float maxDistanceY = 10f;

    [Header("Slash Timing")]
    [SerializeField] private float windupTime = 0.6f;
    [SerializeField] private float windupHoldTime = 0.2f;
    [SerializeField] private float minSlashInterval = 2f;
    [SerializeField] private float maxSlashInterval = 4f;
    [SerializeField] private float minSlashReach = 3f;
    [SerializeField] private float maxSlashReach = 8f;

    private WeaponController weaponController;
    private float nextSlashTime;
    private bool isWindingUp;
    private float windupStartTime;
    private Vector3 pendingStartPoint;
    private Vector3 pendingEndPoint;

    private void Awake()
    {
        weaponController = GetComponent<WeaponController>();
    }

    private void OnEnable()
    {
        weaponController.SlashFinished += ScheduleNextSlash;
        ScheduleNextSlash();
    }

    private void OnDisable()
    {
        weaponController.SlashFinished -= ScheduleNextSlash;
        isWindingUp = false;
    }

    private void Update()
    {
        if (isWindingUp)
        {
            float windupProgress = windupTime > Mathf.Epsilon
                ? Mathf.Clamp01((Time.time - windupStartTime) / windupTime)
                : 1f;

            weaponController.SetTargetPosition(Vector3.Lerp(pendingStartPoint, pendingEndPoint, windupProgress));

            if (Time.time - windupStartTime >= windupTime + windupHoldTime)
            {
                isWindingUp = false;
                weaponController.ReleaseSlash(pendingEndPoint);
            }

            return;
        }

        if (weaponController.IsBusy || Time.time < nextSlashTime)
        {
            return;
        }

        Vector3 center = playCenter != null ? playCenter.position : transform.position;
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 axis = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

        Vector3 startPoint = ClampToBounds(center + axis * Random.Range(minSlashReach, maxSlashReach), center);
        pendingStartPoint = startPoint;
        pendingEndPoint = ClampToBounds(center - axis * Random.Range(minSlashReach, maxSlashReach), center);

        weaponController.BeginSlashCharge(startPoint);
        isWindingUp = true;
        windupStartTime = Time.time;
    }

    private void ScheduleNextSlash()
    {
        nextSlashTime = Time.time + Random.Range(minSlashInterval, maxSlashInterval);
    }

    private Vector3 ClampToBounds(Vector3 point, Vector3 center)
    {
        return new Vector3(
            Mathf.Clamp(point.x, center.x - maxDistanceX, center.x + maxDistanceX),
            Mathf.Clamp(point.y, center.y - maxDistanceY, center.y + maxDistanceY),
            center.z);
    }
}
