using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("General")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Move parameters")]
    [SerializeField] private float moveSpeed = 10;
    [SerializeField] private float airMoveSpeed = 5;
    [SerializeField, Tooltip("Additional speed, total run speed is moveSpeed + runSpeed.")]
    private float runSpeed = 2;
    [SerializeField] private AnimationCurve runCurve;
    [SerializeField] private AnimationCurve jumpCurve;
    [SerializeField] private float additionalGravity = 10;
    [SerializeField] private float jumpForce = 10;
    [SerializeField] private float jumpDuration = 1.5f;
    [SerializeField] private float bouncingPadMultiplier = 10;

    [Header("Mouse look parameters")]
    [SerializeField] private float rotationSpeed = 10;
    [SerializeField] private float sensitivity = 100;
    [SerializeField] private Transform originTsfm;
    [SerializeField] private LayerMask bouncingPadLayer;
    
    [Header("Headbob parameters")]
    [SerializeField] private AnimationCurve headbobCurve;
    [SerializeField] private Transform camHolder;
    [SerializeField] private float headbobAmp = 0.05f;
    [SerializeField] private float headbobFreq = 3f;
    [SerializeField] private float headbobTime = 0.5f;
    [SerializeField] private bool eyeStabilization = true;

    private bool invertYAxis = false;
    
    private Transform mainCam;
    private Rigidbody rb;
    private CapsuleCollider coll;
    private float headHeight;
    private float curSpeed;
    private bool jump;
    private bool jumping;
    private Vector3 moveVector;
    private float curPitch;
    private Quaternion yRotation;
    private bool grounded;
    private bool onBouncingPad;
    private float runTimer;
    private float jumpTimer;
    private float headbobTimer;

    private InputActionMap actionMap;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction runAction;
    private InputAction jumpAction;


    void Awake()
    {
        actionMap = inputActions.FindActionMap("FirstPerson");
        actionMap.Enable();

        moveAction = actionMap.FindAction("Move");
        lookAction = actionMap.FindAction("Look");
        runAction = actionMap.FindAction("Run");
        jumpAction = actionMap.FindAction("Jump");

        rb = GetComponent<Rigidbody>();
        coll = GetComponent<CapsuleCollider>();
        mainCam = GetComponentInChildren<Camera>().transform;

        curSpeed = moveSpeed;
        moveVector = Vector3.zero;
        yRotation = Quaternion.identity;
        headHeight = mainCam.transform.position.y;
        jump = false;
        jumping = false;
        onBouncingPad = false;
        runTimer = 0;
        jumpTimer = 0;
        curPitch = 0;
        headbobTimer = 0;
    }

    // Update is called once per frame
    void Update()
    {
        // Hide & lock mouse cursor inside the screen
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = true;

        // Set grounded or not state
        grounded = false;
        if (Physics.SphereCast(originTsfm.position, coll.radius, -transform.up, out RaycastHit hit, (coll.height * 0.5f) - coll.radius + 0.1f, LayerMask.GetMask("Ground", "BouncingPad")))
        {
            if (hit.transform.gameObject.layer == LayerMask.NameToLayer("BouncingPad"))
            {
                onBouncingPad = true;
            }
            else
            {
                grounded = true;
            }
        }

        // Set movement vector from input to be used in fixedupdate
        Vector2 v = moveAction.ReadValue<Vector2>();
        moveVector = transform.forward * v.y + transform.right * v.x;

        // Set jump boolean to be used in fixedupdate
        if (Input.GetKeyDown(KeyCode.Space))
        {
            jump = true;
        }

        // Increase current speed if running
        if (Input.GetKey(KeyCode.LeftShift))
        {
            curSpeed = moveSpeed + runCurve.Evaluate(runTimer / 1.5f) * runSpeed;

            runTimer += Time.deltaTime;
        }
        else
        {
            curSpeed = moveSpeed;
            runTimer = 0;
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
        Debug.DrawRay(mainCam.position, forwardHeadBobVec * focusPointDist, Color.red);

        // Vector of the headbob looking toward the focus point (what we're trying to achieve)
        Vector3 focusHeadbobVec = focusPoint - mainCam.position;
        
        // Eye vector debug (where the eye should focus theoretically)
        Debug.DrawRay(camHolder.position, camHolder.forward * focusPointDist, Color.blue);

        // Point the mainCam toward the focusPoint
        if (eyeStabilization)
        {
            mainCam.rotation = Quaternion.LookRotation(focusHeadbobVec, Vector3.up);
            Debug.DrawRay(mainCam.position, focusHeadbobVec, Color.orange);
        }

        // Headbob (the mainCam inside the camHolder)
        if (rb.linearVelocity.magnitude > 0.01f && !jumping)
        {
            // Headbob position is set in world space to keep it straight
            mainCam.position = new Vector3(mainCam.position.x, headHeight + headbobCurve.Evaluate(headbobTimer / headbobTime) * headbobAmp, mainCam.position.z);
            
            headbobTimer += Time.deltaTime;
            if (headbobTimer > headbobTime)
            {
                headbobTimer = 0;
            }
        }

        // Mouse look
        Vector3 mouseDelta = lookAction.ReadValue<Vector2>() * sensitivity;
        yRotation *= Quaternion.Euler(0, rotationSpeed * (mouseDelta.x / Screen.width) * Time.deltaTime, 0);

        if (!invertYAxis)
            mouseDelta.y = -mouseDelta.y;

        float mouseMovement = rotationSpeed * (mouseDelta.y / Screen.height) * Time.deltaTime;
        
        // Limit upper and lower orientation of the camera
        curPitch += mouseMovement;
        curPitch = Mathf.Clamp(curPitch, -90, 90);

        // camHolder holds the mainCam
        camHolder.localRotation = Quaternion.Euler(curPitch, 0, 0);
    }

    private void FixedUpdate()
    {
        rb.MoveRotation(yRotation);

        if (jump)
        {
            if (!jumping)
            {
                if (grounded)
                {
                    jumpTimer = 0;
                }
                else if (onBouncingPad)
                {
                    rb.AddForce(transform.up * jumpForce * bouncingPadMultiplier, ForceMode.Impulse);
                }

                jumping = true;
                jump = false;
            }
        }
        else if (jumping) // Jump impulsion phaze
        {
            float jumpVel = jumpCurve.Evaluate(jumpTimer / jumpDuration) * jumpForce * Time.deltaTime;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpVel, rb.linearVelocity.z);
            
            jumpTimer += Time.deltaTime;
            if (jumpTimer > jumpDuration)
            {
                jumpTimer = 0;
                jumping = false;
            }
        }
        else if (!jumping)
        {
            if (grounded) // Regular movement
            {
                rb.linearVelocity = moveVector.normalized * curSpeed * Time.deltaTime;

            }
            else // Jump descent phaze
            {
                rb.linearVelocity += moveVector.normalized * airMoveSpeed * Time.deltaTime;

                rb.AddForce(Vector3.down * additionalGravity, ForceMode.Force);
            }
        }
    }
}
