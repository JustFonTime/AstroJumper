using UnityEngine;

public enum FlightPathRepathReason
{
    None,
    Completed,
    Blocked,
    AnchorDrift,
    TargetChanged,
    Invalid
}

public sealed class SquadFlightPath
{
    public Vector2[] Nodes { get; }
    public int CurrentIndex { get; set; }
    public float CreatedTime { get; }
    public Vector2 AnchorCenter { get; }
    public float AnchorRadius { get; }
    public float TravelSide { get; }
    public FlightPathRepathReason LastRepathReason { get; set; }

    public int NodeCount => Nodes != null ? Nodes.Length : 0;

    public bool HasCurrentNode => Nodes != null && Nodes.Length > 0 && CurrentIndex >= 0 && CurrentIndex < Nodes.Length;

    public Vector2 CurrentNode => HasCurrentNode ? Nodes[CurrentIndex] : AnchorCenter;

    public SquadFlightPath(
        Vector2[] nodes,
        int currentIndex,
        float createdTime,
        Vector2 anchorCenter,
        float anchorRadius,
        float travelSide,
        FlightPathRepathReason lastRepathReason)
    {
        Nodes = nodes ?? new Vector2[0];
        CurrentIndex = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, Nodes.Length - 1));
        CreatedTime = createdTime;
        AnchorCenter = anchorCenter;
        AnchorRadius = Mathf.Max(1f, anchorRadius);
        TravelSide = Mathf.Abs(travelSide) < 0.01f ? 1f : Mathf.Sign(travelSide);
        LastRepathReason = lastRepathReason;
    }
}
