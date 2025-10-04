using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 10f;
    [SerializeField] private float rotationLerp = 10f;

    [Header("Jumping & Gravity")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -9.8f;
    [SerializeField] private float groundedStick = -2f;

    [Header("Animation Smoothing")]
    [SerializeField] private float accelDampTime = 0.10f;
    [SerializeField] private float decelDampTime = 0.04f;

    [Header("Acceleration")]
    [SerializeField] private float accelRate = 12f;
    [SerializeField] private float decelRate = 18f;

    [Header("Air Control")]
    [SerializeField] private float airAccelRate = 3f;
    [SerializeField] private float airDrag = 0.0f;
    [SerializeField] private float maxAirSpeed = 12f;

    private CharacterController controller;
    private Animator anim;

    private Vector2 moveInput;
    private Vector3 velocity;
    private Vector3 currentHorizVel;

    private bool sprintHeld;
    private bool isAttacking;
    private float lockedAttackYaw;
    private float lastGroundedTime;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        anim = GetComponent<Animator>();
        
        if (controller == null)
            Debug.LogError("CharacterController component is missing!");
        if (anim == null)
            Debug.LogError("Animator component is missing!");
    }

    // Input Callbacks
    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        sprintHeld = context.performed;
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        if (controller.isGrounded && !isAttacking)
        {
            PerformJump();
        }
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (context.performed && !isAttacking)
        {
            StartAttack();
        }
    }

    // Animation Events
    public void AttackEnd()
    {
        isAttacking = false;
    }

    private void PerformJump()
    {
        anim.SetTrigger("Jump");

        Vector3 ccVel = controller.velocity;
        currentHorizVel = new Vector3(ccVel.x, 0f, ccVel.z);

        velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        lastGroundedTime = -1f;
    }

    private void StartAttack()
    {
        anim.SetTrigger("Attack");
        anim.SetInteger("AttackCount", 0);
        isAttacking = true;

        Vector3 camFwd = Camera.main.transform.forward;
        camFwd.y = 0f; // 수평 방향만
        if (camFwd.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(camFwd);
        }
        
        lockedAttackYaw = transform.eulerAngles.y;
    }

    private Vector3 GetCameraRelativeMovement()
    {
        if (cameraTransform == null)
        {
            Debug.LogWarning("Camera transform not assigned!");
            return Vector3.zero;
        }

        Vector3 camFwd = cameraTransform.forward;
        camFwd.y = 0f;
        camFwd.Normalize();

        Vector3 camRight = cameraTransform.right;
        camRight.y = 0f;
        camRight.Normalize();

        Vector3 moveDirection = camFwd * moveInput.y + camRight * moveInput.x;
        
        if (moveDirection.sqrMagnitude > 1f)
            moveDirection.Normalize();

        return moveDirection;
    }

    private void HandleRotation(Vector3 moveDirection)
    {
        if (isAttacking)
        {
            transform.rotation = Quaternion.Euler(0f, lockedAttackYaw, 0f);
            return;
        }

        if (moveDirection.sqrMagnitude > 0.0001f)
        {
            float targetYaw = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
            Quaternion targetRot = Quaternion.Euler(0f, targetYaw, 0f);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, 
                targetRot, 
                rotationLerp * Time.deltaTime
            );
        }
        else
        {
            Vector3 euler = transform.eulerAngles;
            transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
        }
    }

    private void ApplyGravity()
    {
        bool isGrounded = controller.isGrounded;

        if (isGrounded)
        {
            lastGroundedTime = Time.time;
            
            if (velocity.y <= 0f)
                velocity.y = groundedStick;
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }
    }

    private void HandleMovement(Vector3 moveDirection)
    {
        bool hasInput = moveInput.sqrMagnitude > 0.01f;
        float targetSpeed = hasInput ? (sprintHeld ? runSpeed : walkSpeed) : 0f;
        Vector3 desiredHorizVel = hasInput ? moveDirection * targetSpeed : Vector3.zero;

        if (controller.isGrounded)
        {
            HandleGroundMovement(desiredHorizVel);
        }
        else
        {
            HandleAirMovement(desiredHorizVel, hasInput);
        }
    }

    private void HandleGroundMovement(Vector3 desiredHorizVel)
    {
        if (isAttacking)
        {
            currentHorizVel = Vector3.MoveTowards(
                currentHorizVel, 
                Vector3.zero, 
                decelRate * Time.deltaTime
            );
        }
        else
        {
            float currentMag = currentHorizVel.magnitude;
            float desiredMag = desiredHorizVel.magnitude;
            float rate = desiredMag > currentMag ? accelRate : decelRate;
            
            currentHorizVel = Vector3.MoveTowards(
                currentHorizVel, 
                desiredHorizVel, 
                rate * Time.deltaTime
            );
        }
    }

    private void HandleAirMovement(Vector3 desiredHorizVel, bool hasInput)
    {
        if (isAttacking)
        {
            if (airDrag > 0f)
            {
                currentHorizVel = Vector3.MoveTowards(
                    currentHorizVel, 
                    Vector3.zero, 
                    airDrag * Time.deltaTime
                );
            }
        }
        else
        {
            currentHorizVel = Vector3.MoveTowards(
                currentHorizVel, 
                desiredHorizVel, 
                airAccelRate * Time.deltaTime
            );

            if (!hasInput && airDrag > 0f)
            {
                currentHorizVel = Vector3.MoveTowards(
                    currentHorizVel, 
                    Vector3.zero, 
                    airDrag * Time.deltaTime
                );
            }

            if (currentHorizVel.sqrMagnitude > maxAirSpeed * maxAirSpeed)
            {
                currentHorizVel = currentHorizVel.normalized * maxAirSpeed;
            }
        }
    }

    private void MoveCharacter()
    {
        Vector3 movement = currentHorizVel + new Vector3(0f, velocity.y, 0f);
        controller.Move(movement * Time.deltaTime);

        if (controller.isGrounded && velocity.y < 0f)
        {
            velocity.y = groundedStick;
        }
    }

    private void UpdateAnimator()
    {
        Vector3 ccVel = controller.velocity;
        float actualHorizSpeed = new Vector2(ccVel.x, ccVel.z).magnitude;

        // 아주 작은 값은 0으로 죽여서 '가만히 서기' 상태로
        if (actualHorizSpeed < 0.05f) actualHorizSpeed = 0f;


        float prevSpeed = anim.GetFloat("Speed");
        float dampTime = actualHorizSpeed > prevSpeed ? accelDampTime : decelDampTime;

        anim.SetFloat("Speed", actualHorizSpeed, dampTime, Time.deltaTime);
        anim.SetBool("IsGrounded", controller.isGrounded);
        anim.SetFloat("VelocityY", velocity.y);
    }

    private void Update()
    {
        Vector3 moveDirection = GetCameraRelativeMovement();
        
        HandleRotation(moveDirection);
        ApplyGravity();
        HandleMovement(moveDirection);
        MoveCharacter();
        UpdateAnimator();
    }
}