using UnityEngine;
using UnityEditor;

public class ReplaceBuildingModels : EditorWindow
{
    [MenuItem("Tools/Replace Building Models")]
    public static void ReplaceModels()
    {
        // Mapping: GameObject name -> FBX path
        var buildingMappings = new System.Collections.Generic.Dictionary<string, string>
        {
            // Root level buildings
            { "House1", "Assets/Art/Environments/Buildings/House_1.fbx" },
            { "House2", "Assets/Art/Environments/Buildings/House_2.fbx" },
            { "Inn", "Assets/Art/Environments/Buildings/Inn.fbx" },
            { "Blacksmith", "Assets/Art/Environments/Buildings/Blacksmith.fbx" },

            // Village buildings
            { "Maison_Forgeron", "Assets/Art/Environments/Buildings/Blacksmith.fbx" },
            { "Maison_Marchand", "Assets/Art/Environments/Buildings/House_3.fbx" },
            { "Taverne", "Assets/Art/Environments/Buildings/Inn.fbx" },
            { "Maison_Mage", "Assets/Art/Environments/Buildings/House_4.fbx" },
            { "Maison_Chef", "Assets/Art/Environments/Buildings/House_2.fbx" }
        };

        int replacedCount = 0;

        foreach (var mapping in buildingMappings)
        {
            string buildingName = mapping.Key;
            string fbxPath = mapping.Value;

            GameObject building = GameObject.Find(buildingName);
            if (building == null)
            {
                Debug.LogWarning($"Building '{buildingName}' not found in scene");
                continue;
            }

            // Load the FBX model
            GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxPrefab == null)
            {
                Debug.LogError($"FBX not found at path: {fbxPath}");
                continue;
            }

            // Get the mesh from the FBX (usually in the first child or root)
            MeshFilter fbxMeshFilter = fbxPrefab.GetComponentInChildren<MeshFilter>();
            if (fbxMeshFilter == null || fbxMeshFilter.sharedMesh == null)
            {
                Debug.LogError($"No mesh found in FBX: {fbxPath}");
                continue;
            }

            // Replace the mesh
            MeshFilter buildingMeshFilter = building.GetComponent<MeshFilter>();
            if (buildingMeshFilter == null)
            {
                buildingMeshFilter = building.AddComponent<MeshFilter>();
            }
            buildingMeshFilter.sharedMesh = fbxMeshFilter.sharedMesh;

            // Ensure MeshRenderer exists
            MeshRenderer buildingRenderer = building.GetComponent<MeshRenderer>();
            if (buildingRenderer == null)
            {
                buildingRenderer = building.AddComponent<MeshRenderer>();
            }

            // Copy materials from FBX
            MeshRenderer fbxRenderer = fbxPrefab.GetComponentInChildren<MeshRenderer>();
            if (fbxRenderer != null && fbxRenderer.sharedMaterials != null)
            {
                buildingRenderer.sharedMaterials = fbxRenderer.sharedMaterials;
            }

            // Adjust transform for Blender FBX
            // These models are correctly oriented in Unity (Y-up)
            building.transform.localRotation = Quaternion.identity;
            // Scale up to appropriate game size
            building.transform.localScale = new Vector3(5f, 5f, 5f);

            // Add BoxCollider if missing
            if (building.GetComponent<BoxCollider>() == null)
            {
                building.AddComponent<BoxCollider>();
            }

            replacedCount++;
            Debug.Log($"✓ Replaced '{buildingName}' with {fbxPath}");
        }

        Debug.Log($"<b>Building replacement complete!</b> {replacedCount}/{buildingMappings.Count} buildings updated.");
        EditorUtility.DisplayDialog("Building Models Replaced",
            $"{replacedCount} buildings have been updated with 3D models.", "OK");
    }
}
