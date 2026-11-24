using System;
using System.Collections.Generic;
using UnityEngine;

//Gestiona el arma en la mano y los accesorios del jugador.
//Coloca cada objeto en el hueso del esqueleto que indica su ItemData.
public class PlayerEquipment : MonoBehaviour
{
    [Tooltip("Objetos disponibles en el inventario del jugador")]
    public List<ItemData> inventory = new List<ItemData>();

    [Header("Equipamiento inicial")]
    public ItemData startingWeapon;
    public List<ItemData> startingAccessories = new List<ItemData>();

    //Se lanza cada vez que cambia el arma o un accesorio (lo usan el inventario y WeaponAnimator)
    public event Action OnEquipmentChanged;

    public ItemData CurrentWeapon { get; private set; }
    public float SpeedMultiplier => CurrentWeapon != null ? CurrentWeapon.moveSpeedMultiplier : 1f;

    private Animator animator;
    private GameObject weaponInstance;
    private readonly Dictionary<ItemData, GameObject> accessoryInstances = new Dictionary<ItemData, GameObject>();
    private readonly Dictionary<EquipSlot, Transform> sockets = new Dictionary<EquipSlot, Transform>();

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        CreateSockets();
    }

    void Start()
    {
        if (startingWeapon != null) EquipWeapon(startingWeapon);
        foreach (var item in startingAccessories)
        {
            if (item != null) EquipAccessory(item);
        }
    }

#if UNITY_EDITOR
    //En el editor se reaplican los offsets cada frame para poder ajustarlos en Play desde el ItemData
    void LateUpdate()
    {
        if (weaponInstance != null) ApplyOffsets(weaponInstance.transform, CurrentWeapon);
        foreach (var pair in accessoryInstances) ApplyOffsets(pair.Value.transform, pair.Key);
    }
#endif

    // ---------- Boca del arma (la usa WeaponShooter) ----------

    //Punto desde el que sale el disparo (BocaCanon del prefab del arma)
    public Transform WeaponMuzzle =>
        weaponInstance != null ? (weaponInstance.transform.Find("BocaCanon") ?? weaponInstance.transform) : null;

    // ---------- Armas ----------

    public void EquipWeapon(ItemData item)
    {
        if (item == null || item.type != ItemType.Arma) return;
        UnequipWeapon(false);
        CurrentWeapon = item;
        weaponInstance = Spawn(item);
        OnEquipmentChanged?.Invoke();
    }

    public void UnequipWeapon()
    {
        UnequipWeapon(true);
    }

    void UnequipWeapon(bool notify)
    {
        if (weaponInstance != null) Destroy(weaponInstance);
        weaponInstance = null;
        CurrentWeapon = null;
        if (notify) OnEquipmentChanged?.Invoke();
    }

    // ---------- Accesorios ----------

    public bool IsEquipped(ItemData item)
    {
        if (item == null) return false;
        return item.type == ItemType.Arma ? CurrentWeapon == item : accessoryInstances.ContainsKey(item);
    }

    public void EquipAccessory(ItemData item)
    {
        if (item == null || item.type != ItemType.Accesorio || accessoryInstances.ContainsKey(item)) return;
        accessoryInstances[item] = Spawn(item);
        OnEquipmentChanged?.Invoke();
    }

    public void UnequipAccessory(ItemData item)
    {
        if (item == null || !accessoryInstances.TryGetValue(item, out var instance)) return;
        Destroy(instance);
        accessoryInstances.Remove(item);
        OnEquipmentChanged?.Invoke();
    }

    public void ToggleAccessory(ItemData item)
    {
        if (IsEquipped(item)) UnequipAccessory(item);
        else EquipAccessory(item);
    }

    // ---------- Interno ----------

    GameObject Spawn(ItemData item)
    {
        var instance = Instantiate(item.prefab, sockets[item.slot]);
        instance.name = item.prefab.name;
        ApplyOffsets(instance.transform, item);
        return instance;
    }

    void ApplyOffsets(Transform t, ItemData item)
    {
        t.localPosition = item.positionOffset;
        t.localRotation = Quaternion.Euler(item.rotationOffset);
        t.localScale = Vector3.one;
    }

    void CreateSockets()
    {
        AddSocket(EquipSlot.ManoDerecha, HumanBodyBones.RightHand);
        AddSocket(EquipSlot.Cabeza, HumanBodyBones.Head);
        AddSocket(EquipSlot.Espalda, HumanBodyBones.Spine);
        AddSocket(EquipSlot.Pecho, HumanBodyBones.Spine);
        AddSocket(EquipSlot.Cintura, HumanBodyBones.Hips);
    }

    void AddSocket(EquipSlot slot, HumanBodyBones boneId)
    {
        Transform bone = animator.GetBoneTransform(boneId);
        var socket = new GameObject("Socket_" + slot).transform;
        socket.SetParent(bone, false);

        //Compensa la escala del hueso para que los objetos mantengan su tamaño real
        Vector3 s = bone.lossyScale;
        socket.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
        sockets[slot] = socket;
    }

    // ---------- Pruebas desde el Inspector (menu del componente) ----------

    [ContextMenu("Equipar siguiente arma")]
    void EquipNextWeapon()
    {
        var weapons = inventory.FindAll(i => i != null && i.type == ItemType.Arma);
        if (weapons.Count == 0) return;
        int index = CurrentWeapon == null ? 0 : weapons.IndexOf(CurrentWeapon) + 1;
        if (index >= weapons.Count) UnequipWeapon();
        else EquipWeapon(weapons[index]);
    }

    [ContextMenu("Alternar todos los accesorios")]
    void ToggleAllAccessories()
    {
        foreach (var item in inventory)
        {
            if (item != null && item.type == ItemType.Accesorio) ToggleAccessory(item);
        }
    }
}
