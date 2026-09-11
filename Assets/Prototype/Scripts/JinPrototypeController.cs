using System;
using System.Collections.Generic;
using Combat;
using UnityEngine;

namespace FightingGame.Prototype
{
    /// <summary>
    /// Compatibility-facing coordinator and temporary procedural presentation for Jin.
    /// Input, locomotion, and combat rules live in dedicated fighter components.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PrototypeFighterInput), typeof(PrototypeFighterCombat), typeof(PrototypeFighterMotor))]
    public sealed class JinPrototypeController : MonoBehaviour
    {
        [Header("Fighter")]
        [SerializeField] private Transform opponentTarget;
        [SerializeField] private bool playerControlled = true;
        [SerializeField] private PrototypeControlProfile controlProfile = PrototypeControlProfile.PlayerOne;
        [SerializeField] private bool showControlHelp = true;

        [Header("Procedural pose")]
        [SerializeField, Min(0f)] private float idleBreathSpeed = 1.7f;
        [SerializeField, Range(0f, 0.04f)] private float idleBobAmount = 0.008f;
        [SerializeField, Range(0f, 1f)] private float idleMotionScale = 0.18f;
        [SerializeField, Range(0f, 1f)] private float stepAmount = 0.28f;

        private Animator animator;
        private HumanPoseHandler poseHandler;
        private HumanPose pose;
        private float[] baseMuscles;
        private Vector3 baseBodyPosition;
        private Quaternion baseBodyRotation;
        private readonly Dictionary<string, int> muscleIndices = new Dictionary<string, int>();
        private readonly Dictionary<string, Transform> fallbackBones = new Dictionary<string, Transform>();
        private readonly Dictionary<string, Quaternion> fallbackRotations = new Dictionary<string, Quaternion>();
        private Transform fallbackHips;
        private Vector3 fallbackHipsPosition;
        private bool humanoidPoseAvailable;
        private PrototypeFighterInput fighterInput;
        private PrototypeFighterCombat fighterCombat;
        private PrototypeFighterMotor fighterMotor;
        private bool controlsEnabled = true;
        private PrototypeDummyOpponent selfReaction;
        private PrototypeInputFrame tickInput;

        public float MoveInput { get { return fighterMotor != null ? fighterMotor.MoveInput : 0f; } }
        public float SideInput { get { return fighterMotor != null ? fighterMotor.SideInput : 0f; } }
        public bool IsMoving { get { return fighterMotor != null && fighterMotor.IsMoving; } }
        public bool IsRunning { get { return fighterMotor != null && fighterMotor.IsRunning; } }
        public bool IsBlocking { get { return fighterMotor != null && fighterMotor.IsBlocking; } }
        public bool IsBackdashing { get { return fighterMotor != null && fighterMotor.IsBackdashing; } }
        public bool PlayerControlled { get { return playerControlled; } }
        private float CurrentPlanarSpeed { get { return fighterMotor != null ? fighterMotor.CurrentPlanarSpeed : 0f; } }

        public void SetOpponent(Transform target)
        {
            opponentTarget = target;
            EnsureModules();
            fighterCombat.SetOpponent(target);
            fighterMotor.SetOpponent(target);
        }

        public void SetPlayerControlled(bool enabled)
        {
            playerControlled = enabled;
            SetControlsEnabled(enabled);
        }

        public void SetControlProfile(PrototypeControlProfile profile)
        {
            controlProfile = profile;
            EnsureModules();
            fighterInput.SetControlProfile(profile);
            fighterCombat.SetControlProfile(profile);
        }

        public void SetShowControlHelp(bool visible)
        {
            showControlHelp = visible;
        }

        public void SetControlsEnabled(bool enabled)
        {
            controlsEnabled = enabled;
            if (!enabled)
            {
                EnsureModules();
                fighterMotor.Stop();
            }
        }

        public void ResetRuntimeState()
        {
            EnsureModules();
            fighterMotor.ResetRuntimeState();
            fighterCombat.ResetRuntimeState();
            fighterInput.ClearPendingInput();
        }

