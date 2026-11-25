using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

//Menu: Herramientas > Hornear NavMesh Osos
//Hornea el NavMesh de la zona de los osos dejando el agua como no caminable.
//El agua no tiene collider, asi que se le agrega uno temporal solo durante el horneado.
public static class HornearNavMeshOsos
{
    const int NotWalkableArea = 1;

    [MenuItem("Herramientas/Hornear NavMesh Osos")]
    public static void Hornear()
    {
        ZonaOsos zona = Object.FindFirstObjectByType<ZonaOsos>();
        if (zona == null)
        {
            Debug.LogError("No hay ningun objeto con ZonaOsos en la escena");
            return;
        }

        NavMeshSurface surface = zona.GetComponent<NavMeshSurface>();
        if (surface == null) surface = zona.gameObject.AddComponent<NavMeshSurface>();

        //Solo se hornea el volumen de la zona (con margen) usando los colliders de la escena
        surface.collectObjects = CollectObjects.Volume;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        surface.center = Vector3.zero;
        surface.size = zona.size + new Vector3(10f, 0f, 10f);

        //Colliders temporales en el agua, marcados como no caminables
        var temporales = new List<Component>();
        foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            if (!mf.gameObject.name.StartsWith("Agua") || mf.GetComponent<Collider>() != null) continue;
            MeshCollider col = mf.gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = mf.sharedMesh;
            NavMeshModifier mod = mf.gameObject.AddComponent<NavMeshModifier>();
            mod.overrideArea = true;
            mod.area = NotWalkableArea;
            temporales.Add(col);
            temporales.Add(mod);
        }

        try
        {
            surface.BuildNavMesh();
        }
        finally
        {
            foreach (Component c in temporales) Object.DestroyImmediate(c);
        }

        //Guarda el NavMesh como asset junto a la escena
        var scene = zona.gameObject.scene;
        string folder = System.IO.Path.ChangeExtension(scene.path, null);
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(scene.path), System.IO.Path.GetFileName(folder));
        }
        string assetPath = folder + "/NavMesh-ZonaOsos.asset";
        AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(surface.navMeshData, assetPath);
        AssetDatabase.SaveAssets();

        EditorUtility.SetDirty(surface);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("NavMesh de los osos horneado en " + assetPath);
    }
}
