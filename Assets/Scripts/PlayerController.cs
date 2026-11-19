using UnityEngine;
using UnityEngine.InputSystem;

public class playerController : MonoBehaviour
{
    public float speed = 5f;
    public float runSpeed = 9f;
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
    private Animator animator;

    Transform cameraTransform;    

    void Start()
    {
        controller = GetComponent<CharacterController>();
        cameraTransform = Camera.main.transform; 
        animator = GetComponentInChildren<Animator>();
        runAction = GetComponent<PlayerInput>().actions["Run"];
    }

    // Update is called once per frame
    void Update()
    {

        bool isGrounded = controller.isGrounded;
        
        //Corre solo mientras Shift esta presionado y hay movimiento
        bool isRunning = runAction.IsPressed() && moveInput.sqrMagnitude > 0.01f;
        float currentSpeed = isRunning ? runSpeed : speed;

        //Blend Tree: 0 = reposo, 1 = caminar, 2 = correr
        float animSpeed = moveInput.magnitude * (isRunning ? 2f : 1f);
        animator.SetFloat("Speed", animSpeed, speedDampTime, Time.deltaTime);

        //Indica si el personaje(modelo) esta en el suelo
        animator.SetBool("IsGrounded", isGrounded);

        if(isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
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
