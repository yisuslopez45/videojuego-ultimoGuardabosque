using UnityEngine;

public enum ItemType
{
    Arma,
    Accesorio
}

//Parte del cuerpo donde se coloca el objeto
public enum EquipSlot
{
    ManoDerecha,
    Cabeza,
    Espalda,
    Pecho,
    Cintura
}

//Datos de un objeto equipable (arma o accesorio).
//Para añadir uno nuevo: clic derecho en Project > Create > Guardabosque > Objeto equipable
[CreateAssetMenu(menuName = "Guardabosque/Objeto equipable", fileName = "NuevoObjeto")]
public class ItemData : ScriptableObject
{
    public string displayName;
    public ItemType type;
    public EquipSlot slot;
    public Sprite icon;
    public GameObject prefab;

    [Header("Colocacion respecto al hueso del slot")]
    public Vector3 positionOffset;
    public Vector3 rotationOffset;

    [Header("Solo armas")]
    public bool twoHanded;
    [Range(0.3f, 1f)]
    public float moveSpeedMultiplier = 1f;

    [Header("Disparo")]
    public float damage = 25f;
    public float range = 80f;
    [Tooltip("Disparos por segundo")]
    public float fireRate = 2f;
    public int magazineSize = 6;
    [Tooltip("Municion de reserva al empezar")]
    public int startingReserve = 30;
    public float reloadTime = 1.5f;
    [Tooltip("Dispersion en grados sin apuntar / apuntando")]
    public float hipSpread = 4f;
    public float aimSpread = 0.5f;
    [Tooltip("Fuerza aplicada a los objetos con Rigidbody")]
    public float impactForce = 8f;
    [Tooltip("Radio de explosion (0 = sin explosion)")]
    public float explosionRadius = 0f;
    [Tooltip("Campo de vision de la camara al apuntar")]
    public float aimFov = 45f;
    [Tooltip("Retroceso: grados que sube la camara en cada disparo")]
    public float recoil = 2f;

    [Header("Animaciones (solo armas)")]
    public AnimationClip animReposo;
    public AnimationClip animCaminar;
    public AnimationClip animApuntar;
    [Tooltip("El disparo ocurre poco despues del inicio del clip (fotograma 6-8)")]
    public AnimationClip animDisparar;
    [Tooltip("Se acelera o frena para que dure exactamente reloadTime")]
    public AnimationClip animRecargar;
    [Tooltip("Giro del torso (hueso Spine) respecto al personaje en la pose de apuntar. " +
             "Se calcula con el menu Guardabosque > Calcular torso de apuntado de las armas")]
    public Vector3 aimChestRotation;
}
