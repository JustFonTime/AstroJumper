using UnityEngine;

public readonly struct ReinforcementRequest
{
    public EnemySquadController Squad { get; }
    public int TeamId { get; }
    public int DesiredCount { get; }
    public int CurrentCount { get; }
    public int MissingCount { get; }
    public Transform FocusTarget { get; }
    public Vector2 RallyPoint { get; }
    public float RequestTime { get; }

    public ReinforcementRequest(
        EnemySquadController squad,
        int teamId,
        int desiredCount,
        int currentCount,
        Transform focusTarget,
        Vector2 rallyPoint,
        float requestTime)
    {
        Squad = squad;
        TeamId = teamId;
        DesiredCount = Mathf.Max(0, desiredCount);
        CurrentCount = Mathf.Max(0, currentCount);
        MissingCount = Mathf.Max(0, DesiredCount - CurrentCount);
        FocusTarget = focusTarget;
        RallyPoint = rallyPoint;
        RequestTime = requestTime;
    }

    public bool IsValid => Squad != null && MissingCount > 0;
}
