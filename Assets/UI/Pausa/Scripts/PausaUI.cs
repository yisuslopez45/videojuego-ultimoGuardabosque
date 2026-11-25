using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

//Menu de pausa: Esc detiene el juego y muestra Continuar, Menu principal y Salir
[RequireComponent(typeof(UIDocument))]
public class PausaUI : MonoBehaviour
{
    public playerController player;
    public CamaraController cameraController;
    public InventoryUI inventory;
    public WeaponWheelUI weaponWheel;

    [Tooltip("Nombre de la escena del menu principal (debe estar en Build Settings).")]
    public string escenaMenu = "Menu";

    //Lo consultan otros scripts (por ejemplo el inventario) para no reaccionar mientras hay pausa
    public static bool IsPaused { get; private set; }

    private VisualElement overlay;
    private Button continuarButton;
    private Button menuButton;
    private Button salirButton;

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        overlay = root.Q<VisualElement>("overlay");
        continuarButton = root.Q<Button>("btn-continuar");
        menuButton = root.Q<Button>("btn-menu");
        salirButton = root.Q<Button>("btn-salir");

        continuarButton.clicked += Continuar;
        menuButton.clicked += IrAlMenu;
        salirButton.clicked += Salir;
        overlay.style.display = DisplayStyle.None;
    }

    void OnDisable()
    {
        if (continuarButton != null) continuarButton.clicked -= Continuar;
        if (menuButton != null) menuButton.clicked -= IrAlMenu;
        if (salirButton != null) salirButton.clicked -= Salir;
    }

    void OnDestroy()
    {
        //Si se cambia de escena estando en pausa, el tiempo no debe quedarse congelado
        if (IsPaused) SetPaused(false);
    }

    void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;

        //El Esc que cierra el inventario no debe abrir tambien la pausa
        //El Esc que cierra la rueda de armas tampoco
        if (inventory != null && (inventory.IsOpen || inventory.ClosedFrame == Time.frameCount)) return;
        if (weaponWheel != null && (weaponWheel.IsOpen || weaponWheel.ClosedFrame == Time.frameCount)) return;

        if (IsPaused) Continuar();
        else Pausar();
    }

    public void Pausar()
    {
        if (overlay == null) return;
        SetPaused(true);
        overlay.style.display = DisplayStyle.Flex;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        continuarButton.Focus();
    }

    public void Continuar()
    {
        if (overlay == null) return;
        SetPaused(false);
        overlay.style.display = DisplayStyle.None;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void IrAlMenu()
    {
        SetPaused(false);
        SceneManager.LoadScene(escenaMenu);
    }

    public void Salir()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void SetPaused(bool paused)
    {
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;
        if (player != null) player.InputBlocked = paused;
        if (cameraController != null) cameraController.InputBlocked = paused;
    }
}