        private void Awake()
        {
            EnsureModules();
            fighterInput.SetControlProfile(controlProfile);
            fighterCombat.Configure(opponentTarget, controlProfile);
            fighterMotor.SetOpponent(opponentTarget);
            controlsEnabled = playerControlled;
            animator = GetComponentInChildren<Animator>();
            selfReaction = GetComponent<PrototypeDummyOpponent>();
            CacheFallbackRig();
            TryInitializeHumanoidPose();
        }

        private void EnsureModules()
        {
            if (fighterInput == null)
            {
                fighterInput = GetComponent<PrototypeFighterInput>();
                if (fighterInput == null) fighterInput = gameObject.AddComponent<PrototypeFighterInput>();
            }
            if (fighterCombat == null)
            {
                fighterCombat = GetComponent<PrototypeFighterCombat>();
                if (fighterCombat == null) fighterCombat = gameObject.AddComponent<PrototypeFighterCombat>();
            }
            if (fighterMotor == null)
            {
                fighterMotor = GetComponent<PrototypeFighterMotor>();
                if (fighterMotor == null) fighterMotor = gameObject.AddComponent<PrototypeFighterMotor>();
            }
        }

        private void OnEnable()
        {
            CombatClock.Register(this);
            if (poseHandler == null)
            {
                TryInitializeHumanoidPose();
            }
        }

        private void OnDisable() { CombatClock.Unregister(this); }

        public void CaptureInput()
        {
            if (controlsEnabled) fighterInput.Capture();
            else fighterInput.ClearPendingInput();
        }

        public void PrepareCombatTick()
        {
            if (!controlsEnabled)
            {
                fighterInput.ClearPendingInput();
                if (!playerControlled && (selfReaction == null || !selfReaction.IsReacting))
                    fighterMotor.SnapFacingToOpponent();
                return;
            }
            tickInput = fighterInput.ConsumeTick();
            fighterMotor.PrepareFrame(tickInput, fighterCombat.IsAttacking);
            fighterCombat.Tick(tickInput.ForwardHeld, tickInput.AttackDirection,
                !fighterMotor.IsBlocking && !fighterMotor.IsBackdashing,
                fighterMotor.GetCurrentFighterState(tickInput));
        }

        public void MoveCombatTick()
        {
            if (!controlsEnabled) return;
            fighterMotor.Simulate(tickInput, fighterCombat.CurrentAttack,
                fighterCombat.TeepStepEnvelope, fighterCombat.OccupiedThisFrame);
        }

        public void ResolveCombatTick()
        {
            if (controlsEnabled) fighterCombat.ResolveHitbox();
        }

        public void FinishCombatTick()
        {
            if (controlsEnabled) fighterCombat.AdvanceMoveFrame();
        }

        private void LateUpdate()
        {
            if (!humanoidPoseAvailable || poseHandler == null || baseMuscles == null)
            {
                ApplyFallbackPose();
                ApplyFallbackBlock();
                ApplyFallbackAttack();
                return;
            }

            poseHandler.GetHumanPose(ref pose);
            Array.Copy(baseMuscles, pose.muscles, baseMuscles.Length);
            pose.bodyPosition = baseBodyPosition;
            pose.bodyRotation = baseBodyRotation;

            float movementBlend = Mathf.Clamp01(CurrentPlanarSpeed / fighterMotor.MoveSpeed);
            float idleWave = Mathf.Sin(Time.time * idleBreathSpeed * 0.55f * Mathf.PI * 2f) * idleMotionScale;
            float stepWave = Mathf.Sin(fighterMotor.StepClock * Mathf.PI * 2f);
            float oppositeStep = Mathf.Sin((fighterMotor.StepClock + 0.5f) * Mathf.PI * 2f);

            // Restrained breathing with the arms hanging naturally beside the torso.
            AddMuscle("Spine Front-Back", -0.04f + idleWave * 0.006f);
            AddMuscle("Chest Front-Back", -0.035f + idleWave * 0.005f);
            AddMuscle("Left Shoulder Down-Up", -0.16f);
            AddMuscle("Right Shoulder Down-Up", -0.16f);
            AddMuscle("Left Arm Down-Up", -0.74f);
            AddMuscle("Right Arm Down-Up", -0.74f);
            AddMuscle("Left Forearm Stretch", -0.10f);
            AddMuscle("Right Forearm Stretch", -0.10f);

            // Simple alternating walk cycle while translating on the fighting plane.
            float legSwing = stepWave * stepAmount * movementBlend;
            AddMuscle("Left Upper Leg Front-Back", legSwing);
            AddMuscle("Right Upper Leg Front-Back", -legSwing);
            AddMuscle("Left Lower Leg Stretch", -Mathf.Max(0f, oppositeStep) * 0.24f * movementBlend);
            AddMuscle("Right Lower Leg Stretch", -Mathf.Max(0f, stepWave) * 0.24f * movementBlend);
            AddMuscle("Left Arm Front-Back", -legSwing * 0.45f);
            AddMuscle("Right Arm Front-Back", legSwing * 0.45f);
            AddMuscle("Spine Twist Left-Right", stepWave * 0.035f * movementBlend);

            ApplyHumanoidBlock();
            ApplyHumanoidAttack();

            pose.bodyPosition.y += idleWave * idleBobAmount * 0.5f * (1f - movementBlend * 0.5f);
            poseHandler.SetHumanPose(ref pose);
        }

