using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Configure les animations du Warrior.fbx avec les bons paramètres de loop.
/// Les animations Idle, Walk, Run doivent boucler. Les autres (Attack, Death, Hit) non.
/// </summary>
public class ConfigureAnimationLoops : MonoBehaviour
{
    [MenuItem("EpicLegends/Animation/Configure Animation Loops")]
    public static void ConfigureLoops()
    {
        string fbxPath = "Assets/Art/Characters/Player/Warrior.fbx";
        
        ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[ConfigureAnimationLoops] Impossible de charger: {fbxPath}");
            return;
        }

        // Récupérer les clips existants ou les créer
        ModelImporterClipAnimation[] sourceClips = importer.defaultClipAnimations;
        
        if (sourceClips == null || sourceClips.Length == 0)
        {
            Debug.LogError("[ConfigureAnimationLoops] Aucun clip trouvé dans le FBX!");
            return;
        }

        Debug.Log($"[ConfigureAnimationLoops] {sourceClips.Length} clips trouvés");

        // Liste des animations qui doivent boucler
        HashSet<string> loopingAnimations = new HashSet<string>
        {
            "Idle", "Idle_Weapon", "Idle_Attacking",
            "Walk", 
            "Run", "Run_Weapon"
        };

        // Configurer chaque clip
        List<ModelImporterClipAnimation> configuredClips = new List<ModelImporterClipAnimation>();
        
        foreach (var sourceClip in sourceClips)
        {
            ModelImporterClipAnimation clip = new ModelImporterClipAnimation();
            clip.name = sourceClip.name;
            clip.takeName = sourceClip.takeName;
            clip.firstFrame = sourceClip.firstFrame;
            clip.lastFrame = sourceClip.lastFrame;
            
            // Vérifier si cette animation doit boucler
            bool shouldLoop = false;
            foreach (string loopAnim in loopingAnimations)
            {
                if (sourceClip.name.Contains(loopAnim))
                {
                    shouldLoop = true;
                    break;
                }
            }
            
            clip.loopTime = shouldLoop;
            clip.loopPose = shouldLoop;
            
            // Paramètres de qualité
            clip.keepOriginalOrientation = true;
            clip.keepOriginalPositionXZ = true;
            clip.keepOriginalPositionY = true;
            clip.lockRootRotation = false;
            clip.lockRootHeightY = false;
            clip.lockRootPositionXZ = false;
            
            configuredClips.Add(clip);
            
            string loopStatus = shouldLoop ? "LOOP" : "ONCE";
            Debug.Log($"  [{loopStatus}] {clip.name}");
        }

        // Appliquer les clips configurés
        importer.clipAnimations = configuredClips.ToArray();
        
        // Sauvegarder et réimporter
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        
        Debug.Log("[ConfigureAnimationLoops] Configuration terminée! Les clips sont maintenant configurés avec loop.");
    }
}
