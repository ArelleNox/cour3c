using UnityEngine;

[RequireComponent(typeof(Camera))]
public class BikeCamera : MonoBehaviour
{
    [Header("General")]
    [SerializeField] private BikeController bike;

    [Header("Follow parameters")]
    [SerializeField] private float distance = 6f;
    [SerializeField] private float height = 2.5f;
    [SerializeField, Tooltip("Height above the bike root that the camera looks at.")]
    private float lookAtHeight = 1f;
    [SerializeField, Tooltip("How fast the camera swings behind the bike in turns.")]
    private float yawFollowSpeed = 4f;

    [Header("FOV parameters")]
    [SerializeField] private float minFov = 60f;
    [SerializeField] private float maxFov = 85f;
    [SerializeField] private float fovSmoothing = 3f;

    private Camera _cam;
    private float _yaw;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        Snap();
    }

    private void LateUpdate()
    {
        if (bike == null) return;

        Transform target = bike.transform;

        // Frame-rate independent smoothing factor
        float yawT = 1f - Mathf.Exp(-yawFollowSpeed * Time.deltaTime);
        float fovT = 1f - Mathf.Exp(-fovSmoothing * Time.deltaTime);

        // The camera slowly swings behind the bike, no player control
        _yaw = Mathf.LerpAngle(_yaw, target.eulerAngles.y, yawT);
        PlaceCamera(target);

        // FOV grows with the speed
        float targetFov = Mathf.Lerp(minFov, maxFov, bike.Speed01);
        _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov, fovT);
    }

    // Instant placement, avoids a camera swoop when it gets enabled
    private void Snap()
    {
        if (bike == null) return;

        Transform target = bike.transform;
        _yaw = target.eulerAngles.y;
        PlaceCamera(target);
        _cam.fieldOfView = minFov;
    }

    private void PlaceCamera(Transform target)
    {
        // Only the yaw is used, so the bike lean and slopes never shake the camera
        Quaternion yawRotation = Quaternion.Euler(0f, _yaw, 0f);
        transform.position = target.position + yawRotation * new Vector3(0f, height, -distance);
        transform.LookAt(target.position + Vector3.up * lookAtHeight);
    }
}