        private void TryInitializeHumanoidPose()
        {
            humanoidPoseAvailable = false;
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                Debug.LogWarning("Jin prototype: the imported avatar is not a valid Humanoid, so the controller is using its bone-based fallback pose.", this);
                return;
            }

            if (poseHandler != null)
            {
                poseHandler.Dispose();
            }

            poseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
            pose = new HumanPose();
            poseHandler.GetHumanPose(ref pose);
            baseMuscles = (float[])pose.muscles.Clone();
            baseBodyPosition = pose.bodyPosition;
            baseBodyRotation = pose.bodyRotation;

            muscleIndices.Clear();
            for (int i = 0; i < HumanTrait.MuscleName.Length; i++)
            {
                muscleIndices[HumanTrait.MuscleName[i]] = i;
            }

            humanoidPoseAvailable = true;
        }

        private void CacheFallbackRig()
        {
            string[] boneNames =
            {
                "spine lower", "spine upper",
                "arm left shoulder 2", "arm right shoulder 2",
                "arm left elbow", "arm right elbow", "arm left wrist", "arm right wrist",
                "leg left thigh", "leg right thigh",
                "leg left knee", "leg right knee", "leg left ankle", "leg right ankle"
            };

            fallbackBones.Clear();
            fallbackRotations.Clear();
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            foreach (Transform child in transforms)
            {
                string key = child.name.ToLowerInvariant();
                if (key == "root hips")
                {
                    fallbackHips = child;
                    fallbackHipsPosition = child.localPosition;
                }

                for (int i = 0; i < boneNames.Length; i++)
                {
                    if (key == boneNames[i])
                    {
                        fallbackBones[key] = child;
                        fallbackRotations[key] = child.localRotation;
                        break;
                    }
                }
            }
        }

        private void ApplyFallbackPose()
        {
            float movementBlend = Mathf.Clamp01(CurrentPlanarSpeed / fighterMotor.MoveSpeed);
            float idleWave = Mathf.Sin(Time.time * idleBreathSpeed * 0.55f * Mathf.PI * 2f) * idleMotionScale;
            float stepWave = Mathf.Sin(fighterMotor.StepClock * Mathf.PI * 2f);

            if (fallbackHips != null)
            {
                fallbackHips.localPosition = fallbackHipsPosition + Vector3.up * (idleWave * idleBobAmount * 0.5f);
            }

            SetFallbackRotation("spine lower", new Vector3(idleWave * 0.35f, 0f, stepWave * movementBlend * 1.5f));
            SetFallbackRotation("spine upper", new Vector3(idleWave * 0.25f, 0f, -stepWave * movementBlend * 1.5f));
            // Reset the arms to the imported bind pose before placing the hands in
            // world space. Fixed Euler offsets pitched this rig's arms forward because
            // its shoulder local axes do not match Unity's usual humanoid axes.
            SetFallbackRotation("arm left shoulder 2", Vector3.zero);
            SetFallbackRotation("arm right shoulder 2", Vector3.zero);
            SetFallbackRotation("arm left elbow", Vector3.zero);
            SetFallbackRotation("arm right elbow", Vector3.zero);
            ApplyFallbackRelaxedArm(
                "arm left shoulder 2", "arm left elbow", "arm left wrist",
                -1f, -stepWave * movementBlend * 0.08f);
            ApplyFallbackRelaxedArm(
                "arm right shoulder 2", "arm right elbow", "arm right wrist",
                1f, stepWave * movementBlend * 0.08f);
            SetFallbackRotation("leg left thigh", new Vector3(stepWave * movementBlend * 16f, 0f, 0f));
            SetFallbackRotation("leg right thigh", new Vector3(-stepWave * movementBlend * 16f, 0f, 0f));
            SetFallbackRotation("leg left knee", new Vector3(Mathf.Max(0f, -stepWave) * movementBlend * 18f, 0f, 0f));
            SetFallbackRotation("leg right knee", new Vector3(Mathf.Max(0f, stepWave) * movementBlend * 18f, 0f, 0f));
        }

