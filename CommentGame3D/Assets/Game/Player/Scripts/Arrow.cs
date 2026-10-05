using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class Arrow : MonoBehaviour
{
    private Rigidbody rb;
    private bool hasStuck = false;

    [Header("Settings")]
    [SerializeField] private float destroyTime = 5f;
    [SerializeField] private float invisibilityDuration = 0.3f;
    [SerializeField] private GameObject arrowMesh;

    [Header("Model Correction")]
    [SerializeField] private Vector3 modelForwardOffset = Vector3.forward;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        Destroy(gameObject, destroyTime);

        // Start the invisibility routine
        if (arrowMesh != null)
        {
            StartCoroutine(HandleInitialVisibility());
        }
    }

    private IEnumerator HandleInitialVisibility()
    {
        // Hide the mesh immediately on spawn
        arrowMesh.SetActive(false);

        // Wait for the specified duration
        yield return new WaitForSeconds(invisibilityDuration);

        // Show the mesh again
        arrowMesh.SetActive(true);
    }

    private void FixedUpdate()
    {
        if (hasStuck || rb == null) return;

        if (rb.linearVelocity.sqrMagnitude > 0.5f)
        {
            Vector3 velocityDir = rb.linearVelocity.normalized;
            Quaternion targetRotation = Quaternion.FromToRotation(modelForwardOffset, velocityDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * 20f);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player")) return;
        if (collision.transform.root.CompareTag("Player")) return;

        Stick(collision);
    }

    private void Stick(Collision collision)
    {
        hasStuck = true;
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.SetParent(collision.transform);
    }
}
