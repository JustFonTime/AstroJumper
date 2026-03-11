using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class FlagshipSlowMovement : MonoBehaviour
{
    private enum SidePreference
    {
        Auto,
        Left,
        Right
    }

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float turnSpeedDegPerSec = 10f;
    [SerializeField] private float roamRadius = 20f;
    [SerializeField] private float arriveDistance = 2f;
    [SerializeField] private float minRetargetDelay = 1.5f;
    [SerializeField] private float maxRetargetDelay = 4f;

    [Header("Boundary Roam")]
    [SerializeField] private bool useBoundaryControllerRoam = true;
    [SerializeField] private SpaceArenaBoundaryController boundaryController;
    [SerializeField] private bool autoFindBoundaryController = true;
    [SerializeField] [Range(0.1f, 1f)] private float boundaryRoamRadiusRatio = 0.82f;
    [SerializeField] private float sideBoundaryBuffer = 12f;
    [SerializeField] private SidePreference sidePreference = SidePreference.Auto;

    [Header("Inner Exclusion")]
    [SerializeField] [Range(0f, 0.95f)] private float innerExclusionRadiusRatio = 0.32f;
    [SerializeField] private float innerExclusionPadding = 8f;

    [Header("Debug")]
    [SerializeField] private bool drawRoamRadiusDebug = true;
    [SerializeField] private bool drawRoamRadiusOnlyWhenSelected = false;
    [SerializeField] private Color roamRadiusDebugColor = new Color(0.2f, 1f, 0.2f, 0.95f);

    [Header("Path Debug")]
    [SerializeField] private bool drawPathTrailDebug = true;
    [SerializeField] private bool drawPathForNonPlayerTeamsOnly = true;
    [SerializeField] [Range(8, 256)] private int maxPathSamples = 96;
    [SerializeField] private float pathSampleInterval = 0.2f;
    [SerializeField] private float minPathSampleDistance = 0.35f;
    [SerializeField] private bool drawCurrentTargetDebug = true;
    [SerializeField] private float targetMarkerRadius = 1.2f;
    [SerializeField] private Color pathTrailColor = new Color(1f, 0.45f, 0.15f, 0.95f);
    [SerializeField] private Color targetLinkColor = new Color(1f, 0.9f, 0.2f, 0.9f);

    private Rigidbody2D rb;
    private TeamAgent teamAgent;
    private Vector2 anchorPosition;
    private Vector2 currentTarget;
    private float retargetTimer;
    private int cachedSideSign;

    private readonly List<Vector3> pathSamples = new List<Vector3>(128);
    private float nextPathSampleTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        teamAgent = GetComponent<TeamAgent>();
        anchorPosition = rb != null ? rb.position : (Vector2)transform.position;

        ResolveBoundaryController();
        cachedSideSign = ResolvePreferredSideSign(GetBoundaryCenterFallback());
        PickNewTarget();
        ResetPathSamples();
    }

    private void OnEnable()
    {
        if (rb != null)
            anchorPosition = rb.position;

        ResolveBoundaryController();
        cachedSideSign = ResolvePreferredSideSign(GetBoundaryCenterFallback());
        PickNewTarget();
        ResetPathSamples();
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        rb.angularVelocity = 0f;

        retargetTimer -= Time.fixedDeltaTime;
        Vector2 toTarget = currentTarget - rb.position;

        if (toTarget.magnitude <= arriveDistance || retargetTimer <= 0f)
        {
            PickNewTarget();
            toTarget = currentTarget - rb.position;
        }

        if (toTarget.sqrMagnitude <= 0.01f)
        {
            rb.linearVelocity = Vector2.zero;
            RecordPathSample();
            return;
        }

        // If we're drifting too close to the inner exclusion circle, steer outward.
        if (TryGetBoundaryRadii(out Vector2 center, out float outerRadius, out float innerRadius))
        {
            float innerGuard = innerRadius + Mathf.Max(0f, innerExclusionPadding);
            Vector2 fromCenter = rb.position - center;
            float dist = fromCenter.magnitude;
            if (dist < innerGuard && dist > 0.001f)
            {
                Vector2 outward = fromCenter / dist;
                float push01 = Mathf.Clamp01(1f - (dist / innerGuard));
                toTarget = Vector2.Lerp(toTarget, outward * Mathf.Max(1f, outerRadius * 0.35f), push01);
            }
        }

        Vector2 desiredDir = toTarget.normalized;
        float desiredAngle = Mathf.Atan2(desiredDir.y, desiredDir.x) * Mathf.Rad2Deg - 90f;
        float newAngle = Mathf.MoveTowardsAngle(rb.rotation, desiredAngle, turnSpeedDegPerSec * Time.fixedDeltaTime);
        rb.MoveRotation(newAngle);
        rb.linearVelocity = (Vector2)transform.up * moveSpeed;

        RecordPathSample();
    }

    private void PickNewTarget()
    {
        if (!TryPickBoundarySideTarget(out currentTarget))
            currentTarget = anchorPosition + Random.insideUnitCircle * roamRadius;

        retargetTimer = Random.Range(minRetargetDelay, maxRetargetDelay);
    }

    private bool TryPickBoundarySideTarget(out Vector2 target)
    {
        target = Vector2.zero;

        if (!TryGetBoundaryRadii(out Vector2 center, out float outerRadius, out float innerRadius))
            return false;

        float laneBuffer = Mathf.Clamp(sideBoundaryBuffer, 0f, outerRadius * 0.8f);

        int sideSign = cachedSideSign;
        if (sideSign == 0)
        {
            sideSign = ResolvePreferredSideSign(center);
            cachedSideSign = sideSign;
        }

        float minAllowedRadius = Mathf.Clamp(innerRadius + Mathf.Max(0f, innerExclusionPadding), 0f, outerRadius - 0.5f);

        for (int i = 0; i < 36; i++)
        {
            Vector2 candidate = center + Random.insideUnitCircle * outerRadius;

            float signedSide = (candidate.x - center.x) * sideSign;
            if (signedSide < laneBuffer)
                continue;

            float radialDistance = Vector2.Distance(candidate, center);
            if (radialDistance < minAllowedRadius)
                continue;

            target = candidate;
            return true;
        }

        float fallbackRadius = Mathf.Lerp(minAllowedRadius, outerRadius, 0.65f);
        Vector2 fallbackDir = new Vector2(sideSign, Random.Range(-0.75f, 0.75f)).normalized;
        target = center + fallbackDir * fallbackRadius;

        float finalSignedSide = (target.x - center.x) * sideSign;
        if (finalSignedSide < laneBuffer)
            target.x = center.x + sideSign * laneBuffer;

        return true;
    }

    private bool TryGetBoundaryRadii(out Vector2 center, out float outerRadius, out float innerRadius)
    {
        center = Vector2.zero;
        outerRadius = 0f;
        innerRadius = 0f;

        if (!useBoundaryControllerRoam)
            return false;

        ResolveBoundaryController();
        if (boundaryController == null)
            return false;

        float safeRadius = boundaryController.SafeRadius;
        if (safeRadius <= 0.01f)
            return false;

        center = boundaryController.CenterPosition;
        outerRadius = Mathf.Max(1f, safeRadius * Mathf.Clamp(boundaryRoamRadiusRatio, 0.1f, 1f));

        float innerRaw = safeRadius * Mathf.Clamp(innerExclusionRadiusRatio, 0f, 0.95f);
        innerRadius = Mathf.Clamp(innerRaw, 0f, Mathf.Max(0f, outerRadius - 0.75f));
        return true;
    }

    private void ResolveBoundaryController()
    {
        if (!useBoundaryControllerRoam)
            return;

        if (boundaryController != null)
            return;

        if (!autoFindBoundaryController)
            return;

        SpaceArenaBoundaryController[] boundaries = FindObjectsOfType<SpaceArenaBoundaryController>(true);
        if (boundaries != null && boundaries.Length > 0)
            boundaryController = boundaries[0];
    }

    private int ResolvePreferredSideSign(Vector2 boundaryCenter)
    {
        switch (sidePreference)
        {
            case SidePreference.Left:
                return -1;

            case SidePreference.Right:
                return 1;

            case SidePreference.Auto:
            default:
                if (teamAgent == null)
                    teamAgent = GetComponent<TeamAgent>();

                if (teamAgent != null)
                {
                    if (teamAgent.TeamId == 0)
                        return -1;

                    if (teamAgent.TeamId > 0)
                        return 1;
                }

                float delta = transform.position.x - boundaryCenter.x;
                if (Mathf.Abs(delta) <= 0.001f && rb != null)
                    delta = rb.position.x - boundaryCenter.x;

                if (Mathf.Abs(delta) <= 0.001f)
                    return cachedSideSign == 0 ? 1 : cachedSideSign;

                return delta < 0f ? -1 : 1;
        }
    }

    private Vector2 GetBoundaryCenterFallback()
    {
        if (boundaryController != null)
            return boundaryController.CenterPosition;

        return rb != null ? rb.position : (Vector2)transform.position;
    }

    private void OnDrawGizmos()
    {
        if (!drawRoamRadiusDebug || drawRoamRadiusOnlyWhenSelected)
            return;

        DrawRoamRadiusGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawRoamRadiusDebug)
            return;

        DrawRoamRadiusGizmo();
    }

    private void DrawRoamRadiusGizmo()
    {
        if (TryGetBoundaryRadii(out Vector2 center, out float outerRadius, out float innerRadius))
        {
            Gizmos.color = roamRadiusDebugColor;

            // Outer roam limit (inside this).
            Gizmos.DrawWireSphere(center, outerRadius);

            // Inner exclusion zone (flagships should stay out of this).
            if (innerRadius > 0.01f)
                Gizmos.DrawWireSphere(center, innerRadius);

            // Team side divider.
            Gizmos.DrawLine(
                new Vector3(center.x, center.y - outerRadius, transform.position.z),
                new Vector3(center.x, center.y + outerRadius, transform.position.z));

            int sideSign = cachedSideSign != 0 ? cachedSideSign : ResolvePreferredSideSign(center);
            Vector3 marker = new Vector3(center.x + (outerRadius * 0.72f * sideSign), center.y, transform.position.z);
            Gizmos.DrawWireSphere(marker, Mathf.Max(0.75f, outerRadius * 0.04f));
        }
        else
        {
            float fallbackRadius = Mathf.Max(0.01f, roamRadius);
            Vector3 fallbackCenter = Application.isPlaying ? (Vector3)anchorPosition : transform.position;

            Gizmos.color = roamRadiusDebugColor;
            Gizmos.DrawWireSphere(fallbackCenter, fallbackRadius);
        }

        DrawPathDebugGizmos();
    }

    private void DrawPathDebugGizmos()
    {
        if (!ShouldDrawPathDebug())
            return;

        Vector3 currentPos = Application.isPlaying && rb != null
            ? new Vector3(rb.position.x, rb.position.y, transform.position.z)
            : transform.position;

        if (pathSamples.Count > 0)
        {
            Gizmos.color = pathTrailColor;

            for (int i = 1; i < pathSamples.Count; i++)
                Gizmos.DrawLine(pathSamples[i - 1], pathSamples[i]);

            Vector3 last = pathSamples[pathSamples.Count - 1];
            if ((currentPos - last).sqrMagnitude > 0.001f)
                Gizmos.DrawLine(last, currentPos);
        }

        if (Application.isPlaying && drawCurrentTargetDebug)
        {
            Vector3 targetPos = new Vector3(currentTarget.x, currentTarget.y, transform.position.z);

            Gizmos.color = targetLinkColor;
            Gizmos.DrawLine(currentPos, targetPos);
            Gizmos.DrawWireSphere(targetPos, Mathf.Max(0.1f, targetMarkerRadius));
        }
    }

    private bool ShouldDrawPathDebug()
    {
        if (!drawPathTrailDebug)
            return false;

        if (!drawPathForNonPlayerTeamsOnly)
            return true;

        if (teamAgent == null)
            teamAgent = GetComponent<TeamAgent>();

        return teamAgent == null || teamAgent.TeamId != 0;
    }

    private void ResetPathSamples()
    {
        pathSamples.Clear();
        nextPathSampleTime = 0f;

        Vector3 currentPos = transform.position;
        pathSamples.Add(currentPos);
    }

    private void RecordPathSample()
    {
        if (!Application.isPlaying || rb == null)
            return;

        if (!ShouldDrawPathDebug())
            return;

        float sampleStep = Mathf.Max(0.02f, pathSampleInterval);
        if (Time.time < nextPathSampleTime)
            return;

        nextPathSampleTime = Time.time + sampleStep;

        Vector3 point = new Vector3(rb.position.x, rb.position.y, transform.position.z);
        if (pathSamples.Count > 0)
        {
            Vector3 last = pathSamples[pathSamples.Count - 1];
            float minDist = Mathf.Max(0f, minPathSampleDistance);
            if ((point - last).sqrMagnitude < minDist * minDist)
                return;
        }

        pathSamples.Add(point);

        int keep = Mathf.Clamp(maxPathSamples, 8, 256);
        while (pathSamples.Count > keep)
            pathSamples.RemoveAt(0);
    }
}
