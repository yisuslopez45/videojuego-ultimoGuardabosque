using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

//Apuntar (clic derecho), disparar (clic izquierdo) y recargar (R) con el arma equipada.
//El disparo es instantaneo (raycast) desde el centro de la camara, para que coincida con la mira;
//visualmente el trazador sale de la boca del arma.
public class WeaponShooter : MonoBehaviour
{
    public PlayerEquipment equipment;
    public WeaponAnimator weaponAnimator;
    public playerController player;
    public CamaraController cameraController;

    [Header("Impactos")]
    public LayerMask hitMask = ~0;

    [Header("Efectos")]
    public GameObject muzzleFlashPrefab;
    public GameObject impactPrefab;
    public GameObject explosionPrefab;
    public Material tracerMaterial;
    public float tracerDuration = 0.06f;

    [Header("Disparo sin apuntar")]
    [Tooltip("Segundos que el jugador sigue mirando al frente tras disparar sin apuntar")]
    public float hipFireLookTime = 0.6f;

    [Header("Mira telescopica")]
    [Tooltip("Cambio de campo de vision por cada paso de la rueda del raton")]
    public float scopeZoomStep = 2.5f;
    [Tooltip("Segundos que se puede contener la respiracion (Shift) y que tarda en recuperarse")]
    public float breathHoldTime = 3f;
    public float breathRecoverTime = 4f;

    //Avisa al HUD de cambios de municion o recarga
    public event Action OnAmmoChanged;

    public bool IsAiming { get; private set; }
    public bool IsReloading { get; private set; }
    public float CurrentSpread { get; private set; }
    //Mirando por el catalejo de un arma de largo alcance (el HUD muestra la mira a pantalla completa)
    public bool IsScoped { get; private set; }
    //Aumentos actuales del catalejo (por ejemplo 6x)
    public float ScopeMagnification { get; private set; }
    //Respiracion disponible (0-1) y si se esta conteniendo
    public float Breath { get; private set; } = 1f;
    public bool HoldingBreath { get; private set; }

    private class AmmoState { public int magazine; public int reserve; }
    private readonly Dictionary<ItemData, AmmoState> ammo = new Dictionary<ItemData, AmmoState>();

    private InputAction aimAction;
    private InputAction attackAction;
    private InputAction reloadAction;
    private InputAction runAction;
    private float aimHeldTime;
    private float scopeFov;
    private bool breathExhausted;
    private Transform head;
    private readonly Dictionary<Renderer, UnityEngine.Rendering.ShadowCastingMode> hiddenRenderers =
        new Dictionary<Renderer, UnityEngine.Rendering.ShadowCastingMode>();
    private float nextFireTime;
    private float lookAheadTimer;
    private Coroutine reloadRoutine;
    private LineRenderer tracer;
    private float tracerTimer;
    private readonly RaycastHit[] hits = new RaycastHit[16];
    private readonly Collider[] overlap = new Collider[32];

    void Start()
    {
        var input = player.GetComponent<PlayerInput>();
        aimAction = input.actions["Aim"];
        attackAction = input.actions["Attack"];
        reloadAction = input.actions["Reload"];
        runAction = input.actions["Run"];
        head = player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
        cameraController.ScopeEye = head;
        equipment.OnEquipmentChanged += HandleEquipmentChanged;
        CreateTracer();
    }

    void OnDestroy()
    {
        if (equipment != null) equipment.OnEquipmentChanged -= HandleEquipmentChanged;
    }

    // ---------- Municion (la usa el HUD) ----------

    public int MagazineAmmo => GetAmmo(equipment.CurrentWeapon)?.magazine ?? 0;
    public int ReserveAmmo => GetAmmo(equipment.CurrentWeapon)?.reserve ?? 0;

    //Municion de cualquier arma (la usa la rueda de armas)
    public void GetAmmoCounts(ItemData weapon, out int magazine, out int reserve)
    {
        AmmoState state = GetAmmo(weapon);
        magazine = state?.magazine ?? 0;
        reserve = state?.reserve ?? 0;
    }

    AmmoState GetAmmo(ItemData weapon)
    {
        if (weapon == null) return null;
        if (!ammo.TryGetValue(weapon, out var state))
        {
            state = new AmmoState { magazine = weapon.magazineSize, reserve = weapon.startingReserve };
            ammo[weapon] = state;
        }
        return state;
    }

    void HandleEquipmentChanged()
    {
        //Cambiar de arma cancela la recarga en curso
        if (reloadRoutine != null) StopCoroutine(reloadRoutine);
        reloadRoutine = null;
        IsReloading = false;
        OnAmmoChanged?.Invoke();
    }

    // ---------- Bucle principal ----------

