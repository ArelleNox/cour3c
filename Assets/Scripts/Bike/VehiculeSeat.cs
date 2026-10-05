using UnityEngine;
using UnityEngine.InputSystem;

public class VehiculeSeat : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private GameObject playerObject;
    [SerializeField] private FirstPersonController playerController;

    [Header("Vehicle")]
    [SerializeField] private BikeController bikeController;
    [SerializeField] private GameObject bikeCamera;
    [SerializeField] private Transform enterExit;
    [SerializeField] private float enterDistance = 3f;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

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
        if (_isRiding)
        {
            ExitBike();
        }
        else if (IsPlayerInRange())
        {
            EnterBike();
        }
    }

    private bool IsPlayerInRange()
    {
        float dist = Vector3.Distance(playerObject.transform.position, transform.position);
        return dist <= enterDistance;
    }

    private void EnterBike()
    {
        // Turning the player off also turns off its camera, collider and FirstPerson input map
        playerObject.SetActive(false);

        // BikeController.OnEnable enables the Vehicle map, BikeCamera.OnEnable snaps behind the bike
        bikeController.enabled = true;
        bikeCamera.SetActive(true);

        _isRiding = true;
    }

    private void ExitBike()
    {
        // BikeController.OnDisable parks the bike and disables the Vehicle map
        bikeController.enabled = false;
        bikeCamera.SetActive(false);

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
    }
}