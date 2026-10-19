using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float runSpeed = 8f;
    public float sneakSpeed = 2.2f;
    public bool crouchToggle = true;        // ticked = press Left Ctrl to toggle sneaking. Unticked = hold Left Ctrl
    public float runStaminaPerSecond = 15f; // stamina burned each second while running
    public float jumpHeight = 1.2f;
    public float gravity = -20f;

    [Header("Feel")]
    public float acceleration = 40f;
    public float deceleration = 30f;
    public float airControl = 0.3f;
    public float bobAmount = 0.04f;
    public float bobFrequency = 2f;
    public float sneakCameraDrop = 0.25f;   // camera lowers while sneaking
    public float flinchRecovery = 8f;       // how quickly the camera settles after being hit

    [Header("Stealth: how LOUD you are (enemy hearing)")]
    public float noiseStill = 0f;
    public float noiseSneak = 0.2f;
    public float noiseWalk = 1f;
    public float noiseRun = 1.6f;

    [Header("Stealth: how VISIBLE you are (enemy sight, 1 = normal)")]
    public float visibilitySneakMoving = 0.35f;
    public float visibilitySneakStill = 0.2f;
    public float visibilityStill = 0.7f;
    public float visibilityRunning = 1.4f;  // multiplies the normal value while running
    public float stealthModifier = 1f;      // lower = harder to spot. Sneak skill, gear and spells will change this later

    [Header("Testing")]
    public bool undetectableWhileSneaking = false;   // tick it: enemies can never notice you while you sneak

    [Header("Look")]
    public Transform cameraPivot;
    public float lookSensitivity = 0.1f;
    public float maxLookAngle = 85f;

    // Read by enemies and other scripts
    public float NoiseLevel { get; private set; }
    public bool IsSneaking { get; private set; }
    public bool IsRunning { get; private set; }

    // How easy you are to see right now. Enemies multiply their suspicion gain by this.
    public float Visibility
    {
        get
        {
            bool moving = horizontalVelocity.sqrMagnitude > 0.25f;

            float v;
            if (IsSneaking) v = moving ? visibilitySneakMoving : visibilitySneakStill;
            else v = moving ? 1f : visibilityStill;

            if (IsRunning) v *= visibilityRunning;

            return v * stealthModifier;
        }
    }

    CharacterController controller;
    PlayerStats stats;
    Vector3 horizontalVelocity;
    Vector3 basePivotPos;
    float verticalVelocity;
    float pitch;
    float flinchPitch;
    float bobTimer;
    bool sneakToggled;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        stats = GetComponent<PlayerStats>();
        basePivotPos = cameraPivot.localPosition;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (stats == null)
            Debug.LogWarning("PlayerController: there is no PlayerStats on this same GameObject, so running won't use stamina.", this);
    }

    void Update()
    {
        Look();
        Move();
        HeadBob();
    }

    // ---------- Called by other scripts ----------

    // Shoves the player away (used when an enemy lands a critical hit)
    public void Recoil(Vector3 direction, float speed)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;
        horizontalVelocity += direction.normalized * speed;
    }

    // Snaps the camera up or down for a moment. Negative = head snaps back (looks up).
    public void KickCamera(float degrees)
    {
        flinchPitch = degrees;
    }

    // ---------- Movement ----------

    void Look()
    {
        Vector2 delta = Mouse.current.delta.ReadValue() * lookSensitivity;
        transform.Rotate(0f, delta.x, 0f);
        pitch = Mathf.Clamp(pitch - delta.y, -maxLookAngle, maxLookAngle);

        // The flinch is added on top of where you're looking, then eases back to 0
        flinchPitch = Mathf.Lerp(flinchPitch, 0f, flinchRecovery * Time.deltaTime);
        cameraPivot.localRotation = Quaternion.Euler(pitch + flinchPitch, 0f, 0f);
    }

    void Move()
    {
        var kb = Keyboard.current;

        float x = (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0);
        float z = (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0);

        Vector3 move = transform.right * x + transform.forward * z;
        move = Vector3.ClampMagnitude(move, 1f);

        if (crouchToggle)
        {
            if (kb.leftCtrlKey.wasPressedThisFrame) sneakToggled = !sneakToggled;
            if (kb.leftShiftKey.wasPressedThisFrame) sneakToggled = false; // running stands you back up
            IsSneaking = sneakToggled;
        }
        else
        {
            sneakToggled = false;
            IsSneaking = kb.leftCtrlKey.isPressed;
        }

        bool hasInput = move.sqrMagnitude > 0.01f;
        bool wantsRun = !IsSneaking && kb.leftShiftKey.isPressed;
        bool running = wantsRun && (stats == null || stats.CanRun);
        IsRunning = running && hasInput;

        // Running burns stamina, but only while you are actually moving
        if (IsRunning && stats != null)
            stats.Drain(runStaminaPerSecond * Time.deltaTime);

        float speed = IsSneaking ? sneakSpeed : (running ? runSpeed : walkSpeed);
        Vector3 targetVelocity = move * speed;

        float rate = hasInput ? acceleration : deceleration;
        if (!controller.isGrounded) rate *= airControl;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, rate * Time.deltaTime);

        // Noise level for enemy hearing
        bool moving = horizontalVelocity.magnitude > 0.5f;
        if (!moving) NoiseLevel = noiseStill;
        else if (IsSneaking) NoiseLevel = noiseSneak;
        else if (running) NoiseLevel = noiseRun;
        else NoiseLevel = noiseWalk;

        // Gravity and jumping
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (controller.isGrounded && kb.spaceKey.wasPressedThisFrame)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    void HeadBob()
    {
        Vector3 target = basePivotPos;
        float hSpeed = horizontalVelocity.magnitude;

        if (IsSneaking) target.y -= sneakCameraDrop;

        if (controller.isGrounded && hSpeed > 0.1f)
        {
            bobTimer += hSpeed * bobFrequency * Time.deltaTime;
            target.y += Mathf.Sin(bobTimer) * bobAmount;
        }

        cameraPivot.localPosition = Vector3.Lerp(cameraPivot.localPosition, target, 15f * Time.deltaTime);
    }
}
