using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;

    [Header("References")]
    [Tooltip("Leave empty to use Camera.main")]
    [SerializeField] private Transform cameraTransform;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float acceleration = 40f;
    [SerializeField] private float turnSpeed = 12f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 6f;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundCheckExtra = 0.15f;

    [Header("Gravity")]
    [SerializeField] private float gravityMultiplier = 2f;   // overall gravity strength
    [SerializeField] private float fallMultiplier = 1.5f;    // extra pull only when falling

    [Header("Roll")]
    [SerializeField] private InputActionReference rollAction;
    [SerializeField] private Animator animator;                  // leave empty to auto-find on children
    [SerializeField] private string rollTriggerName = "Roll";
    [SerializeField] private float rollSpeed = 12f;
    [SerializeField] private float rollDuration = 0.6f;          // match your roll clip length
    [SerializeField] private float rollCooldown = 0.5f;
    [Tooltip("X = roll progress (0 to 1), Y = speed multiplier")]
    [SerializeField]
    private AnimationCurve rollSpeedCurve = new AnimationCurve(
        new Keyframe(0f, 1f), new Keyframe(1f, 0.3f));

    private int rollTriggerHash;
    private bool isRolling;
    private float rollTimer;
    private float nextRollTime;
    private Vector3 rollDirection;

    [Header("Roll Speed Boost")]
    [SerializeField] private float boostMultiplier = 1.6f;   // 1.6 = 60% faster than moveSpeed
    [SerializeField] private float boostDuration = 3f;       // how long the boost lasts after a roll
    [SerializeField] private float boostGainTime = 0.25f;    // time to ramp up to full boost
    [SerializeField] private float boostFadeTime = 1.2f;     // time to ease back to normal speed

    private float boostTimer;
    private float currentSpeedMultiplier = 1f;

    private Rigidbody rb;
    private CapsuleCollider col;

    private Vector2 moveInput;
    private bool jumpQueued;

    [Header("Speed Boost VFX")]
    [SerializeField] private ParticleSystem speedEffect;      // looping particle system (child object)
    [SerializeField] private float effectDisableDelay = 1.5f; // time after stopping before the object is disabled

    private bool effectPlaying;
    private float effectDisableTime;

    [SerializeField] private bool allowAirDash = true;
    private bool isAirDashing;

    [Header("Wall Run - Detection")]
    [SerializeField] private LayerMask wallMask;
    [SerializeField] private Transform wallCheckLeft;
    [SerializeField] private Transform wallCheckRight;
    [SerializeField] private float wallCheckRadius = 0.25f;
    [SerializeField] private float wallRayDistance = 1.2f;       // sideways ray used to get the wall normal

    [Header("Wall Run - Movement")]
    [SerializeField] private float wallRunSpeed = 9f;
    [SerializeField] private float wallRunAcceleration = 30f;
    [SerializeField] private float wallStickForce = 8f;          // pushes you into the wall so you stay attached
    [SerializeField] private float wallRunGravity = 0f;          // gravity scale during the stable part (0 = no sinking)

    [Header("Wall Run - Duration & Fall Off")]
    [SerializeField] private float wallRunMaxTime = 2f;          // total time on the wall
    [SerializeField] private float wallFallOffTime = 0.8f;       // last part of the run where you slide off
    [Tooltip("X = fall-off progress (0 to 1), Y = how much gravity comes back (0 to 1)")]
    [SerializeField] private AnimationCurve wallFallCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Wall Jump")]
    [SerializeField] private float wallJumpUpForce = 8f;
    [SerializeField] private float wallJumpSideForce = 9f;       // pushes away from the wall
    [SerializeField] private float wallJumpForwardForce = 5f;    // pushes where you're looking
    [SerializeField] private float wallJumpCooldown = 0.25f;     // can't re-attach right after jumping
    [SerializeField] private float wallJumpControlLock = 0.25f;  // reduced air control so the push isn't cancelled
    [Range(0f, 1f)]
    [SerializeField] private float wallJumpControlMultiplier = 0.1f;

    private bool isWallRunning;
    private bool wallRunExhausted;   // used the full time, needs landing before the next wall run
    private int wallSide;            // -1 left wall, +1 right wall
    private Vector3 wallNormal;
    private float wallRunTimer;
    private float wallCooldownTimer;
    private float controlLockTimer;

    public int WallRunSide => isWallRunning ? wallSide : 0;   // read by the camera

    [Header("Wall Cling (front wall)")]
    [SerializeField] private Transform wallCheckFront;
    [SerializeField] private float frontCheckRadius = 0.3f;
    [SerializeField] private float frontRayDistance = 1f;
    [SerializeField] private float wallClingMaxTime = 1f;          // total time on the wall
    [SerializeField] private float wallClingFallOffTime = 0.5f;    // last part where you slide down
    [SerializeField] private float wallClingGravity = 0f;          // gravity scale while stuck
    [SerializeField] private float wallClingStickForce = 6f;
    [SerializeField] private AnimationCurve wallClingFallCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float wallClingJumpUpForce = 9f;
    [SerializeField] private float wallClingJumpAwayForce = 8f;    // push away from the wall

    private bool isWallClinging;
    private bool clingExhausted;
    private float clingTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();

        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (animator == null) animator = GetComponentInChildren<Animator>();
        rollTriggerHash = Animator.StringToHash(rollTriggerName);
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
        jumpAction.action.Enable();
        rollAction.action.Enable();
    }

    private void OnDisable()
    {
        moveAction.action.Disable();
        jumpAction.action.Disable();
        rollAction.action.Disable();
        if (rb != null) rb.useGravity = true;
    }

    private void Start()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        moveInput = moveAction.action.ReadValue<Vector2>();

        if (jumpAction.action.WasPressedThisFrame())
            jumpQueued = true;

        

        UpdateBoostEffect();

        if (rollAction.action.WasPressedThisFrame() && !isRolling && !isWallRunning && !isWallClinging && Time.time >= nextRollTime)
        {
            bool grounded = IsGrounded();

            if (grounded)
                StartRoll(false);
            else if (allowAirDash)
                StartRoll(true);
        }
    }

    private void FixedUpdate()
    {
        if (isRolling)
        {
            HandleRoll();
        }
        else
        {
            CheckWallRun();

            if (isWallRunning)
            {
                HandleWallRun();
            }
            else if (isWallClinging)
                HandleWallCling();
            else
            {
                HandleMovement();
                HandleJump();
            }
        }

        ApplyExtraGravity();
    }
    private void HandleMovement()
    {
        UpdateBoost();

        // Movement relative to where the camera is looking (yaw only)
        float yaw = cameraTransform != null ? cameraTransform.eulerAngles.y : transform.eulerAngles.y;
        Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);

        Vector3 forward = yawRot * Vector3.forward;
        Vector3 right = yawRot * Vector3.right;

        if (cameraTransform != null)
        {
            forward = cameraTransform.forward;
            right = cameraTransform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
        }

        Vector3 moveDir = forward * moveInput.y + right * moveInput.x;
        moveDir = Vector3.ClampMagnitude(moveDir, 1f);

        Vector3 targetVelocity = moveDir * moveSpeed * currentSpeedMultiplier;

        Vector3 vel = rb.linearVelocity;
        Vector3 horizontal = new Vector3(vel.x, 0f, vel.z);
        float accel = controlLockTimer > 0f ? acceleration * wallJumpControlMultiplier : acceleration;
        horizontal = Vector3.MoveTowards(horizontal, targetVelocity, accel * Time.fixedDeltaTime);

        // no input and almost stopped -> stop completely
        if (moveInput.sqrMagnitude < 0.01f && horizontal.sqrMagnitude < 0.25f)
            horizontal = Vector3.zero;

        rb.linearVelocity = new Vector3(horizontal.x, vel.y, horizontal.z);

        // Body always faces where the camera looks
        rb.MoveRotation(Quaternion.Euler(0f, yaw, 0f));
    }

    private void HandleJump()
    {
        if (jumpQueued && IsGrounded())
        {
            Vector3 vel = rb.linearVelocity;
            vel.y = 0f;
            rb.linearVelocity = vel;
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
        jumpQueued = false;
    }
    private void StartRoll(bool inAir)
    {
        isRolling = true;
        isAirDashing = inAir;
        rollTimer = 0f;
        jumpQueued = false;

        rollDirection = transform.forward;
        rollDirection.y = 0f;
        rollDirection.Normalize();

        if (inAir)
        {
            // no animation, no gravity during the dash
            rb.useGravity = false;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }
        else if (animator != null)
        {
            animator.SetTrigger(rollTriggerHash);
        }
    }

    private void HandleRoll()
    {
        rollTimer += Time.fixedDeltaTime;
        float t = Mathf.Clamp01(rollTimer / rollDuration);
        float speed = rollSpeed * rollSpeedCurve.Evaluate(t);

        Vector3 vel = rb.linearVelocity;
        float yVel = isAirDashing ? 0f : vel.y;   // <-- changed
        rb.linearVelocity = new Vector3(rollDirection.x * speed, yVel, rollDirection.z * speed);

        if (rollTimer >= rollDuration)
        {
            isRolling = false;
            nextRollTime = Time.time + rollCooldown;
            boostTimer = boostDuration;

            if (isAirDashing)                     // <-- add
            {
                isAirDashing = false;
                rb.useGravity = true;             // gravity back on
            }
            rb.angularVelocity = Vector3.zero;
        }
    }
    private void UpdateBoost()
    {
        bool hasInput = moveInput.sqrMagnitude > 0.01f;

        if (boostTimer > 0f)
        {
            // timer only runs while moving, and stopping cancels the boost
            boostTimer = hasInput ? boostTimer - Time.fixedDeltaTime : 0f;
        }

        float target = boostTimer > 0f ? boostMultiplier : 1f;

        // different ramp speeds for gaining and losing the boost
        float range = Mathf.Max(0.01f, boostMultiplier - 1f);
        float rate = target > currentSpeedMultiplier
            ? range / Mathf.Max(0.01f, boostGainTime)
            : range / Mathf.Max(0.01f, boostFadeTime);

        currentSpeedMultiplier = Mathf.MoveTowards(currentSpeedMultiplier, target, rate * Time.fixedDeltaTime);
    }
    private void UpdateBoostEffect()
    {
        if (speedEffect == null) return;

        bool boostActive = boostTimer > 0f || isRolling;

        if (boostActive && !effectPlaying)
        {
            // boost started: enable the object and emit
            effectPlaying = true;
            speedEffect.gameObject.SetActive(true);
            speedEffect.Play();
        }
        else if (!boostActive && effectPlaying)
        {
            // boost lost: stop emitting, let the existing particles fade out
            effectPlaying = false;
            speedEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            effectDisableTime = Time.time + effectDisableDelay;
        }

        // after the delay, disable the object completely
        if (!effectPlaying && speedEffect.gameObject.activeSelf && Time.time >= effectDisableTime)
            speedEffect.gameObject.SetActive(false);
    }
    private void ApplyExtraGravity()
    {
        if (isAirDashing || isWallRunning || isWallClinging) return;
        // no extra gravity while standing on the ground
        if (IsGrounded() && rb.linearVelocity.y <= 0.01f)
            return;

        float multiplier = gravityMultiplier;

        if (rb.linearVelocity.y < 0f)
            multiplier *= fallMultiplier;

        rb.AddForce(Physics.gravity * (multiplier - 1f), ForceMode.Acceleration);
    }
    private void GetGroundCheck(out Vector3 origin, out float radius, out float distance)
    {
        if (col == null) col = GetComponent<CapsuleCollider>();   // so it works in edit mode too

        Bounds b = col.bounds;
        radius = col.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z) * 0.9f;
        origin = b.center;
        distance = b.extents.y - radius + groundCheckExtra;
    }

    private bool IsGrounded()
    {
        GetGroundCheck(out Vector3 origin, out float radius, out float distance);
        return Physics.SphereCast(origin, radius, Vector3.down, out _, distance,
            groundMask, QueryTriggerInteraction.Ignore);
    }

    private bool DetectWall(Transform point, int side, out Vector3 normal)
    {
        normal = Vector3.zero;
        if (point == null) return false;

        // 1) radius check at the side point
        if (!Physics.CheckSphere(point.position, wallCheckRadius, wallMask, QueryTriggerInteraction.Ignore))
            return false;

        // 2) sideways ray to get the real wall normal
        Vector3 dir = transform.right * side;
        if (Physics.Raycast(col.bounds.center, dir, out RaycastHit hit, wallRayDistance, wallMask,
                QueryTriggerInteraction.Ignore))
        {
            if (Mathf.Abs(hit.normal.y) > 0.3f) return false;   // floors/slopes aren't walls
            normal = hit.normal;
        }
        else
        {
            normal = -dir;
        }

        normal.y = 0f;   // horizontal only
        normal.Normalize();
        return true;
    }

    private void CheckWallRun()
    {
        if (wallCooldownTimer > 0f) wallCooldownTimer -= Time.fixedDeltaTime;
        if (controlLockTimer > 0f) controlLockTimer -= Time.fixedDeltaTime;

        bool grounded = IsGrounded();
        if (grounded)
        {
            wallRunExhausted = false;
            clingExhausted = false;
        }

        bool canAttach = !grounded && moveInput.y > 0.1f && wallCooldownTimer <= 0f;

        // 1) side walls (wall run)
        int side = 0;
        Vector3 normal = Vector3.zero;

        if (canAttach && !wallRunExhausted)
        {
            if (DetectWall(wallCheckRight, 1, out normal)) side = 1;
            else if (DetectWall(wallCheckLeft, -1, out normal)) side = -1;
        }

        if (side != 0)
        {
            if (isWallClinging) StopWallCling();
            wallNormal = normal;
            wallSide = side;
            if (!isWallRunning) StartWallRun();
            return;
        }

        if (isWallRunning) StopWallRun();

        // 2) front wall (cling)
        if (canAttach && !clingExhausted && DetectFrontWall(out Vector3 frontNormal))
        {
            wallNormal = frontNormal;
            if (!isWallClinging) StartWallCling();
        }
        else if (isWallClinging)
        {
            StopWallCling();
        }
    }

    private void StartWallRun()
    {
        isWallRunning = true;
        wallRunTimer = 0f;
        rb.useGravity = false;

        // flatten vertical speed so you can't climb or keep rising
        Vector3 vel = rb.linearVelocity;
        rb.linearVelocity = new Vector3(vel.x, 0f, vel.z);
    }

    private void StopWallRun()
    {
        isWallRunning = false;
        rb.useGravity = true;
    }

    private Vector3 GetWallForward()
    {
        Vector3 f = Vector3.Cross(Vector3.up, wallNormal);
        if (Vector3.Dot(f, transform.forward) < 0f) f = -f;   // run the way you're facing
        return f.normalized;
    }

    private void HandleWallRun()
    {
        UpdateBoost();

        wallRunTimer += Time.fixedDeltaTime;

        // wall jump
        if (jumpQueued)
        {
            jumpQueued = false;
            WallJump();
            return;
        }

        // time's up: detach, and need to land before wall running again
        if (wallRunTimer >= wallRunMaxTime)
        {
            wallRunExhausted = true;
            StopWallRun();
            return;
        }

        // fall-off progress: 0 during the stable part, 0 to 1 during the last wallFallOffTime seconds
        float fallStart = Mathf.Max(0f, wallRunMaxTime - wallFallOffTime);
        float fall01 = Mathf.Clamp01((wallRunTimer - fallStart) / Mathf.Max(0.01f, wallFallOffTime));
        float fallAmount = wallFallCurve.Evaluate(fall01);

        // horizontal movement along the wall only
        Vector3 vel = rb.linearVelocity;
        Vector3 horizontal = new Vector3(vel.x, 0f, vel.z);
        Vector3 target = GetWallForward() * wallRunSpeed * currentSpeedMultiplier;
        horizontal = Vector3.MoveTowards(horizontal, target, wallRunAcceleration * Time.fixedDeltaTime);

        // gravity fades in smoothly during the fall-off
        float gravityScale = Mathf.Lerp(wallRunGravity, 1f, fallAmount);
        float yVel = vel.y + Physics.gravity.y * gravityScale * Time.fixedDeltaTime;
        rb.linearVelocity = new Vector3(horizontal.x, yVel, horizontal.z);

        // stick to the wall, weaker as you fall off
        rb.AddForce(-wallNormal * wallStickForce * (1f - fallAmount), ForceMode.Acceleration);

        // keep facing where the camera looks
        if (cameraTransform != null)
            rb.MoveRotation(Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f));
    }
    private bool DetectFrontWall(out Vector3 normal)
    {
        normal = Vector3.zero;
        if (wallCheckFront == null) return false;

        if (!Physics.CheckSphere(wallCheckFront.position, frontCheckRadius, wallMask, QueryTriggerInteraction.Ignore))
            return false;

        if (Physics.Raycast(col.bounds.center, transform.forward, out RaycastHit hit, frontRayDistance,
                wallMask, QueryTriggerInteraction.Ignore))
        {
            if (Mathf.Abs(hit.normal.y) > 0.3f) return false;   // floors/ceilings aren't walls
            normal = hit.normal;
            normal.y = 0f;
            normal.Normalize();
            return true;
        }
        return false;
    }

    private void StartWallCling()
    {
        isWallClinging = true;
        clingTimer = 0f;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;   // stop dead against the wall, no climbing
    }

    private void StopWallCling()
    {
        isWallClinging = false;
        rb.useGravity = true;
    }

    private void HandleWallCling()
    {
        clingTimer += Time.fixedDeltaTime;

        if (jumpQueued)
        {
            jumpQueued = false;
            WallJump();
            return;
        }

        if (clingTimer >= wallClingMaxTime)
        {
            clingExhausted = true;   // need to land (or wall jump) before clinging again
            StopWallCling();
            return;
        }

        float fallStart = Mathf.Max(0f, wallClingMaxTime - wallClingFallOffTime);
        float fall01 = Mathf.Clamp01((clingTimer - fallStart) / Mathf.Max(0.01f, wallClingFallOffTime));
        float fallAmount = wallClingFallCurve.Evaluate(fall01);

        // gravity fades in smoothly, never goes upward
        float gravityScale = Mathf.Lerp(wallClingGravity, 1f, fallAmount);
        float yVel = Mathf.Min(0f, rb.linearVelocity.y + Physics.gravity.y * gravityScale * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(0f, yVel, 0f);

        rb.AddForce(-wallNormal * wallClingStickForce * (1f - fallAmount), ForceMode.Acceleration);

        if (cameraTransform != null)
            rb.MoveRotation(Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f));
    }
    private void WallJump()
    {
        Vector3 horizontal;
        float up;

        if (isWallClinging)
        {
            horizontal = wallNormal * wallClingJumpAwayForce;   // opposite of the wall you're facing
            up = wallClingJumpUpForce;
            StopWallCling();
        }
        else
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();

            horizontal = wallNormal * wallJumpSideForce + forward * wallJumpForwardForce;
            up = wallJumpUpForce;
            StopWallRun();
        }

        wallRunExhausted = false;   // allow chaining to another wall
        clingExhausted = false;
        wallCooldownTimer = wallJumpCooldown;
        controlLockTimer = wallJumpControlLock;

        rb.linearVelocity = new Vector3(horizontal.x, up, horizontal.z);
    }
    private void OnDrawGizmosSelected()
    {
        GetGroundCheck(out Vector3 origin, out float radius, out float distance);

        Vector3 end = origin + Vector3.down * distance;

        // green when grounded (only meaningful in play mode), red otherwise
        Gizmos.color = Application.isPlaying && IsGrounded() ? Color.green : Color.red;

        Gizmos.DrawWireSphere(origin, radius);   // start of the cast
        Gizmos.DrawWireSphere(end, radius);      // where the sphere ends up
        Gizmos.DrawLine(origin, end);

        Gizmos.color = Color.cyan;
        if (wallCheckLeft != null) Gizmos.DrawWireSphere(wallCheckLeft.position, wallCheckRadius);
        if (wallCheckRight != null) Gizmos.DrawWireSphere(wallCheckRight.position, wallCheckRadius);

        Gizmos.color = Color.yellow;
        if (wallCheckFront != null) Gizmos.DrawWireSphere(wallCheckFront.position, frontCheckRadius);
    }
}
