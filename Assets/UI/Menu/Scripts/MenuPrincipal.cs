using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controla el menú principal: botones Jugar y Salir.
/// </summary>
public class MenuPrincipal : MonoBehaviour
{
    [Tooltip("Nombre de la escena del juego (debe estar en Build Settings).")]
    [SerializeField] private string escenaJuego = "SampleScene";

    private void Start()
    {
        // Al volver desde el juego el cursor puede venir bloqueado.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;
    }

    public void Jugar()
    {
        SceneManager.LoadScene(escenaJuego);
    }

    public void Salir()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
