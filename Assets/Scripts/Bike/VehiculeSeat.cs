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
    private Rigidbody _rb;
    private InputActionMap _actionMap;
    private InputAction _moveAction;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        if (inputActions == null)
        {
            Debug.LogError("BikeController: Input Actions asset is not assigned.", this);
            enabled = false;
            return;
        }

        _actionMap = inputActions.FindActionMap("Global");
        if (_actionMap == null)
        {
            Debug.LogError("BikeController: action map 'global' not found in the Input Actions asset.", this);
            enabled = false;
            return;
        }

        _moveAction = _actionMap.FindAction("enterExit");
        if (_moveAction == null)
        {
            Debug.LogError("BikeController: action 'enterExit' not found in the 'Global' map.", this);
            enabled = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
