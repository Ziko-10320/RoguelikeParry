using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerAttacks : MonoBehaviour
{
    private enum Weapon { Katana, Gun, Bow }

    [Header("Input")]
    [SerializeField] private InputActionReference switchWeaponAction;
    [SerializeField] private InputActionReference shootAction;

    [Header("Weapons")]
    [SerializeField] private GameObject katana;
    [SerializeField] private GameObject gun;
    [SerializeField] private GameObject bow;
    [SerializeField] private Weapon startingWeapon = Weapon.Katana;

    [Header("Bow Configuration")]
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private Transform visualArrowTransform;
    [SerializeField] private Transform bowStartPoint;
    [SerializeField] private Transform trajectoryStartPoint;
    [SerializeField] private LineRenderer trajectoryLine;

    [Header("Bow Physics")]
    [SerializeField] private float minShootForce = 5f;
    [SerializeField] private float maxShootForce = 25f;
    [SerializeField] private float maxChargeTime = 2f;
    [SerializeField] private float gravityMultiplier = 1.0f;
    [SerializeField] private int trajectoryResolution = 30;
    [SerializeField] private float trajectoryDuration = 2f;

    [Header("UI Elements")]
    [SerializeField] private GameObject crosshair;

    [Header("Switch Settings")]
    [SerializeField] private float switchCooldown = 0.3f;

    [Header("Animator (optional)")]
    [SerializeField] private Animator animator;
    [SerializeField] private string switchTriggerName = "SwitchWeapon";
    [SerializeField] private string bowDrawBoolName = "isDrawing";
    [SerializeField] private string bowReleaseTriggerName = "BowRelease";

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;

    private int switchTriggerHash;
    private int bowDrawHash;
    private int bowReleaseHash;
    private float nextSwitchTime;
    private Weapon currentWeapon;

    private bool isDrawingBow = false;
    private float currentChargeTime = 0f;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        switchTriggerHash = Animator.StringToHash(switchTriggerName);
        bowDrawHash = Animator.StringToHash(bowDrawBoolName);
        bowReleaseHash = Animator.StringToHash(bowReleaseTriggerName);

        if (trajectoryLine != null)
        {
            trajectoryLine.enabled = false;
            trajectoryLine.positionCount = trajectoryResolution;
        }
    }

    private void OnEnable()
    {
        switchWeaponAction.action.Enable();
        if (shootAction != null) shootAction.action.Enable();
    }

    private void OnDisable()
    {
        switchWeaponAction.action.Disable();
        if (shootAction != null) shootAction.action.Disable();
    }

    private void Start()
    {
        EquipWeapon(startingWeapon);
    }

    private void Update()
    {
        if (switchWeaponAction.action.WasPressedThisFrame() && Time.time >= nextSwitchTime)
        {
            SwitchWeapon();
        }

        if (currentWeapon == Weapon.Bow)
        {
            HandleBowShooting();
        }
        else if (trajectoryLine != null && trajectoryLine.enabled)
        {
            trajectoryLine.enabled = false;
        }
    }

    private void HandleBowShooting()
    {
        if (shootAction == null) return;

        if (shootAction.action.IsPressed())
        {
            if (!isDrawingBow)
            {
                isDrawingBow = true;
                currentChargeTime = 0f;
                if (animator != null) animator.SetBool(bowDrawHash, true);
            }

            currentChargeTime = Mathf.Min(currentChargeTime + Time.deltaTime, maxChargeTime);
            UpdateTrajectory();
        }
        else if (shootAction.action.WasReleasedThisFrame() && isDrawingBow)
        {
            isDrawingBow = false;

            if (animator != null)
            {
                animator.SetBool(bowDrawHash, false);
                animator.SetTrigger(bowReleaseTriggerName);
            }

            float chargePercent = currentChargeTime / maxChargeTime;
            float finalForce = Mathf.Lerp(minShootForce, maxShootForce, chargePercent);

            SpawnArrow(finalForce);

            if (trajectoryLine != null) trajectoryLine.enabled = false;
            currentChargeTime = 0f;
        }
    }

    private void UpdateTrajectory()
    {
        Transform startT = trajectoryStartPoint != null ? trajectoryStartPoint : bowStartPoint;
        if (trajectoryLine == null || startT == null) return;

        trajectoryLine.enabled = true;

        float chargePercent = currentChargeTime / maxChargeTime;
        float currentForce = Mathf.Lerp(minShootForce, maxShootForce, chargePercent);

        Vector3 startPosition = startT.position;
        Vector3 startVelocity = startT.forward * currentForce;
        Vector3 gravity = Physics.gravity * gravityMultiplier;

        for (int i = 0; i < trajectoryResolution; i++)
        {
            float t = (i / (float)(trajectoryResolution - 1)) * trajectoryDuration;
            Vector3 point = startPosition + (startVelocity * t) + (0.5f * gravity * t * t);
            trajectoryLine.SetPosition(i, point);
        }
    }

    private void SpawnArrow(float force)
    {
        if (arrowPrefab == null || bowStartPoint == null)
        {
            Debug.LogWarning("Arrow prefab or bow start point is missing!");
            return;
        }

        // FIX: Instead of using a fixed offset that might be wrong for the prefab,
        // we instantiate the arrow and immediately align its local forward with the target direction.
        // This is the most robust way because it doesn't matter what the prefab's internal rotation is.

        GameObject arrow = Instantiate(arrowPrefab, bowStartPoint.position, Quaternion.identity);

        // FORCE THE ALIGNMENT:
        // We make the arrow's Z-axis (forward) point exactly where the bow is aiming.
        arrow.transform.forward = bowStartPoint.forward;

        Rigidbody rb = arrow.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(bowStartPoint.forward * force, ForceMode.Impulse);
        }
        else
        {
            Debug.LogWarning("Arrow prefab is missing a Rigidbody!");
        }
    }

    private void SwitchWeapon()
    {
        nextSwitchTime = Time.time + switchCooldown;
        Weapon next = (currentWeapon == Weapon.Katana) ? Weapon.Gun :
                      (currentWeapon == Weapon.Gun) ? Weapon.Bow : Weapon.Katana;
        EquipWeapon(next);
        if (animator != null && !string.IsNullOrEmpty(switchTriggerName))
            animator.SetTrigger(switchTriggerHash);
    }

    private void EquipWeapon(Weapon weapon)
    {
        currentWeapon = weapon;
        if (katana != null) katana.SetActive(weapon == Weapon.Katana);
        if (gun != null) gun.SetActive(weapon == Weapon.Gun);
        if (bow != null) bow.SetActive(weapon == Weapon.Bow);
    }

    private void OnDrawGizmos()
    {
        if (bowStartPoint == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(bowStartPoint.position, 0.1f);

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(bowStartPoint.position, bowStartPoint.forward * 2f);

        if (trajectoryStartPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(trajectoryStartPoint.position, 0.1f);
            Gizmos.DrawRay(trajectoryStartPoint.position, trajectoryStartPoint.forward * 1f);
        }
    }
}
