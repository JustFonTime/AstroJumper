using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class TargetSlots : MonoBehaviour
{
    [SerializeField] private int teamId = 0;
    [SerializeField] private int maxAttackers = 3;

    //whos currently attacking
    private readonly HashSet<TeamAgent> attackers = new HashSet<TeamAgent>();

    public int MaxAttackers => maxAttackers;
    public int CurrentAttackers => attackers.Count;
    public int FreeSlots => Mathf.Max(0, maxAttackers - CurrentAttackers);

    public bool HasAttacker(TeamAgent attacker) => attackers.Contains(attacker);
    public bool IsFull => attackers.Count == maxAttackers;

    public bool TryClaim(TeamAgent attacker)
    {
        if (attacker == null) return false;
        if (attackers.Contains(attacker)) return true; // already clkaimed
        if (attackers.Count >= maxAttackers) return false; // full

        attackers.Add(attacker);
        return true;
    }

    public void Release(TeamAgent attacker)
    {
        if (attacker == null) return;
        attackers.Remove(attacker);
    }


    public void ReleaseAll()
    {
        attackers.Clear();
    }

    private void OnDisable()
    {
        attackers.Clear();
    }

    public void SetTeam(int newTeamId)
    {
        teamId = newTeamId;
    }
}