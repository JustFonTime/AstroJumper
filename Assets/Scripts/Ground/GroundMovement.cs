using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class GroundMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float acceleration = 60f;
    [SerializeField] private float deceleration = 70f;

    [Header("Jump")]
    [SerializeField] private float jumpVelocity = 18f; // tweak this with gravity( grav set to 3 so fall fast enough plus jumps feels decent)
    [SerializeField] private float coyoteTime = 0.08f; // allows for jumping shortly after leaving ground(floating jump for some frames) 
    [SerializeField] private float jumpBufferTime = 0.10f;

    [Header("Jump (Extras)")]
    [SerializeField] private bool enableDoubleJump = false;
    [SerializeField] private int maxAirJumps = 1; // 1 = double jump, 2 = triple . . .
    private int airJumpsRemaining;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.6f, 0.1f);
    [SerializeField] private LayerMask groundMask;   // Ground + OneWayPlatform for jumping 
    [SerializeField] private LayerMask oneWayMask;   // OneWayPlatform only for dropping through select platforms

    [Header("Drop Through")]
    [SerializeField] private float dropDuration = 0.5f;

    [Header("Input Actions")]
    [SerializeField] private InputActionAsset actionsAsset; 
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string jumpActionName = "Jump";
    [SerializeField] private string dropActionName = "Drop";

    private Rigidbody2D rb;
    private Collider2D playerCol;

    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction dropAction;

    private float xInput;
    private float coyoteTimer;
    private float jumpBufferTimer;

    private bool isGrounded;
    private bool isDropping;
    public bool isFacingRight = true; 

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCol = GetComponent<Collider2D>();

        if (groundCheck == null)
            Debug.LogError("GroundMovement: groundCheck is not assigned.");

        if (actionsAsset == null)
            Debug.LogError("GroundMovement: actionsAsset is not assigned (drag InputSystem_Actions in).");

        var map = actionsAsset.FindActionMap(actionMapName, true);
        moveAction = map.FindAction(moveActionName, true);
        jumpAction = map.FindAction(jumpActionName, true);
        dropAction = map.FindAction(dropActionName, true);
    }

    private void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
        dropAction.Enable();

        jumpAction.performed += OnJump;
        dropAction.performed += OnDrop;
    }

    private void OnDisable()
    {
        jumpAction.performed -= OnJump;
        dropAction.performed -= OnDrop;

        moveAction.Disable();
        jumpAction.Disable();
        dropAction.Disable();
    }

    private void Update()
    {
        // Read horizontal movement from Move Vector2
        Vector2 move = moveAction.ReadValue<Vector2>();
        xInput = Mathf.Clamp(move.x, -1f, 1f);

        //fixing timer so it counts down properly
        jumpBufferTimer -= Time.deltaTime;
        if (jumpBufferTimer < 0f) jumpBufferTimer = 0f;

        // Ground check + timers
        isGrounded = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundMask);

        if (isGrounded)
        {
            coyoteTimer = coyoteTime;

            // Reset air jumps when you touch the ground
            airJumpsRemaining = enableDoubleJump ? maxAirJumps : 0;
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
        }
    }

    private void FixedUpdate()
    {
        HandleHorizontal();
        HandleJumpBuffered();
    }

    private void OnJump(InputAction.CallbackContext ctx)
    {
        jumpBufferTimer = jumpBufferTime; // buffer jump press
    }

    private void OnDrop(InputAction.CallbackContext ctx)
    {
        TryDropThrough(); // Only allows one press 
    }

    private void HandleHorizontal()
    {
        float targetSpeed = xInput * moveSpeed;
        float speedDiff = targetSpeed - rb.linearVelocity.x;
        float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? acceleration : deceleration;

        rb.AddForce(new Vector2(speedDiff * accelRate, 0f));

        float clampedX = Mathf.Clamp(rb.linearVelocity.x, -moveSpeed, moveSpeed);
        rb.linearVelocity = new Vector2(clampedX, rb.linearVelocity.y);
        if (xInput > 0.01f)
        {
            isFacingRight = true;
        }
        else if (xInput < -0.01f)
        {
            isFacingRight = false;
        }
    }

    private void HandleJumpBuffered()
    {
        if (isDropping) return;

        bool buffered = jumpBufferTimer > 0f;
        if (!buffered) return;

        bool canGroundJump = coyoteTimer > 0f; // grounded or just left ground (coyote)
        bool canAirJump = enableDoubleJump && !isGrounded && airJumpsRemaining > 0;

        if (canGroundJump || canAirJump)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpVelocity);
            jumpBufferTimer = 0f;

            if (canGroundJump)
            {
                coyoteTimer = 0f; // saftey to prevent double jump within coyote time
            }
            else
            {
                airJumpsRemaining--;
            }
        }
    }

    private void TryDropThrough()
    {
        if (isDropping) return;

        // Only drop if standing on a one-way platform
        Collider2D oneWay = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, oneWayMask);
        if (oneWay == null) return;

        StartCoroutine(DoDrop(oneWay));
    }

    private IEnumerator DoDrop(Collider2D platformCollider)
    {
        isDropping = true;

        Physics2D.IgnoreCollision(playerCol, platformCollider, true);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, -1f);

        yield return new WaitForSeconds(dropDuration);

        Physics2D.IgnoreCollision(playerCol, platformCollider, false);
        isDropping = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
    }
}
