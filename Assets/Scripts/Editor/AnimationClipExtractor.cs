using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Utilitaire pour extraire et configurer les clips d'animation des fichiers FBX.
/// Menu: EpicLegends > Animation > Setup Animation Clips
/// </summary>
public class AnimationClipExtractor : EditorWindow
{
    [MenuItem("EpicLegends/Animation/Setup Player Animations")]
    public static void SetupPlayerAnimations()
    {
        string fbxPath = "Assets/Art/Characters/Player/Warrior.fbx";
        
        if (!File.Exists(fbxPath))
        {
            Debug.LogError($"[AnimationClipExtractor] FBX non trouvé: {fbxPath}");
            return;
        }

        ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[AnimationClipExtractor] Impossible d'obtenir l'importer pour: {fbxPath}");
            return;
        }

        // Configurer pour animation Generic (les modèles Quaternius ne sont pas Humanoid)
        importer.animationType = ModelImporterAnimationType.Generic;
        
        // Définir les clips d'animation basiques
        // Ces valeurs sont approximatives - ajuster selon le FBX réel
        var clips = new List<ModelImporterClipAnimation>();
        
        // Clip Idle (supposé frames 0-30)
        var idleClip = new ModelImporterClipAnimation();
        idleClip.name = "Idle";
        idleClip.takeName = "mixamo.com"; // Nom de la take dans le FBX
        idleClip.firstFrame = 0;
        idleClip.lastFrame = 30;
        idleClip.loopTime = true;
        idleClip.wrapMode = WrapMode.Loop;
        clips.Add(idleClip);

        // Clip Walk (supposé frames 31-60)
        var walkClip = new ModelImporterClipAnimation();
        walkClip.name = "Walk";
        walkClip.takeName = "mixamo.com";
        walkClip.firstFrame = 31;
        walkClip.lastFrame = 60;
        walkClip.loopTime = true;
        walkClip.wrapMode = WrapMode.Loop;
        clips.Add(walkClip);

        // Clip Run (supposé frames 61-90)
        var runClip = new ModelImporterClipAnimation();
        runClip.name = "Run";
        runClip.takeName = "mixamo.com";
        runClip.firstFrame = 61;
        runClip.lastFrame = 90;
        runClip.loopTime = true;
        runClip.wrapMode = WrapMode.Loop;
        clips.Add(runClip);

        importer.clipAnimations = clips.ToArray();
        importer.SaveAndReimport();

        Debug.Log($"[AnimationClipExtractor] Configuration terminée pour {fbxPath}");
        Debug.Log("[AnimationClipExtractor] IMPORTANT: Vérifiez les frames dans l'inspecteur du FBX et ajustez si nécessaire!");
    }

    [MenuItem("EpicLegends/Animation/List FBX Animations")]
    public static void ListFBXAnimations()
    {
        string[] fbxPaths = new string[]
        {
            "Assets/Art/Characters/Player/Warrior.fbx",
            "Assets/Art/Characters/Mixamo/Kachujin.fbx",
            "Assets/Art/Characters/Mixamo/XBot.fbx"
        };

        foreach (string path in fbxPaths)
        {
            if (!File.Exists(path)) continue;

            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            Debug.Log($"=== {path} ===");
            Debug.Log($"Animation Type: {importer.animationType}");
            Debug.Log($"Import Animation: {importer.importAnimation}");

            // Lister les animations existantes
            var animations = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var anim in animations)
            {
                if (anim is AnimationClip clip)
                {
                    Debug.Log($"  - Clip: {clip.name}, Length: {clip.length}s, FrameRate: {clip.frameRate}");
                }
            }
        }
    }

    [MenuItem("EpicLegends/Animation/Assign Animator to Player")]
    public static void AssignAnimatorToPlayer()
    {
        // Trouver le Player dans la scène
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("[AnimationClipExtractor] Player non trouvé dans la scène!");
            return;
        }

        // Trouver ou créer le PlayerModel
        Transform playerModel = player.transform.Find("PlayerModel");
        if (playerModel == null)
        {
            Debug.LogError("[AnimationClipExtractor] PlayerModel non trouvé sous Player!");
            return;
        }

        // Ajouter ou récupérer l'Animator
        Animator animator = playerModel.GetComponent<Animator>();
        if (animator == null)
        {
            animator = playerModel.gameObject.AddComponent<Animator>();
            Debug.Log("[AnimationClipExtractor] Animator ajouté à PlayerModel");
        }

        // Charger et assigner le controller
        string controllerPath = "Assets/Animations/Player/PlayerAnimator.controller";
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
        
        if (controller != null)
        {
            animator.runtimeAnimatorController = controller;
            Debug.Log($"[AnimationClipExtractor] Controller assigné: {controllerPath}");
        }
        else
        {
            Debug.LogWarning($"[AnimationClipExtractor] Controller non trouvé: {controllerPath}");
            Debug.LogWarning("[AnimationClipExtractor] Exécutez d'abord: EpicLegends > Create Player Animator Controller");
        }

        // Marquer la scène comme modifiée
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[AnimationClipExtractor] Configuration terminée!");
    }
}
