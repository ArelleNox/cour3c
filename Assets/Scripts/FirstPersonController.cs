using System.Collections;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("General")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Transform seedPrefab;

    [Header("Move parameters")]
    [SerializeField] private float moveSpeed = 10;
    [SerializeField] private float airMoveSpeed = 5;
    [SerializeField, Tooltip("Additional speed, total run speed is moveSpeed + runSpeed.")]
    private float runSpeed = 2;
    [SerializeField] private AnimationCurve runCurve;
    [SerializeField] private AnimationCurve jumpCurve;
    [SerializeField] private float additionalGravity = 10;
    [SerializeField] private float maxJumpForce = 10;
    [SerializeField] private float maxJumpTime = 1;
    [SerializeField] private float jumpDuration = 1.5f;

    [Header("Mouse look parameters")]
    [SerializeField] private float rotationSpeed = 10;
    [SerializeField] private float sensitivity = 100;
    [SerializeField] private Transform originTsfm;
    
    [Header("Headbob parameters")]
    [SerializeField] private AnimationCurve headbobCurve;
    [SerializeField] private Transform camHolder;
    [SerializeField] private float headbobAmp = 0.05f;
    [SerializeField] private float headbobFreq = 3f;
    [SerializeField] private float headbobTime = 0.5f;
    [SerializeField] private bool eyeStabilization = true;

    [Header("Others")]
    [SerializeField] private float maxSeedSize = 10;

    private bool _invertYAxis = false;
    
    private Transform _mainCam;
    private Rigidbody _rb;
    private CapsuleCollider _coll;
    private Animator _animController;

    private float _curSpeed;
    private bool _jump;
    private bool _jumping;
    private Vector3 _moveVector;
    private float _curJumpForce;
    private float _curPitch;
    private Quaternion _yRotation;
    private bool _grounded;
    private float _runTimer;
    private float _jumpTimer;
    private float _headbobTimer;

    private InputActionMap _actionMap;
    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _runAction;
    private InputAction _jumpAction;


    void Awake()
    {
        _actionMap = inputActions.FindActionMap("FirstPerson");
        _actionMap.Enable();

        _moveAction = _actionMap.FindAction("Move");
        _lookAction = _actionMap.FindAction("Look");
        _runAction = _actionMap.FindAction("Run");
        _jumpAction = _actionMap.FindAction("Jump");

        _jumpAction.performed += OnJump;

        _rb = GetComponent<Rigidbody>();
        _coll = GetComponent<CapsuleCollider>();
        _mainCam = GetComponentInChildren<Camera>().transform;
        _animController = GetComponent<Animator>();

        _curSpeed = moveSpeed;
        _moveVector = Vector3.zero;
        _yRotation = Quaternion.identity;
        _jump = false;
        _jumping = false;
        _runTimer = 0;
        _jumpTimer = 0;
        _curPitch = 0;
        _headbobTimer = 0;
        _curJumpForce = 0;
    }

    // Update is called once per frame
    void Update()
    {
        // Hide & lock mouse cursor inside the screen
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = true;

        // Set grounded or not state
        _grounded = false;
        if (Physics.SphereCast(originTsfm.position, _coll.radius, -transform.up, out RaycastHit hit, (_coll.height * 0.5f) - _coll.radius + 0.1f, LayerMask.GetMask("Ground")))
        {
            _grounded = true;
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            Transform seed = Instantiate(seedPrefab, transform.position + transform.forward * 2f, Quaternion.identity);

            StartCoroutine(GrowCoroutine(seed));
        }

        // Set movement vector from input to be used in fixedupdate
        Vector2 v = _moveAction.ReadValue<Vector2>();
        _moveVector = transform.forward * v.y + transform.right * v.x;

        _animController.SetFloat("HorSpeed", v.x);
        _animController.SetFloat("VertSpeed", v.y);

        // Increase current speed if running
        if (_runAction.IsPressed())
        {
            _curSpeed = moveSpeed + runCurve.Evaluate(_runTimer / 1.5f) * runSpeed;

            _runTimer += Time.deltaTime;

            _animController.SetBool("Run", true);
        }
        else
        {
            _curSpeed = moveSpeed;
            _runTimer = 0;

            _animController.SetBool("Run", false);
        }

        // Eyes stabilization, find the focus point and make the headbob look at it
        float maxDist = 30f;
        float focusPointDist = maxDist;
        Vector3 focusPoint = camHolder.position + camHolder.forward * maxDist;
        if (Physics.Raycast(camHolder.position, camHolder.forward, out RaycastHit hitInf, maxDist, ~LayerMask.GetMask("Player")))
        {
            focusPoint = hitInf.point;
            focusPointDist = hitInf.distance;
        }

        // Vector of the headbob looking forward (this is what we want to change)
        Vector3 forwardHeadBobVec = camHolder.forward;
        Debug.DrawRay(_mainCam.position, forwardHeadBobVec * focusPointDist, Color.red);

        // Vector of the headbob looking toward the focus point (what we're trying to achieve)
        Vector3 focusHeadbobVec = focusPoint - _mainCam.position;
        
        // Eye vector debug (where the eye should focus theoretically)
        Debug.DrawRay(camHolder.position, camHolder.forward * focusPointDist, Color.blue);

        // Point the mainCam toward the focusPoint
        if (eyeStabilization)
        {
            _mainCam.rotation = Quaternion.LookRotation(focusHeadbobVec, Vector3.up);
            Debug.DrawRay(_mainCam.position, focusHeadbobVec, Color.orange);
        }

        // Headbob (the mainCam inside the camHolder)
        if (_rb.linearVelocity.magnitude > 0.01f && !_jumping)
        {
            // Headbob position is set in world space to keep it straight
            _mainCam.localPosition = new Vector3(0, headbobCurve.Evaluate(_headbobTimer / headbobTime) * headbobAmp, 0);

            _headbobTimer += Time.deltaTime;
            if (_headbobTimer > headbobTime)
            {
                _headbobTimer = 0;
            }
        }

        // Mouse look
        Vector3 mouseDelta = _lookAction.ReadValue<Vector2>() * sensitivity;
        _yRotation *= Quaternion.Euler(0, rotationSpeed * (mouseDelta.x / Screen.width) * Time.deltaTime, 0);

        if (!_invertYAxis)
            mouseDelta.y = -mouseDelta.y;

        float mouseMovement = rotationSpeed * (mouseDelta.y / Screen.height) * Time.deltaTime;
        
        // Limit upper and lower orientation of the camera
        _curPitch += mouseMovement;
        _curPitch = Mathf.Clamp(_curPitch, -90, 90);

        // camHolder holds the mainCam
        camHolder.localRotation = Quaternion.Euler(_curPitch, 0, 0);
    }

    private void FixedUpdate()
    {
        _rb.MoveRotation(_yRotation);

        if (_jump)
        {
            if (!_jumping)
            {
                if (_grounded)
                {
                    _jumpTimer = 0;
                }

                _jumping = true;
                _jump = false;
            }
        }
        else if (_jumping) // Jump impulsion phaze
        {
            float jumpVel = jumpCurve.Evaluate(_jumpTimer / jumpDuration) * _curJumpForce * Time.deltaTime;
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, jumpVel, _rb.linearVelocity.z);
            
            _jumpTimer += Time.deltaTime;
            if (_jumpTimer > jumpDuration)
            {
                _jumpTimer = 0;
                _jumping = false;
            }
        }
        else if (!_jumping)
        {
            if (_grounded) // Regular movement
            {
                _rb.linearVelocity = _moveVector * _curSpeed * Time.deltaTime;

            }
            else // Jump descent phaze
            {
                _rb.linearVelocity += _moveVector * airMoveSpeed * Time.deltaTime;

                _rb.AddForce(Vector3.down * additionalGravity, ForceMode.Force);
            }
        }
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        _jump = true;

        _curJumpForce = ((float)context.duration / maxJumpTime) * maxJumpForce;
    }

    private IEnumerator GrowCoroutine(Transform seed)
    {
        for (float s = 0.1f; s < maxSeedSize; s += Time.deltaTime)
        {
            seed.localScale = new Vector3(s, s, s);

            yield return null;
        }
    }
}
