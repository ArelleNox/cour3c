using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

// Generic seat : put one on each vehicle (bike, helicopter, train...).
public class VehiculeSeat : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private GameObject playerObject;

    [Header("Vehicle")]
    [SerializeField, FormerlySerializedAs("bikeController"),
     Tooltip("The controller script of THIS vehicle (BikeController, HelicopterController...).")]
    private Behaviour vehicleController;
    [SerializeField, FormerlySerializedAs("bikeCamera")] private GameObject vehicleCamera;
    [SerializeField] private Transform enterExit;
    [SerializeField] private float enterDistance = 3f;

    [Header("Exit rules")]
    [SerializeField, Tooltip("Tick for flying vehicles : the player can only get out close to the ground.")]
    private bool requireGroundToExit = false;
    [SerializeField] private float maxExitHeight = 3f;
    [SerializeField] private LayerMask groundMask;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    // Shared by all seats : prevents two seats from reacting to the same key press
    private static int _lastActionFrame = -1;

    private bool _isRiding = false;
    private Rigidbody _playerRb;
    private InputActionMap _actionMap;
    private InputAction _interactAction;

    private void Awake()
    {
        if (inputActions == null)
        {
            Debug.LogError("VehiculeSeat: Input Actions asset is not assigned.", this);
            enabled = false;
            return;
        }

        if (vehicleController == null || vehicleController == this)
        {
            Debug.LogError("VehiculeSeat: 'Vehicle Controller' must be the controller script of this vehicle (not empty, not the seat itself).", this);
            enabled = false;
            return;
        }

        _actionMap = inputActions.FindActionMap("Global");
        if (_actionMap == null)
        {
            Debug.LogError("VehiculeSeat: action map 'Global' not found in the Input Actions asset.", this);
            enabled = false;
            return;
        }

        _interactAction = _actionMap.FindAction("enterExit");
        if (_interactAction == null)
        {
            Debug.LogError("VehiculeSeat: action 'enterExit' not found in the 'Global' map.", this);
            enabled = false;
            return;
        }

        if (playerObject != null)
            _playerRb = playerObject.GetComponent<Rigidbody>();

        if (requireGroundToExit && groundMask.value == 0)
            groundMask = LayerMask.GetMask("Ground");
    }

    private void OnEnable()
    {
        if (_actionMap == null) return;

        _actionMap.Enable();
        _interactAction.performed += OnInteract;
    }

    private void OnDisable()
    {
        if (_actionMap == null) return;

        _interactAction.performed -= OnInteract;
        _actionMap.Disable();
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        // Another seat already handled this key press
        if (_lastActionFrame == Time.frameCount) return;

        if (_isRiding)
        {
            if (!CanExit()) return;

            ExitVehicle();
            _lastActionFrame = Time.frameCount;
        }
        // The player is only active when nobody is riding any vehicle
        else if (playerObject.activeSelf && IsPlayerInRange())
        {
            EnterVehicle();
            _lastActionFrame = Time.frameCount;
        }
    }

    private bool IsPlayerInRange()
    {
        float dist = Vector3.Distance(playerObject.transform.position, transform.position);
        return dist <= enterDistance;
    }

    private bool CanExit()
    {
        if (!requireGroundToExit) return true;

        return Physics.Raycast(transform.position, Vector3.down, maxExitHeight, groundMask, QueryTriggerInteraction.Ignore);
    }

    private void EnterVehicle()
    {
        // Turning the player off also turns off its camera, collider and FirstPerson input map
        playerObject.SetActive(false);

        // The controller and the camera snap into place in their own OnEnable
        vehicleController.enabled = true;
        vehicleCamera.SetActive(true);

        _isRiding = true;
    }

    private void ExitVehicle()
    {
        // The controller parks the vehicle in its own OnDisable
        vehicleController.enabled = false;
        vehicleCamera.SetActive(false);

        // Move the player while it is still inactive, then wake it up
        playerObject.transform.SetPositionAndRotation(
            enterExit.position,
            Quaternion.Euler(0f, transform.eulerAngles.y, 0f));

        playerObject.SetActive(true);

        if (_playerRb != null)
        {
            _playerRb.linearVelocity = Vector3.zero;
            _playerRb.angularVelocity = Vector3.zero;
        }

        _isRiding = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, enterDistance);

        if (requireGroundToExit)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + Vector3.down * maxExitHeight);
        }
    }
}