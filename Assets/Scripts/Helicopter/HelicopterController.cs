using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class HelicopterController : MonoBehaviour
{
    [Header("General")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField, Tooltip("Child holding ALL the meshes. Only this one tilts, never the Rigidbody.")]
    private Transform visual;
    [SerializeField, Tooltip("Starts parked (script off, body kinematic). VehiculeSeat turns the script on when the player gets in.")]
    private bool startParked = true;

    [Header("Tilt")]
    [SerializeField, Tooltip("Maximum tilt of the body in degrees (forward, backward, left, right).")]
    private float maxTiltAngle = 20f;
    [SerializeField, Tooltip("Degrees per second when a key is pressed. Low = heavy, slow to react.")]
    private float tiltRate = 45f;
    [SerializeField, Tooltip("Degrees per second when keys are released and the body levels out.")]
    private float tiltReturnRate = 25f;
    [SerializeField, Tooltip("Extra visual roll when turning (cosmetic only).")]
    private float yawBankAngle = 8f;

    [Header("Horizontal movement")]
    [SerializeField, Tooltip("Acceleration (m/s2) when fully tilted.")]
    private float maxTiltAcceleration = 10f;
    [SerializeField, Tooltip("Air resistance. Low = the helicopter keeps drifting after you stop tilting.")]
    private float horizontalDrag = 0.5f;
    [SerializeField] private float maxHorizontalSpeed = 18f;

    [Header("Vertical movement")]
    [SerializeField] private float climbSpeed = 6f;
    [SerializeField] private float verticalAcceleration = 4f;
    [SerializeField, Tooltip("World height the helicopter cannot go above.")]
    private float maxAltitude = 80f;

    [Header("Yaw")]
    [SerializeField] private float yawSpeed = 70f;
    [SerializeField, Tooltip("How fast the yaw input builds up and fades out.")]
    private float yawResponse = 3f;

    [Header("Rotors")]
    [SerializeField, Tooltip("Time for the rotors to reach full power. The helicopter only moves once the rotors are spinning.")]
    private float rotorSpinUpTime = 1.5f;
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

    // Current body tilt in degrees : _pitch > 0 = nose down (forward), _roll > 0 = leaning right
    private float _pitch;
    private float _roll;

    // 0 = rotors stopped, 1 = full power
    private float _power;

    // Rotation the visual had in the editor, the tilt is applied on top of it
    private Quaternion _visualBaseRotation = Quaternion.identity;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;

        if (visual != null)
            _visualBaseRotation = visual.localRotation;
        else
            Debug.LogWarning("HelicopterController: 'Visual' is not assigned, the helicopter cannot tilt.", this);

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
            return;
        }

        // Whatever the checkbox says in the Inspector, nobody drives it at the start of the scene
        if (startParked)
        {
            _rb.isKinematic = true;
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
        _pitch = 0f;
        _roll = 0f;
        _power = 0f;

        if (_rb != null)
        {
            // Gravity is off, so the whole velocity must be cleared, then the body is parked
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
        }

        if (visual != null)
            visual.localRotation = _visualBaseRotation;
    }

    private void Update()
    {
        _move = _moveAction.ReadValue<Vector2>();
        _elevation = _elevationAction.ReadValue<float>();

        // Rotors spin up progressively
        _power = Mathf.MoveTowards(_power, 1f, Time.deltaTime / Mathf.Max(rotorSpinUpTime, 0.01f));

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
        Quaternion newRotation = _rb.rotation * Quaternion.Euler(0f, _yaw * yawSpeed * _power * dt, 0f);
        _rb.MoveRotation(newRotation);

        Vector3 vel = _rb.linearVelocity;
        Vector3 horizontalVel = new Vector3(vel.x, 0f, vel.z);

        // The tilt creates the acceleration : nose down pushes forward, leaning right pushes right
        float forwardTilt = _pitch / Mathf.Max(maxTiltAngle, 0.01f);
        float sideTilt = _roll / Mathf.Max(maxTiltAngle, 0.01f);
        Vector3 acceleration = newRotation * new Vector3(sideTilt, 0f, forwardTilt) * (maxTiltAcceleration * _power);

        horizontalVel += acceleration * dt;

        // Air resistance : without tilt the helicopter slowly drifts to a stop
        horizontalVel *= Mathf.Exp(-horizontalDrag * dt);
        horizontalVel = Vector3.ClampMagnitude(horizontalVel, maxHorizontalSpeed);

        // Vertical : no input = hover
        float verticalTarget = _elevation * climbSpeed * _power;
        if (_rb.position.y >= maxAltitude)
            verticalTarget = Mathf.Min(verticalTarget, 0f);

        float verticalVel = Mathf.MoveTowards(vel.y, verticalTarget, verticalAcceleration * dt);

        _rb.linearVelocity = new Vector3(horizontalVel.x, verticalVel, horizontalVel.z);
    }

    private void UpdateTilt()
    {
        // Z / S tilt forward / backward, Q / D tilt left / right
        float targetPitch = _move.y * maxTiltAngle;
        float targetRoll = _move.x * maxTiltAngle;

        // Fast when a key is pressed, slow when the body levels out
        float pitchRate = Mathf.Approximately(_move.y, 0f) ? tiltReturnRate : tiltRate;
        float rollRate = Mathf.Approximately(_move.x, 0f) ? tiltReturnRate : tiltRate;

        _pitch = Mathf.MoveTowards(_pitch, targetPitch, pitchRate * Time.deltaTime);
        _roll = Mathf.MoveTowards(_roll, targetRoll, rollRate * Time.deltaTime);

        if (visual == null) return;

        // Positive X rotation = nose down, negative Z rotation = lean right
        float bank = -_roll - (_yaw * yawBankAngle);
        visual.localRotation = _visualBaseRotation * Quaternion.Euler(_pitch, 0f, bank);
    }

    private void SpinRotors()
    {
        if (rotor != null)
            rotor.Rotate(rotorAxis, rotorSpinSpeed * _power * Time.deltaTime, Space.Self);

        if (tailRotor != null)
            tailRotor.Rotate(tailRotorAxis, tailRotorSpinSpeed * _power * Time.deltaTime, Space.Self);
    }
}