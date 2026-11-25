using UnityEngine;
using UnityEngine.UIElements;

//HUD del jugador: mira en el centro y municion del arma equipada
[RequireComponent(typeof(UIDocument))]
public class HudUI : MonoBehaviour
{
    public WeaponShooter shooter;
    public PlayerEquipment equipment;
    public InventoryUI inventory;
    public WeaponWheelUI weaponWheel;

    [Tooltip("Pixeles de separacion de la mira por cada grado de dispersion")]
    public float crosshairPixelsPerDegree = 6f;
    public float crosshairMinGap = 5f;

    private VisualElement crosshair, ammoPanel, top, bottom, left, right;
    private VisualElement scopeLayer, breathFill;
    private VisualElement stamina, staminaFill;
    private ScopeOverlay scopeOverlay;
    private Label scopeZoom;
    private Label weaponName, magLabel, reserveLabel, reloadLabel, controlsHint;
    private float gap;

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        crosshair = root.Q("crosshair");
        ammoPanel = root.Q("ammo-panel");
        top = root.Q("ch-top");
        bottom = root.Q("ch-bottom");
        left = root.Q("ch-left");
        right = root.Q("ch-right");
        weaponName = root.Q<Label>("weapon-name");
        magLabel = root.Q<Label>("ammo-mag");
        reserveLabel = root.Q<Label>("ammo-reserve");
        reloadLabel = root.Q<Label>("reload");
        controlsHint = root.Q<Label>("controls-hint");
        scopeLayer = root.Q("scope-layer");
        stamina = root.Q("stamina");
        staminaFill = root.Q("stamina-fill");
        scopeZoom = root.Q<Label>("scope-zoom");
        breathFill = root.Q("scope-breath-fill");
        if (scopeLayer != null && scopeOverlay == null)
        {
            scopeOverlay = new ScopeOverlay();
            scopeOverlay.AddToClassList("scope-overlay");
            scopeLayer.Insert(0, scopeOverlay);
        }
        if (shooter != null) shooter.OnAmmoChanged += RefreshAmmo;
    }

    void OnDisable()
    {
        if (shooter != null) shooter.OnAmmoChanged -= RefreshAmmo;
    }

    void Start()
    {
        RefreshAmmo();
    }

    void Update()
    {
        if (crosshair == null || equipment == null) return;
        bool hasWeapon = equipment.CurrentWeapon != null;
        bool inventoryOpen = (inventory != null && inventory.IsOpen) || (weaponWheel != null && weaponWheel.IsOpen);
        bool scoped = shooter != null && shooter.IsScoped;

        //La mira solo aparece mientras se apunta (clic derecho); con el catalejo se usa la suya propia
        bool aiming = shooter != null && shooter.IsAiming;
        crosshair.style.display = hasWeapon && aiming && !inventoryOpen && !scoped ? DisplayStyle.Flex : DisplayStyle.None;
        ammoPanel.style.display = hasWeapon ? DisplayStyle.Flex : DisplayStyle.None;
        controlsHint.style.display = hasWeapon && !inventoryOpen && !scoped ? DisplayStyle.Flex : DisplayStyle.None;
        UpdateScope(scoped);
        UpdateStamina();
        if (!hasWeapon) return;

        //La mira se abre o se cierra suavemente segun la dispersion actual
        float targetGap = crosshairMinGap + shooter.CurrentSpread * crosshairPixelsPerDegree;
        gap = Mathf.Lerp(gap, targetGap, 15f * Time.deltaTime);
        top.style.top = -gap - 12f;
        bottom.style.top = gap;
        left.style.left = -gap - 12f;
        right.style.left = gap;
    }

    //Resistencia al correr: la barra aparece al gastarla y se pone roja si el jugador esta agotado
    void UpdateStamina()
    {
        if (stamina == null || shooter == null || shooter.player == null) return;
        playerController p = shooter.player;
        stamina.EnableInClassList("stamina--visible", p.Stamina < 0.999f);
        staminaFill.style.width = Length.Percent(p.Stamina * 100f);
        staminaFill.EnableInClassList("stamina-fill--exhausted", p.IsExhausted);
    }

    //Mira del catalejo: aumentos y respiracion
    void UpdateScope(bool scoped)
    {
        if (scopeLayer == null) return;
        scopeLayer.style.display = scoped ? DisplayStyle.Flex : DisplayStyle.None;
        if (!scoped) return;
        scopeZoom.text = shooter.ScopeMagnification.ToString("0.0") + "x";
        breathFill.style.width = Length.Percent(shooter.Breath * 100f);
        breathFill.EnableInClassList("scope-breath-fill--holding", shooter.HoldingBreath);
        breathFill.EnableInClassList("scope-breath-fill--empty", !shooter.HoldingBreath && shooter.Breath < 0.5f);
    }

    void RefreshAmmo()
    {
        if (magLabel == null || equipment == null || shooter == null) return;
        ItemData weapon = equipment.CurrentWeapon;
        if (weapon == null) return;
        weaponName.text = weapon.displayName;
        magLabel.text = shooter.MagazineAmmo.ToString();
        reserveLabel.text = "/ " + shooter.ReserveAmmo;
        magLabel.EnableInClassList("ammo-mag--empty", shooter.MagazineAmmo == 0);
        reloadLabel.text = shooter.IsReloading
            ? "Recargando..."
            : (shooter.MagazineAmmo == 0 ? (shooter.ReserveAmmo > 0 ? "Pulsa R para recargar" : "Sin municion") : "");
        reloadLabel.style.display = reloadLabel.text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
