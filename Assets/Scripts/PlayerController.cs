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

    Transform cameraTransform;    

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
        animator.SetFloat("Speed", animSpeed, speedDampTime, Time.deltaTime);

        //Indica si el personaje(modelo) esta en el suelo
        animator.SetBool("IsGrounded", isGrounded);

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
            animator.SetTrigger("Jump");
        }

        verticalVelocity += gravity * Time.deltaTime;
        
        //Nos indica si el personaje esta cayendo
        animator.SetFloat("VerticalVelocity", verticalVelocity);

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
        Debug.Log("OnMove Llamado" + moveInput);
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
        animator.SetBool("IsCrouching", crouch);

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
        foreach (var hit in Physics.OverlapSphere(top, radius * 0.9f, ~0, QueryTriggerInteraction.Ignore))
        {
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
        Vector3 respawnPoint = spawnPoint.position + Vector3.up * respawnHeightOffset;
        transform.position = respawnPoint;
        verticalVelocity = 0f;
        controller.enabled = true;
    }
}
