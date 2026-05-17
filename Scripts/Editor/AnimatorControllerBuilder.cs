// ============================================================
//  AnimatorControllerBuilder.cs  —  Assets/Scripts/Editor/
//  Unity menu → Tools → Build Player Animator Controller
// ============================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Auto-creates a PlayerAnimator.controller with all states and
/// parameters that PlayerController.cs expects.
/// After running, assign the controller + character Animator to the Player GO.
/// </summary>
public static class AnimatorControllerBuilder
{
    private const string SAVE_PATH = "Assets/Animations/PlayerAnimator.controller";

    [MenuItem("Tools/Build Player Animator Controller")]
    public static void Build()
    {
        // ── ensure folder ──────────────────────────────────────
        if (!AssetDatabase.IsValidFolder("Assets/Animations"))
            AssetDatabase.CreateFolder("Assets", "Animations");

        // ── create controller ──────────────────────────────────
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(SAVE_PATH);

        // ── parameters ─────────────────────────────────────────
        ctrl.AddParameter("runSpeed",   AnimatorControllerParameterType.Float);
        ctrl.AddParameter("isGrounded", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("dodgeLeft",  AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("dodgeRight", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("roll",       AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("jump2",      AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("airDodgeLeft",  AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("airDodgeRight", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("caught",     AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("death",      AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("introRun",   AnimatorControllerParameterType.Trigger);

        // ── base layer state machine ───────────────────────────
        var sm = ctrl.layers[0].stateMachine;
        sm.entryPosition  = new Vector3(-200,  0);
        sm.anyStatePosition = new Vector3(-200, 80);
        sm.exitPosition   = new Vector3(-200, 160);

        // helper: add a state with a clip loaded from Assets/Animations/
        AnimatorState AddState(string stateName, string clipPath, Vector3 pos)
        {
            var s = sm.AddState(stateName, pos);
            s.name = stateName;
            if (!string.IsNullOrEmpty(clipPath))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip != null) s.motion = clip;
                else Debug.LogWarning($"[AnimCtrl] Clip not found: {clipPath} — assign manually.");
            }
            return s;
        }

        // helper: add Any-State → state transition
        void AnyToState(AnimatorState target, string param, bool isTrigger)
        {
            var t = sm.AddAnyStateTransition(target);
            t.hasExitTime = false;
            t.duration    = 0.1f;
            if (isTrigger)
                t.AddCondition(AnimatorConditionMode.If, 0, param);
            else
                t.AddCondition(AnimatorConditionMode.IfNot, 0, param);
        }

        // ── states (paths relative to Assets/Animations/) ──────
        // Swap these paths for your actual Mixamo FBX clip paths
        var sRun        = AddState("run",        "Assets/Animations/Anim_Run.fbx",         new Vector3(200,   0));
        var sJump       = AddState("jump2",       "Assets/Animations/Anim_Jump.fbx",        new Vector3(200, -80));
        var sRoll       = AddState("roll",        "Assets/Animations/Anim_Roll.fbx",        new Vector3(200, -160));
        var sDodgeL     = AddState("dodgeLeft",   "Assets/Animations/Anim_DodgeLeft.fbx",   new Vector3(200, -240));
        var sDodgeR     = AddState("dodgeRight",  "Assets/Animations/Anim_DodgeRight.fbx",  new Vector3(200, -320));
        var sAirL       = AddState("airDodgeLeft","Assets/Animations/Anim_AirDodgeLeft.fbx",new Vector3(400, -240));
        var sAirR       = AddState("airDodgeRight","Assets/Animations/Anim_AirDodgeRight.fbx",new Vector3(400,-320));
        var sCaught     = AddState("catch",       "Assets/Animations/Anim_Caught.fbx",      new Vector3(200, -400));
        var sDeath      = AddState("death",       "Assets/Animations/Anim_Death.fbx",       new Vector3(200, -480));
        var sIntroRun   = AddState("introRun",    "Assets/Animations/Anim_IntroRun.fbx",    new Vector3(200,  80));

        // default state = run
        sm.defaultState = sRun;

        // ── Any-State triggers ──────────────────────────────────
        AnyToState(sJump,   "jump2",      true);
        AnyToState(sRoll,   "roll",       true);
        AnyToState(sDodgeL, "dodgeLeft",  true);
        AnyToState(sDodgeR, "dodgeRight", true);
        AnyToState(sAirL,   "airDodgeLeft",  true);
        AnyToState(sAirR,   "airDodgeRight", true);
        AnyToState(sCaught, "caught",     true);
        AnyToState(sDeath,  "death",      true);
        AnyToState(sIntroRun,"introRun",  true);

        // ── return to run after each action ────────────────────
        foreach (var src in new[] { sJump, sRoll, sDodgeL, sDodgeR, sAirL, sAirR, sIntroRun })
        {
            var t = src.AddTransition(sRun);
            t.hasExitTime = true;
            t.exitTime    = 0.85f;
            t.duration    = 0.15f;
        }

        // runSpeed drives blend in run state (set it on the run clip directly)
        // isGrounded bool — used by PlayerController but doesn't need a transition

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[AnimCtrl] ✅ Created {SAVE_PATH}\n" +
                  "Next steps:\n" +
                  "  1. Import Mixamo FBX files into Assets/Animations/\n" +
                  "  2. Re-run this tool (clips auto-assign by path)\n" +
                  "  3. Add Animator component to Player → assign this controller\n" +
                  "  4. Set Avatar from the character FBX (Humanoid rig)");
    }
}
#endif
