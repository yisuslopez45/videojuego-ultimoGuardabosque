using UnityEngine;
using UnityEngine.AI;

//Area donde viven los osos. Se ve como un rectangulo naranja en la vista Scene
public class ZonaOsos : MonoBehaviour
{
    public Vector3 size = new Vector3(56f, 40f, 48f);

    public bool Contains(Vector3 point)
    {
        Vector3 local = transform.InverseTransformPoint(point);
        return Mathf.Abs(local.x) <= size.x / 2f && Mathf.Abs(local.z) <= size.z / 2f;
    }

    //Devuelve el punto mas cercano dentro de la zona
    public Vector3 ClampPoint(Vector3 point)
    {
        Vector3 local = transform.InverseTransformPoint(point);
        local.x = Mathf.Clamp(local.x, -size.x / 2f, size.x / 2f);
        local.z = Mathf.Clamp(local.z, -size.z / 2f, size.z / 2f);
        return transform.TransformPoint(local);
    }

    //Busca un punto aleatorio de la zona que este sobre el NavMesh
    public bool RandomPoint(out Vector3 result, int attempts = 20)
    {
        for (int i = 0; i < attempts; i++)
        {
            Vector3 local = new Vector3(
                Random.Range(-size.x / 2f, size.x / 2f),
                0f,
                Random.Range(-size.z / 2f, size.z / 2f));
            Vector3 point = transform.TransformPoint(local);
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, size.y / 2f, NavMesh.AllAreas) && Contains(hit.position))
            {
                result = hit.position;
                return true;
            }
        }
        result = transform.position;
        return false;
    }

    void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
        Gizmos.DrawCube(Vector3.zero, size);
        Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
        Gizmos.DrawWireCube(Vector3.zero, size);
    }
}
