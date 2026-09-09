using UnityEngine;
using Combat;

namespace FightingGame.Prototype
{
    /// <summary>
    /// Owns opponent-relative locomotion, movement gestures, blocking, facing, and pushboxes.
    /// Attack state is supplied by the combat component rather than inferred here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrototypeFighterMotor : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 2.25f;
        [SerializeField, Min(0f)] private float acceleration = 14f;
        [SerializeField, Min(0.1f)] private float pushboxRadius = 0.42f;

        [Header("Movement techniques")]
        [SerializeField, Range(0.1f, 0.5f)] private float doubleTapWindow = 0.24f;
        [SerializeField, Min(1f)] private float runSpeedMultiplier = 1.65f;
        [SerializeField, Min(0.1f)] private float backdashSpeed = 5.4f;
        [SerializeField, Range(0.1f, 0.8f)] private float backdashDuration = 0.32f;
        [SerializeField, Min(0f)] private float teepStepSpeed = 2.4f;
        [SerializeField, Min(0.1f)] private float stepFrequency = 2.3f;

        private Transform opponentTarget;
        private float currentSpeed;
        private float currentSideSpeed;
        private float lastForwardTapTime = -10f;
        private float lastBackwardTapTime = -10f;
        private float backdashTimer;

        public float MoveInput { get; private set; }
        public float SideInput { get; private set; }
        public float StepClock { get; private set; }
        public float MoveSpeed { get { return moveSpeed; } }
        public float CurrentPlanarSpeed { get { return new Vector2(currentSpeed, currentSideSpeed).magnitude; } }
        public bool IsMoving { get { return CurrentPlanarSpeed > 0.03f; } }
        public bool IsRunning { get; private set; }
        public bool IsBlocking { get; private set; }
        public bool IsBackdashing { get { return backdashTimer > 0f; } }

        public void SetOpponent(Transform target)
        {
            opponentTarget = target;
            SnapFacingToOpponent();
        }

        public void PrepareFrame(PrototypeInputFrame input, bool isAttacking)
        {
            UpdateMovementGestures(input, isAttacking);
            IsBlocking = input.ForwardHeld && input.BackwardHeld && !isAttacking && !IsBackdashing;
            if (IsBlocking) IsRunning = false;
        }

        public FighterState GetCurrentFighterState(PrototypeInputFrame input)
        {
            if (IsBlocking) return FighterState.Block;
            if (IsBackdashing) return FighterState.Backdash;
            if (IsRunning) return FighterState.Run;
            if (Mathf.Abs(input.Side) > 0.01f) return FighterState.Sidestep;
            if (input.ForwardHeld || input.BackwardHeld) return FighterState.Walk;
            return FighterState.Idle;
        }

        public void Simulate(
            PrototypeInputFrame input,
            PrototypeAttack attack,
            float teepStepEnvelope,
            bool movementLocked)
        {
            MoveInput = 0f;
            SideInput = 0f;
            if (!IsBlocking && !IsBackdashing)
            {
                if (input.ForwardHeld) MoveInput += 1f;
                if (input.BackwardHeld) MoveInput -= 1f;
                SideInput = input.Side;
            }

            if (movementLocked)
            {
                MoveInput = 0f;
                SideInput = 0f;
                currentSpeed = 0f;
                currentSideSpeed = 0f;
            }

            Vector2 desiredMovement = Vector2.ClampMagnitude(new Vector2(MoveInput, SideInput), 1f);
            float movementSpeed = IsRunning && input.ForwardHeld ? moveSpeed * runSpeedMultiplier : moveSpeed;
            float targetSpeed = desiredMovement.x * movementSpeed;
            float targetSideSpeed = desiredMovement.y * moveSpeed;

            if (IsBackdashing)
            {
                backdashTimer = Mathf.Max(0f, backdashTimer - Time.deltaTime);
                float normalizedBackdash = backdashDuration > 0f ? backdashTimer / backdashDuration : 0f;
                currentSpeed = -backdashSpeed * Mathf.Sin(normalizedBackdash * Mathf.PI);
                currentSideSpeed = Mathf.MoveTowards(currentSideSpeed, 0f, acceleration * Time.deltaTime);
            }
            else if (attack == PrototypeAttack.StepTeep)
            {
                currentSpeed = teepStepSpeed * teepStepEnvelope;
                currentSideSpeed = 0f;
            }
            else
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
                currentSideSpeed = Mathf.MoveTowards(currentSideSpeed, targetSideSpeed, acceleration * Time.deltaTime);
            }

            Vector3 position = transform.position;
            Vector3 fightForward = GetFightForward(position);
            Vector3 sidestepDirection = Vector3.Cross(fightForward, Vector3.up).normalized;
            position += (fightForward * currentSpeed + sidestepDirection * currentSideSpeed) * Time.deltaTime;
            transform.position = ResolveFighterSeparation(position);
            SnapFacingToOpponent();

            if (CurrentPlanarSpeed > 0.03f)
            {
                StepClock += Time.deltaTime * stepFrequency *
                             Mathf.Lerp(0.65f, 1f, CurrentPlanarSpeed / moveSpeed);
            }
        }

        public void Stop()
        {
            MoveInput = 0f;
            SideInput = 0f;
            currentSpeed = 0f;
            currentSideSpeed = 0f;
            IsRunning = false;
            IsBlocking = false;
            backdashTimer = 0f;
        }

        public void ResetRuntimeState()
        {
            Stop();
            StepClock = 0f;
            lastForwardTapTime = -10f;
            lastBackwardTapTime = -10f;
        }

        public void SnapFacingToOpponent()
        {
            if (opponentTarget == null) return;
            Vector3 forward = opponentTarget.position - transform.position;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            }
        }

        private void UpdateMovementGestures(PrototypeInputFrame input, bool isAttacking)
        {
            if (input.ForwardPressed && !input.BackwardHeld)
            {
                if (Time.time - lastForwardTapTime <= doubleTapWindow)
                {
                    IsRunning = true;
                    lastForwardTapTime = -10f;
                }
                else lastForwardTapTime = Time.time;
            }
            if (!input.ForwardHeld) IsRunning = false;

            if (input.BackwardPressed && !input.ForwardHeld && !isAttacking)
            {
                if (Time.time - lastBackwardTapTime <= doubleTapWindow)
                {
                    backdashTimer = backdashDuration;
                    lastBackwardTapTime = -10f;
                    IsRunning = false;
                    currentSideSpeed = 0f;
                }
                else lastBackwardTapTime = Time.time;
            }
        }

        private Vector3 GetFightForward(Vector3 position)
        {
            if (opponentTarget == null) return transform.forward;
            Vector3 forward = opponentTarget.position - position;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;
        }

        private Vector3 ResolveFighterSeparation(Vector3 proposedPosition)
        {
            if (opponentTarget == null) return proposedPosition;
            Vector3 separation = proposedPosition - opponentTarget.position;
            separation.y = 0f;
            float minimumDistance = pushboxRadius * 2f;
            if (separation.sqrMagnitude >= minimumDistance * minimumDistance) return proposedPosition;

            Vector3 direction = separation.sqrMagnitude > 0.0001f ? separation.normalized : -transform.forward;
            Vector3 corrected = opponentTarget.position + direction * minimumDistance;
            corrected.y = proposedPosition.y;
            return corrected;
        }
    }
}
