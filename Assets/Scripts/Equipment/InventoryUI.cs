using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

//Inventario del jugador: se abre con Tab y permite elegir arma y ponerse o quitarse accesorios
[RequireComponent(typeof(UIDocument))]
public class InventoryUI : MonoBehaviour
{
    public PlayerEquipment equipment;
    public playerController player;
    public CamaraController cameraController;

    public bool IsOpen { get; private set; }

    private VisualElement overlay;
    private VisualElement grid;
    private Button tabWeapons;
    private Button tabAccessories;
    private Label currentWeaponLabel;
    private bool showingWeapons = true;
    private InputAction inventoryAction;

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        overlay = root.Q<VisualElement>("overlay");
        grid = root.Q<VisualElement>("grid");
        tabWeapons = root.Q<Button>("tab-weapons");
        tabAccessories = root.Q<Button>("tab-accessories");
        currentWeaponLabel = root.Q<Label>("current-weapon");

        tabWeapons.clicked += ShowWeapons;
        tabAccessories.clicked += ShowAccessories;
        overlay.style.display = DisplayStyle.None;

        if (equipment != null) equipment.OnEquipmentChanged += Refresh;
        if (player != null) inventoryAction = player.GetComponent<PlayerInput>().actions["Inventory"];
    }

    void OnDisable()
    {
        if (tabWeapons != null) tabWeapons.clicked -= ShowWeapons;
        if (tabAccessories != null) tabAccessories.clicked -= ShowAccessories;
        if (equipment != null) equipment.OnEquipmentChanged -= Refresh;
    }

    void Update()
    {
        if (inventoryAction != null && inventoryAction.WasPressedThisFrame())
        {
            Toggle();
        }
        else if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close();
        }
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (overlay == null) return;
        IsOpen = true;
        overlay.style.display = DisplayStyle.Flex;
        SetInputBlocked(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Refresh();
    }

    public void Close()
    {
        if (overlay == null) return;
        IsOpen = false;
        overlay.style.display = DisplayStyle.None;
        SetInputBlocked(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void SetInputBlocked(bool blocked)
    {
        if (player != null) player.InputBlocked = blocked;
        if (cameraController != null) cameraController.InputBlocked = blocked;
    }

    void ShowWeapons()
    {
        showingWeapons = true;
        Refresh();
    }

    void ShowAccessories()
    {
        showingWeapons = false;
        Refresh();
    }

    //Reconstruye la cuadricula de la pestaña activa
    void Refresh()
    {
        if (grid == null || equipment == null) return;

        tabWeapons.EnableInClassList("tab--active", showingWeapons);
        tabAccessories.EnableInClassList("tab--active", !showingWeapons);
        currentWeaponLabel.text = "Arma actual: " + (equipment.CurrentWeapon != null ? equipment.CurrentWeapon.displayName : "ninguna");

        grid.Clear();
        if (showingWeapons)
        {
            grid.Add(CreateSlot(null, "Sin arma", equipment.CurrentWeapon == null, () => equipment.UnequipWeapon()));
        }

        ItemType type = showingWeapons ? ItemType.Arma : ItemType.Accesorio;
        foreach (ItemData item in equipment.inventory)
        {
            if (item == null || item.type != type) continue;
            ItemData current = item;
            System.Action onClick = showingWeapons
                ? () => equipment.EquipWeapon(current)
                : () => equipment.ToggleAccessory(current);
            grid.Add(CreateSlot(item.icon, item.displayName, equipment.IsEquipped(item), onClick));
        }
    }

    VisualElement CreateSlot(Sprite icon, string label, bool equipped, System.Action onClick)
    {
        var slot = new VisualElement();
        slot.AddToClassList("slot");
        slot.EnableInClassList("slot--equipped", equipped);

        if (icon != null)
        {
            var image = new VisualElement();
            image.AddToClassList("slot-icon");
            image.style.backgroundImage = new StyleBackground(icon);
            slot.Add(image);
        }
        else
        {
            var empty = new Label("—");
            empty.AddToClassList("slot-empty-icon");
            slot.Add(empty);
        }

        var name = new Label(label);
        name.AddToClassList("slot-name");
        slot.Add(name);

        if (equipped)
        {
            var badge = new Label("EQUIPADO");
            badge.AddToClassList("slot-badge");
            slot.Add(badge);
        }

        slot.RegisterCallback<ClickEvent>(_ => onClick());
        return slot;
    }
}
