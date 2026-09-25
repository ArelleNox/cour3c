using UnityEngine;

public class PlayerController : MonoBehaviour
{
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

    private bool invertYAxis = false;
    
    private Camera mainCam;
    private Rigidbody rb;
    private CapsuleCollider coll;

    private float curSpeed;
    private bool jump;
    private bool jumping;
    private Vector3 moveX;
    private Vector3 moveY;
    private float curPitch;
    private bool grounded;
    private bool onBouncingPad;
    private float runTimer;
    private float jumpTimer;
    private float headbobTimer;
    private float headHeight;
    private Quaternion upperLimit;
    private Quaternion bottomLimit;


    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        coll = GetComponent<CapsuleCollider>();
        mainCam = GetComponentInChildren<Camera>();

        curSpeed = moveSpeed;
        moveX = Vector3.zero;
        moveY = Vector3.zero;
        jump = false;
        jumping = false;
        onBouncingPad = false;
        runTimer = 0;
        jumpTimer = 0;
        curPitch = 0;
        headbobTimer = 0;
        headHeight = mainCam.transform.localPosition.y;
        upperLimit = Quaternion.Euler(-80, 0, 0);
        bottomLimit = Quaternion.Euler(80, 0, 0);
    }

    // Update is called once per frame
    void Update()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = true;

        grounded = false;
        if (Physics.SphereCast(originTsfm.position, coll.radius, -transform.up, out RaycastHit hit, 2f, LayerMask.GetMask("Ground", "BouncingPad")))
        {
            if (hit.distance <= (coll.height * 0.5f) - coll.radius + 0.1f)
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
        }
        
        if (Input.GetKey(KeyCode.W))
        {
            moveX = transform.forward;
        }
        else if (Input.GetKey(KeyCode.S))
        {
            moveX = -transform.forward;
        }
        
        if (Input.GetKey(KeyCode.A))
        {
            moveY = -transform.right;
        }
        else if (Input.GetKey(KeyCode.D))
        {
            moveY = transform.right;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            jump = true;
        }

        // Run
        if (Input.GetKey(KeyCode.LeftShift))
        {
            //curSpeed = moveSpeed * Mathf.Lerp(1, runFactor, Mathf.Pow(runTimer / 1.5f, 4));
            //curSpeed = Mathf.Lerp(moveSpeed, moveSpeed * runCurve.Evaluate(runTimer / 1.5f) * runFactor, runTimer / 1.5f);
            curSpeed = moveSpeed + runCurve.Evaluate(runTimer / 1.5f) * runSpeed;

            runTimer += Time.deltaTime;
        }
        else
        {
            curSpeed = moveSpeed;
            runTimer = 0;
        }

        // Headbob
        if (Physics.Raycast(camHolder.position, mainCam.transform.forward, out RaycastHit hitInf, 30))
        {
            Vector3 focusPoint = hitInf.point;
            float focusPointDist = hitInf.distance;
        }

        //mainCam.transform.localPosition = new Vector3(0, headHeight + Mathf.Sin(headbobTimer * headbobFreq * rb.linearVelocity.magnitude) * headbobAmp, 0);
        if (rb.linearVelocity.magnitude > 0.01f && !jumping)
        {
            mainCam.transform.localPosition = new Vector3(0, headHeight + headbobCurve.Evaluate(headbobTimer / headbobTime) * headbobAmp, 0);
            headbobTimer += Time.deltaTime;
            if (headbobTimer > headbobTime)
            {
                headbobTimer = 0;
            }
        }

        // Mouse look
        Vector3 mouseDelta = Input.mousePositionDelta * sensitivity;
        transform.rotation *= Quaternion.Euler(0, rotationSpeed * (mouseDelta.x / Screen.width) * Time.deltaTime, 0);

        if (!invertYAxis)
            mouseDelta.y = -mouseDelta.y;

        float mouseMovement = rotationSpeed * (mouseDelta.y / Screen.height) * Time.deltaTime;

        curPitch += mouseMovement;
        curPitch = Mathf.Clamp(curPitch, -90, 90);

        mainCam.transform.localRotation = Quaternion.Euler(curPitch, 0, 0);
    }

    private void FixedUpdate()
    {
        //rb.MoveRotation();

        if (jump)
        {
            if (!jumping)
            {
                if (grounded)
                {
                    //rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);

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
                rb.linearVelocity = (moveX + moveY).normalized * curSpeed * Time.deltaTime;

                moveX = Vector3.zero;
                moveY = Vector3.zero;
            }
            else // Jump descent phaze
            {
                rb.linearVelocity += (moveX + moveY).normalized * airMoveSpeed * Time.deltaTime;
                //rb.AddForce((moveX + moveY).normalized * airMoveSpeed * Time.deltaTime, ForceMode.Force);

                rb.AddForce(Vector3.down * additionalGravity, ForceMode.Force);
            }
        }
    }
}
