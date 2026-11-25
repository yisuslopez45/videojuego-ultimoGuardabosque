using System.Collections.Generic;
using UnityEngine;

//Animaciones con armas: sostener (reposo / caminar), apuntar, disparar y recargar.
//Se reproducen en la capa "Arma" del Animator, que solo mueve brazos, manos y cabeza:
//las piernas siguen con la locomocion normal (andar, correr, agacharse, saltar).
//Cada arma trae sus clips en su ItemData y aqui se cambian en el controller con un AnimatorOverrideController.
//Como la capa no mueve el torso, al apuntar se gira el pecho por codigo para reproducir la postura del clip.
public class WeaponAnimator : MonoBehaviour
{
    public PlayerEquipment equipment;

    [Header("Clips del controller que se sustituyen por los de cada arma")]
    public AnimationClip baseReposo;
    public AnimationClip baseCaminar;
    public AnimationClip baseApuntar;
    public AnimationClip baseDisparar;
    public AnimationClip baseRecargar;

    [Header("Mezcla")]
    [Tooltip("Velocidad con la que los brazos pasan a sostener el arma (o la sueltan)")]
    public float layerBlendSpeed = 6f;
    [Tooltip("Cuanto se inclina el torso para seguir el angulo vertical de la camara al apuntar")]
    [Range(0f, 1f)] public float spinePitchWeight = 0.8f;

    //Angulo vertical de la camara (lo pone WeaponShooter cada frame)
    public float AimPitch { get; set; }

    static readonly int AimingHash = Animator.StringToHash("Apuntando");
    static readonly int FireHash = Animator.StringToHash("Disparar");
    static readonly int ReloadHash = Animator.StringToHash("Recargar");
    static readonly int ReloadSpeedHash = Animator.StringToHash("VelocidadRecarga");
    static readonly int AimTagHash = Animator.StringToHash("Apuntar");
    static readonly int HoldStateHash = Animator.StringToHash("Sostener");

    private Animator animator;
    private AnimatorOverrideController overrides;
    private readonly List<KeyValuePair<AnimationClip, AnimationClip>> overrideList = new List<KeyValuePair<AnimationClip, AnimationClip>>();
    private int layer = -1;
    private float layerTarget;
    //Columna: el avatar solo mapea este hueso del torso; el pecho, el cuello y los brazos cuelgan de el
    private Transform spine;
    private ItemData currentWeapon;
    //Ultima arma con la que se animo: se mantiene mientras la capa se desvanece al guardar el arma
    private ItemData poseWeapon;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        overrides = new AnimatorOverrideController(animator.runtimeAnimatorController);
        animator.runtimeAnimatorController = overrides;
        layer = animator.GetLayerIndex("Arma");
        spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        if (layer < 0) Debug.LogWarning("WeaponAnimator: el Animator no tiene la capa 'Arma'", this);
    }

    void OnEnable()
    {
        if (equipment != null) equipment.OnEquipmentChanged += HandleEquipmentChanged;
    }

    void OnDisable()
    {
        if (equipment != null) equipment.OnEquipmentChanged -= HandleEquipmentChanged;
    }

    void Start()
    {
        HandleEquipmentChanged();
    }

    void HandleEquipmentChanged()
    {
        ItemData weapon = equipment.CurrentWeapon;
        layerTarget = weapon != null ? 1f : 0f;
        if (weapon == currentWeapon) return;
        currentWeapon = weapon;
        if (weapon == null || layer < 0) return;

        //Cambia los clips de la capa por los de la nueva arma
        poseWeapon = weapon;
        overrideList.Clear();
        AddOverride(baseReposo, weapon.animReposo);
        AddOverride(baseCaminar, weapon.animCaminar);
        AddOverride(baseApuntar, weapon.animApuntar);
        AddOverride(baseDisparar, weapon.animDisparar);
        AddOverride(baseRecargar, weapon.animRecargar);
        overrides.ApplyOverrides(overrideList);

        //Empieza la nueva arma desde la pose de sostener (cancela un disparo o una recarga a medias)
        animator.ResetTrigger(FireHash);
        animator.ResetTrigger(ReloadHash);
        animator.Play(HoldStateHash, layer, 0f);
    }

    void AddOverride(AnimationClip original, AnimationClip replacement)
    {
        if (original != null) overrideList.Add(new KeyValuePair<AnimationClip, AnimationClip>(original, replacement != null ? replacement : original));
    }

    void Update()
    {
        if (layer < 0) return;
        float w = Mathf.MoveTowards(animator.GetLayerWeight(layer), layerTarget, layerBlendSpeed * Time.deltaTime);
        animator.SetLayerWeight(layer, w);
    }

    // ---------- Ordenes (las usa WeaponShooter) ----------

    public void SetAiming(bool aiming)
    {
        animator.SetBool(AimingHash, aiming);
    }

    public void PlayFire()
    {
        animator.ResetTrigger(ReloadHash);
        animator.SetTrigger(FireHash);
    }

    //La animacion de recarga se ajusta para durar 'duration' segundos
    public void PlayReload(float duration)
    {
        AnimationClip clip = currentWeapon != null && currentWeapon.animRecargar != null ? currentWeapon.animRecargar : baseRecargar;
        float speed = clip != null ? clip.length / Mathf.Max(duration, 0.05f) : 1f;
        animator.SetFloat(ReloadSpeedHash, speed);
        animator.ResetTrigger(FireHash);
        animator.SetTrigger(ReloadHash);
    }

    // ---------- Torso al apuntar ----------

    //0 = brazos sosteniendo el arma, 1 = en pose de apuntar o disparar (estados con la etiqueta "Apuntar")
    public float AimPoseWeight
    {
        get
        {
            if (layer < 0) return 0f;
            float w = animator.GetCurrentAnimatorStateInfo(layer).tagHash == AimTagHash ? 1f : 0f;
            if (animator.IsInTransition(layer))
            {
                float next = animator.GetNextAnimatorStateInfo(layer).tagHash == AimTagHash ? 1f : 0f;
                w = Mathf.Lerp(w, next, animator.GetAnimatorTransitionInfo(layer).normalizedTime);
            }
            return w * animator.GetLayerWeight(layer);
        }
    }

    //Despues de la animacion: gira el torso como en el clip de apuntar y lo inclina con la camara
    void LateUpdate()
    {
        if (poseWeapon == null || spine == null) return;
        float w = AimPoseWeight;
        if (w <= 0.001f) return;

        //1. Postura del clip: la columna mira donde la tiene el clip (las piernas siguen caminando)
        Quaternion target = animator.transform.rotation * Quaternion.Euler(poseWeapon.aimChestRotation);
        Quaternion delta = target * Quaternion.Inverse(spine.rotation);
        spine.rotation = Quaternion.Slerp(Quaternion.identity, delta, w) * spine.rotation;

        //2. El arma sigue el angulo vertical de la camara
        float pitch = Mathf.Clamp(AimPitch, -45f, 45f) * spinePitchWeight * w;
        spine.rotation = Quaternion.AngleAxis(pitch, transform.right) * spine.rotation;
    }
}
