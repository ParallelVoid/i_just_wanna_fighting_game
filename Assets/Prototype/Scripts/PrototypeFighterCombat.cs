using System.Collections.Generic;
using Combat;
using UnityEngine;

namespace FightingGame.Prototype
{
    public enum PrototypeAttack { None, StraightPunch, HighKick, LowKick, StepTeep }

    /// <summary>Recognizes prototype commands and executes frame-authored move data.</summary>
    [DisallowMultipleComponent]
    public sealed class PrototypeFighterCombat : MonoBehaviour
    {

        [Header("Move definitions")]
        [SerializeField] private MoveDefinition straightPunch;
        [SerializeField] private MoveDefinition highKick;
        [SerializeField] private MoveDefinition lowKick;
        [SerializeField] private MoveDefinition stepTeep;
        [SerializeField, Range(0.15f, 0.8f)] private float teepSequenceWindow = 0.42f;

        [Header("Hitbox prototype")]
        [SerializeField] private bool showHitboxes = true;
        [SerializeField] private Color activeHitboxColor = new Color(1f, 0.12f, 0.08f, 0.3f);
        [SerializeField] private Color connectedHitboxColor = new Color(0.15f, 1f, 0.25f, 0.38f);

        private readonly Collider[] hitboxResults = new Collider[16];
        private readonly List<GameObject> hitboxVisuals = new List<GameObject>();
        private readonly List<HitVolumeShape> hitboxVisualShapes = new List<HitVolumeShape>();
        private readonly Dictionary<string, Transform> boneCache = new Dictionary<string, Transform>();
        private readonly List<MoveDefinition> runtimeMoves = new List<MoveDefinition>();
        private Transform opponentTarget;
        private PrototypeDamageHealth opponentHealth;
        private PrototypeDummyOpponent opponentDummy;
        private JinPrototypeController opponentController;
        private PrototypeRingOutReset matchFlow;
        private PrototypeControlProfile controlProfile;
        private MoveDefinition currentMove;
        private double teepBackInputTime = -10f;
        private bool attackDirectionHeld;
        private bool attackConnected;
        private Material hitboxMaterial;

        public PrototypeAttack CurrentAttack { get; private set; }
        public MoveDefinition CurrentMove { get { return currentMove; } }
        public int CurrentMoveFrame { get; private set; }
        public bool IsAttacking { get { return CurrentAttack != PrototypeAttack.None && currentMove != null; } }
        public bool AttackConnected { get { return attackConnected; } }
        public bool LastHitWasBlocked { get; private set; }
        public bool OccupiedThisFrame { get; private set; }

        public float AttackEnvelope
        {
            get
            {
                if (!IsAttacking || currentMove.TotalFrames <= 0) return 0f;
                float time = Mathf.Clamp01(GetFractionalMoveFrame() / currentMove.TotalFrames);
                if (time < 0.22f) return Mathf.SmoothStep(0f, 1f, time / 0.22f);
                if (time < 0.52f) return 1f;
                return Mathf.SmoothStep(1f, 0f, (time - 0.52f) / 0.48f);
            }
        }

        public float TeepStepEnvelope
        {
            get
            {
                if (CurrentAttack != PrototypeAttack.StepTeep || currentMove == null || currentMove.TotalFrames <= 0)
                    return 0f;
                float time = Mathf.Clamp01(GetFractionalMoveFrame() / currentMove.TotalFrames);
                return time < 0.68f ? Mathf.Sin((time / 0.68f) * Mathf.PI) : 0f;
            }
        }

        private void Awake()
        {
            matchFlow = FindObjectOfType<PrototypeRingOutReset>();
            EnsureMoveDefinitions();
            CacheBones();
        }

        public void Configure(Transform opponent, PrototypeControlProfile profile)
        {
            controlProfile = profile;
            SetOpponent(opponent);
        }

        public void ConfigureMoves(MoveDefinition punch, MoveDefinition high, MoveDefinition low, MoveDefinition teep)
        {
            straightPunch = punch;
            highKick = high;
            lowKick = low;
            stepTeep = teep;
        }

        public void SetOpponent(Transform opponent)
        {
            opponentTarget = opponent;
            opponentHealth = FindOpponentComponent<PrototypeDamageHealth>(opponent);
            opponentDummy = FindOpponentComponent<PrototypeDummyOpponent>(opponent);
            opponentController = FindOpponentComponent<JinPrototypeController>(opponent);
        }

        public void SetControlProfile(PrototypeControlProfile profile) { controlProfile = profile; }

