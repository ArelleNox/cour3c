using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class HelicopterCamera : MonoBehaviour
{
    [Header("General")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Transform target;
    [SerializeField, Tooltip("Height above the helicopter pivot that the camera orbits around.")]
    private float pivotHeight = 1f;

    [Header("Orbit parameters")]
    [SerializeField, Tooltip("Degrees per pixel of mouse movement.")]
    private float lookSensitivity = 0.15f;
    [SerializeField] private float minPitch = -10f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private float startPitch = 20f;

    [Header("Zoom parameters")]
    [SerializeField] private float startDistance = 12f;
    [SerializeField] private float minDistance = 5f;
    [SerializeField] private float maxDistance = 30f;
    [SerializeField, Tooltip("Distance change per scroll notch.")]
    private float zoomStep = 2f;
    [SerializeField] private float zoomSmoothing = 8f;

    [Header("Obstacle avoidance")]
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private float collisionRadius = 0.4f;
    [SerializeField, Tooltip("How fast the camera moves closer when something is in the way.")]
    private float pushInSpeed = 25f;
    [SerializeField, Tooltip("How fast the camera moves back out once the way is clear.")]
    private float easeOutSpeed = 3f;

    private Camera _cam;
    private InputAction _lookAction;
    private InputAction _zoomAction;

    private readonly RaycastHit[] _hits = new RaycastHit[16];

    private float _yaw;
    private float _pitch;
    private float _targetDistance;
    private float _distance;
    private float _currentDistance;

    private void Awake()
    {
        _cam = GetComponent<Camera>();

        if (inputActions == null)
        {
            Debug.LogError("HelicopterCamera: Input Actions asset is not assigned.", this);
            enabled = false;
            return;
        }

        // The Helicopter map is enabled by HelicopterController, here we only read from it
        InputActionMap map = inputActions.FindActionMap("Helicopter");
        if (map == null)
        {
            Debug.LogError("HelicopterCamera: action map 'Helicopter' not found in the Input Actions asset.", this);
            enabled = false;
            return;
        }

        _lookAction = map.FindAction("Look");
        _zoomAction = map.FindAction("Zoom");

        if (_lookAction == null || _zoomAction == null)
        {
            Debug.LogError("HelicopterCamera: actions 'Look' or 'Zoom' not found in the 'Helicopter' map.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (target == null) return;

        // Start behind the helicopter, no swoop
        _yaw = target.eulerAngles.y;
        _pitch = startPitch;
        _targetDistance = Mathf.Clamp(startDistance, minDistance, maxDistance);
        _distance = _targetDistance;
        _currentDistance = _targetDistance;

        PlaceCamera();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Orbit : mouse delta is already per frame, no deltaTime
        Vector2 look = _lookAction.ReadValue<Vector2>();
        _yaw += look.x * lookSensitivity;
        _pitch = Mathf.Clamp(_pitch - look.y * lookSensitivity, minPitch, maxPitch);

        // Zoom : only the direction of the scroll is used, so the device scale does not matter
        float scroll = _zoomAction.ReadValue<float>();
        if (!Mathf.Approximately(scroll, 0f))
        {
            _targetDistance = Mathf.Clamp(_targetDistance - Mathf.Sign(scroll) * zoomStep, minDistance, maxDistance);
        }

        float zoomT = 1f - Mathf.Exp(-zoomSmoothing * Time.deltaTime);
        _distance = Mathf.Lerp(_distance, _targetDistance, zoomT);

        PlaceCamera();
    }

    private void PlaceCamera()
    {
        Vector3 pivot = target.position + Vector3.up * pivotHeight;
        Quaternion orbitRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 dir = orbitRotation * Vector3.back;

        // Obstacle avoidance : the camera never goes farther than the first obstacle
        float allowed = Mathf.Min(_distance, GetFreeDistance(pivot, dir, _distance));

        // Pull in fast, ease out slowly
        float speed = allowed < _currentDistance ? pushInSpeed : easeOutSpeed;
        float t = 1f - Mathf.Exp(-speed * Time.deltaTime);
        _currentDistance = Mathf.Lerp(_currentDistance, allowed, t);

        transform.position = pivot + dir * _currentDistance;
        transform.LookAt(pivot);
    }

    private float GetFreeDistance(Vector3 pivot, Vector3 dir, float maxDist)
    {
        int count = Physics.SphereCastNonAlloc(pivot, collisionRadius, dir, _hits, maxDist, obstacleMask, QueryTriggerInteraction.Ignore);

        float closest = maxDist;
        for (int i = 0; i < count; i++)
        {
            // The helicopter itself is not an obstacle
            if (_hits[i].collider.transform.IsChildOf(target) || _hits[i].collider.transform == target)
                continue;

            if (_hits[i].distance < closest)
                closest = _hits[i].distance;
        }

        return Mathf.Max(closest, minDistance * 0.25f);
    }
}