using UnityEngine;

[RequireComponent(typeof(WeaponController))]
public class NpcWeaponController : MonoBehaviour
{
    [Header("Bounds")]
    [SerializeField] private Transform playCenter;
    [SerializeField] private float maxDistanceX = 10f;
    [SerializeField] private float maxDistanceY = 10f;

    [Header("Slash Timing")]
    [SerializeField] private float minSlashInterval = 2f;
    [SerializeField] private float maxSlashInterval = 4f;
    [SerializeField] private float minSlashReach = 3f;
    [SerializeField] private float maxSlashReach = 8f;

    private WeaponController weaponController;
    private float nextSlashTime;

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
    }

    private void Update()
    {
        if (weaponController.IsBusy || Time.time < nextSlashTime)
        {
            return;
        }

        Vector3 center = playCenter != null ? playCenter.position : transform.position;
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 axis = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

        Vector3 startPoint = ClampToBounds(center + axis * Random.Range(minSlashReach, maxSlashReach), center);
        Vector3 endPoint = ClampToBounds(center - axis * Random.Range(minSlashReach, maxSlashReach), center);

        weaponController.BeginSlashCharge(startPoint);
        weaponController.ReleaseSlash(endPoint);
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