        public void ResetRuntimeState()
        {
            CurrentAttack = PrototypeAttack.None;
            currentMove = null;
            CurrentMoveFrame = 0;
            teepBackInputTime = -10f;
            attackDirectionHeld = false;
            attackConnected = false;
            LastHitWasBlocked = false;
            OccupiedThisFrame = false;
            SetHitboxVisualCount(0);
        }

        public void Tick(
            bool forwardHeld,
            Vector2 attackDirection,
            bool acceptAttackInput,
            FighterState fighterState)
        {
            EnsureMoveDefinitions();
            OccupiedThisFrame = IsAttacking;
            if (acceptAttackInput) UpdateAttackInput(forwardHeld, attackDirection, fighterState);
            OccupiedThisFrame |= IsAttacking;
        }

        public void AdvanceMoveFrame()
        {
            if (!IsAttacking) return;
            CurrentMoveFrame++;
            if (CurrentMoveFrame >= currentMove.TotalFrames) FinishAttack();
        }

        public void ResolveHitbox()
        {
            if (!IsAttacking || currentMove.hitboxes == null)
            {
                SetHitboxVisualCount(0);
                return;
            }

            int visualCount = 0;
            for (int i = 0; i < currentMove.hitboxes.Count; i++)
            {
                HitboxDefinition hitbox = currentMove.hitboxes[i];
                if (hitbox == null || CurrentMoveFrame < hitbox.startFrame || CurrentMoveFrame > hitbox.endFrame)
                    continue;

                Vector3 center;
                Quaternion rotation;
                GetHitboxPose(hitbox, out center, out rotation);
                UpdateHitboxVisual(visualCount++, hitbox, center, rotation);
                if (!attackConnected && opponentTarget != null && HitboxTouchesOpponent(hitbox, center, rotation))
                    ConnectAttack(hitbox);
            }
            SetHitboxVisualCount(showHitboxes ? visualCount : 0);
        }

        private void UpdateAttackInput(bool forwardHeld, Vector2 direction, FighterState fighterState)
        {
            bool active = direction.sqrMagnitude >= 0.36f;
            if (CombatClock.TimeSeconds - teepBackInputTime > teepSequenceWindow) teepBackInputTime = -10f;
            if (!active) { attackDirectionHeld = false; return; }
            if (attackDirectionHeld || IsAttacking) return;

            attackDirectionHeld = true;
            if (forwardHeld && direction.x < -0.45f) { teepBackInputTime = CombatClock.TimeSeconds; return; }
            if (forwardHeld && direction.x > 0.45f && CombatClock.TimeSeconds - teepBackInputTime <= teepSequenceWindow)
            {
                teepBackInputTime = -10f;
                StartAttack(PrototypeAttack.StepTeep, stepTeep, fighterState);
                return;
            }
            if (direction.y > 0.45f) StartAttack(PrototypeAttack.HighKick, highKick, fighterState);
            else if (direction.y < -0.45f) StartAttack(PrototypeAttack.LowKick, lowKick, fighterState);
            else if (direction.x > 0.35f) StartAttack(PrototypeAttack.StraightPunch, straightPunch, fighterState);
        }

        private void StartAttack(PrototypeAttack attack, MoveDefinition definition, FighterState fighterState)
        {
            if (definition == null || definition.TotalFrames <= 0 || !AllowsState(definition, fighterState)) return;
            CurrentAttack = attack;
            currentMove = definition;
            CurrentMoveFrame = 0;
            attackConnected = false;
            LastHitWasBlocked = false;
            if (hitboxMaterial != null) hitboxMaterial.color = activeHitboxColor;
        }

        private static bool AllowsState(MoveDefinition definition, FighterState fighterState)
        {
            if (definition.requiredFighterStates == null || definition.requiredFighterStates.Length == 0)
            {
                return fighterState == FighterState.Idle || fighterState == FighterState.Walk ||
                       fighterState == FighterState.Sidestep || fighterState == FighterState.Run;
            }
            for (int i = 0; i < definition.requiredFighterStates.Length; i++)
                if (definition.requiredFighterStates[i] == fighterState) return true;
            return false;
        }

        private void FinishAttack()
        {
            CurrentAttack = PrototypeAttack.None;
            currentMove = null;
            CurrentMoveFrame = 0;
            SetHitboxVisualCount(0);
        }

