using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.InputAction;

public class PlayerController : MonoBehaviour
{
    #region Singleton
    private static PlayerController instance;
    public static PlayerController Instance { get { return instance; } }

    private void Initialize()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
    }

    private void Terminate()
    {
        if (this == Instance)
        {
            instance = null;
        }
    }
    #endregion

    [Header("Movement")]
    [SerializeField] private float acceleration = 70f; // The movement speed acceleration of the player
    [SerializeField] private float maxSpeed = 12f; // The maximum speed of the player
    [SerializeField] private float groundLinDrag = 20f; // The friction applied when not moving <= decceleration

    [Header("Buffer & Timer")]
    [SerializeField] private float jumpBufferTime = .1f; // The time window that allows the player to perform an action before it is allowed
    private float jumpBufferTimer = 1000f;
    [SerializeField] private float coyoteTimeTime = .1f; // The time window in which the player can jump after walking over an edge
    private float coyoteTimeTimer = 1000f;
    [SerializeField] private float dashBufferTime = .1f;
    private float dashBufferTimer = 1000f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight; // The jump height of the object in units(metres)
    [SerializeField] private float airLinDrag = 2.5f; // The air resistance while jumping
    [SerializeField] private float fullJumpFallMultiplier = 8f; // Gravity applied when doing a full jump
    [SerializeField] private float halfJumpFallMultiplier = 5f; // Gravity applied when doing half jump
    [SerializeField] private int amountOfJumps = 1; // The amount of additional jumps the player can make
    private int jumpsCounted;
    private Vector2 lastJumpPos;

    [Header("Dash")]
    [SerializeField] private float dashForce = 15f;
    private bool isDashing;

    [Header("References")]
    public Rigidbody2D RigidBody;
    [SerializeField] private SpriteRenderer m_SpriteRenderer;
    [SerializeField] private CollisionCheck cc;

    private bool jumpHeld;
    private Vector2 moveVal;
    private Vector3 mousePos;

    [SerializeField] private Health m_Health;

    private Camera mainCam;
    private Mouse mouse;

    private float horizontalDir => moveVal.x;
    private bool changingDir => (RigidBody.velocity.x > 0f && horizontalDir < 0f)
                                 || (RigidBody.velocity.x < 0f && horizontalDir > 0f);
    private bool canMove => horizontalDir != 0f;
    private bool canJump
    {
        get
        {
            if (amountOfJumps > 1)
            {
                return jumpBufferTimer < jumpBufferTime
                    && (coyoteTimeTimer < coyoteTimeTime || jumpsCounted < amountOfJumps);
            }
            else
            {
                return jumpBufferTimer < jumpBufferTime
                    && coyoteTimeTimer < coyoteTimeTime;
            }
        }
    }
    private bool canDash => dashBufferTimer < dashBufferTime && !isDashing;
    private bool facingRight;

    private void Awake()
    {
        Initialize();
    }

    private void Start()
    {
        mouse = Mouse.current;
        mainCam = Camera.main;

        #region Add Death event
        if (m_Health == null)
            m_Health = GetComponent<Health>();

        m_Health.E_TriggerDeath += PlayerDied;
        #endregion
    }
    private void Update()
    {
        #region Player Look at Mouse Position
        mousePos = mainCam.ScreenToWorldPoint(mouse.position.ReadValue());

        if (mousePos.x < transform.position.x)
        {
            facingRight = false;
            m_SpriteRenderer.flipX = true;
        }
        else if (mousePos.x > transform.position.x)
        {
            facingRight = true;
            m_SpriteRenderer.flipX = false;
        }
        #endregion


        #region Apply Drag & Gravity
        if (cc.m_IsGrounded)
        {
            ApplyGroundLinearDrag();
            jumpsCounted = 0; //reset jumps counter
            coyoteTimeTimer = 0; //reset coyote time counter
            isDashing = false;
        }
        else
        {
            ApplyAirLinearDrag();
            ApplyFallGravity();
        }
        #endregion

        coyoteTimeTimer += Time.deltaTime;
        jumpBufferTimer += Time.deltaTime;
        dashBufferTimer += Time.deltaTime;
    }

    private void FixedUpdate()
    {
        if (canDash)
            Dash(mousePos.x, mousePos.y);

        if (!isDashing)
        {
            if (canMove)
                Move();
            if (canJump)
                Jump(jumpHeight, Vector2.up);
        }
    }

    private void PlayerDied()
    {
        Debug.LogWarning("Player Dies");

        m_SpriteRenderer.color = Color.red;
    }

    #region Input

    public void OnMove(CallbackContext ctx)
    {
        moveVal = ctx.ReadValue<Vector2>();
    }
    public void OnJump(CallbackContext ctx)
    {
        if (ctx.performed)
        {
            jumpBufferTimer = 0; //reset the jump buffer
            jumpHeld = true;
        }
        if (ctx.canceled)
            jumpHeld = false;
    }
    public void OnDash(CallbackContext ctx)
    {
        dashBufferTimer = 0;
    }

    #endregion

    private void Move()
    {
        RigidBody.AddForce(new Vector2(horizontalDir, 0f) * acceleration);
        if (Mathf.Abs(RigidBody.velocity.x) > maxSpeed)
            RigidBody.velocity = new Vector2(Mathf.Sign(RigidBody.velocity.x) * maxSpeed, RigidBody.velocity.y); //Clamp velocity when max speed is reached!
    }

    /// <summary>
    /// Makes the player jump with a specific force to reach an exact amount of units in vertical space
    /// </summary>
    public void Jump(float _jumpHeight, Vector2 _dir)
    {
        if (coyoteTimeTimer > coyoteTimeTime && jumpsCounted < 1)
        {
            jumpsCounted = amountOfJumps;
        }

        lastJumpPos = transform.position;
        coyoteTimeTimer = coyoteTimeTime;
        jumpBufferTimer = jumpBufferTime;
        jumpsCounted++;

        ApplyAirLinearDrag();

        RigidBody.gravityScale = fullJumpFallMultiplier;

        RigidBody.velocity = new Vector2(RigidBody.velocity.x, 0f); //set y velocity to 0
        float jumpForce;

        cc.StartCoroutine(cc.DisableWallRay());

        jumpForce = Mathf.Sqrt(_jumpHeight * -2f * (Physics.gravity.y * RigidBody.gravityScale));
        RigidBody.AddForce(_dir * jumpForce, ForceMode2D.Impulse);
    }

    private void Dash(float _x, float _y, bool directionBased = false)
    {
        Debug.Log("Start Dash");

        isDashing = true;

        RigidBody.velocity = Vector2.zero;
        RigidBody.gravityScale = 0f;
        RigidBody.drag = 0f;

        Vector2 dir = Vector2.zero;

        if (directionBased)
        {
            // Based on the moving direction of the player
            if (_x != 0f || _y != 0f)
                dir = new Vector2(_x, _y);
            else
            {
                // Based on the direction the player faces
                if (facingRight)
                    dir = new Vector2(1, 0);
                else
                    dir = new Vector2(-1, 0);
            }
        }
        else
        {
            // Based on position of x&y relative to the player
            dir = new Vector2(_x - transform.position.x, _y - transform.position.y);
        }

        dir.Normalize();

        RigidBody.AddForce(dir * dashForce, ForceMode2D.Impulse);
    }

    #region Drag&Gravity
    /// <summary>
    /// Applies the ground friction based on wether the player is moving or giving no horizontal inputs
    /// </summary>
    private void ApplyGroundLinearDrag()
    {
        if (Mathf.Abs(horizontalDir) < .4f || changingDir)
        {
            RigidBody.drag = groundLinDrag;
        }
        else
        {
            RigidBody.drag = 0f;
        }
    }
    /// <summary>
    /// Applies the air resistance when the player is jumping
    /// </summary>
    private void ApplyAirLinearDrag()
    {
        RigidBody.drag = airLinDrag;
    }
    /// <summary>
    /// Applies the fall gravity based on the players jump height and input
    /// </summary>
    private void ApplyFallGravity()
    {
        if (RigidBody.velocity.y < 0f || transform.position.y - lastJumpPos.y > jumpHeight)
        {
            RigidBody.gravityScale = fullJumpFallMultiplier;
        }
        else if (RigidBody.velocity.y > 0f && !jumpHeld)
        {
            RigidBody.gravityScale = halfJumpFallMultiplier;
        }
        else
        {
            RigidBody.gravityScale = 1f;
        }
    }
    #endregion

    private void OnDestroy()
    {
        Terminate();
    }
}