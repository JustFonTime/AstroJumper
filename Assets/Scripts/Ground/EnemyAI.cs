using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    private enum State { Patrol, Chase, Attack, Knockback, Return }

    [System.Flags]
    public enum AttackType // different attack types
    {
        None = 0,
        Melee = 1 << 0,
        Ranged = 1 << 1
    }

    [Header("Refs")]
    [SerializeField] private EnemySensors sensors;
    [SerializeField] private EnemyMotor motor;

    [Header("Chase/Attack")]
    [SerializeField] private float chaseRange = 4f;
    [SerializeField] private float attackCooldown = 1.0f;

    // for giving the player a chance to escape or hide after being seen also not instant deagro
    [Header("Aggro Memory")] 
    [SerializeField] private float loseSightGrace = 0.5f;
    [SerializeField] private float investigateDuration = 2.0f;
    [SerializeField] private float investigateTolerance = 0.15f;


    [Header("Attack Capabilities")]
    [SerializeField] private AttackType attackTypes = AttackType.Ranged;
    
    // numbers are defaults for melee and ranged reach
    [Header("Attack Ranges")]
    // this is a buffer because it keeps stopping right before range and not being able to atk
    [SerializeField] private float meleeEnterBuffer = 0.25f;
                                                             
    [SerializeField] private float meleeRange = 2f;
    [SerializeField] private float rangedRange = 7f;


    [Header("Ranged Attack")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private EnemyProjectile projectilePrefab;
    [SerializeField] private float projectileSpeed = 8f;
    [SerializeField] private float minShootRange = 0.0f;
    
    //leash distance that makes the enemy give up and return back to home point
    [Header("Return")]
    [SerializeField] private Transform homePoint;
    [SerializeField] private float homeTolerance = 0.2f;
    [SerializeField] private float maxLeashDistance = 15f;
    

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
        // want to add sleep off screen for better performance later (pooling or sleep state idk yet)
        // if (!IsOnScreen()) return . . .

        switch (state)
        {
            case State.Patrol: TickPatrol(); break;
            case State.Chase: TickChase(); break;
            case State.Attack: TickAttack(); break;
            case State.Return: TickReturn(); break;
            case State.Knockback: break;
        }
    }

    //debugging helper to see state changes
    private void ChangeState(State newState, string reason)
    {
        if (state == newState) return;

        Debug.Log(
            $"[EnemyAI:{name}] {state} -> {newState} | Reason: {reason}",
            this
        );

        state = newState;
    }

    private float GetMeleeEnterRange() => meleeRange + meleeEnterBuffer;

    private void TickPatrol()
    {
        // Check if we've wandered too far from home while patrolling
        if (homePoint)
        {
            float distFromHome = Mathf.Abs(transform.position.x - homePoint.position.x);
            if (distFromHome > maxLeashDistance)
            {
                ChangeState(State.Return, "Wandered too far from home during patrol");
                return;
            }
        }

        //Check for Player
        Transform seen = sensors.DetectPlayer();
        if (seen && Mathf.Abs(seen.position.x - transform.position.x) <= chaseRange)
        {
            player = seen;
            lastSeenPos = player.position;
            lastSeenTime = Time.time;
            ChangeState(State.Chase, "Player detected in patrol");
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

        // If we have a player target, evaluate attack/chase
        if (player != null)
        {
            float distToPlayer = Mathf.Abs(player.position.x - transform.position.x);

            // Check if player is too far (beyond chase range + grace)
            float timeSinceSeen = Time.time - lastSeenTime;
            if (distToPlayer > chaseRange && timeSinceSeen > loseSightGrace)
            {
                // player not found anymore
                player = null;
            }
            else
            {
                // We have the player - check attack range
                motor.SetFacingToward(player.position.x);

                // Melee only: enter attack when in melee range
                if (attackTypes == AttackType.Melee)
                {
                    if (distToPlayer <= GetMeleeEnterRange())
                    {
                        ChangeState(State.Attack, "Entered melee range");
                        return;
                    }
                }
                else
                {
                    // Ranged or hybrid
                    float maxAttackRange = GetMaxAttackRange();
                    if (distToPlayer <= maxAttackRange)
                    {
                        ChangeState(State.Attack, "Entered attack range");
                        return;
                    }
                }

                // Not in attack range, continue chasing
                // Don't chase off ledges or into walls
                if (sensors.NoGroundAhead() || sensors.WallAhead())
                {
                    motor.StopHorizontal();
                    return;
                }

                motor.Move();
                return;
            }
        }

        // No current player target so investigate last position
        float timeSinceLastSeen = Time.time - lastSeenTime;

        // Give up after time limit and return home if player isn't found at last seen position
        if (timeSinceLastSeen > investigateDuration)
        {
            ChangeState(State.Return, "Player lost for too long");
            return;
        }

        // Move toward last seen position and check if we reached the investigation point
        motor.SetFacingToward(lastSeenPos.x);

        if (Mathf.Abs(lastSeenPos.x - transform.position.x) <= investigateTolerance)
        {
            ChangeState(State.Return, "Reached last seen position, no player found");
            return;
        }

        // Stay on platform don't fall off or run into walls while investigating
        if (sensors.NoGroundAhead() || sensors.WallAhead())
        {
            motor.StopHorizontal();
            // If blocked from investigating, give up faster
            if (timeSinceLastSeen > loseSightGrace)
            {
                ChangeState(State.Return, "Blocked from investigating");
            }
            return;
        }

        motor.Move();
    }

    private void TickAttack()
    {
        if (!player)
        {
            ChangeState(State.Return, "Lost player in attack state");
            return;
        }

        float dist = Mathf.Abs(player.position.x - transform.position.x);
        motor.SetFacingToward(player.position.x);

        // Melee logic 
        if (attackTypes == AttackType.Melee)
        {
            // If player is out of  range go back to chase
            if (dist > GetMeleeEnterRange())
            {
                ChangeState(State.Chase, "Player out of melee range");
                return;
            }

            // If not in true hit range yet, continue to get closer
            if (dist > meleeRange)
            {
                motor.Move();
                return;
            }

            // Now we're close enough to strike - stop and attack
            motor.StopHorizontal();

            if (Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + attackCooldown;
                DoAttack();
            }
            return;
        }

        // ranged and hybrid logic
        float maxAttackRange = GetMaxAttackRange();
        if (dist > maxAttackRange)
        {
            ChangeState(State.Chase, "Player out of attack range");
            return;
        }

        motor.StopHorizontal();

        if (Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + attackCooldown;
            DoAttack();
        }
    }


    //for attacking type (melee vs ranged or both) can change within the hierachy 
    // There can customize the range of each attack type, will prioriize melee if they have both and
    // if in ranged of melee
    private void DoAttack()
    {
        if (!player)
        {
            ChangeState(State.Return, "Player lost before attack");
            return;
        }

        float dist = Vector2.Distance(transform.position, player.position);
        if (attackTypes.HasFlag(AttackType.Melee) && dist <= meleeRange)
        {
            DoMeleeAttack();
            return;
        }

        if (attackTypes.HasFlag(AttackType.Ranged) &&
            dist >= minShootRange &&
            dist <= rangedRange)
        {
            DoRangedAttack();
            return;
        }

        ChangeState(State.Chase, "Player out of attack range during attack");
    }

    private float GetMaxAttackRange()
    {
        float max = 0f;

        if (attackTypes.HasFlag(AttackType.Melee))
            max = Mathf.Max(max, meleeRange);

        if (attackTypes.HasFlag(AttackType.Ranged))
            max = Mathf.Max(max, rangedRange);

        return max;
    }

    private void DoMeleeAttack()
    {
        // Placeholder
        Debug.Log($"{name} performs MELEE attack");
    }

    private void DoRangedAttack()
    {
        if (!projectilePrefab || !firePoint) return;

        Vector2 dir = (player.position - firePoint.position).normalized;
        EnemyProjectile proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        proj.Fire(dir * projectileSpeed);
    }


    private void TickReturn()
    {
        if (!homePoint)
        {
            ChangeState(State.Patrol, "No home point set");
            return;
        }

        float distFromHome = Mathf.Abs(transform.position.x - homePoint.position.x);

        // Check if we've reached home
        if (distFromHome <= homeTolerance)
        {
            motor.StopHorizontal();
            ChangeState(State.Patrol, "Reached home point");
            return;
        }

        // If player is detected while returning, chase/atk again, chase/attack always takes priority
        Transform seen = sensors.DetectPlayer();
        if (seen)
        {
            float distToPlayer = Mathf.Abs(seen.position.x - transform.position.x);
            if (distToPlayer <= chaseRange)
            {
                player = seen;
                lastSeenPos = player.position;
                lastSeenTime = Time.time;
                ChangeState(State.Chase, "Player detected while returning");
                return;
            }
        }

        // Move toward home
        motor.SetFacingToward(homePoint.position.x);

        // Don't walk off ledges or into walls while returning
        if (sensors.NoGroundAhead() || sensors.WallAhead())
        {
            motor.StopHorizontal();
            return;
        }

        motor.Move();
    }

    // Call this from dmg system
    public void EnterKnockback(Vector2 force, float knockbackTime = 0.2f)
    {
        ChangeState(State.Knockback, "Entered knockback");
        motor.ApplyKnockback(force);
        Invoke(nameof(ExitKnockback), knockbackTime);
    }

    private void ExitKnockback()
    {
        ChangeState(State.Return, "Exiting knockback");
    }
}