using UnityEngine;
using UnityEngine.InputSystem;

public class CamaraController : MonoBehaviour
{
    public Transform target;

    public float distance = 6f;
    public float height = 2f;
    public float rotationSpeed = 5f;

    private float currentYaw = 0f;
    private bool isDraggind = false; 

    void Update()
    {
        if(Mouse.current.leftButton.wasPressedThisFrame)
        {
            isDraggind = true;
        }

        if(Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isDraggind = false;
        }

        if(isDraggind) 
        {
            float mauseX = Mouse.current.delta.ReadValue().x;
            currentYaw += mauseX * rotationSpeed * Time.deltaTime;
        }
    }

    void LateUpdate()
    {
        if(target == null) return;

        Quaternion rotation = Quaternion.Euler(0f, currentYaw, 0f);
        Vector3 offset = new Vector3(0, height, -distance);

        transform.position = target.position + rotation * offset;
        transform.rotation = Quaternion.LookRotation(target.position - transform.position);
    }
}
