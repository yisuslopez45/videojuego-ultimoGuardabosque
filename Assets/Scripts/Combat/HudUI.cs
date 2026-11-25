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

        crosshair.style.display = hasWeapon && !inventoryOpen ? DisplayStyle.Flex : DisplayStyle.None;
        ammoPanel.style.display = hasWeapon ? DisplayStyle.Flex : DisplayStyle.None;
        controlsHint.style.display = hasWeapon && !inventoryOpen ? DisplayStyle.Flex : DisplayStyle.None;
        if (!hasWeapon) return;

        //La mira se abre o se cierra suavemente segun la dispersion actual
        float targetGap = crosshairMinGap + shooter.CurrentSpread * crosshairPixelsPerDegree;
        gap = Mathf.Lerp(gap, targetGap, 15f * Time.deltaTime);
        top.style.top = -gap - 12f;
        bottom.style.top = gap;
        left.style.left = -gap - 12f;
        right.style.left = gap;
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