    void Update()
    {
        ItemData weapon = equipment.CurrentWeapon;
        bool canUse = weapon != null && !player.InputBlocked;

        //Mientras se recarga no se puede apuntar
        IsAiming = canUse && !IsReloading && aimAction.IsPressed();
        lookAheadTimer -= Time.deltaTime;
        player.IsAiming = IsAiming || (canUse && lookAheadTimer > 0f);
        cameraController.IsAiming = IsAiming;
        weaponAnimator.SetAiming(IsAiming);
        weaponAnimator.AimPitch = cameraController.Pitch;
        if (weapon != null) cameraController.AimFov = weapon.aimFov;

        //Catalejo: tras apuntar un instante con un arma de largo alcance se mira por la mira
        aimHeldTime = IsAiming ? aimHeldTime + Time.deltaTime : 0f;
        bool scoped = IsAiming && weapon.hasScope && aimHeldTime >= weapon.scopeDelay;
        if (scoped != IsScoped) SetScoped(scoped, weapon);
        if (IsScoped) UpdateScope(weapon);
        else RecoverBreath();

        CurrentSpread = weapon == null ? 0f : (IsScoped ? weapon.scopeSpread : (IsAiming ? weapon.aimSpread : weapon.hipSpread));

        UpdateTracer();
        if (!canUse) return;

        if (reloadAction.WasPressedThisFrame()) StartReload();

        if (attackAction.WasPressedThisFrame() && Time.time >= nextFireTime && !IsReloading)
        {
            if (GetAmmo(weapon).magazine > 0) Fire(weapon);
            else StartReload();
        }
    }

    // ---------- Disparo ----------

    void Fire(ItemData weapon)
    {
        nextFireTime = Time.time + 1f / Mathf.Max(weapon.fireRate, 0.01f);
        GetAmmo(weapon).magazine--;
        lookAheadTimer = hipFireLookTime;
        weaponAnimator.PlayFire();

        //Direccion: centro de la camara con dispersion aleatoria dentro de un cono
        Transform camT = cameraController.transform;
        Vector2 random = UnityEngine.Random.insideUnitCircle * Mathf.Tan(CurrentSpread * Mathf.Deg2Rad);
        Vector3 direction = (camT.forward + camT.right * random.x + camT.up * random.y).normalized;

        Vector3 endPoint = camT.position + direction * weapon.range;
        bool hasHit = TryGetHit(camT.position, direction, weapon.range, out RaycastHit hit);
        if (hasHit) endPoint = hit.point;

        Transform muzzle = equipment.WeaponMuzzle;
        Vector3 muzzlePos = muzzle != null ? muzzle.position : player.transform.position + Vector3.up * 1.4f;
        //Por el catalejo el arma no se ve: el trazador sale justo delante de la camara y no hay fogonazo
        if (IsScoped) muzzlePos = camT.position + camT.forward * 1.5f - camT.up * 0.15f;

        if (hasHit) ApplyHit(weapon, hit, direction);
        if (weapon.explosionRadius > 0f) Explode(weapon, endPoint);

        //Efectos
        if (muzzleFlashPrefab != null && muzzle != null && !IsScoped)
            Instantiate(muzzleFlashPrefab, muzzlePos, Quaternion.LookRotation(endPoint - muzzlePos));
        if (hasHit && impactPrefab != null && weapon.explosionRadius <= 0f)
            Instantiate(impactPrefab, hit.point, Quaternion.LookRotation(hit.normal));
        ShowTracer(muzzlePos, endPoint);

        //El retroceso del arma lo hace la animacion de disparo; la camara sube un poco
        cameraController.AddRecoil(weapon.recoil);
        OnAmmoChanged?.Invoke();
    }

    //Raycast que ignora al propio jugador
    bool TryGetHit(Vector3 origin, Vector3 direction, float range, out RaycastHit closest)
    {
        closest = default;
        float best = float.MaxValue;
        int count = Physics.RaycastNonAlloc(origin, direction, hits, range, hitMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (hits[i].transform.IsChildOf(player.transform)) continue;
            if (hits[i].distance < best)
            {
                best = hits[i].distance;
                closest = hits[i];
            }
        }
        return best < float.MaxValue;
    }

    void ApplyHit(ItemData weapon, RaycastHit hit, Vector3 direction)
    {
        var damageable = hit.collider.GetComponentInParent<IDamageable>();
        damageable?.TakeDamage(weapon.damage, hit.point, direction);
        if (hit.rigidbody != null)
            hit.rigidbody.AddForceAtPosition(direction * weapon.impactForce, hit.point, ForceMode.Impulse);
    }

    void Explode(ItemData weapon, Vector3 center)
    {
        if (explosionPrefab != null) Instantiate(explosionPrefab, center, Quaternion.identity);
        int count = Physics.OverlapSphereNonAlloc(center, weapon.explosionRadius, overlap, hitMask, QueryTriggerInteraction.Ignore);
        var damaged = new HashSet<IDamageable>();
        for (int i = 0; i < count; i++)
        {
            Collider c = overlap[i];
            if (c.transform.IsChildOf(player.transform)) continue;
            float falloff = 1f - Mathf.Clamp01(Vector3.Distance(center, c.ClosestPoint(center)) / weapon.explosionRadius);
            var damageable = c.GetComponentInParent<IDamageable>();
            if (damageable != null && damaged.Add(damageable))
                damageable.TakeDamage(weapon.damage * falloff, c.transform.position, c.transform.position - center);
            if (c.attachedRigidbody != null)
                c.attachedRigidbody.AddExplosionForce(weapon.impactForce * 2f, center, weapon.explosionRadius, 0.5f, ForceMode.Impulse);
        }
    }

