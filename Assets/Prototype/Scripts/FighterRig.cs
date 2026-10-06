using System;
using System.Collections.Generic;
using Combat;
using UnityEngine;

namespace FightingGame.Prototype
{
    [Serializable]
    public struct HitboxAnchorBinding
    {
        public HitboxAnchor anchor;
        [Tooltip("Exact child transform name. Overrides automatic Humanoid resolution.")]
        public string transformName;
    }

    /// <summary>Resolves move-authored semantic anchors against one fighter's skeleton.</summary>
    [DisallowMultipleComponent]
    public sealed class FighterRig : MonoBehaviour
    {
        [SerializeField] private HitboxAnchorBinding[] customBindings = Array.Empty<HitboxAnchorBinding>();
        private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        private Animator animator;

        private void Awake() { RebuildCache(); }

        public void SetCustomBindings(HitboxAnchorBinding[] bindings)
        {
            customBindings = bindings ?? Array.Empty<HitboxAnchorBinding>();
            RebuildCache();
        }

        public void RebuildCache()
        {
            bones.Clear();
            animator = GetComponentInChildren<Animator>();
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++) bones[transforms[i].name.ToLowerInvariant()] = transforms[i];
        }

        public Transform Resolve(HitboxAnchor anchor, string customAnchorName, string legacyBoneName)
        {
            if (anchor == HitboxAnchor.FighterRoot) return transform;

            for (int i = 0; i < customBindings.Length; i++)
            {
                if (customBindings[i].anchor != anchor || string.IsNullOrWhiteSpace(customBindings[i].transformName)) continue;
                Transform custom = FindByName(customBindings[i].transformName);
                if (custom != null) return custom;
            }

            Transform humanoid = ResolveHumanoid(anchor);
            if (humanoid != null) return humanoid;
            if (anchor == HitboxAnchor.Custom && !string.IsNullOrWhiteSpace(customAnchorName))
                return FindByName(customAnchorName);
            if (!string.IsNullOrWhiteSpace(legacyBoneName)) return FindByName(legacyBoneName);
            return null;
        }

        private Transform ResolveHumanoid(HitboxAnchor anchor)
        {
            if (animator == null || !animator.isHuman) return null;
            switch (anchor)
            {
                case HitboxAnchor.Hips: return animator.GetBoneTransform(HumanBodyBones.Hips);
                case HitboxAnchor.Chest: return animator.GetBoneTransform(HumanBodyBones.Chest);
                case HitboxAnchor.Head: return animator.GetBoneTransform(HumanBodyBones.Head);
                case HitboxAnchor.LeftHand: return animator.GetBoneTransform(HumanBodyBones.LeftHand);
                case HitboxAnchor.RightHand: return animator.GetBoneTransform(HumanBodyBones.RightHand);
                case HitboxAnchor.LeftFoot:
                    return animator.GetBoneTransform(HumanBodyBones.LeftToes) ?? animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                case HitboxAnchor.RightFoot:
                    return animator.GetBoneTransform(HumanBodyBones.RightToes) ?? animator.GetBoneTransform(HumanBodyBones.RightFoot);
                default: return null;
            }
        }

        private Transform FindByName(string boneName)
        {
            Transform result;
            return bones.TryGetValue(boneName.ToLowerInvariant(), out result) ? result : null;
        }
    }
}
