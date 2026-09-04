using System;
using System.Collections.Generic;
using UnityEngine;

namespace FightingGame.Prototype
{
    /// <summary>
    /// Small, dependency-free controller used to evaluate the imported Jin model.
    /// It provides opponent-relative movement, procedural posing, and directional attacks.
    /// Replace this with authored clips once the combat controller is ready.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class JinPrototypeController : MonoBehaviour
    {
        private enum PrototypeAttack
        {
            None,
            StraightPunch,
            HighKick,
            LowKick,
            StepTeep
        }

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 2.25f;
        [SerializeField, Min(0f)] private float acceleration = 14f;
        [SerializeField, Min(0f)] private float facingSpeed = 14f;
        [SerializeField] private Transform opponentTarget;

        [Header("Movement techniques")]
        [SerializeField, Range(0.1f, 0.5f)] private float doubleTapWindow = 0.24f;
        [SerializeField, Min(1f)] private float runSpeedMultiplier = 1.65f;
        [SerializeField, Min(0.1f)] private float backdashSpeed = 5.4f;
        [SerializeField, Range(0.1f, 0.8f)] private float backdashDuration = 0.32f;

        [Header("Procedural pose")]
        [SerializeField, Min(0f)] private float idleBreathSpeed = 1.7f;
        [SerializeField, Range(0f, 0.04f)] private float idleBobAmount = 0.008f;
        [SerializeField, Range(0f, 1f)] private float idleMotionScale = 0.18f;
        [SerializeField, Min(0.1f)] private float stepFrequency = 2.3f;
        [SerializeField, Range(0f, 1f)] private float stepAmount = 0.28f;

        [Header("Attack prototype")]
        [SerializeField, Min(0.1f)] private float punchDuration = 0.42f;
        [SerializeField, Min(0.1f)] private float highKickDuration = 0.62f;
        [SerializeField, Min(0.1f)] private float lowKickDuration = 0.5f;
        [SerializeField, Min(0.1f)] private float teepDuration = 0.72f;
        [SerializeField, Range(0.15f, 0.8f)] private float teepSequenceWindow = 0.42f;
        [SerializeField, Min(0f)] private float teepStepSpeed = 2.4f;

        [Header("Hitbox prototype")]
        [SerializeField] private bool showHitboxes = true;
        [SerializeField] private Color activeHitboxColor = new Color(1f, 0.12f, 0.08f, 0.3f);
        [SerializeField] private Color connectedHitboxColor = new Color(0.15f, 1f, 0.25f, 0.38f);

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
        private float currentSpeed;
        private float currentSideSpeed;
        private float stepClock;
        private bool humanoidPoseAvailable;
        private PrototypeAttack currentAttack;
        private float attackTimer;
        private float attackDuration;
        private bool attackDirectionHeld;
        private bool controlsEnabled = true;
        private float lastForwardTapTime = -10f;
        private float lastBackwardTapTime = -10f;
        private float backdashTimer;
        private bool isRunning;
        private bool isBlocking;
        private float teepBackInputTime = -10f;
        private bool attackConnected;
        private GameObject hitboxVisual;
        private Renderer hitboxRenderer;
        private Material hitboxMaterial;
        private readonly Collider[] hitboxResults = new Collider[16];
        private PrototypeDamageHealth opponentHealth;
        private PrototypeDummyOpponent opponentDummy;

        public float MoveInput { get; private set; }
        public float SideInput { get; private set; }
        public bool IsMoving { get { return CurrentPlanarSpeed > 0.03f; } }
        public bool IsRunning { get { return isRunning; } }
        public bool IsBlocking { get { return isBlocking; } }
        public bool IsBackdashing { get { return backdashTimer > 0f; } }
        private float CurrentPlanarSpeed { get { return new Vector2(currentSpeed, currentSideSpeed).magnitude; } }

        public void SetOpponent(Transform target)
        {
            opponentTarget = target;
            opponentHealth = FindOpponentHealth(target);
            opponentDummy = FindOpponentDummy(target);
        }

        public void SetControlsEnabled(bool enabled)
        {
            controlsEnabled = enabled;
            if (!enabled)
            {
                MoveInput = 0f;
                SideInput = 0f;
                currentSpeed = 0f;
                currentSideSpeed = 0f;
                isRunning = false;
                isBlocking = false;
                backdashTimer = 0f;
            }
        }

        public void ResetRuntimeState()
        {
            currentSpeed = 0f;
            currentSideSpeed = 0f;
            stepClock = 0f;
            currentAttack = PrototypeAttack.None;
            attackTimer = 0f;
            attackDirectionHeld = false;
            lastForwardTapTime = -10f;
            lastBackwardTapTime = -10f;
            teepBackInputTime = -10f;
            backdashTimer = 0f;
            isRunning = false;
            isBlocking = false;
            attackConnected = false;
            MoveInput = 0f;
            SideInput = 0f;
            SetHitboxVisualActive(false);
        }

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            CacheFallbackRig();
            TryInitializeHumanoidPose();
        }