        private void SetFallbackRotation(string boneName, Vector3 eulerOffset)
        {
            Transform bone;
            Quaternion baseRotation;
            if (fallbackBones.TryGetValue(boneName, out bone) && fallbackRotations.TryGetValue(boneName, out baseRotation))
            {
                bone.localRotation = baseRotation * Quaternion.Euler(eulerOffset);
            }
        }

        private void ApplyFallbackRelaxedArm(
            string shoulderName,
            string elbowName,
            string wristName,
            float sideSign,
            float forwardSwing)
        {
            Transform shoulder;
            Transform elbow;
            Transform wrist;
            if (!TryGetFallbackChain(shoulderName, elbowName, wristName, out shoulder, out elbow, out wrist))
            {
                return;
            }

            Vector3 handTarget = shoulder.position +
                                 Vector3.down * 0.48f +
                                 transform.right * sideSign * 0.1f +
                                 transform.forward * forwardSwing;
            Vector3 elbowPole = shoulder.position +
                                transform.right * sideSign * 0.38f +
                                transform.forward * 0.08f +
                                Vector3.down * 0.18f;
            ApplyTwoBoneIk(shoulder, elbow, wrist, handTarget, elbowPole);
        }

        private void ApplyHumanoidBlock()
        {
            if (!IsBlocking)
            {
                return;
            }

            AddMuscle("Left Arm Down-Up", 0.52f);
            AddMuscle("Right Arm Down-Up", 0.52f);
            AddMuscle("Left Arm Front-Back", -0.3f);
            AddMuscle("Right Arm Front-Back", -0.3f);
            AddMuscle("Left Forearm Stretch", -0.52f);
            AddMuscle("Right Forearm Stretch", -0.52f);
            AddMuscle("Spine Front-Back", -0.06f);
        }

        private void ApplyFallbackBlock()
        {
            if (!IsBlocking)
            {
                return;
            }

            Transform root;
            Transform mid;
            Transform end;

            if (TryGetFallbackChain("arm left shoulder 2", "arm left elbow", "arm left wrist", out root, out mid, out end))
            {
                Vector3 target = root.position + transform.forward * 0.34f + Vector3.up * 0.13f + transform.right * 0.08f;
                Vector3 pole = root.position - transform.right * 0.38f + Vector3.down * 0.12f;
                ApplyTwoBoneIk(root, mid, end, target, pole);
            }

            if (TryGetFallbackChain("arm right shoulder 2", "arm right elbow", "arm right wrist", out root, out mid, out end))
            {
                Vector3 target = root.position + transform.forward * 0.34f + Vector3.up * 0.13f - transform.right * 0.08f;
                Vector3 pole = root.position + transform.right * 0.38f + Vector3.down * 0.12f;
                ApplyTwoBoneIk(root, mid, end, target, pole);
            }
        }

