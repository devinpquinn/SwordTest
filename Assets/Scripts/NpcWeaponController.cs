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

    [Header("Movement")]
    [SerializeField] private float minMoveCheckInterval = 0.4f;
    [SerializeField] private float maxMoveCheckInterval = 1.2f;
    [SerializeField, Range(0f, 1f)] private float stayStillChance = 0.25f;

    [Header("Slash")]
    [SerializeField] private float windupTime = 0.6f;
    [SerializeField] private float windupHoldTime = 0.2f;
    [SerializeField] private Ease windupEaseType = Ease.OutCubic;
    [SerializeField] private float minSlashLength = 8f;
    [SerializeField] private float maxSlashLength = 16f;
    [SerializeField] private float slashTrackingCheckFrequency = 0.5f;
    [SerializeField] private float slashTrackingSmoothTime = 0.3f;
    [SerializeField] private float minAimLeadTime = 0.1f;
    [SerializeField] private float maxAimLeadTime = 0.4f;
    [SerializeField] private float heartVelocitySmoothTime = 0.2f;
    
    [Header("Block")]
    [SerializeField] private float minBlockReactionTime = 0.2f;
    [SerializeField] private float maxBlockReactionTime = 1f;
    [SerializeField] private float minBlockIntercept = 0.3f;
    [SerializeField] private float maxBlockIntercept = 0.7f;   
    [SerializeField] private float minBlockCentered = 0.4f;
    [SerializeField] private float maxBlockCentered = 0.6f;
    [SerializeField] private float maxBlockAngleVariance = 20f;
    [SerializeField] private float blockCreateTime = 0.5f;
    [SerializeField] private float minBlockTrackingCheckInterval = 0.1f;
    [SerializeField] private float maxBlockTrackingCheckInterval = 0.3f;
    [SerializeField] private float blockTrackingSmoothTime = 0.2f;
    [SerializeField] private Ease blockEaseType = Ease.OutCubic;
    [SerializeField] private float minBlockLength = 6f;
    [SerializeField] private float maxBlockLength = 12f;

    private WeaponController weaponController;
    private float nextSlashTime;
    private bool isWindingUp;
    private float windupStartTime;
    private Vector3 pendingStartPoint;
    private Vector3 pendingEndPoint;
    private Vector3 slashAxis;
    private float slashForwardReach;
    private float slashBackwardReach;
    private Vector3 aimPoint;
    private Vector3 trackedAimPoint;
    private Vector3 aimVelocity;
    private float nextTrackingCheckTime;
    private Vector3 previousHeartPosition;
    private Vector3 estimatedHeartVelocity;
    private Vector3 moveDirection;
    private float nextMoveCheckTime;

    private static readonly Vector3[] MoveDirections =
    {
        new Vector3(1f, 0f, 0f),
        new Vector3(-1f, 0f, 0f),
        new Vector3(0f, 1f, 0f),
        new Vector3(0f, -1f, 0f),
        new Vector3(1f, 1f, 0f).normalized,
        new Vector3(1f, -1f, 0f).normalized,
        new Vector3(-1f, 1f, 0f).normalized,
        new Vector3(-1f, -1f, 0f).normalized
    };

    private readonly float[] moveWeights = new float[MoveDirections.Length];

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
    private Vector3 trackedBlockEndPoint;
    private Vector3 blockEndVelocity;
    private Vector3 blockAttackOrigin;
    private float blockInterceptT;
    private float blockInterceptFraction;
    private float nextBlockTrackingCheckTime;

    private void Awake()
    {
        weaponController = GetComponent<WeaponController>();
        if (playerHeart != null)
        {
            previousHeartPosition = playerHeart.position;
        }
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
        UpdateHeartVelocity();
        UpdateMovement();

        if (UpdateDefense())
        {
            return;
        }

        if (isWindingUp)
        {
            UpdateSlashTracking();

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
        aimPoint = playerHeart != null
            ? ClampToBounds(playerHeart.position + estimatedHeartVelocity * Random.Range(minAimLeadTime, maxAimLeadTime), center)
            : center;
        trackedAimPoint = aimPoint;
        aimVelocity = Vector3.zero;
        nextTrackingCheckTime = Time.time + Random.Range(0f, slashTrackingCheckFrequency);

        float angle = Random.Range(0f, Mathf.PI * 2f);
        slashAxis = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        slashForwardReach = Random.Range(minSlashLength, maxSlashLength);
        slashBackwardReach = Random.Range(minSlashLength, maxSlashLength);
        pendingStartPoint = aimPoint + slashAxis * Mathf.Min(slashForwardReach, MaxReach(aimPoint, slashAxis, center));
        UpdateSlashEndPoint(center);

        weaponController.BeginSlashCharge(pendingStartPoint);
        isWindingUp = true;
        windupStartTime = Time.time;
    }

    // Aim only re-samples the heart on a randomized interval, so the NPC lags behind perfect tracking.
    private void UpdateSlashTracking()
    {
        if (playerHeart == null)
        {
            return;
        }

        Vector3 center = playCenter != null ? playCenter.position : transform.position;

        if (Time.time >= nextTrackingCheckTime)
        {
            Vector3 lead = estimatedHeartVelocity * Random.Range(minAimLeadTime, maxAimLeadTime);
            trackedAimPoint = ClampToBounds(playerHeart.position + lead, center);
            nextTrackingCheckTime = Time.time + Random.Range(0f, slashTrackingCheckFrequency);
        }

        aimPoint = Vector3.SmoothDamp(aimPoint, trackedAimPoint, ref aimVelocity, slashTrackingSmoothTime);
        UpdateSlashEndPoint(center);
    }

    // The origin is locked in when the charge starts, so tracking only swings the end point through the aim point.
    private void UpdateSlashEndPoint(Vector3 center)
    {
        Vector3 toAim = aimPoint - pendingStartPoint;
        Vector3 direction = toAim.sqrMagnitude > Mathf.Epsilon ? toAim.normalized : -slashAxis;
        pendingEndPoint = aimPoint + direction * Mathf.Min(slashBackwardReach, MaxReach(aimPoint, direction, center));
    }

    private void ScheduleNextSlash()
    {
        nextSlashTime = Time.time + Random.Range(minSlashInterval, maxSlashInterval);
    }

    private void UpdateMovement()
    {
        if (Time.time >= nextMoveCheckTime)
        {
            moveDirection = PickMoveDirection();
            nextMoveCheckTime = Time.time + Random.Range(minMoveCheckInterval, maxMoveCheckInterval);
        }

        weaponController.MoveHeart(moveDirection);
    }

    private Vector3 PickMoveDirection()
    {
        if (Random.value < stayStillChance)
        {
            return Vector3.zero;
        }

        Vector2 offset = weaponController.NormalizedHeartOffset;
        float total = 0f;
        for (int i = 0; i < MoveDirections.Length; i++)
        {
            moveWeights[i] = AxisWeight(MoveDirections[i].x, offset.x) * AxisWeight(MoveDirections[i].y, offset.y);
            total += moveWeights[i];
        }

        if (total <= Mathf.Epsilon)
        {
            return Vector3.zero;
        }

        float roll = Random.value * total;
        for (int i = 0; i < MoveDirections.Length; i++)
        {
            roll -= moveWeights[i];
            if (roll <= 0f)
            {
                return MoveDirections[i];
            }
        }

        return MoveDirections[MoveDirections.Length - 1];
    }

    // Moving toward an edge gets less likely the closer the heart already is to it.
    private static float AxisWeight(float direction, float offset)
    {
        if (Mathf.Abs(direction) <= Mathf.Epsilon)
        {
            return 0.5f;
        }

        return Mathf.Clamp01((1f - offset * Mathf.Sign(direction)) * 0.5f);
    }

    private void UpdateHeartVelocity()
    {
        if (playerHeart == null || Time.deltaTime <= Mathf.Epsilon)
        {
            return;
        }

        Vector3 frameVelocity = (playerHeart.position - previousHeartPosition) / Time.deltaTime;
        previousHeartPosition = playerHeart.position;
        estimatedHeartVelocity = Vector3.Lerp(
            estimatedHeartVelocity,
            frameVelocity,
            heartVelocitySmoothTime > Mathf.Epsilon ? Mathf.Clamp01(Time.deltaTime / heartVelocitySmoothTime) : 1f);
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

                if (Time.time >= nextBlockTrackingCheckTime)
                {
                    trackedBlockEndPoint = TrackedBlockEndPoint();
                    nextBlockTrackingCheckTime = Time.time + Random.Range(minBlockTrackingCheckInterval, maxBlockTrackingCheckInterval);
                }

                blockEndPoint = Vector3.SmoothDamp(blockEndPoint, trackedBlockEndPoint, ref blockEndVelocity, blockTrackingSmoothTime);

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
                else
                {
                    weaponController.UpdateBlockDrag(blockEndPoint);
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
        blockAttackOrigin = origin;
        blockInterceptT = Random.Range(minBlockIntercept, maxBlockIntercept);
        Vector3 interceptPoint = InterceptPoint(center);
        float length = Random.Range(minBlockLength, maxBlockLength);
        // Fraction of the block that sits on the start side of the intercept point; 0.5 centers it.
        float centeredFraction = Random.Range(minBlockCentered, maxBlockCentered);
        float startReach = length * centeredFraction;
        float endReach = length - startReach;

        blockStartPoint = interceptPoint + perpendicular * Mathf.Min(startReach, MaxReach(interceptPoint, perpendicular, center));
        blockEndPoint = interceptPoint - perpendicular * Mathf.Min(endReach, MaxReach(interceptPoint, -perpendicular, center));

        float startToEnd = (blockEndPoint - blockStartPoint).magnitude;
        blockInterceptFraction = startToEnd > Mathf.Epsilon
            ? (interceptPoint - blockStartPoint).magnitude / startToEnd
            : 0.5f;
        trackedBlockEndPoint = blockEndPoint;
        blockEndVelocity = Vector3.zero;

        weaponController.BeginBlock(blockStartPoint);
        blockCreateStartTime = Time.time;
        nextBlockTrackingCheckTime = Time.time + Random.Range(minBlockTrackingCheckInterval, maxBlockTrackingCheckInterval);
        defenseState = DefenseState.Creating;
    }

    // Where the incoming attack line (slash origin to heart) should cross the block; the heart moving swings this line.
    private Vector3 InterceptPoint(Vector3 center)
    {
        Vector3 target = npcHeart != null ? npcHeart.position : transform.position;
        return ClampToBounds(Vector3.LerpUnclamped(blockAttackOrigin, target, blockInterceptT), center);
    }

    // The block pivots on its start point, so holding the crossing point means stretching and swinging the free end.
    private Vector3 TrackedBlockEndPoint()
    {
        if (blockInterceptFraction <= Mathf.Epsilon)
        {
            return blockEndPoint;
        }

        Vector3 center = playCenter != null ? playCenter.position : transform.position;
        Vector3 delta = (InterceptPoint(center) - blockStartPoint) / blockInterceptFraction;
        if (delta.sqrMagnitude <= Mathf.Epsilon)
        {
            return blockEndPoint;
        }

        Vector3 direction = delta.normalized;
        return blockStartPoint + direction * Mathf.Min(delta.magnitude, MaxReach(blockStartPoint, direction, center));
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
