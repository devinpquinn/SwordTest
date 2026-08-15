using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(WeaponController))]
public class NpcWeaponController : MonoBehaviour
{
    [Header("Bounds")]
    [SerializeField] private Transform playCenter;
    [SerializeField] private float maxDistanceX = 20f;
    [SerializeField] private float maxDistanceY = 10f;

    [Header("Targeting")]
    [SerializeField] private Transform playerHeart;
    [SerializeField] private Transform npcHeart;
    [SerializeField] private WeaponController playerWeapon;

    [Header("Behavior")]
    [SerializeField] private float minSlashInterval = 2f;
    [SerializeField] private float maxSlashInterval = 4f;

    [Header("Slash")]
    [SerializeField] private float windupTime = 0.6f;
    [SerializeField] private float windupHoldTime = 0.2f;
    [SerializeField] private Ease windupEaseType = Ease.OutCubic;
    [SerializeField] private float minSlashLength = 8f;
    [SerializeField] private float maxSlashLength = 16f;
    
    [Header("Block")]
    [SerializeField] private float minBlockReactionTime = 0.2f;
    [SerializeField] private float maxBlockReactionTime = 1f;
    [SerializeField] private float minBlockIntercept = 0.3f;
    [SerializeField] private float maxBlockIntercept = 0.7f;   
    [SerializeField] private float minBlockCentered = 0.4f;
    [SerializeField] private float maxBlockCentered = 0.6f;
    [SerializeField] private float maxBlockAngleVariance = 20f;
    [SerializeField] private float blockCreateTime = 0.5f;
    [SerializeField] private Ease blockEaseType = Ease.OutCubic;
    [SerializeField] private float minBlockLength = 6f;
    [SerializeField] private float maxBlockLength = 12f;

    private WeaponController weaponController;
    private float nextSlashTime;
    private bool isWindingUp;
    private float windupStartTime;
    private Vector3 pendingStartPoint;
    private Vector3 pendingEndPoint;

    private enum DefenseState
    {
        None,
        Reacting,
        Creating,
        Holding,
        Releasing
    }

