using UnityEngine;

[RequireComponent(typeof(Animator), typeof(GroundMovement), typeof(Rigidbody2D))]
// Mangages player animations based on movement and grounded state
// Uses the animator 
// can make the landing animation more smooth but for now is good(JumpLand can tweak to become shorter or make it so that you can't move when landing)
public class PlayerAnimator : MonoBehaviour
{
    private Animator animator;
    private Rigidbody2D rb;
    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int IsGroundedParam = Animator.StringToHash("IsGrounded");
    private static readonly int YVelocityParam = Animator.StringToHash("YVelocity");

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.6f, 0.1f);
    [SerializeField] private LayerMask groundMask;

    void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
    }


    void Update()
    {
        // Horizontal speed drives Idle from and to Walk
        float horizontalSpeed = Mathf.Abs(rb.linearVelocity.x);
        animator.SetFloat(SpeedParam, horizontalSpeed);

        // Vertical velocity drives JumpRise to JumpFall
        animator.SetFloat(YVelocityParam, rb.linearVelocity.y);

        // Grounded check 
        bool isGrounded = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundMask);
        animator.SetBool(IsGroundedParam, isGrounded);
    }
}