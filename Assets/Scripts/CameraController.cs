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

    [Header("Apuntar (clic derecho con un arma)")]
    public float aimDistance = 2.2f;
    //Desplazamiento lateral para mirar por encima del hombro derecho
    public Vector3 aimShoulderOffset = new Vector3(0.6f, 0.15f, 0f);
    public float aimBlendSpeed = 8f;

    [Header("Colision")]
    public float collisionRadius = 0.25f;
    public float collisionOffset = 0.2f;
    public float minDistance = 0.5f;
    public LayerMask collisionMask = ~0;

    private float yaw;
    private float pitch = 18f;

    //Modo apuntar: lo activa WeaponShooter
    public bool IsAiming { get; set; }
    public float AimFov { get; set; } = 45f;
    public float Yaw => yaw + recoilYaw;
    public float Pitch => pitch - recoilPitch;
    private float aimBlend;
    private float normalFov;
    private Camera cam;

    [Header("Mira telescopica")]
    [Tooltip("Posicion del ojo respecto al hueso de la cabeza al mirar por el catalejo")]
    public Vector3 scopeEyeOffset = new Vector3(0f, 0.06f, 0.12f);

    //Modo catalejo: lo activa WeaponShooter. La camara se pone en el ojo del jugador con mucho zoom
    public bool IsScoped { get; set; }
    public float ScopeFov { get; set; } = 10f;
    public Transform ScopeEye { get; set; }
    //Balanceo del pulso en grados (x = horizontal, y = vertical)
    public Vector2 ScopeSway { get; set; }
    public float NormalFov => normalFov;

    [Header("Retroceso")]
    [Tooltip("Velocidad con la que la camara vuelve a su sitio tras el retroceso")]
    public float recoilRecovery = 8f;
    private float recoilPitch;
    private float recoilYaw;

    //Retroceso del arma: sube la camara y la desvia un poco hacia los lados; luego se recupera sola
    public void AddRecoil(float degrees)
    {
        recoilPitch += degrees;
        recoilYaw += Random.Range(-degrees, degrees) * 0.3f;
    }

    //Mientras esta activo (por ejemplo con el inventario abierto) la camara no gira ni toca el cursor.
    //Se recuerda el frame en que se desbloquea para que el Esc que cierra el inventario
    //no libere tambien el cursor en ese mismo frame.
    private bool inputBlocked;
    private int unblockedFrame = -1;
    public bool InputBlocked
    {
        get => inputBlocked;
        set
        {
            if (inputBlocked && !value) unblockedFrame = Time.frameCount;
            inputBlocked = value;
        }
    }

    //Buffer reutilizable para la colision (evita crear un array nuevo cada frame)
    private readonly RaycastHit[] hitBuffer = new RaycastHit[8];

    void Start()
    {
        yaw = transform.eulerAngles.y;
        cam = GetComponent<Camera>();
        normalFov = cam.fieldOfView;
        LockCursor(true);
    }

    void Update()
    {
        if (inputBlocked || Time.frameCount == unblockedFrame) return;

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
        //Con zoom la sensibilidad baja en proporcion, para poder apuntar con precision
        Vector2 delta = Mouse.current.delta.ReadValue();
        float sensitivity = mouseSensitivity * (cam != null && normalFov > 0f ? cam.fieldOfView / normalFov : 1f);
        yaw += delta.x * sensitivity;
        pitch += (invertY ? delta.y : -delta.y) * sensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    void LateUpdate()
    {
        if (target == null) return;

        //Transicion suave entre la camara normal y la de apuntar
        aimBlend = Mathf.MoveTowards(aimBlend, IsAiming ? 1f : 0f, aimBlendSpeed * Time.deltaTime);
        float blend = Mathf.SmoothStep(0f, 1f, aimBlend);
        cam.fieldOfView = Mathf.Lerp(normalFov, AimFov, blend);

        //El retroceso se recupera poco a poco
        float decay = 1f - Mathf.Exp(-recoilRecovery * Time.deltaTime);
        recoilPitch = Mathf.Lerp(recoilPitch, 0f, decay);
        recoilYaw = Mathf.Lerp(recoilYaw, 0f, decay);

        //Catalejo: la camara mira desde el ojo del jugador (el modelo se oculta) y sin colisiones
        if (IsScoped && ScopeEye != null)
        {
            aimBlend = 1f;
            cam.fieldOfView = ScopeFov;
            transform.rotation = Quaternion.Euler(Pitch + ScopeSway.y, Yaw + ScopeSway.x, 0f);
            transform.position = ScopeEye.position + Quaternion.Euler(0f, Yaw, 0f) * scopeEyeOffset;
            return;
        }

        Quaternion rotation = Quaternion.Euler(Pitch, Yaw, 0f);
        Vector3 shoulder = Quaternion.Euler(0f, Yaw, 0f) * aimShoulderOffset * blend;
        Vector3 pivot = target.position + targetOffset + shoulder;
        Vector3 direction = rotation * Vector3.back;

        //Con mucho zoom (FOV pequeño) la camara se aleja en proporcion para que el personaje no tape la vista
        float zoomCompensation = Mathf.Tan(normalFov * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(Mathf.Max(AimFov, 5f) * 0.5f * Mathf.Deg2Rad);
        float wantedDistance = Mathf.Lerp(distance, aimDistance * zoomCompensation, blend);

        //Si algo se interpone entre el jugador y la camara, la camara se acerca
        float currentDistance = wantedDistance;
        int count = Physics.SphereCastNonAlloc(pivot, collisionRadius, direction, hitBuffer, wantedDistance, collisionMask, QueryTriggerInteraction.Ignore);
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