        private void ApplyHumanoidAttack()
        {
            float amount = GetAttackEnvelope();
            if (amount <= 0f)
            {
                return;
            }

            switch (fighterCombat.CurrentAttack)
            {
                case PrototypeAttack.StraightPunch:
                    AddMuscle("Right Arm Down-Up", 0.66f * amount);
                    AddMuscle("Right Arm Front-Back", -0.78f * amount);
                    AddMuscle("Right Forearm Stretch", 0.92f * amount);
                    AddMuscle("Spine Twist Left-Right", -0.16f * amount);
                    break;

                case PrototypeAttack.HighKick:
                    AddMuscle("Right Upper Leg Front-Back", -0.92f * amount);
                    AddMuscle("Right Lower Leg Stretch", 0.22f * amount);
                    AddMuscle("Spine Front-Back", 0.12f * amount);
                    break;

                case PrototypeAttack.LowKick:
                    AddMuscle("Right Upper Leg Front-Back", -0.5f * amount);
                    AddMuscle("Right Lower Leg Stretch", 0.34f * amount);
                    AddMuscle("Spine Front-Back", -0.06f * amount);
                    break;

                case PrototypeAttack.StepTeep:
                    AddMuscle("Right Upper Leg Front-Back", -0.72f * amount);
                    AddMuscle("Right Lower Leg Stretch", 0.82f * amount);
                    AddMuscle("Right Foot Up-Down", 0.18f * amount);
                    AddMuscle("Spine Front-Back", 0.14f * amount);
                    AddMuscle("Left Arm Front-Back", -0.14f * amount);
                    AddMuscle("Right Arm Front-Back", 0.14f * amount);
                    break;
            }
        }

        private void ApplyFallbackAttack()
        {
            float amount = GetAttackEnvelope();
            if (amount <= 0f)
            {
                return;
            }

            Transform root;
            Transform mid;
            Transform end;
            Vector3 target;
            Vector3 pole;

            switch (fighterCombat.CurrentAttack)
            {
                case PrototypeAttack.StraightPunch:
                    if (TryGetFallbackChain("arm right shoulder 2", "arm right elbow", "arm right wrist", out root, out mid, out end))
                    {
                        target = root.position + transform.forward * 0.64f + Vector3.down * 0.06f;
                        target = Vector3.Lerp(end.position, target, amount);
                        pole = root.position + transform.right * 0.5f + Vector3.down * 0.18f;
                        ApplyTwoBoneIk(root, mid, end, target, pole);
                    }
                    break;

                case PrototypeAttack.HighKick:
                    if (TryGetFallbackChain("leg right thigh", "leg right knee", "leg right ankle", out root, out mid, out end))
                    {
                        target = root.position + transform.forward * 0.72f + Vector3.up * 0.42f;
                        target = Vector3.Lerp(end.position, target, amount);
                        pole = root.position + transform.forward * 0.4f + Vector3.down * 0.35f;
                        ApplyTwoBoneIk(root, mid, end, target, pole);
                    }
                    break;

                case PrototypeAttack.LowKick:
                    if (TryGetFallbackChain("leg right thigh", "leg right knee", "leg right ankle", out root, out mid, out end))
                    {
                        target = root.position + transform.forward * 0.68f + Vector3.down * 0.42f;
                        target = Vector3.Lerp(end.position, target, amount);
                        pole = root.position + transform.forward * 0.35f + Vector3.down * 0.45f;
                        ApplyTwoBoneIk(root, mid, end, target, pole);
                    }
                    break;

                case PrototypeAttack.StepTeep:
                    if (TryGetFallbackChain("leg right thigh", "leg right knee", "leg right ankle", out root, out mid, out end))
                    {
                        target = root.position + transform.forward * 0.82f + Vector3.up * 0.05f;
                        target = Vector3.Lerp(end.position, target, amount);
                        pole = root.position + transform.forward * 0.36f + Vector3.down * 0.38f;
                        ApplyTwoBoneIk(root, mid, end, target, pole);
                    }
                    break;
            }
        }

        private bool TryGetFallbackChain(string rootName, string midName, string endName,
            out Transform root, out Transform mid, out Transform end)
        {
            root = null;
            mid = null;
            end = null;
            bool foundRoot = fallbackBones.TryGetValue(rootName, out root);
            bool foundMid = fallbackBones.TryGetValue(midName, out mid);
            bool foundEnd = fallbackBones.TryGetValue(endName, out end);
            return foundRoot && foundMid && foundEnd;
        }

