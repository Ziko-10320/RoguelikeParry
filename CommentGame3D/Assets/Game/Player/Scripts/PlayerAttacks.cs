using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttacks : MonoBehaviour
{
    private enum Weapon { Katana, Gun }

    [Header("Input")]
    [SerializeField] private InputActionReference switchWeaponAction;

    [Header("Weapons")]
    [SerializeField] private GameObject katana;
    [SerializeField] private GameObject gun;
    [SerializeField] private Weapon startingWeapon = Weapon.Katana;

    [Header("Switch Settings")]
    [SerializeField] private float switchCooldown = 0.3f;

    [Header("Animator (optional)")]
    [SerializeField] private Animator animator;
    [SerializeField] private string switchTriggerName = "SwitchWeapon";

    private int switchTriggerHash;
    private float nextSwitchTime;
    private Weapon currentWeapon;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        switchTriggerHash = Animator.StringToHash(switchTriggerName);
    }

    private void OnEnable()
    {
        switchWeaponAction.action.Enable();
    }

    private void OnDisable()
    {
        switchWeaponAction.action.Disable();
    }

    private void Start()
    {
        // make sure only one weapon is active at the start
        EquipWeapon(startingWeapon);
    }

    private void Update()
    {
        if (switchWeaponAction.action.WasPressedThisFrame() && Time.time >= nextSwitchTime)
        {
            SwitchWeapon();
        }
    }

    private void SwitchWeapon()
    {
        nextSwitchTime = Time.time + switchCooldown;

        // if katana is active -> gun, otherwise -> katana
        Weapon next = (katana != null && katana.activeSelf) ? Weapon.Gun : Weapon.Katana;
        EquipWeapon(next);

        if (animator != null && !string.IsNullOrEmpty(switchTriggerName))
            animator.SetTrigger(switchTriggerHash);
    }

    private void EquipWeapon(Weapon weapon)
    {
        currentWeapon = weapon;

        if (katana != null) katana.SetActive(weapon == Weapon.Katana);
        if (gun != null) gun.SetActive(weapon == Weapon.Gun);
    }
}