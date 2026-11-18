using UnityEngine;
using UnityEngine.InputSystem;

public class playerController : MonoBehaviour
{
    public float speed = 5f;
    public float gravity = -20f;
    public float jumpHeight = 3f;
    public Transform spawnPoint;
    public float respawnHeightOffset = 1.5f;
    public float rotationSpeed = 10f;    

    private CharacterController controller;
    private Vector2 moveInput;
    private float verticalVelocity;
    private bool jumpPressed;   
    private Animator animator;

    Transform cameraTransform;    

    void Start()
    {
        controller = GetComponent<CharacterController>();
        cameraTransform = Camera.main.transform; 
        animator = GetComponentInChildren<Animator>();
    }

    // Update is called once per frame
    void Update()
    {

        bool isGrounded = controller.isGrounded;
        float speedValue = moveInput.magnitude;

        animator.SetFloat("Speed", speedValue);

        if(isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -3f;
        }

        if(jumpPressed && isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -3f * gravity);
            jumpPressed = false;
        }

        verticalVelocity += gravity * Time.deltaTime;

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
        move.y = verticalVelocity / speed;

        controller.Move(move * speed * Time.deltaTime);
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