        private static void ApplyTwoBoneIk(Transform root, Transform mid, Transform end, Vector3 target, Vector3 pole)
        {
            float upperLength = Vector3.Distance(root.position, mid.position);
            float lowerLength = Vector3.Distance(mid.position, end.position);
            Vector3 targetVector = target - root.position;
            float targetDistance = Mathf.Clamp(targetVector.magnitude, 0.001f, upperLength + lowerLength - 0.001f);
            Vector3 targetDirection = targetVector.normalized;

            Vector3 poleDirection = pole - root.position;
            poleDirection -= targetDirection * Vector3.Dot(poleDirection, targetDirection);
            if (poleDirection.sqrMagnitude < 0.0001f)
            {
                poleDirection = Vector3.Cross(targetDirection, Vector3.up);
            }
            poleDirection.Normalize();

            float along = (upperLength * upperLength + targetDistance * targetDistance - lowerLength * lowerLength) /
                          (2f * targetDistance);
            float away = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 desiredMidPosition = root.position + targetDirection * along + poleDirection * away;

            Vector3 currentUpperDirection = mid.position - root.position;
            Vector3 desiredUpperDirection = desiredMidPosition - root.position;
            if (currentUpperDirection.sqrMagnitude > 0.0001f && desiredUpperDirection.sqrMagnitude > 0.0001f)
            {
                root.rotation = Quaternion.FromToRotation(currentUpperDirection, desiredUpperDirection) * root.rotation;
            }

            Vector3 currentLowerDirection = end.position - mid.position;
            Vector3 desiredLowerDirection = target - mid.position;
            if (currentLowerDirection.sqrMagnitude > 0.0001f && desiredLowerDirection.sqrMagnitude > 0.0001f)
            {
                mid.rotation = Quaternion.FromToRotation(currentLowerDirection, desiredLowerDirection) * mid.rotation;
            }
        }

        private float GetAttackEnvelope()
        {
            return fighterCombat != null ? fighterCombat.AttackEnvelope : 0f;
        }

        private void AddMuscle(string muscleName, float amount)
        {
            int index;
            if (muscleIndices.TryGetValue(muscleName, out index) && index >= 0 && index < pose.muscles.Length)
            {
                pose.muscles[index] = Mathf.Clamp(pose.muscles[index] + amount, -1f, 1f);
            }
        }

        private void OnDestroy()
        {
            if (poseHandler != null)
            {
                poseHandler.Dispose();
                poseHandler = null;
            }

        }

        private void OnGUI()
        {
            if (!playerControlled || !showControlHelp)
            {
                return;
            }

            const int width = 430;
            Rect panel = new Rect(18f, 18f, width, 198f);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 14f, panel.y + 10f, width - 28f, 24f), "JIN BURIKI-STYLE PROTOTYPE");
            GUI.Label(new Rect(panel.x + 14f, panel.y + 34f, width - 28f, 22f), "A / D: move backward / forward   (Gamepad: LB / RB)");
            GUI.Label(new Rect(panel.x + 14f, panel.y + 54f, width - 28f, 22f), "W / S: circle the opponent");
            GUI.Label(new Rect(panel.x + 14f, panel.y + 74f, width - 28f, 22f), "Double-tap D: run   Double-tap A: backdash");
            GUI.Label(new Rect(panel.x + 14f, panel.y + 94f, width - 28f, 22f), "Hold A + D together: block");
            GUI.Label(new Rect(panel.x + 14f, panel.y + 114f, width - 28f, 22f), "Attack directions: Right = punch, Up = high kick, Down = low kick");
            GUI.Label(new Rect(panel.x + 14f, panel.y + 134f, width - 28f, 22f), "Hold D, then attack Back → Neutral → Forward: stepping teep");
            GUI.Label(new Rect(panel.x + 14f, panel.y + 154f, width - 28f, 22f), "Gamepad: hold RB and flick attack stick Back → Forward");

            string state;
            if (fighterCombat != null && fighterCombat.IsAttacking)
            {
                string moveName = fighterCombat.CurrentMove != null
                    ? fighterCombat.CurrentMove.displayName
                    : fighterCombat.CurrentAttack.ToString();
                string result = fighterCombat.LastHitWasBlocked
                    ? "  BLOCKED"
                    : fighterCombat.AttackConnected ? "  HIT" : string.Empty;
                state = "Attack: " + moveName + "  F" + fighterCombat.CurrentMoveFrame + result;
            }
            else if (IsBlocking) state = "State: Blocking";
            else if (IsBackdashing) state = "State: Backdash";
            else if (IsRunning) state = "State: Running";
            else state = IsMoving ? "State: Moving" : "State: Idle";
            GUI.Label(new Rect(panel.x + 14f, panel.y + 174f, width - 28f, 20f), state);
        }
    }
}
