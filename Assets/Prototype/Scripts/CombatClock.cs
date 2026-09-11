using System;
using System.Collections.Generic;
using UnityEngine;

namespace FightingGame.Prototype
{
    /// <summary>One scene clock for existing prototype gameplay. Rendering never skips a combat tick.</summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class CombatClock : MonoBehaviour
    {
        public const int Rate = 60;
        public const float StepSeconds = 1f / Rate;
        private const double Step = 1.0 / Rate;
        private static CombatClock instance;
        private readonly List<JinPrototypeController> fighters = new List<JinPrototypeController>();
        private PrototypeDummyOpponent[] reactions;
        private PrototypeDamageHealth[] health;
        private PrototypeRingOutReset[] rounds;
        private TekkenPrototypeCamera[] cameras;
        private double accumulator;
        public long TickIndex { get; private set; }
        public static double TimeSeconds { get { return instance == null ? 0.0 : instance.TickIndex * Step; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; }

        public static void Register(JinPrototypeController fighter)
        {
            if (instance == null)
            {
                instance = FindObjectOfType<CombatClock>();
                if (instance == null) instance = new GameObject("Combat Clock (60 Hz)").AddComponent<CombatClock>();
            }
            if (!instance.fighters.Contains(fighter))
            {
                instance.fighters.Add(fighter);
                instance.fighters.Sort((a, b) =>
                {
                    int profile = a.GetComponent<PrototypeFighterInput>().ControlProfile.CompareTo(
                        b.GetComponent<PrototypeFighterInput>().ControlProfile);
                    return profile != 0 ? profile : a.GetInstanceID().CompareTo(b.GetInstanceID());
                });
            }
            instance.RefreshParticipants();
        }

        public static void Unregister(JinPrototypeController fighter)
        {
            if (instance != null) instance.fighters.Remove(fighter);
        }

        private void Awake()
        {
            if (instance != null && instance != this) { enabled = false; Destroy(this); return; }
            instance = this;
        }

        private void Start() { RefreshParticipants(); }

        // Also callable after adding prototype health/reaction/round components at runtime.
        public void RefreshParticipants()
        {
            reactions = FindObjectsOfType<PrototypeDummyOpponent>();
            health = FindObjectsOfType<PrototypeDamageHealth>();
            rounds = FindObjectsOfType<PrototypeRingOutReset>();
            cameras = FindObjectsOfType<TekkenPrototypeCamera>();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            foreach (var fighter in fighters) fighter.CaptureInput();
            Advance(Time.deltaTime);
        }

        /// <summary>Consumes elapsed scaled game time. Catch-up ticks each check contact.</summary>
        public void Advance(double elapsedSeconds)
        {
            if (elapsedSeconds < 0 || double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds))
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            accumulator += elapsedSeconds;
            while (accumulator + 1e-10 >= Step)
            {
                accumulator = Math.Max(0.0, accumulator - Step);
                TickIndex++;
                SimulateTick();
            }
        }

        private void SimulateTick()
        {
            // Preserve the current prototype reaction rules, including their control locks.
            foreach (var reaction in reactions)
                if (reaction != null && reaction.isActiveAndEnabled) reaction.SimulateTick();
            foreach (var value in health)
                if (value != null && value.isActiveAndEnabled) value.SimulateTick();
            foreach (var camera in cameras)
                if (camera != null && camera.isActiveAndEnabled) camera.SimulateCountdown();

            foreach (var fighter in fighters) fighter.PrepareCombatTick();
            foreach (var fighter in fighters) fighter.MoveCombatTick();
            Physics.SyncTransforms();
            // Stable P1/P2 resolution preserves single-hit interruption; trade/clash rules are deferred.
            foreach (var fighter in fighters) fighter.ResolveCombatTick();
            foreach (var fighter in fighters) fighter.FinishCombatTick();
            foreach (var round in rounds)
                if (round != null && round.isActiveAndEnabled) round.SimulateTick();
        }

        private void OnDestroy() { if (instance == this) instance = null; }
    }
}
