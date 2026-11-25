using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

//Rueda de armas: mientras se mantiene Tab aparece la rueda y el cursor para elegir arma.
//Pasar el raton por un sector lo resalta, clic lo equipa y al soltar Tab se cierra
//(equipando el arma resaltada). Mientras esta abierta el juego va a camara lenta.
[RequireComponent(typeof(UIDocument))]
public class WeaponWheelUI : MonoBehaviour
{
    public PlayerEquipment equipment;
    public WeaponShooter shooter;
    public playerController player;
    public CamaraController cameraController;
    [Tooltip("Inventario de accesorios: la rueda no se abre si esta abierto")]
    public InventoryUI inventory;

    [Header("Rueda")]
    public float outerRadius = 300f;
    public float innerRadius = 150f;
    [Tooltip("Velocidad del juego mientras la rueda esta abierta")]
    [Range(0.05f, 1f)] public float slowMotion = 0.3f;

    public bool IsOpen { get; private set; }

    private VisualElement overlay;
    private VisualElement wheelArea;
    private VisualElement slotsLayer;
    private WeaponWheelRing ring;
    private Label nameLabel;
    private Label magLabel;
    private Label reserveLabel;
    private Label stateLabel;
    private VisualElement statsBox;
    private VisualElement damageBar;
    private VisualElement rangeBar;
    private VisualElement fireRateBar;

    //Opciones de la rueda: la primera (null) es "Sin arma"
    private readonly List<ItemData> options = new List<ItemData>();
    private readonly List<VisualElement> slots = new List<VisualElement>();
    private int selected = -1;
    private InputAction wheelAction;
    private float previousTimeScale = 1f;
    private float previousFixedDeltaTime = 0.02f;
    private bool unblockPending;

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        overlay = root.Q("wheel-overlay");
        wheelArea = root.Q("wheel-area");
        slotsLayer = root.Q("wheel-slots");
        nameLabel = root.Q<Label>("info-name");
        magLabel = root.Q<Label>("info-mag");
        reserveLabel = root.Q<Label>("info-reserve");
        stateLabel = root.Q<Label>("info-state");
        statsBox = root.Q("info-stats");
        damageBar = root.Q("bar-damage");
        rangeBar = root.Q("bar-range");
        fireRateBar = root.Q("bar-firerate");

        //Anillo dibujado por codigo, detras de iconos y textos
        ring = new WeaponWheelRing { OuterRadius = outerRadius, InnerRadius = innerRadius };
        ring.AddToClassList("wheel-ring");
        wheelArea.Insert(0, ring);
        float size = (outerRadius + 30f) * 2f;
        wheelArea.style.width = size;
        wheelArea.style.height = size;

        overlay.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        overlay.RegisterCallback<PointerDownEvent>(OnPointerDown);
        overlay.style.display = DisplayStyle.None;