    // ---------- Catalejo ----------

    void SetScoped(bool scoped, ItemData weapon)
    {
        IsScoped = scoped;
        cameraController.IsScoped = scoped;
        if (scoped)
        {
            scopeFov = weapon.scopeFov;
            //El jugador desaparece de la vista pero sigue proyectando su sombra
            hiddenRenderers.Clear();
            foreach (Renderer r in player.GetComponentsInChildren<Renderer>())
            {
                hiddenRenderers[r] = r.shadowCastingMode;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
        }
        else
        {
            foreach (var pair in hiddenRenderers)
            {
                if (pair.Key != null) pair.Key.shadowCastingMode = pair.Value;
            }
            hiddenRenderers.Clear();
            cameraController.ScopeSway = Vector2.zero;
            HoldingBreath = false;
        }
    }

    void UpdateScope(ItemData weapon)
    {
        //Rueda del raton: mas o menos aumentos
        float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
        if (Mathf.Abs(scroll) > 0.01f)
            scopeFov = Mathf.Clamp(scopeFov - Mathf.Sign(scroll) * scopeZoomStep, weapon.scopeMinFov, weapon.scopeMaxFov);
        cameraController.ScopeFov = scopeFov;
        float normalFov = cameraController.NormalFov;
        ScopeMagnification = Mathf.Tan(normalFov * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(scopeFov * 0.5f * Mathf.Deg2Rad);

        //Respiracion: con Shift se contiene unos segundos y el pulso casi no se mueve
        HoldingBreath = runAction.IsPressed() && !breathExhausted;
        if (HoldingBreath)
        {
            Breath -= Time.deltaTime / Mathf.Max(breathHoldTime, 0.01f);
            if (Breath <= 0f)
            {
                Breath = 0f;
                breathExhausted = true;
                HoldingBreath = false;
            }
        }
        else RecoverBreath();

        //Balanceo del pulso: ruido suave; mas fuerte sin aire, casi nulo conteniendo la respiracion
        float amount = weapon.scopeSway * (HoldingBreath ? 0.12f : (breathExhausted ? 1.8f : 1f));
        float t = Time.time;
        Vector2 sway = new Vector2(Mathf.PerlinNoise(t * 0.45f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t * 0.38f) - 0.5f) * 2f * amount;
        cameraController.ScopeSway = Vector2.Lerp(cameraController.ScopeSway, sway, 1f - Mathf.Exp(-6f * Time.deltaTime));
    }

    void RecoverBreath()
    {
        Breath = Mathf.Min(1f, Breath + Time.deltaTime / Mathf.Max(breathRecoverTime, 0.01f));
        if (breathExhausted && Breath >= 0.5f) breathExhausted = false;
    }

    // ---------- Recarga ----------

    void StartReload()
    {
        ItemData weapon = equipment.CurrentWeapon;
        AmmoState state = GetAmmo(weapon);
        if (weapon == null || IsReloading || state.magazine >= weapon.magazineSize || state.reserve <= 0) return;
        reloadRoutine = StartCoroutine(Reload(weapon, state));
    }

    IEnumerator Reload(ItemData weapon, AmmoState state)
    {
        IsReloading = true;
        weaponAnimator.PlayReload(weapon.reloadTime);
        OnAmmoChanged?.Invoke();
        yield return new WaitForSeconds(weapon.reloadTime);
        int needed = weapon.magazineSize - state.magazine;
        int taken = Mathf.Min(needed, state.reserve);
        state.magazine += taken;
        state.reserve -= taken;
        IsReloading = false;
        reloadRoutine = null;
        OnAmmoChanged?.Invoke();
    }

    //Añade municion de reserva (para recogidas de municion en el futuro)
    public void AddReserveAmmo(ItemData weapon, int amount)
    {
        GetAmmo(weapon).reserve += amount;
        OnAmmoChanged?.Invoke();
    }

    // ---------- Trazador ----------

    void CreateTracer()
    {
        var go = new GameObject("Trazador");
        tracer = go.AddComponent<LineRenderer>();
        tracer.material = tracerMaterial;
        tracer.positionCount = 2;
        tracer.startWidth = 0.03f;
        tracer.endWidth = 0.01f;
        tracer.startColor = new Color(1f, 0.9f, 0.6f, 0.9f);
        tracer.endColor = new Color(1f, 0.8f, 0.4f, 0.2f);
        tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tracer.enabled = false;
    }

    void ShowTracer(Vector3 from, Vector3 to)
    {
        tracer.SetPosition(0, from);
        tracer.SetPosition(1, to);
        tracer.enabled = true;
        tracerTimer = tracerDuration;
    }

    void UpdateTracer()
    {
        if (!tracer.enabled) return;
        tracerTimer -= Time.deltaTime;
        if (tracerTimer <= 0f) tracer.enabled = false;
    }
}
