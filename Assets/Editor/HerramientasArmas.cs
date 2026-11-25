using UnityEditor;
using UnityEngine;

//Herramientas de editor para las armas.
//"Calcular torso de apuntado": reproduce el clip de apuntar de cada arma sobre el personaje y guarda
//en su ItemData hacia donde mira el torso (hueso Spine). WeaponAnimator lo usa para que, con las piernas caminando,
//el torso adopte la misma postura que en el clip. Hay que repetirlo si se regeneran las animaciones.
public static class HerramientasArmas
{
    const string CharacterPrefabPath = "Assets/Prefabs/guardabosque_B.prefab";

    [MenuItem("Guardabosque/Calcular torso de apuntado de las armas")]
    static void CalculateAimChestRotation()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
        if (prefab == null)
        {
            Debug.LogError("No se encuentra el personaje en " + CharacterPrefabPath);
            return;
        }

        var character = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        character.hideFlags = HideFlags.HideAndDontSave;
        var animator = character.GetComponentInChildren<Animator>();
        Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        int count = 0;

        AnimationMode.StartAnimationMode();
        try
        {
            foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null || item.type != ItemType.Arma || item.animApuntar == null) continue;

                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(character, item.animApuntar, 0f);
                AnimationMode.EndSampling();

                Quaternion local = Quaternion.Inverse(animator.transform.rotation) * spine.rotation;
                Undo.RecordObject(item, "Calcular torso de apuntado");
                item.aimChestRotation = local.eulerAngles;
                EditorUtility.SetDirty(item);
                Debug.Log(string.Format("{0}: torso de apuntado {1}", item.name, item.aimChestRotation));
                count++;
            }
        }
        finally
        {
            AnimationMode.StopAnimationMode();
            Object.DestroyImmediate(character);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Torso de apuntado calculado para " + count + " armas");
    }
}