        private void OnEnable()
        {
            if (poseHandler == null)
            {
                TryInitializeHumanoidPose();
            }
        }

        private void Update()
        {
            if (!controlsEnabled)
            {
                return;
            }

            bool forwardHeld = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.JoystickButton5);
            bool backwardHeld = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.JoystickButton4);
            bool forwardPressed = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.JoystickButton5);
            bool backwardPressed = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.JoystickButton4);

            UpdateMovementGestures(forwardHeld, backwardHeld, forwardPressed, backwardPressed);
            isBlocking = forwardHeld && backwardHeld && currentAttack == PrototypeAttack.None && !IsBackdashing;
            if (isBlocking)
            {
                isRunning = false;
            }

            if (!isBlocking && !IsBackdashing)
            {
                UpdateAttackInput(forwardHeld);
            }

            // Buriki-style separation: buttons move, directions attack.
            MoveInput = 0f;
            if (!isBlocking && !IsBackdashing)
            {
                if (forwardHeld) MoveInput += 1f;
                if (backwardHeld) MoveInput -= 1f;
            }

            SideInput = 0f;
            if (!isBlocking && !IsBackdashing)
            {
                if (Input.GetKey(KeyCode.W)) SideInput += 1f;
                if (Input.GetKey(KeyCode.S)) SideInput -= 1f;
            }

            if (currentAttack != PrototypeAttack.None)
            {
                MoveInput = 0f;
                SideInput = 0f;
                attackTimer += Time.deltaTime;
                if (attackTimer >= attackDuration)
                {
                    currentAttack = PrototypeAttack.None;
                    attackTimer = 0f;
                }
            }

            Vector2 desiredMovement = Vector2.ClampMagnitude(new Vector2(MoveInput, SideInput), 1f);
            float movementSpeed = isRunning && forwardHeld ? moveSpeed * runSpeedMultiplier : moveSpeed;
            float targetSpeed = desiredMovement.x * movementSpeed;
            float targetSideSpeed = desiredMovement.y * moveSpeed;

            if (IsBackdashing)
            {
                backdashTimer = Mathf.Max(0f, backdashTimer - Time.deltaTime);
                float normalizedBackdash = backdashDuration > 0f ? backdashTimer / backdashDuration : 0f;
                currentSpeed = -backdashSpeed * Mathf.Sin(normalizedBackdash * Mathf.PI);
                currentSideSpeed = Mathf.MoveTowards(currentSideSpeed, 0f, acceleration * Time.deltaTime);
            }
            else if (currentAttack == PrototypeAttack.StepTeep)
            {
                currentSpeed = teepStepSpeed * GetTeepStepEnvelope();
                currentSideSpeed = 0f;
            }
            else
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
                currentSideSpeed = Mathf.MoveTowards(currentSideSpeed, targetSideSpeed, acceleration * Time.deltaTime);
            }

            Vector3 position = transform.position;
            Vector3 fightForward = Vector3.right;
            if (opponentTarget != null)
            {
                fightForward = opponentTarget.position - position;
                fightForward.y = 0f;
                if (fightForward.sqrMagnitude > 0.0001f)
                {
                    fightForward.Normalize();
                }
                else
                {
                    fightForward = transform.forward;
                }
            }

            // Forward/back follows the current fight line. Sidestep follows its tangent,
            // producing an arc around the opponent instead of a flat world-Z slide.
            Vector3 sidestepDirection = Vector3.Cross(fightForward, Vector3.up).normalized;
            position += (fightForward * currentSpeed + sidestepDirection * currentSideSpeed) * Time.deltaTime;
            transform.position = position;

            if (opponentTarget != null)
            {
                Vector3 updatedFightForward = opponentTarget.position - position;
                updatedFightForward.y = 0f;
                if (updatedFightForward.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(updatedFightForward.normalized, Vector3.up);
                    float rotationBlend = 1f - Mathf.Exp(-facingSpeed * Time.deltaTime);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationBlend);
                }
            }

            if (CurrentPlanarSpeed > 0.03f)
            {
                stepClock += Time.deltaTime * stepFrequency * Mathf.Lerp(0.65f, 1f, CurrentPlanarSpeed / moveSpeed);
            }

            UpdateAttackHitbox();
        }

        private void UpdateMovementGestures(bool forwardHeld, bool backwardHeld, bool forwardPressed, bool backwardPressed)
        {
            if (forwardPressed && !backwardHeld)
            {
                if (Time.time - lastForwardTapTime <= doubleTapWindow)
                {
                    isRunning = true;
                    lastForwardTapTime = -10f;
                }
                else
                {
                    lastForwardTapTime = Time.time;
                }
            }

            if (!forwardHeld)
            {
                isRunning = false;
            }

            if (backwardPressed && !forwardHeld && currentAttack == PrototypeAttack.None)
            {
                if (Time.time - lastBackwardTapTime <= doubleTapWindow)
                {
                    backdashTimer = backdashDuration;
                    lastBackwardTapTime = -10f;
                    isRunning = false;
                    currentSideSpeed = 0f;
                }
                else
                {
                    lastBackwardTapTime = Time.time;
                }
            }
        }

        private void UpdateAttackInput(bool forwardHeld)
        {
            Vector2 attackDirection = ReadAttackDirection();
            bool directionActive = attackDirection.sqrMagnitude >= 0.36f;

            if (Time.time - teepBackInputTime > teepSequenceWindow)
            {
                teepBackInputTime = -10f;
            }

            if (!directionActive)
            {
                attackDirectionHeld = false;
                return;
            }

            if (attackDirectionHeld || currentAttack != PrototypeAttack.None)
            {
                return;
            }

            attackDirectionHeld = true;
            if (forwardHeld && attackDirection.x < -0.45f)
            {
                teepBackInputTime = Time.time;
                return;
            }

            if (forwardHeld && attackDirection.x > 0.45f &&
                Time.time - teepBackInputTime <= teepSequenceWindow)
            {
                teepBackInputTime = -10f;
                StartAttack(PrototypeAttack.StepTeep, teepDuration);
                return;
            }

            if (attackDirection.y > 0.45f)
            {
                StartAttack(PrototypeAttack.HighKick, highKickDuration);
            }
            else if (attackDirection.y < -0.45f)
            {
                StartAttack(PrototypeAttack.LowKick, lowKickDuration);
            }
            else if (attackDirection.x > 0.35f)
            {
                StartAttack(PrototypeAttack.StraightPunch, punchDuration);
            }
        }

        private Vector2 ReadAttackDirection()
        {
            Vector2 keyboardDirection = Vector2.zero;
            if (Input.GetKey(KeyCode.RightArrow)) keyboardDirection.x += 1f;
            if (Input.GetKey(KeyCode.LeftArrow)) keyboardDirection.x -= 1f;
            if (Input.GetKey(KeyCode.UpArrow)) keyboardDirection.y += 1f;
            if (Input.GetKey(KeyCode.DownArrow)) keyboardDirection.y -= 1f;
            if (keyboardDirection.sqrMagnitude > 0f)
            {
                return Vector2.ClampMagnitude(keyboardDirection, 1f);
            }

            // Ignore the legacy keyboard contribution to these axes while WASD is held;
            // otherwise they serve as the gamepad attack stick.
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) ||
                Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D))
            {
                return Vector2.zero;
            }

            return Vector2.ClampMagnitude(
                new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
        }

        private void StartAttack(PrototypeAttack attack, float duration)
        {
            currentAttack = attack;
            attackDuration = Mathf.Max(0.1f, duration);
            attackTimer = 0f;
            attackConnected = false;
            currentSpeed = 0f;
            currentSideSpeed = 0f;
        }

        private void UpdateAttackHitbox()
        {
            Vector3 center;
            Vector3 size;
            Quaternion rotation;
            if (!TryGetActiveHitbox(out center, out size, out rotation))
            {
                SetHitboxVisualActive(false);
                return;
            }

            UpdateHitboxVisual(center, size, rotation);
            if (attackConnected || opponentTarget == null)
            {
                return;
            }

            int hitCount = Physics.OverlapBoxNonAlloc(
                center,
                size * 0.5f,
                hitboxResults,
                rotation,
                ~0,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hitCount; i++)
            {
                Collider candidate = hitboxResults[i];
                if (candidate == null || candidate.transform.IsChildOf(transform))
                {
                    continue;
                }

                Transform candidateTransform = candidate.transform;
                bool belongsToOpponent = candidateTransform == opponentTarget ||
                                         candidateTransform.IsChildOf(opponentTarget) ||
                                         opponentTarget.IsChildOf(candidateTransform);
                if (!belongsToOpponent)
                {
                    continue;
                }

                attackConnected = true;
                if (opponentHealth == null)
                {
                    opponentHealth = FindOpponentHealth(opponentTarget);
                }
                if (opponentDummy == null)
                {
                    opponentDummy = FindOpponentDummy(opponentTarget);
                }

                float damage = GetAttackDamage();
                if (opponentHealth != null)
                {
                    opponentHealth.ReceiveDamage(damage);
                }
                if (opponentDummy != null)
                {
                    opponentDummy.ReactToHit(
                        transform.forward,
                        damage,
                        currentAttack == PrototypeAttack.StepTeep);
                }
                if (hitboxMaterial != null)
                {
                    hitboxMaterial.color = connectedHitboxColor;
                }
                break;
            }
        }

        private float GetAttackDamage()
        {
            switch (currentAttack)
            {
                case PrototypeAttack.StraightPunch:
                    return 8f;
                case PrototypeAttack.HighKick:
                    return 16f;
                case PrototypeAttack.LowKick:
                    return 11f;
                case PrototypeAttack.StepTeep:
                    return 18f;
                default:
                    return 0f;
            }
        }

        private static PrototypeDamageHealth FindOpponentHealth(Transform target)
        {
            if (target == null)
            {
                return null;
            }

            PrototypeDamageHealth health = target.GetComponent<PrototypeDamageHealth>();
            if (health == null)
            {
                health = target.GetComponentInParent<PrototypeDamageHealth>();
            }
            if (health == null)
            {
                health = target.GetComponentInChildren<PrototypeDamageHealth>();
            }
            return health;
        }

        private static PrototypeDummyOpponent FindOpponentDummy(Transform target)
        {
            if (target == null)
            {
                return null;
            }

            PrototypeDummyOpponent dummy = target.GetComponent<PrototypeDummyOpponent>();
            if (dummy == null)
            {
                dummy = target.GetComponentInParent<PrototypeDummyOpponent>();
            }
            if (dummy == null)
            {
                dummy = target.GetComponentInChildren<PrototypeDummyOpponent>();
            }
            return dummy;
        }

        private bool TryGetActiveHitbox(out Vector3 center, out Vector3 size, out Quaternion rotation)
        {
            center = Vector3.zero;
            size = Vector3.zero;
            rotation = transform.rotation;
            if (currentAttack == PrototypeAttack.None || attackDuration <= 0f)
            {
                return false;
            }

            float normalizedTime = Mathf.Clamp01(attackTimer / attackDuration);
            float activeStart;
            float activeEnd;
            float forwardOffset;
            float height;

            switch (currentAttack)
            {
                case PrototypeAttack.StraightPunch:
                    activeStart = 0.2f;
                    activeEnd = 0.5f;
                    forwardOffset = 0.72f;
                    height = 1.25f;
                    size = new Vector3(0.5f, 0.32f, 0.56f);
                    break;

                case PrototypeAttack.HighKick:
                    activeStart = 0.28f;
                    activeEnd = 0.56f;
                    forwardOffset = 0.76f;
                    height = 1.38f;
                    size = new Vector3(0.55f, 0.46f, 0.62f);
                    break;

                case PrototypeAttack.LowKick:
                    activeStart = 0.23f;
                    activeEnd = 0.55f;
                    forwardOffset = 0.68f;
                    height = 0.46f;
                    size = new Vector3(0.58f, 0.3f, 0.64f);
                    break;

                case PrototypeAttack.StepTeep:
                    activeStart = 0.32f;
                    activeEnd = 0.64f;
                    forwardOffset = 0.88f;
                    height = 0.94f;
                    size = new Vector3(0.58f, 0.42f, 0.68f);
                    break;

                default:
                    return false;
            }

            if (normalizedTime < activeStart || normalizedTime > activeEnd)
            {
                return false;
            }

            center = transform.position + transform.forward * forwardOffset + Vector3.up * height;
            return true;
        }

        private void UpdateHitboxVisual(Vector3 center, Vector3 size, Quaternion rotation)
        {
            if (!showHitboxes)
            {
                SetHitboxVisualActive(false);
                return;
            }

            EnsureHitboxVisual();
            hitboxVisual.transform.SetPositionAndRotation(center, rotation);
            hitboxVisual.transform.localScale = size;
            hitboxMaterial.color = attackConnected ? connectedHitboxColor : activeHitboxColor;
            SetHitboxVisualActive(true);
        }

        private void EnsureHitboxVisual()
        {
            if (hitboxVisual != null)
            {
                return;
            }

            hitboxVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hitboxVisual.name = "Active Attack Hitbox (Demo)";
            hitboxVisual.hideFlags = HideFlags.DontSave;

            Collider visualCollider = hitboxVisual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                Destroy(visualCollider);
            }

            hitboxRenderer = hitboxVisual.GetComponent<Renderer>();
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }
            hitboxMaterial = new Material(shader);
            hitboxMaterial.name = "Runtime Hitbox Material";
            hitboxMaterial.color = activeHitboxColor;
            hitboxRenderer.sharedMaterial = hitboxMaterial;
        }

        private void SetHitboxVisualActive(bool active)
        {
            if (hitboxVisual != null && hitboxVisual.activeSelf != active)
            {
                hitboxVisual.SetActive(active);
            }
        }

        private float GetTeepStepEnvelope()
        {
            if (currentAttack != PrototypeAttack.StepTeep || attackDuration <= 0f)
            {
                return 0f;
            }

            float normalizedTime = Mathf.Clamp01(attackTimer / attackDuration);
            if (normalizedTime >= 0.68f)
            {
                return 0f;
            }

            return Mathf.Sin((normalizedTime / 0.68f) * Mathf.PI);
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

            float movementBlend = Mathf.Clamp01(CurrentPlanarSpeed / moveSpeed);
            float idleWave = Mathf.Sin(Time.time * idleBreathSpeed * 0.55f * Mathf.PI * 2f) * idleMotionScale;
            float stepWave = Mathf.Sin(stepClock * Mathf.PI * 2f);
            float oppositeStep = Mathf.Sin((stepClock + 0.5f) * Mathf.PI * 2f);

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
            float movementBlend = Mathf.Clamp01(CurrentPlanarSpeed / moveSpeed);
            float idleWave = Mathf.Sin(Time.time * idleBreathSpeed * 0.55f * Mathf.PI * 2f) * idleMotionScale;
            float stepWave = Mathf.Sin(stepClock * Mathf.PI * 2f);

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
            if (!isBlocking)
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
            if (!isBlocking)
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

            switch (currentAttack)
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

            switch (currentAttack)
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
            if (currentAttack == PrototypeAttack.None || attackDuration <= 0f)
            {
                return 0f;
            }

            float normalizedTime = Mathf.Clamp01(attackTimer / attackDuration);
            if (normalizedTime < 0.22f)
            {
                return Mathf.SmoothStep(0f, 1f, normalizedTime / 0.22f);
            }
            if (normalizedTime < 0.52f)
            {
                return 1f;
            }
            return Mathf.SmoothStep(1f, 0f, (normalizedTime - 0.52f) / 0.48f);
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

            if (hitboxVisual != null)
            {
                Destroy(hitboxVisual);
            }
            if (hitboxMaterial != null)
            {
                Destroy(hitboxMaterial);
            }
        }

        private void OnGUI()
        {
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
            if (currentAttack != PrototypeAttack.None) state = "Attack: " + currentAttack + (attackConnected ? "  HIT" : string.Empty);
            else if (isBlocking) state = "State: Blocking";
            else if (IsBackdashing) state = "State: Backdash";
            else if (isRunning) state = "State: Running";
            else state = IsMoving ? "State: Moving" : "State: Idle";
            GUI.Label(new Rect(panel.x + 14f, panel.y + 174f, width - 28f, 20f), state);
        }
    }
}
