using UnityEngine;
using System;


/// <summary>
/// Attack ID, so other things know what side of the battle this thing is on 
/// </summary>
public class TeamAgent : MonoBehaviour
{
    //what team are they on
    [SerializeField] private int teamId = 0;
    public int TeamId => teamId;


    public TargetSlots Slots { get; private set; }

    private void Awake()
    {
        Slots = GetComponent<TargetSlots>();
        if (Slots != null)
            Slots.SetTeam(teamId);
    }

    private void OnEnable()
    {
        //Debug.Log($"TeamAgent OnEnable team={TeamId}\n{Environment.StackTrace}");
        TeamRegistry.Register(this);
    }

    private void OnDisable()
    {
        //Debug.Log($"TeamAgent OnDisable team={TeamId}\n{Environment.StackTrace}");

        TeamRegistry.Unregister(this);
    }

    public void SetTeam(int newTeamId)
    {
        teamId = newTeamId;
        if (Slots != null)
            Slots.SetTeam(teamId);
    }
}
