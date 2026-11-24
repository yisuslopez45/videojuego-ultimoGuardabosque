using System.Collections;
using UnityEngine;

//Vida basica para objetos que reciben disparos. Parpadea en rojo al recibir daño y,
//al llegar a cero, cae por fisica y desaparece (o reaparece si respawnTime > 0).
public class Damageable : MonoBehaviour, IDamageable
{
    public float maxHealth = 100f;
    [Tooltip("Segundos hasta reaparecer tras ser destruido (0 = no reaparece)")]
    public float respawnTime = 4f;
    public Color hitColor = new Color(1f, 0.3f, 0.2f);

    public float Health { get; private set; }
    public bool IsDead { get; private set; }

    private Renderer[] renderers;
    private MaterialPropertyBlock block;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Rigidbody body;
    private bool hadRigidbody;

    void Awake()
    {
        Health = maxHealth;
        renderers = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
        startPosition = transform.position;
        startRotation = transform.rotation;
        body = GetComponent<Rigidbody>();
        hadRigidbody = body != null;
    }

    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (IsDead) return;
        Health -= amount;
        StopCoroutine(nameof(Flash));
        StartCoroutine(nameof(Flash));
        if (Health <= 0f) Die(hitPoint, hitDirection);
    }

    void Die(Vector3 hitPoint, Vector3 hitDirection)
    {
        IsDead = true;
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = false;
        body.AddForceAtPosition(hitDirection.normalized * 4f, hitPoint, ForceMode.Impulse);
        if (respawnTime > 0f) StartCoroutine(Respawn());
        else Destroy(gameObject, 5f);
    }

    IEnumerator Respawn()
    {
        yield return new WaitForSeconds(respawnTime);
        if (!hadRigidbody) { Destroy(body); body = null; }
        else { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
        transform.SetPositionAndRotation(startPosition, startRotation);
        Health = maxHealth;
        IsDead = false;
    }

    IEnumerator Flash()
    {
        block.Clear();
        block.SetColor("_BaseColor", hitColor);
        foreach (var r in renderers) r.SetPropertyBlock(block);
        yield return new WaitForSeconds(0.08f);

        //Quitar el tinte: cada pieza vuelve al color de su material
        foreach (var r in renderers) r.SetPropertyBlock(null);
    }
}
