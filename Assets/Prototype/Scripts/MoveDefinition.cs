using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// A single frame-authored move (punch, kick, teep, throw, etc.), as a
    /// data asset rather than a hardcoded animation prototype. Startup/active/
    /// recovery are expressed in simulation frames (see CombatClock), not
    /// seconds, so timing is identical regardless of render framerate.
    ///
    /// Create via: Assets > Create > Combat > Move Definition
    /// </summary>
    [CreateAssetMenu(fileName = "NewMove", menuName = "Combat/Move Definition")]
    public class MoveDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable identifier used for lookups/save data. Do not change once referenced.")]
        public string moveId = "move_id_here";

        public string displayName = "New Move";

        [Header("Input")]
        [Tooltip("The command that triggers this move, e.g. \"AttackRight\", \"Back,Neutral,Forward+MoveHeld\". " +
                 "Free-text for now; can be replaced with a structured command asset later.")]
        public string inputCommand = "";

        [Tooltip("Fighter states this move is legal to start from. Empty = only Idle/Walk/Sidestep/Run.")]
        public FighterState[] requiredFighterStates = { FighterState.Idle, FighterState.Walk };

        [Header("Frame data (simulation frames, 60 Hz)")]
        [Min(0)] public int startupFrames = 6;
        [Min(1)] public int activeFrames = 3;
        [Min(0)] public int recoveryFrames = 12;

        [Header("Damage")]
        public int damage = 8;
        [Tooltip("Damage dealt when the move is blocked (0 for no chip damage).")]
        public int chipDamage = 0;

        [Header("Stun")]
        [Tooltip("Frames the defender is locked in hitstun on a normal hit.")]
        public int hitstunFrames = 14;
        [Tooltip("Frames the defender is locked in blockstun on a blocked hit.")]
        public int blockstunFrames = 10;

        [Header("Guard / hit properties")]
        [Tooltip("Default hit level if a hitbox entry doesn't specify its own.")]
        public HitLevel defaultHitLevel = HitLevel.Mid;

        [Tooltip("Extra hitstun/knockdown applied when this move lands as a counter-hit.")]
        public int counterHitBonusStunFrames = 6;
        public KnockdownType counterHitKnockdown = KnockdownType.None;

        [Header("Pushback (world units)")]
        public float pushbackOnHit = 0.25f;
        public float pushbackOnBlock = 0.15f;

        [Header("Knockdown")]
        public KnockdownType knockdownType = KnockdownType.None;

        [Header("Tracking")]
        [Tooltip("0 = no tracking, fully sidestep-vulnerable. 1 = fully tracks opponent sidesteps.")]
        [Range(0f, 1f)] public float trackingStrength = 0f;

        [Header("Cancels")]
        public CancelCategory cancelCategories = CancelCategory.None;
        [Tooltip("Frame window (inclusive, relative to move start) during which a cancel is allowed. " +
                 "Ignored if cancelCategories is None.")]
        public int cancelWindowStartFrame = 0;
        public int cancelWindowEndFrame = 0;

        [Header("Meter")]
        public int meterCost = 0;
        public int meterGainOnWhiff = 2;
        public int meterGainOnHit = 6;
        public int meterGainOnBlock = 3;

        [Header("Hitbox timeline")]
        public List<HitboxDefinition> hitboxes = new List<HitboxDefinition>();

        [Header("Presentation references")]
        [Tooltip("Animation clip or state name played for this move. Kept as a name/reference, " +
                 "not a hard AnimationClip link, so presentation can swap without touching combat data.")]
        public string animationStateName = "";

        public List<TimedEvent> soundEvents = new List<TimedEvent>();
        public List<TimedEvent> effectEvents = new List<TimedEvent>();

        [Header("Variation requirement")]
        [Tooltip("Empty = available in every variation (part of the shared core kit). " +
                 "Otherwise, the identifier of the one variation that grants this move.")]
        public string requiredVariationId = "";

        // ---- Derived ----

        /// <summary>Total move length in frames: startup + active + recovery.</summary>
        public int TotalFrames => startupFrames + activeFrames + recoveryFrames;

        /// <summary>First/last frame (inclusive) during which the move is in its active window.</summary>
        public int ActiveWindowStart => startupFrames;
        public int ActiveWindowEnd => startupFrames + activeFrames - 1;

        /// <summary>
        /// Validates frame ranges and references. Intended to be called from an
        /// editor validation pass (see Sprint A: "Add editor validation for
        /// missing or invalid frame ranges") as well as at runtime load, so bad
        /// data fails loudly instead of silently misbehaving mid-match.
        /// </summary>
        public bool Validate(out List<string> errors)
        {
            errors = new List<string>();

            if (string.IsNullOrWhiteSpace(moveId))
                errors.Add("moveId is empty.");

            if (activeFrames <= 0)
                errors.Add($"'{displayName}': activeFrames must be at least 1.");

            if (cancelCategories != CancelCategory.None)
            {
                if (cancelWindowEndFrame < cancelWindowStartFrame)
                    errors.Add($"'{displayName}': cancel window end ({cancelWindowEndFrame}) " +
                               $"is before start ({cancelWindowStartFrame}).");

                if (cancelWindowStartFrame < 0 || cancelWindowEndFrame > TotalFrames)
                    errors.Add($"'{displayName}': cancel window [{cancelWindowStartFrame}, " +
                               $"{cancelWindowEndFrame}] falls outside the move's total frames (0-{TotalFrames}).");
            }

            if (hitboxes.Count == 0 && defaultHitLevel != HitLevel.Throw)
            {
                errors.Add($"'{displayName}': has no hitbox timeline entries. " +
                           "A move with no hitboxes can never hit anything.");
            }

            foreach (var hb in hitboxes)
            {
                if (!hb.Validate(out string hbError))
                {
                    errors.Add($"'{displayName}': {hbError}");
                    continue;
                }

                if (hb.startFrame < ActiveWindowStart || hb.endFrame > ActiveWindowEnd)
                {
                    errors.Add($"'{displayName}': hitbox '{hb.label}' window [{hb.startFrame}, {hb.endFrame}] " +
                               $"falls outside the move's active window [{ActiveWindowStart}, {ActiveWindowEnd}]. " +
                               "Either extend activeFrames or fix the hitbox timing.");
                }
            }

            return errors.Count == 0;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Validate(out List<string> errors))
                return;

            foreach (var e in errors)
                Debug.LogWarning($"[MoveDefinition:{name}] {e}", this);
        }
#endif
    }

    /// <summary>
    /// A sound or effect fired at a specific simulation frame within a move
    /// (e.g. a footstep sound on frame 3, an impact spark on the hit frame).
    /// </summary>
    [Serializable]
    public struct TimedEvent
    {
        [Tooltip("Simulation frame, relative to move start, that this event fires on.")]
        public int frame;

        [Tooltip("Identifier looked up by the audio/VFX system, e.g. \"sfx_punch_whiff\", \"vfx_dust_step\".")]
        public string eventId;
    }
}
