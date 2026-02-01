using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public class CreatePlayerAnimatorController : MonoBehaviour
{
    [MenuItem("EpicLegends/Create Player Animator Controller")]
    private static void CreateAnimator()
    {
        string path = "Assets/Animations/Player/PlayerAnimator.controller";
        
        // Vérifier si le dossier existe
        if (!AssetDatabase.IsValidFolder("Assets/Animations"))
            AssetDatabase.CreateFolder("Assets", "Animations");
        if (!AssetDatabase.IsValidFolder("Assets/Animations/Player"))
            AssetDatabase.CreateFolder("Assets/Animations", "Player");
        
        // Créer l'AnimatorController
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        
        // Ajouter les paramètres
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsSprinting", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        
        // Récupérer le layer par défaut
        var rootStateMachine = controller.layers[0].stateMachine;
        
        // Créer les états
        var idleState = rootStateMachine.AddState("Idle");
        var walkState = rootStateMachine.AddState("Walk");
        var runState = rootStateMachine.AddState("Run");
        var jumpState = rootStateMachine.AddState("Jump");
        
        // Définir Idle comme état par défaut
        rootStateMachine.defaultState = idleState;
        
        // Transitions: Idle <-> Walk
        var idleToWalk = idleState.AddTransition(walkState);
        idleToWalk.hasExitTime = false;
        idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        
        var walkToIdle = walkState.AddTransition(idleState);
        walkToIdle.hasExitTime = false;
        walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        
        // Transitions: Walk <-> Run
        var walkToRun = walkState.AddTransition(runState);
        walkToRun.hasExitTime = false;
        walkToRun.AddCondition(AnimatorConditionMode.If, 0, "IsSprinting");
        
        var runToWalk = runState.AddTransition(walkState);
        runToWalk.hasExitTime = false;
        runToWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSprinting");
        
        // Transition: Any State -> Jump
        var anyStateToJump = rootStateMachine.AddAnyStateTransition(jumpState);
        anyStateToJump.hasExitTime = false;
        anyStateToJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");
        anyStateToJump.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        
        // Transition: Jump -> Idle (retour automatique)
        var jumpToIdle = jumpState.AddTransition(idleState);
        jumpToIdle.hasExitTime = true;
        jumpToIdle.exitTime = 0.9f;
        
        AssetDatabase.SaveAssets();
        
        Debug.Log($"PlayerAnimator.controller créé avec succès à {path}");
    }
}