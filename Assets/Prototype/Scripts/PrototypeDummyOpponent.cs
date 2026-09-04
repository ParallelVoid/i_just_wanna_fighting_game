using UnityEngine;

namespace FightingGame.Prototype
{
    /// <summary>
    /// Lightweight visual reaction for the capsule opponent. This is intentionally
    /// presentation-only until real hitstun and fighter physics are implemented.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrototypeDummyOpponent : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float reactionDuration = 0.32f;
        [SerializeField, Min(0f)] private float minimumPushSpeed = 0.65f;
        [SerializeField, Min(0f)] private float maximumPushSpeed = 1.8f;
        [SerializeField, Range(0f, 30f)] private float maximumLeanAngle = 13f;
        [SerializeField, Range(0f, 0.2f)] private float squashAmount = 0.08f;
        [SerializeField, Min(0.01f)] private float flashDuration = 0.11f;

        [Header("Teep knockdown")]
        [SerializeField, Min(0.05f)] private float knockdownFallDuration = 0.28f;
        [SerializeField, Min(0f)] private float knockdownGroundedDuration = 0.7f;
        [SerializeField, Min(0.05f)] private float knockdownRecoveryDuration = 0.38f;
        [SerializeField, Range(45f, 110f)] private float knockdownAngle = 90f;
        [SerializeField, Min(0f)] private float knockdownDrop = 0.35f;
        [SerializeField, Min(0f)] private float knockdownPushSpeed = 2.4f;

        private Renderer dummyRenderer;
        private Material runtimeMaterial;
        private Color baseColor;
        private Vector3 baseScale;
        private Quaternion baseRotation;
        private Vector3 reactionDirection;
        private float reactionTimer;
        private float flashTimer;
        private float pushSpeed;
        private float leanAngle;
        private bool knockdownActive;
        private float knockdownTimer;
        private float knockdownStartHeight;
        private Vector3 knockdownAxis;

        private void Awake()
        {
            baseScale = transform.localScale;
            baseRotation = transform.rotation;
            dummyRenderer = GetComponentInChildren<Renderer>();
            if (dummyRenderer != null)
            {
                runtimeMaterial = dummyRenderer.material;
                baseColor = runtimeMaterial.color;
            }
        }

        public void ReactToHit(Vector3 hitDirection, float damage, bool knockdown)
        {
            hitDirection.y = 0f;
            reactionDirection = hitDirection.sqrMagnitude > 0.0001f
                ? hitDirection.normalized
                : transform.forward;

            float strength = Mathf.Clamp01(damage / 18f);
            flashTimer = flashDuration;

            if (knockdown)
            {
                StartKnockdown();
                return;
            }

            if (knockdownActive)
            {
                return;
            }

            pushSpeed = Mathf.Lerp(minimumPushSpeed, maximumPushSpeed, strength);
            leanAngle = Mathf.Lerp(maximumLeanAngle * 0.55f, maximumLeanAngle, strength);
            reactionTimer = reactionDuration;
        }

        private void StartKnockdown()
        {
            knockdownActive = true;
            knockdownTimer = 0f;
            knockdownStartHeight = transform.position.y;
            knockdownAxis = Vector3.Cross(Vector3.up, reactionDirection).normalized;
            if (knockdownAxis.sqrMagnitude < 0.0001f)
            {
                knockdownAxis = transform.forward;
            }
            reactionTimer = 0f;
            transform.localScale = baseScale;
        }

        public void ResetReaction()
        {
            reactionTimer = 0f;
            flashTimer = 0f;
            pushSpeed = 0f;
            knockdownActive = false;
            knockdownTimer = 0f;
            transform.localScale = baseScale;
            transform.rotation = baseRotation;
            RestoreColor();
        }

        private void Update()
        {
            UpdateFlash();

            if (knockdownActive)
            {
                UpdateKnockdown();
                return;
            }

            if (reactionTimer <= 0f)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, baseScale, 16f * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, baseRotation, 16f * Time.deltaTime);
                return;
            }

            reactionTimer = Mathf.Max(0f, reactionTimer - Time.deltaTime);
            float progress = reactionDuration > 0f ? 1f - reactionTimer / reactionDuration : 1f;
            float reactionEnvelope = Mathf.Sin(progress * Mathf.PI);
            float pushEnvelope = (1f - progress) * (1f - progress);

            transform.position += reactionDirection * pushSpeed * pushEnvelope * Time.deltaTime;

            Vector3 leanAxis = Vector3.Cross(Vector3.up, reactionDirection);
            if (leanAxis.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.AngleAxis(leanAngle * reactionEnvelope, leanAxis.normalized) * baseRotation;
            }

            Vector3 squashScale = new Vector3(
                baseScale.x * (1f + squashAmount * 0.45f * reactionEnvelope),
                baseScale.y * (1f - squashAmount * reactionEnvelope),
                baseScale.z * (1f + squashAmount * 0.45f * reactionEnvelope));
            transform.localScale = squashScale;

            if (reactionTimer <= 0f)
            {
                transform.localScale = baseScale;
                transform.rotation = baseRotation;
            }
        }

        private void UpdateKnockdown()
        {
            knockdownTimer += Time.deltaTime;
            float fallEnd = knockdownFallDuration;
            float groundedEnd = fallEnd + knockdownGroundedDuration;
            float recoveryEnd = groundedEnd + knockdownRecoveryDuration;
            float angle;
            float heightOffset;

            if (knockdownTimer < fallEnd)
            {
                float fallProgress = Mathf.SmoothStep(0f, 1f, knockdownTimer / knockdownFallDuration);
                angle = knockdownAngle * fallProgress;
                heightOffset = -knockdownDrop * fallProgress;
                float pushEnvelope = 1f - fallProgress;
                transform.position += reactionDirection * knockdownPushSpeed * pushEnvelope * Time.deltaTime;
            }
            else if (knockdownTimer < groundedEnd)
            {
                angle = knockdownAngle;
                heightOffset = -knockdownDrop;
            }
            else if (knockdownTimer < recoveryEnd)
            {
                float recoveryProgress = Mathf.SmoothStep(
                    0f,
                    1f,
                    (knockdownTimer - groundedEnd) / knockdownRecoveryDuration);
                angle = Mathf.Lerp(knockdownAngle, 0f, recoveryProgress);
                heightOffset = Mathf.Lerp(-knockdownDrop, 0f, recoveryProgress);
            }
            else
            {
                knockdownActive = false;
                transform.rotation = baseRotation;
                transform.localScale = baseScale;
                Vector3 recoveredPosition = transform.position;
                recoveredPosition.y = knockdownStartHeight;
                transform.position = recoveredPosition;
                return;
            }

            transform.rotation = Quaternion.AngleAxis(angle, knockdownAxis) * baseRotation;
            transform.localScale = baseScale;
            Vector3 position = transform.position;
            position.y = knockdownStartHeight + heightOffset;
            transform.position = position;
        }

        private void UpdateFlash()
        {
            if (runtimeMaterial == null)
            {
                return;
            }

            if (flashTimer > 0f)
            {
                flashTimer = Mathf.Max(0f, flashTimer - Time.deltaTime);
                float flashAmount = flashDuration > 0f ? flashTimer / flashDuration : 0f;
                runtimeMaterial.color = Color.Lerp(baseColor, Color.white, flashAmount);
            }
            else if (runtimeMaterial.color != baseColor)
            {
                RestoreColor();
            }
        }

        private void RestoreColor()
        {
            if (runtimeMaterial != null)
            {
                runtimeMaterial.color = baseColor;
            }
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }
        }
    }
}
