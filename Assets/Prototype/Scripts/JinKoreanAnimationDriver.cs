using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Combat;

namespace FightingGame.Prototype
{
    /// <summary>
    /// Samples the imported attack clips against the deterministic combat frame.
    /// Locomotion and hit timing stay owned by the prototype combat components.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class JinKoreanAnimationDriver : MonoBehaviour
    {
        public const string MiddleSideKickState = "MiddleSideKick";
        public const string CrescentKickState = "CrescentKick";
        public const string LowSideKickState = "LowSideKick";
        public const string AxeKickState = "AxeKick";

        [SerializeField] private Animator animator;
        [SerializeField] private PrototypeFighterCombat fighterCombat;
        [SerializeField] private PrototypeFighterMotor fighterMotor;
        [SerializeField] private AnimationClip idleClip;
        [SerializeField, Range(0f, 90f)] private float attackCameraAwayAngle = 30f;
        private PlayableGraph graph;
        private AnimationClipPlayable clipPlayable;
        private AnimationClip activeClip;
        private HumanPoseHandler attackPoseHandler;
        private HumanPose attackPose;
        private Camera fightCamera;

        public bool ShouldDrivePose
        {
            get
            {
                bool drivesAttack = fighterCombat != null && fighterCombat.IsAttacking &&
                                    GetClip(fighterCombat.CurrentMove) != null;
                bool drivesIdle = idleClip != null && fighterCombat != null && !fighterCombat.IsAttacking &&
                                  fighterMotor != null && !fighterMotor.IsMoving &&
                                  !fighterMotor.IsBlocking && !fighterMotor.IsBackdashing;
                return drivesAttack || drivesIdle;
            }
        }

        public void Configure(
            Animator targetAnimator,
            PrototypeFighterCombat combat,
            AnimationClip idleAnimation)
        {
            animator = targetAnimator;
            fighterCombat = combat;
            fighterMotor = GetComponent<PrototypeFighterMotor>();
            idleClip = idleAnimation;
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (fighterCombat == null) fighterCombat = GetComponent<PrototypeFighterCombat>();
            if (fighterMotor == null) fighterMotor = GetComponent<PrototypeFighterMotor>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }

        private void LateUpdate()
        {
            if (!ShouldDrivePose || animator == null || fighterCombat.CurrentMove == null)
            {
                StopClip();
                return;
            }

            AnimationClip clip = GetClip(fighterCombat.CurrentMove);
            if (clip != activeClip)
            {
                StartClip(clip);
            }

            float normalizedTime = Mathf.Clamp01(
                fighterCombat.CurrentMoveFrame /
                (float)Mathf.Max(1, fighterCombat.CurrentMove.TotalFrames - 1));

            // Combat is frame-authored, so explicitly sample the visual clip at the
            // matching normalized point instead of letting render delta time drift it.
            Vector3 fighterPosition = transform.position;
            Quaternion fighterRotation = transform.rotation;
            Vector3 fighterScale = transform.localScale;
            clipPlayable.SetTime(clip.length * normalizedTime);
            graph.Evaluate(0f);
            ApplyCameraAwayAngle();

            // Imported root curves are presentation data only. The fighter motor owns
            // movement and facing, and the camera follows this stable gameplay root.
            transform.SetPositionAndRotation(fighterPosition, fighterRotation);
            transform.localScale = fighterScale;
        }

        private void StartClip(AnimationClip clip)
        {
            StopClip();
            graph = PlayableGraph.Create("jin_but_korean attack");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            clipPlayable = AnimationClipPlayable.Create(graph, clip);
            clipPlayable.SetApplyFootIK(false);
            clipPlayable.SetApplyPlayableIK(false);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Attack", animator);
            output.SetSourcePlayable(clipPlayable);
            graph.Play();
            clipPlayable.SetSpeed(0d);
            activeClip = clip;
        }

        private void ApplyCameraAwayAngle()
        {
            if (attackCameraAwayAngle <= 0f || animator.avatar == null ||
                !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                return;
            }

            if (fightCamera == null) fightCamera = Camera.main;
            if (fightCamera == null) return;

            Vector3 awayFromCamera = animator.transform.position - fightCamera.transform.position;
            awayFromCamera.y = 0f;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (awayFromCamera.sqrMagnitude < 0.0001f || forward.sqrMagnitude < 0.0001f) return;

            awayFromCamera.Normalize();
            forward.Normalize();
            Vector3 positive = Quaternion.AngleAxis(attackCameraAwayAngle, Vector3.up) * forward;
            Vector3 negative = Quaternion.AngleAxis(-attackCameraAwayAngle, Vector3.up) * forward;
            float signedAngle = Vector3.Dot(positive, awayFromCamera) >= Vector3.Dot(negative, awayFromCamera)
                ? attackCameraAwayAngle
                : -attackCameraAwayAngle;

            if (attackPoseHandler == null)
            {
                attackPoseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
                attackPose = new HumanPose();
            }

            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Vector3 anchoredHipsPosition = hips != null ? hips.position : Vector3.zero;
            attackPoseHandler.GetHumanPose(ref attackPose);
            Quaternion relativeBodyRotation = Quaternion.Inverse(animator.transform.rotation) *
                                              attackPose.bodyRotation;
            attackPose.bodyRotation = Quaternion.AngleAxis(signedAngle, Vector3.up) *
                                      relativeBodyRotation;
            attackPoseHandler.SetHumanPose(ref attackPose);

            // Rotating a Humanoid body can orbit an authored, translated pose around
            // the avatar origin. Offset the body position so the visible fighter stays
            // anchored where the unrotated attack clip placed its hips.
            if (hips != null)
            {
                Vector3 hipsCorrection = anchoredHipsPosition - hips.position;
                if (hipsCorrection.sqrMagnitude > 0.0000001f)
                {
                    float humanScale = Mathf.Max(0.0001f, animator.humanScale);
                    attackPose.bodyPosition += animator.transform.InverseTransformVector(hipsCorrection) /
                                               humanScale;
                    attackPoseHandler.SetHumanPose(ref attackPose);
                }
            }
        }

        private void StopClip()
        {
            if (graph.IsValid()) graph.Destroy();
            activeClip = null;
        }

        private void OnDisable()
        {
            StopClip();
            DisposePoseHandler();
        }

        private void OnDestroy()
        {
            StopClip();
            DisposePoseHandler();
        }

        private void DisposePoseHandler()
        {
            if (attackPoseHandler == null) return;
            attackPoseHandler.Dispose();
            attackPoseHandler = null;
        }

        private AnimationClip GetClip(MoveDefinition move)
        {
            return move != null ? move.animationClip : null;
        }

    }
}
