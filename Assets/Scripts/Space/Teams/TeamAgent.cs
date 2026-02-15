using UnityEngine;


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
    }

    private void OnEnable()
    {
        TeamRegistry.Register(this);
    }

    private void OnDisable()
    {
        TeamRegistry.Unregister(this);
    }

    public void SetTeam(int newTeamId)
    {
        teamId = newTeamId;
    }
}