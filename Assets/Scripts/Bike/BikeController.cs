using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class BikeController : MonoBehaviour
{
    [Header("General")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField, Tooltip("Child object holding the mesh. Only this one leans, never the Rigidbody.")]
    private Transform visual;

    [Header("Speed parameters")]
    [SerializeField] private float maxSpeed = 50f;
    [SerializeField, Tooltip("Fast acceleration (m/s per second).")]
    private float acceleration = 20f;
    [SerializeField, Tooltip("Low value = strong inertia when releasing the accelerator.")]
    private float coastDeceleration = 2f;
    [SerializeField, Tooltip("High value = powerful brakes.")]
    private float brakeDeceleration = 40f;
    [SerializeField, Tooltip("If the real speed drops this much below the target speed, we consider the bike hit something.")]
    private float crashSpeedTolerance = 3f;

    [Header("Steering parameters")]
    [SerializeField, Tooltip("Yaw rotation speed (deg/s) at full steering.")]
    private float maxTurnRate = 100f;
    [SerializeField, Tooltip("X = speed / maxSpeed, Y = turn multiplier. 0 at standstill, tighter at low speed, wider at high speed.")]
    private AnimationCurve turnBySpeedCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.25f, 1f),
        new Keyframe(1f, 0.35f));
    [SerializeField, Tooltip("How fast the handlebar turns toward the pressed key.")]
    private float steerRate = 4f;
    [SerializeField, Tooltip("How fast the handlebar goes back to center when no key is pressed.")]
    private float steerReturnRate = 6f;

    [Header("Lean parameters")]
    [SerializeField] private float maxLeanAngle = 35f;
    [SerializeField] private float leanSmoothing = 6f;

    private Rigidbody _rb;
    private InputActionMap _actionMap;
    private InputAction _moveAction;

    private Vector2 _input;
    private float _speed;
    private float _steer;
    private float _lean;

    public float Speed => _speed;
    public float Speed01 => Mathf.Clamp01(_speed / Mathf.Max(maxSpeed, 0.01f));

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        if (inputActions == null)
        {
            Debug.LogError("BikeController: Input Actions asset is not assigned.", this);
            enabled = false;
            return;
        }

        _actionMap = inputActions.FindActionMap("Vehicle");
        if (_actionMap == null)
        {
            Debug.LogError("BikeController: action map 'Vehicle' not found in the Input Actions asset.", this);
            enabled = false;
            return;
        }

        _moveAction = _actionMap.FindAction("Move");
        if (_moveAction == null)
        {
            Debug.LogError("BikeController: action 'Move' not found in the 'Vehicle' map.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (_actionMap == null) return;
        _actionMap.Enable();
    }

    private void OnDisable()
    {
        if (_actionMap == null) return;
        _actionMap.Disable();

        // Park the bike when nobody drives it
        _input = Vector2.zero;
        _speed = 0f;
        _steer = 0f;
        _lean = 0f;

        if (_rb != null)
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);

        if (visual != null)
            visual.localRotation = Quaternion.identity;
    }

    private void Update()
    {
        _input = _moveAction.ReadValue<Vector2>();

        // Handlebar value goes progressively toward the input (-1 left, 1 right)
        float rate = Mathf.Approximately(_input.x, 0f) ? steerReturnRate : steerRate;
        _steer = Mathf.MoveTowards(_steer, _input.x, rate * Time.deltaTime);

        UpdateLean();
    }

    private void FixedUpdate()
    {
        Vector3 vel = _rb.linearVelocity;

        // If the bike hit a wall, the real speed is lower than the target speed: follow it
        float actualSpeed = new Vector2(vel.x, vel.z).magnitude;
        if (_speed - actualSpeed > crashSpeedTolerance)
            _speed = actualSpeed;

        UpdateSpeed();

        // Turn amplitude depends on the speed
        float turn = _steer * maxTurnRate * turnBySpeedCurve.Evaluate(Speed01) * Time.fixedDeltaTime;
        Quaternion newRotation = _rb.rotation * Quaternion.Euler(0f, turn, 0f);
        _rb.MoveRotation(newRotation);

        // Move forward along the new heading, keep the vertical speed (gravity)
        Vector3 dir = newRotation * Vector3.forward;
        _rb.linearVelocity = new Vector3(dir.x * _speed, vel.y, dir.z * _speed);
    }

    private void UpdateSpeed()
    {
        float dt = Time.fixedDeltaTime;

        if (_input.y > 0.01f)
        {
            // Z : fast acceleration
            _speed = Mathf.MoveTowards(_speed, maxSpeed, acceleration * dt);
        }
        else if (_input.y < -0.01f)
        {
            // S : powerful brakes
            _speed = Mathf.MoveTowards(_speed, 0f, brakeDeceleration * dt);
        }
        else
        {
            // No input : inertia
            _speed = Mathf.MoveTowards(_speed, 0f, coastDeceleration * dt);
        }
    }

    private void UpdateLean()
    {
        if (visual == null) return;

        // Lean toward the inside of the turn, more when going fast.
        // Positive Z rotation tilts left, so a right turn (steer > 0) needs a negative angle.
        float targetLean = -_steer * maxLeanAngle * Speed01;
        _lean = Mathf.Lerp(_lean, targetLean, leanSmoothing * Time.deltaTime);

        visual.localRotation = Quaternion.Euler(0f, 0f, _lean);
    }
}