        if (player != null) wheelAction = player.GetComponent<PlayerInput>().actions["Inventory"];
    }

    void OnDisable()
    {
        if (overlay != null)
        {
            overlay.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            overlay.UnregisterCallback<PointerDownEvent>(OnPointerDown);
        }
        if (IsOpen) Close(false);
    }

    void Update()
    {
        if (wheelAction == null || equipment == null) return;

        if (!IsOpen)
        {
            bool otherMenuOpen = inventory != null && inventory.IsOpen;
            if (wheelAction.WasPressedThisFrame() && !otherMenuOpen) Open();
            return;
        }

        //Soltar Tab cierra la rueda y equipa el arma resaltada; Esc cierra sin cambiar
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close(false);
        else if (!wheelAction.IsPressed()) Close(true);
    }

    //El input se desbloquea despues de los Update de este frame, para que el clic o el Esc
    //que cierran la rueda no disparen ni hagan nada mas
    void LateUpdate()
    {
        if (!unblockPending) return;
        unblockPending = false;
        SetInputBlocked(false);
    }

    // ---------- Abrir / cerrar ----------

    public void Open()
    {
        if (overlay == null) return;
        IsOpen = true;
        unblockPending = false;
        BuildOptions();
        Select(Mathf.Max(0, options.IndexOf(equipment.CurrentWeapon)));

        overlay.style.display = DisplayStyle.Flex;
        //La clase se pone un frame despues para que se vea la transicion de entrada
        overlay.RemoveFromClassList("wheel-overlay--open");
        overlay.schedule.Execute(() => overlay.AddToClassList("wheel-overlay--open"));

        SetInputBlocked(true);
        previousTimeScale = Time.timeScale;
        previousFixedDeltaTime = Time.fixedDeltaTime;
        Time.timeScale = slowMotion;
        Time.fixedDeltaTime = previousFixedDeltaTime * slowMotion;

        //Cursor visible y en el centro de la rueda
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (Mouse.current != null) Mouse.current.WarpCursorPosition(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
    }

    public void Close(bool equipSelected)
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (equipSelected) EquipSelected();

        overlay.RemoveFromClassList("wheel-overlay--open");
        overlay.style.display = DisplayStyle.None;

        Time.timeScale = previousTimeScale;
        Time.fixedDeltaTime = previousFixedDeltaTime;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        unblockPending = true;
    }

    void SetInputBlocked(bool blocked)
    {
        if (player != null) player.InputBlocked = blocked;
        if (cameraController != null) cameraController.InputBlocked = blocked;
    }

    void EquipSelected()
    {
        if (selected < 0 || selected >= options.Count) return;
        ItemData item = options[selected];
        if (item == equipment.CurrentWeapon) return;
        if (item == null) equipment.UnequipWeapon();
        else equipment.EquipWeapon(item);
    }

    // ---------- Raton ----------

    void OnPointerMove(PointerMoveEvent evt)
    {
        int index = IndexAt(evt.position);
        if (index >= 0 && index != selected) Select(index);
    }

    void OnPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0) return;
        int index = IndexAt(evt.position);
        if (index < 0) return;
        Select(index);
        EquipSelected();
        RefreshSlots();
    }

    //Sector bajo el cursor segun el angulo respecto al centro; en el disco central no cambia nada
    int IndexAt(Vector2 panelPosition)
    {
        if (options.Count == 0) return -1;
        Vector2 local = wheelArea.WorldToLocal(panelPosition) - wheelArea.contentRect.center;
        if (local.magnitude < innerRadius * 0.6f) return -1;

        //0 grados = arriba, en sentido horario (en la UI la Y crece hacia abajo)
        float angle = Mathf.Atan2(local.x, -local.y) * Mathf.Rad2Deg;
        float step = 360f / options.Count;
        int index = Mathf.RoundToInt(angle / step);
        return ((index % options.Count) + options.Count) % options.Count;
    }

    // ---------- Contenido ----------

    void BuildOptions()
    {
        options.Clear();
        options.Add(null);
        foreach (ItemData item in equipment.inventory)
        {
            if (item != null && item.type == ItemType.Arma) options.Add(item);
        }

        ring.SectorCount = options.Count;
        slotsLayer.Clear();
        slots.Clear();

        float radius = (innerRadius + outerRadius) * 0.5f;
        Vector2 center = new Vector2(outerRadius + 30f, outerRadius + 30f);
        for (int i = 0; i < options.Count; i++)
        {
            float a = WeaponWheelRing.SectorAngleFromTop(i, options.Count) * Mathf.Deg2Rad;
            Vector2 pos = center + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * radius;
            VisualElement slot = CreateSlot(options[i]);
            slot.style.left = pos.x;
            slot.style.top = pos.y;
            slotsLayer.Add(slot);
            slots.Add(slot);
        }
        RefreshSlots();
    }

    VisualElement CreateSlot(ItemData item)
    {
        //El slot tiene tamaño 0 en su punto central; el contenido se centra sobre el con translate
        var slot = new VisualElement();
        slot.AddToClassList("wheel-slot");
        var content = new VisualElement();
        content.AddToClassList("wheel-slot-content");
        slot.Add(content);

        Sprite sprite = item == null ? null : (item.wheelIcon != null ? item.wheelIcon : item.icon);
        if (sprite != null)
        {
            var icon = new VisualElement();
            icon.AddToClassList("wheel-slot-icon");
            icon.style.backgroundImage = new StyleBackground(sprite);
            content.Add(icon);
        }
        else
        {
            var empty = new Label(item == null ? "Sin arma" : item.displayName);
            empty.AddToClassList("wheel-slot-text");
            content.Add(empty);
        }

        var ammo = new Label();
        ammo.name = "ammo";
        ammo.AddToClassList("wheel-slot-ammo");
        content.Add(ammo);

        var badge = new Label("EN MANO");
        badge.name = "badge";
        badge.AddToClassList("wheel-slot-badge");
        content.Add(badge);
        return slot;
    }

    void RefreshSlots()
    {
        int equipped = options.IndexOf(equipment.CurrentWeapon);
        for (int i = 0; i < slots.Count; i++)
        {
            ItemData item = options[i];
            slots[i].EnableInClassList("wheel-slot--selected", i == selected);
            slots[i].Q<Label>("badge").style.display = i == equipped ? DisplayStyle.Flex : DisplayStyle.None;

            var ammo = slots[i].Q<Label>("ammo");
            if (item != null && shooter != null)
            {
                shooter.GetAmmoCounts(item, out int mag, out int reserve);
                ammo.text = mag + " / " + reserve;
                ammo.EnableInClassList("wheel-slot-ammo--empty", mag + reserve == 0);
            }
            else ammo.text = "";
        }
        ring.Equipped = equipped;
        ring.MarkDirtyRepaint();
    }

    void Select(int index)
    {
        selected = index;
        ring.Selected = index;
        ring.MarkDirtyRepaint();
        RefreshSlots();
        RefreshInfo();
    }

    //Informacion del centro: nombre, municion y caracteristicas del arma resaltada
    void RefreshInfo()
    {
        ItemData item = selected >= 0 && selected < options.Count ? options[selected] : null;
        bool equipped = item == equipment.CurrentWeapon;

        if (item == null)
        {
            nameLabel.text = "Sin arma";
            magLabel.text = "";
            reserveLabel.text = "Guardar el arma";
            statsBox.style.display = DisplayStyle.None;
        }
        else
        {
            nameLabel.text = item.displayName;
            if (shooter != null)
            {
                shooter.GetAmmoCounts(item, out int mag, out int reserve);
                magLabel.text = mag.ToString();
                reserveLabel.text = "/ " + reserve;
            }
            statsBox.style.display = DisplayStyle.Flex;

            //Barras relativas a la mejor arma del inventario
            float maxDamage = 0f, maxRange = 0f, maxRate = 0f;
            foreach (ItemData w in options)
            {
                if (w == null) continue;
                maxDamage = Mathf.Max(maxDamage, w.damage);
                maxRange = Mathf.Max(maxRange, w.range);
                maxRate = Mathf.Max(maxRate, w.fireRate);
            }
            SetBar(damageBar, item.damage / Mathf.Max(maxDamage, 0.01f));
            SetBar(rangeBar, item.range / Mathf.Max(maxRange, 0.01f));
            SetBar(fireRateBar, item.fireRate / Mathf.Max(maxRate, 0.01f));
        }
        stateLabel.text = equipped ? "EN MANO" : "";
    }

    static void SetBar(VisualElement bar, float value)
    {
        bar.style.width = Length.Percent(Mathf.Clamp01(value) * 100f);
    }
}
