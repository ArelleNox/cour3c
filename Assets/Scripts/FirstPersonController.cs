using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Move parameters")]
    [SerializeField] private float moveSpeed = 10;
    [SerializeField] private float airMoveSpeed = 5;
    [SerializeField, Tooltip("Additional speed, total run speed is moveSpeed + runSpeed.")]
    private float runSpeed = 2;
    [SerializeField] private AnimationCurve runCurve;
    [Header("Mouse look parameters")]
    [SerializeField] private float rotationSpeed = 10;
    [SerializeField] private float additionalGravity = 10;
    [SerializeField] private float jumpForce = 10;
    [SerializeField] private float bouncingPadMultiplier = 10;
    [SerializeField] private float sensitivity = 100;
    [SerializeField] private Transform originTsfm;
    [SerializeField] private LayerMask bouncingPadLayer;
    [Header("Headbob parameters")]
    [SerializeField] private AnimationCurve headbobCurve;
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
    private bool grounded;
    private bool onBouncingPad;
    private float runTimer;
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
            else
            {
                jumping = false;
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
        //mainCam.transform.localPosition = new Vector3(0, headHeight + Mathf.Sin(headbobTimer * headbobFreq * rb.linearVelocity.magnitude) * headbobAmp, 0);
        if (rb.linearVelocity.magnitude > 0.01f)
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

        Quaternion limit = Quaternion.identity * upperLimit;
        float mouseMovement = rotationSpeed * (mouseDelta.y / Screen.height) * Time.deltaTime;
        if (mouseMovement > 0) // Up movement
        {
            limit = Quaternion.identity * upperLimit;

            Debug.Log(limit.eulerAngles.x);
        }
        else if (mouseMovement < 0) // Down movement
        {
            limit = Quaternion.identity * bottomLimit;

            Debug.Log(limit.eulerAngles.x);
        }

        if (Mathf.Abs(mouseMovement) < Quaternion.Angle(mainCam.transform.localRotation * Quaternion.Euler(mouseMovement, 0, 0), limit))
        {
            mainCam.transform.localRotation *= Quaternion.Euler(mouseMovement, 0, 0);
        }
        else
        {
            mainCam.transform.localRotation = limit;
        }
    }

    private void FixedUpdate()
    {
        //todo rb.MoveRotation();

        if (jump)
        {
            if (!jumping)
            {
                if (grounded)
                {
                    rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
                }
                else if (onBouncingPad)
                {
                    rb.AddForce(transform.up * jumpForce * bouncingPadMultiplier, ForceMode.Impulse);
                }

                jumping = true;
                jump = false;
            }
        }
        else if (!jumping)
        {
            if (grounded)
            {
                rb.linearVelocity = (moveX + moveY).normalized * curSpeed * Time.deltaTime;

                moveX = Vector3.zero;
                moveY = Vector3.zero;
            }
            else
            {
                rb.linearVelocity += (moveX + moveY).normalized * airMoveSpeed * Time.deltaTime;
                //rb.AddForce((moveX + moveY).normalized * airMoveSpeed * Time.deltaTime, ForceMode.Force);

                rb.AddForce(Vector3.down * additionalGravity, ForceMode.Force);
            }
        }
    }
}