        private void ConnectAttack(HitboxDefinition hitbox)
        {
            attackConnected = true;
            if (opponentHealth == null) opponentHealth = FindOpponentComponent<PrototypeDamageHealth>(opponentTarget);
            if (opponentDummy == null) opponentDummy = FindOpponentComponent<PrototypeDummyOpponent>(opponentTarget);
            if (opponentController == null) opponentController = FindOpponentComponent<JinPrototypeController>(opponentTarget);

            LastHitWasBlocked = opponentController != null && opponentController.IsBlocking &&
                                hitbox.hitLevel != HitLevel.Throw && hitbox.hitLevel != HitLevel.Unblockable;
            float damage = LastHitWasBlocked ? currentMove.chipDamage :
                (hitbox.damageOverride >= 0 ? hitbox.damageOverride : currentMove.damage);
            bool strongAttack = !LastHitWasBlocked && currentMove.damage >= 16;
            bool matchWon = damage > 0f && opponentHealth != null && opponentHealth.ReceiveDamage(damage, strongAttack);

            if (!LastHitWasBlocked && opponentDummy != null)
            {
                bool knockdown = currentMove.knockdownType == KnockdownType.SoftKnockdown ||
                                 currentMove.knockdownType == KnockdownType.HardKnockdown ||
                                 currentMove.knockdownType == KnockdownType.RingOut || matchWon;
                opponentDummy.ReactToHit(transform.forward, damage, knockdown);
            }
            if (matchWon)
            {
                if (matchFlow == null) matchFlow = FindObjectOfType<PrototypeRingOutReset>();
                if (matchFlow != null)
                    matchFlow.DeclareWinner(controlProfile == PrototypeControlProfile.PlayerTwo ? "PLAYER 2" : "PLAYER 1");
            }
            if (hitboxMaterial != null) hitboxMaterial.color = connectedHitboxColor;
        }

        private bool HitboxTouchesOpponent(HitboxDefinition hitbox, Vector3 center, Quaternion rotation)
        {
            int count;
            switch (hitbox.shape)
            {
                case HitVolumeShape.Sphere:
                    count = Physics.OverlapSphereNonAlloc(center, hitbox.size.x, hitboxResults, ~0, QueryTriggerInteraction.Collide);
                    break;
                case HitVolumeShape.Capsule:
                    float radius = hitbox.size.x;
                    float halfSegment = Mathf.Max(0f, hitbox.size.y * 0.5f - radius);
                    Vector3 axis = rotation * Vector3.up * halfSegment;
                    count = Physics.OverlapCapsuleNonAlloc(center - axis, center + axis, radius,
                        hitboxResults, ~0, QueryTriggerInteraction.Collide);
                    break;
                default:
                    count = Physics.OverlapBoxNonAlloc(center, hitbox.size, hitboxResults,
                        rotation, ~0, QueryTriggerInteraction.Collide);
                    break;
            }

            for (int i = 0; i < count; i++)
            {
                Collider candidate = hitboxResults[i];
                if (candidate == null || candidate.transform.IsChildOf(transform)) continue;
                Transform candidateTransform = candidate.transform;
                if (candidateTransform == opponentTarget || candidateTransform.IsChildOf(opponentTarget) ||
                    opponentTarget.IsChildOf(candidateTransform)) return true;
            }
            return false;
        }

        private void GetHitboxPose(HitboxDefinition hitbox, out Vector3 center, out Quaternion rotation)
        {
            Transform attachment = transform;
            if (!string.IsNullOrEmpty(hitbox.attachedBoneName))
            {
                Transform bone;
                if (boneCache.TryGetValue(hitbox.attachedBoneName.ToLowerInvariant(), out bone)) attachment = bone;
            }
            center = attachment.TransformPoint(hitbox.localPosition);
            rotation = attachment.rotation * Quaternion.Euler(hitbox.localEulerRotation);
        }

        private void UpdateHitboxVisual(int index, HitboxDefinition hitbox, Vector3 center, Quaternion rotation)
        {
            if (!showHitboxes) return;
            EnsureHitboxVisual(index, hitbox.shape);
            GameObject visual = hitboxVisuals[index];
            visual.transform.SetPositionAndRotation(center, rotation);
            if (hitbox.shape == HitVolumeShape.Sphere) visual.transform.localScale = Vector3.one * hitbox.size.x * 2f;
            else if (hitbox.shape == HitVolumeShape.Capsule)
                visual.transform.localScale = new Vector3(hitbox.size.x * 2f, hitbox.size.y * 0.5f, hitbox.size.x * 2f);
            else visual.transform.localScale = hitbox.size * 2f;
            if (!visual.activeSelf) visual.SetActive(true);
        }

