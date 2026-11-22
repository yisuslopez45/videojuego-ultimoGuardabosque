using UnityEngine;
using UnityEngine.InputSystem;

public class CamaraController : MonoBehaviour
{
    public Transform target;

    [Header("Distancia y encuadre")]
    public float distance = 6f;
    //Punto al que mira la camara respecto al centro del jugador (altura de los hombros)
    public Vector3 targetOffset = new Vector3(0f, 0.5f, 0f);

    [Header("Raton")]
    //Grados que gira la camara por cada pixel que se mueve el raton
    public float mouseSensitivity = 0.15f;
    public bool invertY = false;
    public float minPitch = -30f;
    public float maxPitch = 60f;

    [Header("Colision")]
    public float collisionRadius = 0.25f;
    public float collisionOffset = 0.2f;
    public float minDistance = 0.5f;
    public LayerMask collisionMask = ~0;

    private float yaw;
    private float pitch = 18f;

    //Buffer reutilizable para la colision (evita crear un array nuevo cada frame)
    private readonly RaycastHit[] hitBuffer = new RaycastHit[8];

    void Start()
    {
        yaw = transform.eulerAngles.y;
        LockCursor(true);
    }

    void Update()
    {
        //Esc libera el cursor, clic izquierdo lo vuelve a bloquear
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            LockCursor(false);
        }
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            LockCursor(true);
        }

        if (Mouse.current == null || Cursor.lockState != CursorLockMode.Locked) return;

        //El delta del raton ya es el desplazamiento de este frame, no se multiplica por deltaTime
        Vector2 delta = Mouse.current.delta.ReadValue();
        yaw += delta.x * mouseSensitivity;
        pitch += (invertY ? delta.y : -delta.y) * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 pivot = target.position + targetOffset;
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 direction = rotation * Vector3.back;

        //Si algo se interpone entre el jugador y la camara, la camara se acerca
        float currentDistance = distance;
        int count = Physics.SphereCastNonAlloc(pivot, collisionRadius, direction, hitBuffer, distance, collisionMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hitBuffer[i];
            if (hit.transform.IsChildOf(target)) continue;
            currentDistance = Mathf.Min(currentDistance, Mathf.Max(hit.distance - collisionOffset, minDistance));
        }

        transform.position = pivot + direction * currentDistance;
        transform.rotation = rotation;
    }

    void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
