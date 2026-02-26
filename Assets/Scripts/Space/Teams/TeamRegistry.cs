using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class TeamRegistry : MonoBehaviour
{
    //All active Agetns
    private static readonly List<TeamAgent> agents = new List<TeamAgent>(256);

    public static void Register(TeamAgent agent)
    {
        if (agent == null) return;
        if (!agents.Contains(agent))
            agents.Add(agent);
    }

    public static void Unregister(TeamAgent agent)
    {
        if (agent == null) return;
        agents.Remove(agent);
    }

    public static bool IsHostile(int myTeam, int otherTeam) => myTeam != otherTeam;

    public static TeamAgent FindNearestHostile(TeamAgent seeker, Vector2 seekerPos, float radius, bool requireOpenSlot)
    {
        if (seeker == null) return null;
        float r2 = radius <= 0f ? float.PositiveInfinity : radius * radius;

        TeamAgent nearest = null;
        float bestD2 = float.PositiveInfinity;

        for (int i = 0; i < agents.Count; i++)
        {
            TeamAgent agent = agents[i];
            if (agent == null || agent == seeker) continue; //skip if null or itslef
            if (!agent.isActiveAndEnabled) continue; //skip if agent isnt active (maybe pooled)

            if (!IsHostile(seeker.TeamId, agent.TeamId)) continue; //skip if not hositle

            Vector2 p = agent.transform.position;
            float d2 = (p - seekerPos).magnitude;

            if (d2 > r2) continue; //skip if outside range

            //check for open slot 
            if (requireOpenSlot)
            {
                TargetSlots slots = agent.Slots;
                if (slots != null && slots.IsFull) continue; //skip if there are no open slots
            }

            if (d2 < bestD2)
            {
                bestD2 = d2;
                nearest = agent;
            }
        }

        return nearest;
    }

    public static TeamAgent FindAndClaim(TeamAgent attacker, Vector2 attackerPos, float radius)
    {
        if (attacker == null) return null;

        float r2 = radius <= 0f ? float.PositiveInfinity : radius * radius;

        TeamAgent best = null;
        float bestD2 = float.PositiveInfinity;

        for (int i = 0; i < agents.Count; i++)
        {
            TeamAgent agent = agents[i];
            if (agent == null || agent == attacker) continue; //skip if null or itslef
            if (!agent.isActiveAndEnabled) continue; //skip if agent isnt active (maybe pooled)

            if (!IsHostile(attacker.TeamId, agent.TeamId)) continue; //skip if not hositle

            Vector2 p = agent.transform.position;
            float d2 = (p - attackerPos).sqrMagnitude;
            if (d2 > r2) continue;

            if (d2 >= bestD2) continue;

            // If it has slots, require claim success
            TargetSlots slots = agent.Slots;
            if (slots != null)
            {
                if (slots.IsFull) continue;
                if (!slots.TryClaim(attacker)) continue;
            }

            bestD2 = d2;
            best = agent;
        }

        return best;
    }
}