using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class HelicopterController : MonoBehaviour
{
    [Header("General")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField, Tooltip("Child holding the meshes. Only this one tilts, never the Rigidbody.")]
    private Transform visual;

    [Header("Horizontal movement")]
    [SerializeField] private float maxHorizontalSpeed = 15f;
    [SerializeField, Tooltip("Low value = slow, heavy start.")]
    private float horizontalAcceleration = 8f;
    [SerializeField, Tooltip("Low value = the helicopter keeps drifting when keys are released.")]
    private float horizontalDeceleration = 4f;

    [Header("Vertical movement")]
    [SerializeField] private float climbSpeed = 6f;
    [SerializeField] private float verticalAcceleration = 6f;
    [SerializeField, Tooltip("World height the helicopter cannot go above.")]
    private float maxAltitude = 80f;

    [Header("Yaw")]
    [SerializeField] private float yawSpeed = 80f;
    [SerializeField, Tooltip("How fast the yaw input builds up and fades out.")]
    private float yawResponse = 4f;

    [Header("Tilt")]
    [SerializeField] private float maxTiltAngle = 20f;
    [SerializeField] private float tiltSmoothing = 4f;
    [SerializeField, Range(0f, 1f), Tooltip("0 = tilt follows the keys only, 1 = tilt follows the real velocity only.")]
    private float velocityTiltBlend = 0.5f;

    [Header("Rotors")]
    [SerializeField] private Transform rotor;
    [SerializeField] private Vector3 rotorAxis = Vector3.up;
    [SerializeField] private float rotorSpinSpeed = 1000f;
    [SerializeField] private Transform tailRotor;
    [SerializeField] private Vector3 tailRotorAxis = Vector3.right;
    [SerializeField] private float tailRotorSpinSpeed = 1500f;

    private Rigidbody _rb;
    private InputActionMap _actionMap;
    private InputAction _moveAction;
    private InputAction _elevationAction;
    private InputAction _yawAction;

    private Vector2 _move;
    private float _elevation;
    private float _yaw;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;

        if (inputActions == null)
        {
            Debug.LogError("HelicopterController: Input Actions asset is not assigned.", this);
            enabled = false;
            return;
        }

        _actionMap = inputActions.FindActionMap("Helicopter");
        if (_actionMap == null)
        {
            Debug.LogError("HelicopterController: action map 'Helicopter' not found in the Input Actions asset.", this);
            enabled = false;
            return;
        }

        _moveAction = _actionMap.FindAction("Move");
        _elevationAction = _actionMap.FindAction("Elevation");
        _yawAction = _actionMap.FindAction("Yaw");

        if (_moveAction == null || _elevationAction == null || _yawAction == null)
        {
            Debug.LogError("HelicopterController: actions 'Move', 'Elevation' or 'Yaw' not found in the 'Helicopter' map.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (_actionMap == null) return;

        _actionMap.Enable();
        _rb.isKinematic = false;
    }

    private void OnDisable()
    {
        if (_actionMap == null) return;

        _actionMap.Disable();

        _move = Vector2.zero;
        _elevation = 0f;
        _yaw = 0f;

        if (_rb != null)
        {
            // Gravity is off, so the whole velocity must be cleared, then the body is parked
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
        }

        if (visual != null)
            visual.localRotation = Quaternion.identity;
    }

    private void Update()
    {
        _move = _moveAction.ReadValue<Vector2>();
        _elevation = _elevationAction.ReadValue<float>();

        // Yaw input builds up and fades out progressively
        float yawInput = _yawAction.ReadValue<float>();
        _yaw = Mathf.MoveTowards(_yaw, yawInput, yawResponse * Time.deltaTime);

        UpdateTilt();
        SpinRotors();
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // Yaw : the helicopter turns on itself
        Quaternion newRotation = _rb.rotation * Quaternion.Euler(0f, _yaw * yawSpeed * dt, 0f);
        _rb.MoveRotation(newRotation);

        Vector3 vel = _rb.linearVelocity;

        // Horizontal : the keys give a target velocity, the real velocity moves toward it
        Vector3 localInput = new Vector3(_move.x, 0f, _move.y);
        Vector3 horizontalTarget = newRotation * Vector3.ClampMagnitude(localInput, 1f) * maxHorizontalSpeed;

        bool hasInput = _move.sqrMagnitude > 0.0001f;
        float horizontalRate = hasInput ? horizontalAcceleration : horizontalDeceleration;

        Vector3 horizontalVel = Vector3.MoveTowards(new Vector3(vel.x, 0f, vel.z), horizontalTarget, horizontalRate * dt);

        // Vertical : no input = hover
        float verticalTarget = _elevation * climbSpeed;
        if (_rb.position.y >= maxAltitude)
            verticalTarget = Mathf.Min(verticalTarget, 0f);

        float verticalVel = Mathf.MoveTowards(vel.y, verticalTarget, verticalAcceleration * dt);

        _rb.linearVelocity = new Vector3(horizontalVel.x, verticalVel, horizontalVel.z);
    }

    private void UpdateTilt()
    {
        if (visual == null) return;

        // Tilt mixes what the player asks (immediate) and what the helicopter really does (natural)
        Vector3 localVel = transform.InverseTransformDirection(_rb.linearVelocity);
        float velForward = Mathf.Clamp(localVel.z / maxHorizontalSpeed, -1f, 1f);
        float velSide = Mathf.Clamp(localVel.x / maxHorizontalSpeed, -1f, 1f);

        float forwardTilt = Mathf.Lerp(_move.y, velForward, velocityTiltBlend);
        float sideTilt = Mathf.Lerp(_move.x, velSide, velocityTiltBlend);

        // Positive X rotation = nose down, negative Z rotation = lean right
        Quaternion targetTilt = Quaternion.Euler(forwardTilt * maxTiltAngle, 0f, -sideTilt * maxTiltAngle);

        float t = 1f - Mathf.Exp(-tiltSmoothing * Time.deltaTime);
        visual.localRotation = Quaternion.Slerp(visual.localRotation, targetTilt, t);
    }

    private void SpinRotors()
    {
        if (rotor != null)
            rotor.Rotate(rotorAxis, rotorSpinSpeed * Time.deltaTime, Space.Self);

        if (tailRotor != null)
            tailRotor.Rotate(tailRotorAxis, tailRotorSpinSpeed * Time.deltaTime, Space.Self);
    }
}