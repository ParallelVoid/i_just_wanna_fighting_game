using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    [CreateAssetMenu(fileName = "NewMovePack", menuName = "Combat/Move Pack")]
    public sealed class MovePack : ScriptableObject
    {
        [Tooltip("Stable fighter or loadout identifier.")]
        public string fighterId;
        public List<MoveDefinition> moves = new List<MoveDefinition>();

        public MoveDefinition Resolve(
            IReadOnlyList<BufferedMoveInput> inputBuffer,
            bool forwardHeld,
            FighterState fighterState)
        {
            MoveDefinition best = null;
            int bestStepCount = -1;
            int bestPriority = int.MinValue;

            for (int i = 0; i < moves.Count; i++)
            {
                MoveDefinition candidate = moves[i];
                if (candidate == null || candidate.command == null ||
                    !candidate.command.Matches(inputBuffer, forwardHeld) || !candidate.AllowsState(fighterState))
                    continue;

                int stepCount = candidate.command.steps.Length;
                if (stepCount > bestStepCount ||
                    (stepCount == bestStepCount && candidate.command.priority > bestPriority))
                {
                    best = candidate;
                    bestStepCount = stepCount;
                    bestPriority = candidate.command.priority;
                }
            }
            return best;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var ids = new HashSet<string>();
            for (int i = 0; i < moves.Count; i++)
            {
                MoveDefinition move = moves[i];
                if (move == null)
                {
                    Debug.LogWarning($"[MovePack:{name}] Move slot {i} is empty.", this);
                    continue;
                }
                if (!ids.Add(move.moveId))
                    Debug.LogWarning($"[MovePack:{name}] Duplicate moveId '{move.moveId}'.", this);
            }
        }
#endif
    }
}
