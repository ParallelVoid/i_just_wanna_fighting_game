using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    public enum MoveInputToken
    {
        Neutral,
        AttackForward,
        AttackBack,
        AttackUp,
        AttackDown
    }

    [Serializable]
    public struct MoveCommandStep
    {
        public MoveInputToken token;
        [Min(1)] public int maxDelayFrames;
    }

    /// <summary>A frame-buffered command authored with the move instead of in fighter code.</summary>
    [Serializable]
    public class MoveCommand
    {
        public MoveCommandStep[] steps = Array.Empty<MoveCommandStep>();
        [Tooltip("Commands with more steps win first; priority breaks ties.")]
        public int priority;
        public bool requireForwardHeld;

        public bool Matches(IReadOnlyList<BufferedMoveInput> buffer, bool forwardHeld)
        {
            if (steps == null || steps.Length == 0 || buffer == null || buffer.Count < steps.Length) return false;
            if (requireForwardHeld && !forwardHeld) return false;

            int bufferIndex = buffer.Count - 1;
            long laterFrame = buffer[bufferIndex].frame;
            for (int stepIndex = steps.Length - 1; stepIndex >= 0; stepIndex--, bufferIndex--)
            {
                BufferedMoveInput input = buffer[bufferIndex];
                MoveCommandStep step = steps[stepIndex];
                if (input.token != step.token) return false;
                if (stepIndex < steps.Length - 1 && laterFrame - input.frame > Mathf.Max(1, steps[stepIndex + 1].maxDelayFrames))
                    return false;
                laterFrame = input.frame;
            }
            return true;
        }
    }

    public struct BufferedMoveInput
    {
        public MoveInputToken token;
        public long frame;

        public BufferedMoveInput(MoveInputToken inputToken, long inputFrame)
        {
            token = inputToken;
            frame = inputFrame;
        }
    }
}