        private void EnsureHitboxVisual(int index, HitVolumeShape shape)
        {
            EnsureHitboxMaterial();
            while (hitboxVisuals.Count <= index) { hitboxVisuals.Add(null); hitboxVisualShapes.Add(HitVolumeShape.Box); }
            if (hitboxVisuals[index] != null && hitboxVisualShapes[index] == shape) return;
            if (hitboxVisuals[index] != null) Destroy(hitboxVisuals[index]);
            PrimitiveType primitive = shape == HitVolumeShape.Sphere ? PrimitiveType.Sphere :
                shape == HitVolumeShape.Capsule ? PrimitiveType.Capsule : PrimitiveType.Cube;
            GameObject visual = GameObject.CreatePrimitive(primitive);
            visual.name = "Active Attack Hitbox (Demo)";
            visual.hideFlags = HideFlags.DontSave;
            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null) { visualCollider.enabled = false; Destroy(visualCollider); }
            visual.GetComponent<Renderer>().sharedMaterial = hitboxMaterial;
            hitboxVisuals[index] = visual;
            hitboxVisualShapes[index] = shape;
        }

        private void EnsureHitboxMaterial()
        {
            if (hitboxMaterial != null) return;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            hitboxMaterial = new Material(shader) { name = "Runtime Hitbox Material", color = activeHitboxColor };
        }

        private void SetHitboxVisualCount(int activeCount)
        {
            for (int i = 0; i < hitboxVisuals.Count; i++)
                if (hitboxVisuals[i] != null && hitboxVisuals[i].activeSelf != (i < activeCount))
                    hitboxVisuals[i].SetActive(i < activeCount);
        }

        private void CacheBones()
        {
            boneCache.Clear();
            Transform[] bones = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < bones.Length; i++) boneCache[bones[i].name.ToLowerInvariant()] = bones[i];
        }

        private float GetFractionalMoveFrame() { return CurrentMoveFrame; }

        private void EnsureMoveDefinitions()
        {
            if (straightPunch == null) straightPunch = CreateRuntimeMove("straight_punch", "Straight Punch", 5, 8, 12, 8,
                HitLevel.High, KnockdownType.None, new Vector3(0f, 1.25f, 0.72f), new Vector3(0.10f, 0.16f, 0.28f));
            if (highKick == null) highKick = CreateRuntimeMove("high_kick", "High Kick", 10, 12, 15, 16,
                HitLevel.High, KnockdownType.None, new Vector3(0f, 1.38f, 0.76f), new Vector3(0.13f, 0.23f, 0.31f));
            if (lowKick == null) lowKick = CreateRuntimeMove("low_kick", "Low Kick", 7, 10, 13, 11,
                HitLevel.Low, KnockdownType.None, new Vector3(0f, 0.46f, 0.68f), new Vector3(0.16f, 0.15f, 0.32f));
            if (stepTeep == null) stepTeep = CreateRuntimeMove("step_teep", "Stepping Teep", 14, 14, 15, 18,
                HitLevel.Mid, KnockdownType.HardKnockdown, new Vector3(0f, 0.94f, 0.88f), new Vector3(0.11f, 0.21f, 0.34f));
        }

        private MoveDefinition CreateRuntimeMove(string id, string displayName, int startup, int active, int recovery,
            int damage, HitLevel hitLevel, KnockdownType knockdown, Vector3 position, Vector3 halfExtents)
        {
            MoveDefinition move = ScriptableObject.CreateInstance<MoveDefinition>();
            move.hideFlags = HideFlags.DontSave;
            move.moveId = id;
            move.displayName = displayName;
            move.startupFrames = startup;
            move.activeFrames = active;
            move.recoveryFrames = recovery;
            move.damage = damage;
            move.requiredFighterStates = new[]
            {
                FighterState.Idle, FighterState.Walk, FighterState.Sidestep, FighterState.Run
            };
            move.defaultHitLevel = hitLevel;
            move.knockdownType = knockdown;
            move.hitboxes.Add(new HitboxDefinition
            {
                label = id, shape = HitVolumeShape.Box, size = halfExtents, localPosition = position,
                startFrame = startup, endFrame = startup + active - 1, hitLevel = hitLevel
            });
            runtimeMoves.Add(move);
            return move;
        }

        private static T FindOpponentComponent<T>(Transform target) where T : Component
        {
            if (target == null) return null;
            T result = target.GetComponent<T>();
            if (result == null) result = target.GetComponentInParent<T>();
            if (result == null) result = target.GetComponentInChildren<T>();
            return result;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < hitboxVisuals.Count; i++) if (hitboxVisuals[i] != null) Destroy(hitboxVisuals[i]);
            if (hitboxMaterial != null) Destroy(hitboxMaterial);
            for (int i = 0; i < runtimeMoves.Count; i++) if (runtimeMoves[i] != null) Destroy(runtimeMoves[i]);
        }
    }
}
