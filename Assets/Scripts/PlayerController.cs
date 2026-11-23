using UnityEngine;
using UnityEngine.InputSystem;

public class playerController : MonoBehaviour
{
    public float speed = 5f;
    public float runSpeed = 9f;
    public float crouchSpeed = 2.5f;
    public float crouchHeight = 1.3f;
    public float speedDampTime = 0.1f;
    public float gravity = -20f;
    public float jumpHeight = 1.5f;
    public float respawnHeightOffset = 1.5f;
    public float rotationSpeed = 10f;    
    public Transform spawnPoint;

    private float verticalVelocity;
    private bool jumpPressed;   
    private CharacterController controller;
    private Vector2 moveInput;
    private InputAction runAction;
    private InputAction crouchAction;
    private Animator animator;
    private bool isCrouching;
    private bool isRunning;
    private float standHeight;
    private Vector3 standCenter;
    private Vector3 startPosition;

    Transform cameraTransform;

    //IDs de los parametros del Animator (evita buscar el nombre en cada frame)
    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    static readonly int VerticalVelocityHash = Animator.StringToHash("VerticalVelocity");
    static readonly int JumpHash = Animator.StringToHash("Jump");
    static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");

    //Buffer reutilizable para la comprobacion del techo (sin generar basura)
    private readonly Collider[] overlapBuffer = new Collider[8];

    void Start()
    {
        controller = GetComponent<CharacterController>();
        cameraTransform = Camera.main.transform; 
        animator = GetComponentInChildren<Animator>();
        runAction = GetComponent<PlayerInput>().actions["Run"];
        crouchAction = GetComponent<PlayerInput>().actions["Crouch"];

        //Medidas de la capsula de pie, para restaurarlas al levantarse
        standHeight = controller.height;
        standCenter = controller.center;

        //Punto de reaparicion por defecto si la escena no asigna spawnPoint
        startPosition = transform.position;
    }

    // Update is called once per frame
    void Update()
    {

        bool isGrounded = controller.isGrounded;

        //Agachado solo mientras Control esta presionado
        //Si hay un techo encima, se mantiene agachado aunque se suelte la tecla
        bool wantsCrouch = crouchAction.IsPressed();
        if (wantsCrouch != isCrouching && (wantsCrouch || CanStandUp()))
        {
            SetCrouch(wantsCrouch);
        }

        //Corre solo mientras Shift esta presionado, hay movimiento y no esta agachado
        //Solo se decide en el suelo: en el aire se conserva el estado del despegue
        if (isGrounded)
        {
            isRunning = !isCrouching && runAction.IsPressed() && moveInput.sqrMagnitude > 0.01f;
        }
        float currentSpeed = isCrouching ? crouchSpeed : (isRunning ? runSpeed : speed);

        //Blend Tree: 0 = reposo, 1 = caminar, 2 = correr
        float animSpeed = moveInput.magnitude * (isRunning ? 2f : 1f);
        animator.SetFloat(SpeedHash, animSpeed, speedDampTime, Time.deltaTime);

        //Indica si el personaje(modelo) esta en el suelo
        animator.SetBool(IsGroundedHash, isGrounded);

        if(isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }

        //No se puede saltar estando agachado
        if(jumpPressed && isCrouching)
        {
            jumpPressed = false;
        }

        if(jumpPressed && isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpPressed = false;

            //Indica al animator que estamos saltando
            animator.SetTrigger(JumpHash);
        }

        verticalVelocity += gravity * Time.deltaTime;
        
        //Nos indica si el personaje esta cayendo
        animator.SetFloat(VerticalVelocityHash, verticalVelocity);

        //Movimiento relativo a la camara
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        //Inhabilitar inclincion de la camara
        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = forward * moveInput.y + right * moveInput.x;

        //Rotacion del player
        if(moveDirection.magnitude > 0.1f){
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        Vector3 move = moveDirection.normalized;
        move.y = verticalVelocity / currentSpeed;

        controller.Move(move * currentSpeed * Time.deltaTime);
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if(value.isPressed)
        {
            jumpPressed = true;
        }
    }

    void SetCrouch(bool crouch)
    {
        isCrouching = crouch;
        animator.SetBool(IsCrouchingHash, crouch);

        //Reduce la capsula manteniendo los pies en el mismo sitio
        float height = crouch ? crouchHeight : standHeight;
        float bottom = standCenter.y - standHeight / 2f;
        controller.height = height;
        controller.center = new Vector3(standCenter.x, bottom + height / 2f, standCenter.z);
    }

    bool CanStandUp()
    {
        //Comprueba si hay espacio libre sobre la cabeza para la capsula de pie
        float radius = controller.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
        Vector3 top = transform.TransformPoint(standCenter + Vector3.up * (standHeight / 2f)) - Vector3.up * radius;
        int count = Physics.OverlapSphereNonAlloc(top, radius * 0.9f, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hit = overlapBuffer[i];
            if (hit != controller && !hit.transform.IsChildOf(transform))
            {
                return false;
            }
        }
        return true;
    }

    public void setSpawnPoint(Transform newSpawnPoint)
    {
        spawnPoint = newSpawnPoint;
    }

    public void Die()
    {
        controller.enabled = false;
        Vector3 respawnPoint = spawnPoint != null
            ? spawnPoint.position + Vector3.up * respawnHeightOffset
            : startPosition;
        transform.position = respawnPoint;
        verticalVelocity = 0f;
        controller.enabled = true;
    }
}
