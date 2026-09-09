using System;

namespace Combat
{
    /// <summary>
    /// How a move must be blocked/guarded against, or whether it can be
    /// guarded at all.
    /// </summary>
    public enum HitLevel
    {
        High,
        Mid,
        Low,
        Throw,
        Unblockable
    }

    /// <summary>
    /// Shape used for a hitbox/hurtbox volume.
    /// </summary>
    public enum HitVolumeShape
    {
        Sphere,
        Capsule,
        Box
    }

    /// <summary>
    /// What kind of knockdown a landed hit causes, if any.
    /// </summary>
    public enum KnockdownType
    {
        None,
        Stagger,
        SoftKnockdown,
        HardKnockdown,
        RingOut
    }

    /// <summary>
    /// Broad combat states referenced by "Required fighter state" and by the
    /// combat-state priority list in the design doc. Kept intentionally
    /// coarse here; a real FighterStateMachine can subdivide further.
    /// </summary>
    public enum FighterState
    {
        Idle,
        Walk,
        Sidestep,
        Run,
        Backdash,
        Block,
        Attacking,
        Hitstun,
        Blockstun,
        Thrown,
        Knockdown,
        RingOutFalling
    }

    /// <summary>
    /// Whether/when a move can be canceled into another action.
    /// Concrete cancel windows (frame ranges) live on MoveDefinition;
    /// this just tags the category so tooling/UI can group moves.
    /// </summary>
    [Flags]
    public enum CancelCategory
    {
        None = 0,
        SelfCancelOnHit = 1 << 0,
        SelfCancelOnBlock = 1 << 1,
        SpecialCancel = 1 << 2,
        MovementCancel = 1 << 3
    }
}