    private DefenseState defenseState;
    private float defenseActionTime;
    private float blockCreateStartTime;
    private Vector3 blockStartPoint;
    private Vector3 blockEndPoint;

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
        defenseState = DefenseState.None;
    }

    private void Update()
    {
        if (UpdateDefense())
        {
            return;
        }

        if (isWindingUp)
        {
            float windupProgress = windupTime > Mathf.Epsilon
                ? Mathf.Clamp01((Time.time - windupStartTime) / windupTime)
                : 1f;

            weaponController.SetTargetPosition(Vector3.LerpUnclamped(
                pendingStartPoint,
                pendingEndPoint,
                DOVirtual.EasedValue(0f, 1f, windupProgress, windupEaseType)));

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
        Vector3 aimPoint = ClampToBounds(playerHeart != null ? playerHeart.position : center, center);
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 axis = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

        float forwardReach = Mathf.Min(Random.Range(minSlashLength, maxSlashLength), MaxReach(aimPoint, axis, center));
        float backwardReach = Mathf.Min(Random.Range(minSlashLength, maxSlashLength), MaxReach(aimPoint, -axis, center));

        pendingStartPoint = aimPoint + axis * forwardReach;
        pendingEndPoint = aimPoint - axis * backwardReach;

        weaponController.BeginSlashCharge(pendingStartPoint);
        isWindingUp = true;
        windupStartTime = Time.time;
    }

    private void ScheduleNextSlash()
    {
        nextSlashTime = Time.time + Random.Range(minSlashInterval, maxSlashInterval);
    }

    // Returns true while the NPC is committed to defending, which suspends its attack routine.
    private bool UpdateDefense()
    {
        if (playerWeapon == null)
        {
            return false;
        }

        bool playerCharging = playerWeapon.IsChargingSlash;
        bool playerAttackOver = !playerCharging && !playerWeapon.IsBusy;

        switch (defenseState)
        {
            case DefenseState.None:
                if (playerCharging && !isWindingUp && !weaponController.IsBusy && !weaponController.IsBlocking)
                {
                    defenseState = DefenseState.Reacting;
                    defenseActionTime = Time.time + Random.Range(minBlockReactionTime, maxBlockReactionTime);
                    return true;
                }

                return false;

            case DefenseState.Reacting:
                if (playerAttackOver)
                {
                    EndDefense();
                    return false;
                }

                if (Time.time >= defenseActionTime)
                {
                    StartBlockCreation();
                }

                return true;

            case DefenseState.Creating:
            case DefenseState.Holding:
                if (!weaponController.IsBlocking)
                {
                    EndDefense();
                    return false;
                }

                if (defenseState == DefenseState.Creating)
                {
                    float progress = blockCreateTime > Mathf.Epsilon
                        ? Mathf.Clamp01((Time.time - blockCreateStartTime) / blockCreateTime)
                        : 1f;

                    weaponController.UpdateBlockDrag(Vector3.LerpUnclamped(
                        blockStartPoint,
                        blockEndPoint,
                        DOVirtual.EasedValue(0f, 1f, progress, blockEaseType)));

                    if (progress >= 1f)
                    {
                        defenseState = DefenseState.Holding;
                    }
                }

                if (playerAttackOver)
                {
                    defenseState = DefenseState.Releasing;
                    defenseActionTime = Time.time + Random.Range(minBlockReactionTime, maxBlockReactionTime);
                }

                return true;

            case DefenseState.Releasing:
                if (!weaponController.IsBlocking || Time.time >= defenseActionTime)
                {
                    weaponController.EndBlock();
                    EndDefense();
                    return false;
                }

                return true;
        }

        return false;
    }

    private void StartBlockCreation()
    {
        Vector3 origin = playerWeapon.SlashStartPosition;
        Vector3 target = npcHeart != null ? npcHeart.position : transform.position;
        Vector3 incoming = target - origin;
        Vector3 perpendicular = incoming.sqrMagnitude > Mathf.Epsilon
            ? new Vector3(-incoming.y, incoming.x, 0f).normalized
            : Vector3.up;

        perpendicular = Quaternion.Euler(0f, 0f, Random.Range(-maxBlockAngleVariance, maxBlockAngleVariance)) * perpendicular;

        Vector3 center = playCenter != null ? playCenter.position : transform.position;
        Vector3 interceptPoint = ClampToBounds(
            Vector3.LerpUnclamped(origin, target, Random.Range(minBlockIntercept, maxBlockIntercept)),
            center);
        float length = Random.Range(minBlockLength, maxBlockLength);
        // Fraction of the block that sits on the start side of the intercept point; 0.5 centers it.
        float centeredFraction = Random.Range(minBlockCentered, maxBlockCentered);
        float startReach = length * centeredFraction;
        float endReach = length - startReach;

        blockStartPoint = interceptPoint + perpendicular * Mathf.Min(startReach, MaxReach(interceptPoint, perpendicular, center));
        blockEndPoint = interceptPoint - perpendicular * Mathf.Min(endReach, MaxReach(interceptPoint, -perpendicular, center));

        weaponController.BeginBlock(blockStartPoint);
        blockCreateStartTime = Time.time;
        defenseState = DefenseState.Creating;
    }

    private void EndDefense()
    {
        defenseState = DefenseState.None;
        ScheduleNextSlash();
    }

    private Vector3 ClampToBounds(Vector3 point, Vector3 center)
    {
        return new Vector3(
            Mathf.Clamp(point.x, center.x - maxDistanceX, center.x + maxDistanceX),
            Mathf.Clamp(point.y, center.y - maxDistanceY, center.y + maxDistanceY),
            center.z);
    }

    // Slab test: how far we can travel from origin along direction before leaving the play bounds.
    private float MaxReach(Vector3 origin, Vector3 direction, Vector3 center)
    {
        float limit = Mathf.Min(
            AxisReach(origin.x - center.x, direction.x, maxDistanceX),
            AxisReach(origin.y - center.y, direction.y, maxDistanceY));

        return Mathf.Max(limit, 0f);
    }

    private static float AxisReach(float offset, float direction, float halfExtent)
    {
        if (Mathf.Abs(direction) <= Mathf.Epsilon)
        {
            return float.MaxValue;
        }

        return ((direction > 0f ? halfExtent : -halfExtent) - offset) / direction;
    }
}
