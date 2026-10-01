using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class FirstPersonController : MonoBehaviour
{
    [Header("General")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Transform seedPrefab;

    [Header("Move parameters")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float airMoveSpeed = 10f;
    [SerializeField, Tooltip("Additional speed, total run speed is moveSpeed + runSpeed.")]
    private float runSpeed = 2f;
    [SerializeField] private float runRampTime = 1.5f;
    [SerializeField] private AnimationCurve runCurve;
    [SerializeField] private AnimationCurve jumpCurve;
    [SerializeField] private float additionalGravity = 10f;
    [SerializeField] private float minJumpForce = 4f;
    [SerializeField] private float maxJumpForce = 10f;
    [SerializeField] private float maxJumpTime = 1f;
    [SerializeField] private float jumpDuration = 1.5f;

    [Header("Mouse look parameters")]
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float sensitivity = 25;
    [SerializeField] private Transform originTsfm;

    [Header("Headbob parameters")]
    [SerializeField] private AnimationCurve headbobCurve;
    [SerializeField] private Transform camHolder;
    [SerializeField] private float headbobAmp = 0.05f;
    [SerializeField] private float headbobTime = 0.5f;
    [SerializeField] private float headbobReturnSpeed = 10f;
    [SerializeField] private bool eyeStabilization = true;

    [Header("Others")]
    [SerializeField] private float maxSeedSize = 10f;

    private const float MaxPitch = 85f;
    private const float FocusMaxDist = 30f;

    // Animator hashes (évite les recherches par string)
    private static readonly int HorSpeedHash = Animator.StringToHash("HorSpeed");
    private static readonly int VertSpeedHash = Animator.StringToHash("VertSpeed");
    private static readonly int RunHash = Animator.StringToHash("Run");

    private bool _invertYAxis = false;

    private Transform _mainCam;
    private Rigidbody _rb;
    private CapsuleCollider _coll;
    private Animator _animController;

    // Masques de layers mis en cache
    private int _groundMask;
    private int _focusMask;

    private float _curSpeed;
    private bool _jumpRequested;
    private bool _jumping;
    private Vector3 _moveVector;
    private float _pendingJumpForce;
    private float _jumpForce;
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

        _moveAction = _actionMap.FindAction("Move");
        _lookAction = _actionMap.FindAction("Look");
        _runAction = _actionMap.FindAction("Run");
        _jumpAction = _actionMap.FindAction("Jump");

        _rb = GetComponent<Rigidbody>();
        _coll = GetComponent<CapsuleCollider>();
        _mainCam = GetComponentInChildren<Camera>().transform;
        _animController = GetComponent<Animator>();

        _groundMask = LayerMask.GetMask("Ground");
        _focusMask = ~LayerMask.GetMask("Player");

        _curSpeed = moveSpeed;
        _moveVector = Vector3.zero;
        _yRotation = _rb.rotation;
        _jumpRequested = false;
        _jumping = false;
        _runTimer = 0;
        _jumpTimer = 0;
        _curPitch = 0;
        _headbobTimer = 0;
        _jumpForce = 0;
        _pendingJumpForce = 0;
    }

    private void OnEnable()
    {
        _actionMap.Enable();
        _jumpAction.performed += OnJump;

        // Cache et verrouille le curseur
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        _jumpAction.performed -= OnJump;
        _actionMap.Disable();
    }

    void Update()
    {
        CheckGrounded();

        // Nouveau Input System
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            Transform seed = Instantiate(seedPrefab, transform.position + transform.forward * 2f, Quaternion.identity);
            StartCoroutine(GrowCoroutine(seed));
        }

        HandleMovementInput();
        HandleMouseLook();
    }

    private void LateUpdate()
    {
        // La caméra est gérée après tous les Update pour éviter un décalage d'une frame
        HandleHeadbob();
        HandleEyeStabilization();
    }

    private void FixedUpdate()
    {
        _rb.MoveRotation(_yRotation);

        // Démarrage d'un saut : uniquement au sol et pas déjà en train de sauter
        if (_jumpRequested)
        {
            _jumpRequested = false;

            if (_grounded && !_jumping)
            {
                _jumping = true;
                _jumpTimer = 0;
                _jumpForce = _pendingJumpForce;
            }
        }

        if (_jumping) // Phase d'impulsion
        {
            float jumpVel = jumpCurve.Evaluate(_jumpTimer / jumpDuration) * _jumpForce;
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, jumpVel, _rb.linearVelocity.z);
            ApplyAirControl();

            _jumpTimer += Time.fixedDeltaTime;
            if (_jumpTimer > jumpDuration)
            {
                _jumpTimer = 0;
                _jumping = false;
            }
        }
        else if (_grounded) // Déplacement normal : on garde la vitesse verticale
        {
            Vector3 horizontal = _moveVector * _curSpeed;
            _rb.linearVelocity = new Vector3(horizontal.x, _rb.linearVelocity.y, horizontal.z);
        }
        else // Phase de descente
        {
            ApplyAirControl();
            _rb.AddForce(Vector3.down * additionalGravity, ForceMode.Force);
        }
    }

    private void CheckGrounded()
    {
        float castDist = (_coll.height * 0.5f) - _coll.radius + 0.1f;
        _grounded = Physics.SphereCast(originTsfm.position, _coll.radius, Vector3.down, out _, castDist, _groundMask);
    }

    private void HandleMovementInput()
    {
        Vector2 v = _moveAction.ReadValue<Vector2>();
        _moveVector = Vector3.ClampMagnitude(transform.forward * v.y + transform.right * v.x, 1f);

        // Le dampTime lisse les transitions de l'Animator
        _animController.SetFloat(HorSpeedHash, v.x, 0.1f, Time.deltaTime);
        _animController.SetFloat(VertSpeedHash, v.y, 0.1f, Time.deltaTime);

        if (_runAction.IsPressed())
        {
            _curSpeed = moveSpeed + runCurve.Evaluate(_runTimer / runRampTime) * runSpeed;
            _runTimer += Time.deltaTime;
            _animController.SetBool(RunHash, true);
        }
        else
        {
            _curSpeed = moveSpeed;
            _runTimer = 0;
            _animController.SetBool(RunHash, false);
        }
    }

    private void HandleMouseLook()
    {
        // Le delta souris est déjà "par frame" : pas de Time.deltaTime
        Vector2 mouseDelta = _lookAction.ReadValue<Vector2>() * sensitivity;

        _yRotation *= Quaternion.Euler(0, rotationSpeed * (mouseDelta.x / Screen.width), 0);

        if (!_invertYAxis)
            mouseDelta.y = -mouseDelta.y;

        _curPitch += rotationSpeed * (mouseDelta.y / Screen.height);
        _curPitch = Mathf.Clamp(_curPitch, -MaxPitch, MaxPitch);

        // camHolder porte la mainCam
        camHolder.localRotation = Quaternion.Euler(_curPitch, 0, 0);
    }

    private void HandleHeadbob()
    {
        Vector3 horizontalVel = new Vector3(_rb.linearVelocity.x, 0, _rb.linearVelocity.z);

        if (_grounded && !_jumping && horizontalVel.sqrMagnitude > 0.01f)
        {
            _mainCam.localPosition = new Vector3(0, headbobCurve.Evaluate(_headbobTimer / headbobTime) * headbobAmp, 0);

            _headbobTimer += Time.deltaTime;
            if (_headbobTimer > headbobTime)
            {
                _headbobTimer = 0;
            }
        }
        else
        {
            // À l'arrêt, la caméra revient doucement à sa position de repos
            _headbobTimer = 0;
            _mainCam.localPosition = Vector3.Lerp(_mainCam.localPosition, Vector3.zero, headbobReturnSpeed * Time.deltaTime);
        }
    }

    private void HandleEyeStabilization()
    {
        if (!eyeStabilization)
        {
            _mainCam.localRotation = Quaternion.identity;
            return;
        }

        // Point de focus : là où regarde le camHolder
        float focusPointDist = FocusMaxDist;
        Vector3 focusPoint = camHolder.position + camHolder.forward * FocusMaxDist;
        if (Physics.Raycast(camHolder.position, camHolder.forward, out RaycastHit hitInf, FocusMaxDist, _focusMask))
        {
            focusPoint = hitInf.point;
            focusPointDist = hitInf.distance;
        }

        Vector3 focusHeadbobVec = focusPoint - _mainCam.position;
        if (focusHeadbobVec.sqrMagnitude > 0.0001f)
        {
            _mainCam.rotation = Quaternion.LookRotation(focusHeadbobVec, Vector3.up);
        }

        // Debug
        Debug.DrawRay(camHolder.position, camHolder.forward * focusPointDist, Color.blue);
        Debug.DrawRay(_mainCam.position, focusHeadbobVec, Color.yellow);
    }

    private void ApplyAirControl()
    {
        _rb.linearVelocity += _moveVector * (airMoveSpeed * Time.fixedDeltaTime);

        // On borne la vitesse horizontale pour qu'elle ne grimpe pas à l'infini en l'air
        Vector3 v = _rb.linearVelocity;
        Vector3 horizontal = Vector3.ClampMagnitude(new Vector3(v.x, 0, v.z), moveSpeed + runSpeed);
        _rb.linearVelocity = new Vector3(horizontal.x, v.y, horizontal.z);
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        _jumpRequested = true;

        // Plus l'appui est long, plus le saut est fort (entre min et max)
        float charge = Mathf.Clamp01((float)context.duration / maxJumpTime);
        _pendingJumpForce = Mathf.Lerp(minJumpForce, maxJumpForce, charge);
    }

    private IEnumerator GrowCoroutine(Transform seed)
    {
        for (float s = 0.1f; s < maxSeedSize; s += Time.deltaTime)
        {
            if (seed == null) yield break;

            seed.localScale = new Vector3(s, s, s);

            yield return null;
        }
    }
}
