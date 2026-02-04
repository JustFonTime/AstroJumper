using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    private enum State { Patrol, Chase, Attack, Knockback, Return }

    [Header("Refs")]
    [SerializeField] private EnemySensors sensors;
    [SerializeField] private EnemyMotor motor;

    [Header("Chase/Attack")]
    [SerializeField] private float chaseRange = 4f;
    [SerializeField] private float attackRange = 1.2f;
    [SerializeField] private float attackCooldown = 1.0f;


    [Header("Aggro Memory")]
    [SerializeField] private float loseSightGrace = 0.5f;
    [SerializeField] private float investigateDuration = 2.0f;
    [SerializeField] private float investigateTolerance = 0.15f;

    [Header("Return")]
    [SerializeField] private Transform homePoint;
    [SerializeField] private float homeTolerance = 0.2f;

    private State state = State.Patrol;
    private Transform player;
    private float nextAttackTime;


    private Vector2 lastSeenPos;
    private float lastSeenTime = -999f;


    private void Reset()
    {
        sensors = GetComponentInChildren<EnemySensors>();
        motor = GetComponent<EnemyMotor>();
    }

    private void Update()
    {
        // want to add sleep off screen for better performance later
        // if (!IsOnScreen()) return . . .

        switch (state)
        {
            case State.Patrol: TickPatrol(); break;
            case State.Chase: TickChase(); break;
            case State.Attack: TickAttack(); break;
            case State.Return: TickReturn(); break;
            case State.Knockback:  break;
        }
    }

    private void TickPatrol()
    {
        //Check for Player
        Transform seen = sensors.DetectPlayer();
        if (seen && Mathf.Abs(seen.position.x - transform.position.x) <= chaseRange)
        {
            player = seen;
            state = State.Chase;
            return;
        }

        // Obstacle Handling (Only flip if we hit something)
        if (sensors.WallAhead() || sensors.NoGroundAhead())
        {
            motor.Flip();
        }

        motor.Move();
    }

    private void TickChase()
    {
        //  detection range and tracking last seen position
        Transform seen = sensors.DetectPlayer();
        if (seen)
        {
            player = seen;
            lastSeenPos = player.position;
            lastSeenTime = Time.time;
        }

        // If we haven't seen the player recently, give up and return
        float timeSinceSeen = Time.time - lastSeenTime;
        if (player == null && timeSinceSeen > investigateDuration)
        {
            state = State.Return;
            return;
        }

        // choose between player first then vs last seen locations 
        Vector2 chaseTarget = (player != null) ? (Vector2)player.position : lastSeenPos;

        // Drop aggro after grace if player is far beyond chaseRange
        if (player != null)
        {
            float distToPlayer = Mathf.Abs(player.position.x - transform.position.x);
            if (distToPlayer > chaseRange && timeSinceSeen > loseSightGrace)
            {
                // player not found anymore
                player = null;
            }
        }

        // Face target
        motor.SetFacingToward(chaseTarget.x);

        // Attack only if we currently have the player
        if (player != null)
        {
            float dist = Mathf.Abs(player.position.x - transform.position.x);
            if (dist <= attackRange)
            {
                motor.StopHorizontal();
                state = State.Attack;
                return;
            }
        }

        // If we reached the last seen spot and still didn't reacquire, return
        if (player == null && Mathf.Abs(chaseTarget.x - transform.position.x) <= investigateTolerance)
        {
            state = State.Return;
            return;
        }

        //  don't chase off ledges 
        if (sensors.NoGroundAhead() || sensors.WallAhead())
        {
            motor.StopHorizontal();
            //  state = State.Patrol; 
            return;
        }

        motor.Move();
    }

    private void TickAttack()
    {
        if (!player)
        {
            state = State.Return;
            return;
        }

        float dist = Mathf.Abs(player.position.x - transform.position.x);
        if (dist > attackRange)
        {
            state = State.Chase;
            return;
        }

        motor.SetFacingToward(player.position.x);

        if (Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + attackCooldown;
            DoAttack(); // hook into melee/shoot later
        }
    }

    private void DoAttack()
    {
        // Placeholder: call animation trigger, spawn projectile, damage hitbox, etc.
        // Debug.Log("Attack!");
    }

    private void TickReturn()
    {
        if (!homePoint)
        {
            state = State.Patrol;
            return;
        }

        float dx = homePoint.position.x - transform.position.x;
        if (Mathf.Abs(dx) <= homeTolerance)
        {
            motor.StopHorizontal();
            state = State.Patrol;
            return;
        }

        motor.SetFacingToward(homePoint.position.x);

        // Turn around if wall/ledge blocks return
        if (sensors.WallAhead() || sensors.NoGroundAhead())
        {
            // If you can't safely return, just patrol
            state = State.Patrol;
            return;
        }

        motor.Move();
    }

    // Call this from dmg system
    public void EnterKnockback(Vector2 force, float knockbackTime = 0.2f)
    {
        state = State.Knockback;
        motor.ApplyKnockback(force);
        Invoke(nameof(ExitKnockback), knockbackTime);
    }

    private void ExitKnockback()
    {
        state = State.Return;
    }
}
