using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Linq;

/// <summary>
/// Configure les états de l'AnimatorController avec les vrais clips d'animation du Warrior.fbx
/// </summary>
public class SetupPlayerAnimatorStates : MonoBehaviour
{
    [MenuItem("EpicLegends/Animation/Setup Player Animator States")]
    public static void SetupStates()
    {
        string controllerPath = "Assets/Animations/Player/PlayerAnimator.controller";
        string fbxPath = "Assets/Art/Characters/Player/Warrior.fbx";

        // Charger le controller
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            Debug.LogError($"[SetupPlayerAnimatorStates] Controller non trouvé: {controllerPath}");
            Debug.LogError("[SetupPlayerAnimatorStates] Exécutez d'abord: EpicLegends > Create Player Animator Controller");
            return;
        }

        // Charger les clips d'animation depuis le FBX
        var allAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        var clips = allAssets.OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToList();

        if (clips.Count == 0)
        {
            Debug.LogError($"[SetupPlayerAnimatorStates] Aucun clip d'animation trouvé dans: {fbxPath}");
            return;
        }

        Debug.Log($"[SetupPlayerAnimatorStates] {clips.Count} clips trouvés:");
        foreach (var clip in clips)
        {
            Debug.Log($"  - {clip.name}");
        }

        // Récupérer le state machine
        var rootStateMachine = controller.layers[0].stateMachine;

        // Trouver les états existants
        var states = rootStateMachine.states;

        // Mapper les clips aux états
        foreach (var stateInfo in states)
        {
            var state = stateInfo.state;
            AnimationClip matchingClip = null;

            switch (state.name)
            {
                case "Idle":
                    matchingClip = clips.FirstOrDefault(c => c.name.Contains("Idle") && !c.name.Contains("Weapon") && !c.name.Contains("Attacking"));
                    break;
                case "Walk":
                    matchingClip = clips.FirstOrDefault(c => c.name.Contains("Walk"));
                    break;
                case "Run":
                    matchingClip = clips.FirstOrDefault(c => c.name.Contains("Run") && !c.name.Contains("Weapon"));
                    break;
                case "Jump":
                    // Utiliser Roll comme animation de saut (pas de Jump dans ce FBX)
                    matchingClip = clips.FirstOrDefault(c => c.name.Contains("Roll"));
                    break;
                case "Attack":
                    matchingClip = clips.FirstOrDefault(c => c.name.Contains("Sword_Attack") && !c.name.Contains("Fast"));
                    break;
            }

            if (matchingClip != null)
            {
                state.motion = matchingClip;
                Debug.Log($"[SetupPlayerAnimatorStates] État '{state.name}' -> Clip '{matchingClip.name}'");
            }
            else
            {
                Debug.LogWarning($"[SetupPlayerAnimatorStates] Aucun clip trouvé pour l'état '{state.name}'");
            }
        }

        // Ajouter l'état Attack s'il n'existe pas
        bool hasAttackState = states.Any(s => s.state.name == "Attack");
        if (!hasAttackState)
        {
            var attackState = rootStateMachine.AddState("Attack");
            var attackClip = clips.FirstOrDefault(c => c.name.Contains("Sword_Attack") && !c.name.Contains("Fast"));
            if (attackClip != null)
            {
                attackState.motion = attackClip;
                Debug.Log($"[SetupPlayerAnimatorStates] État 'Attack' créé -> Clip '{attackClip.name}'");
            }

            // Ajouter transition depuis Any State vers Attack
            var anyToAttack = rootStateMachine.AddAnyStateTransition(attackState);
            anyToAttack.hasExitTime = false;
            anyToAttack.duration = 0.1f;
            anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");

            // Transition de retour vers Idle
            var attackToIdle = attackState.AddTransition(rootStateMachine.defaultState);
            attackToIdle.hasExitTime = true;
            attackToIdle.exitTime = 0.9f;
            attackToIdle.duration = 0.1f;
        }

        // Ajouter l'état Death s'il n'existe pas
        bool hasDeathState = states.Any(s => s.state.name == "Death");
        if (!hasDeathState)
        {
            var deathState = rootStateMachine.AddState("Death");
            var deathClip = clips.FirstOrDefault(c => c.name.Contains("Death"));
            if (deathClip != null)
            {
                deathState.motion = deathClip;
                Debug.Log($"[SetupPlayerAnimatorStates] État 'Death' créé -> Clip '{deathClip.name}'");
            }

            // Ajouter le paramètre Death s'il n'existe pas
            bool hasDeathParam = controller.parameters.Any(p => p.name == "Death");
            if (!hasDeathParam)
            {
                controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);
            }

            // Transition depuis Any State vers Death
            var anyToDeath = rootStateMachine.AddAnyStateTransition(deathState);
            anyToDeath.hasExitTime = false;
            anyToDeath.duration = 0.1f;
            anyToDeath.AddCondition(AnimatorConditionMode.If, 0, "Death");
        }

        // Ajouter l'état Hit s'il n'existe pas
        bool hasHitState = states.Any(s => s.state.name == "Hit");
        if (!hasHitState)
        {
            var hitState = rootStateMachine.AddState("Hit");
            var hitClip = clips.FirstOrDefault(c => c.name.Contains("RecieveHit") && !c.name.Contains("_2"));
            if (hitClip != null)
            {
                hitState.motion = hitClip;
                Debug.Log($"[SetupPlayerAnimatorStates] État 'Hit' créé -> Clip '{hitClip.name}'");
            }

            // Ajouter le paramètre Hit s'il n'existe pas
            bool hasHitParam = controller.parameters.Any(p => p.name == "Hit");
            if (!hasHitParam)
            {
                controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            }

            // Transition depuis Any State vers Hit
            var anyToHit = rootStateMachine.AddAnyStateTransition(hitState);
            anyToHit.hasExitTime = false;
            anyToHit.duration = 0.05f;
            anyToHit.AddCondition(AnimatorConditionMode.If, 0, "Hit");

            // Transition de retour vers Idle
            var hitToIdle = hitState.AddTransition(rootStateMachine.defaultState);
            hitToIdle.hasExitTime = true;
            hitToIdle.exitTime = 0.9f;
            hitToIdle.duration = 0.1f;
        }

        // Sauvegarder
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log("[SetupPlayerAnimatorStates] Configuration terminée!");
        Debug.Log("[SetupPlayerAnimatorStates] États configurés: Idle, Walk, Run, Jump(Roll), Attack, Death, Hit");
    }
}
