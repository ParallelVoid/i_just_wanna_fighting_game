using System;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// One hitbox in a move's hitbox timeline. A single MoveDefinition can
    /// carry several of these (e.g. an early knee hitbox followed by a later
    /// shin hitbox on the same kick), each active for its own frame window.
    ///
    /// This is a plain serializable class rather than its own ScriptableObject
    /// because hitboxes only make sense in the context of the move that owns
    /// them — there's no reuse case for sharing one hitbox asset across moves.
    /// </summary>
    [Serializable]
    public class HitboxDefinition
    {
        [Header("Identity")]
        [Tooltip("Optional label for debugging/training-mode display, e.g. \"shin\", \"knee_early\".")]
        public string label = "hitbox";

        [Header("Shape")]
        public HitVolumeShape shape = HitVolumeShape.Box;

        [Tooltip("Half-extents for Box, radius+height for Capsule, radius for Sphere. " +
                 "Interpretation depends on 'shape'; unused fields are ignored.")]
        public Vector3 size = new Vector3(0.3f, 0.3f, 0.3f);

        [Header("Attachment")]
        [Tooltip("Bone this hitbox follows. Leave empty to use fighter-root space instead.")]
        public string attachedBoneName = "";

        [Tooltip("Local offset from the bone (or fighter root if no bone is set).")]
        public Vector3 localPosition;

        [Tooltip("Local rotation offset from the bone (or fighter root if no bone is set).")]
        public Vector3 localEulerRotation;

        [Header("Timing (inclusive, in simulation frames relative to move start)")]
        [Tooltip("First frame this hitbox is active. Must fall within the move's active window.")]
        public int startFrame;

        [Tooltip("Last frame this hitbox is active. Must be >= startFrame.")]
        public int endFrame;

        [Header("Combat properties")]
        public HitLevel hitLevel = HitLevel.Mid;

        [Tooltip("Higher priority wins when two hitboxes would trade/clash on the same frame.")]
        public int hitPriority = 0;

        [Header("Overrides (optional)")]
        [Tooltip("If >= 0, overrides the move's base damage for this specific hitbox.")]
        public int damageOverride = -1;

        [Tooltip("If >= 0, overrides the move's base hitstun for this specific hitbox.")]
        public int hitstunOverride = -1;

        /// <summary>Duration of this hitbox's active window, in frames.</summary>
        public int DurationFrames => Mathf.Max(0, endFrame - startFrame + 1);

        /// <summary>Basic self-consistency check. Move-level context (e.g. whether
        /// this window fits inside the move's active frames) is validated by the
        /// owning MoveDefinition, which has that context.</summary>
        public bool Validate(out string error)
        {
            if (endFrame < startFrame)
            {
                error = $"Hitbox '{label}': endFrame ({endFrame}) is before startFrame ({startFrame}).";
                return false;
            }

            if (size.x <= 0f || size.y <= 0f || (shape == HitVolumeShape.Box && size.z <= 0f))
            {
                error = $"Hitbox '{label}': size must be positive on all dimensions used by shape '{shape}'.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
