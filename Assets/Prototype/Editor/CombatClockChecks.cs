using System;
using Combat;
using FightingGame.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FightingGame.EditorTools
{
    /// <summary>Play-mode regression checks. Batch: -executeMethod FightingGame.EditorTools.CombatClockChecks.RunBatch</summary>
    [InitializeOnLoad]
    public static class CombatClockChecks
    {
        private const string PendingKey = "FightingGame.CombatClockChecks.Pending";
        private const string BatchKey = "FightingGame.CombatClockChecks.Batch";
        static CombatClockChecks() { EditorApplication.playModeStateChanged += OnPlayMode; }

        [MenuItem("Tools/Jin Prototype/Validate Shared Combat Clock")]
        public static void RunInteractive()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Start(false);
        }

        public static void RunBatch() { Start(true); }

        private static void Start(bool batch)
        {
            SessionState.SetBool("FightingGame.JinPrototypeSetup.v19", true);
            SessionState.SetBool(PendingKey, true);
            SessionState.SetBool(BatchKey, batch);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
            EditorApplication.delayCall += Validate;
        }

        private static void Validate()
        {
            SessionState.SetBool(PendingKey, false);
            int exitCode = 0;
            try
            {
                CheckInput();
                CheckActiveFrames();
                CheckMovementAndRecovery();
                Debug.Log("COMBAT CLOCK CHECKS PASSED: buffered edges/neutral, analog coalescing, startup/active/recovery, catch-up collision, one hit, block, whiff, render-rate equivalence, damage recovery.");
            }
            catch (Exception exception) { Debug.LogException(exception); exitCode = 1; }
            finally
            {
                ClearScene();
                if (SessionState.GetBool(BatchKey, false)) EditorApplication.Exit(exitCode);
                else EditorApplication.ExitPlaymode();
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception("Combat clock regression: " + message);
        }

        private static void ClearScene()
        {
            foreach (var fighter in UnityEngine.Object.FindObjectsOfType<JinPrototypeController>())
                UnityEngine.Object.DestroyImmediate(fighter.gameObject);
            foreach (var input in UnityEngine.Object.FindObjectsOfType<PrototypeFighterInput>())
                UnityEngine.Object.DestroyImmediate(input.gameObject);
            foreach (var clock in UnityEngine.Object.FindObjectsOfType<CombatClock>())
                UnityEngine.Object.DestroyImmediate(clock.gameObject);
        }

        private static void CheckInput()
        {
            ClearScene();
            var input = new GameObject("Input test").AddComponent<PrototypeFighterInput>();
            input.Capture(new PrototypeInputFrame { ForwardHeld = true, ForwardPressed = true, AttackDirection = Vector2.left });
            input.Capture(new PrototypeInputFrame { ForwardHeld = true });
            input.Capture(new PrototypeInputFrame { ForwardHeld = true, AttackDirection = Vector2.right });
            Require(input.ConsumeTick().ForwardPressed, "press lost between render frames");
            Require(input.ConsumeTick().AttackDirection == Vector2.zero, "neutral transition lost");
            Require(input.ConsumeTick().AttackDirection == Vector2.right, "forward transition lost");
            Require(!input.ConsumeTick().ForwardPressed, "press repeated on catch-up tick");
            input.ClearPendingInput();
            Require(!input.ConsumeTick().ForwardHeld, "reset retained held input");
            for (int i = 0; i < 100; i++)
                input.Capture(new PrototypeInputFrame { AttackDirection = new Vector2(0.7f + i * 0.001f, 0), Side = i * 0.001f });
            input.Capture(default(PrototypeInputFrame));
            Require(input.ConsumeTick().AttackDirection.x > 0.79f, "analog sample is stale");
            Require(input.ConsumeTick().AttackDirection == Vector2.zero, "analog samples built up a queue");
        }

        private static JinPrototypeController Fighter(string name, Vector3 position, PrototypeControlProfile profile)
        {
            var obj = new GameObject(name);
            obj.transform.position = position;
            var collider = obj.AddComponent<CapsuleCollider>();
            collider.center = Vector3.up;
            collider.height = 2f;
            collider.radius = 0.3f;
            obj.AddComponent<PrototypeDamageHealth>();
            var fighter = obj.AddComponent<JinPrototypeController>();
            fighter.SetControlProfile(profile);
            fighter.SetShowControlHelp(false);
            return fighter;
        }

        private static CombatClock Pair(out JinPrototypeController attacker, out JinPrototypeController defender, float distance = 0.9f)
        {
            ClearScene();
            attacker = Fighter("P1", Vector3.zero, PrototypeControlProfile.PlayerOne);
            defender = Fighter("P2", new Vector3(0, 0, distance), PrototypeControlProfile.PlayerTwo);
            attacker.SetOpponent(defender.transform);
            defender.SetOpponent(attacker.transform);
            var clock = UnityEngine.Object.FindObjectOfType<CombatClock>();
            clock.enabled = false; // The test supplies elapsed time and input explicitly.
            clock.RefreshParticipants();
            return clock;
        }

        private static void CheckActiveFrames()
        {
            JinPrototypeController a, b;
            var clock = Pair(out a, out b);
            var damage = b.GetComponent<PrototypeDamageHealth>();
            var attack = a.GetComponent<PrototypeFighterCombat>();
            a.GetComponent<PrototypeFighterInput>().Capture(new PrototypeInputFrame { AttackDirection = Vector2.right });
            clock.Advance(5.0 / 60);
            Require(damage.AccumulatedDamage == 0, "hit during startup");
            clock.Advance(1.0 / 60);
            Require(damage.AccumulatedDamage == 8, "first active frame did not hit");
            clock.Advance(19.0 / 60);
            Require(damage.AccumulatedDamage == 8 && !attack.IsAttacking, "move duration or single-hit latch");

            foreach (bool blocked in new[] { false, true })
            {
                clock = Pair(out a, out b);
                var move = ScriptableObject.CreateInstance<MoveDefinition>();
                move.startupFrames = 1; move.activeFrames = 1; move.recoveryFrames = 1;
                move.hitboxes.Add(new HitboxDefinition { startFrame = 1, endFrame = 1,
                    localPosition = new Vector3(0, 1, 0.7f), size = Vector3.one * 0.3f });
                a.GetComponent<PrototypeFighterCombat>().ConfigureMoves(move, move, move, move);
                a.GetComponent<PrototypeFighterInput>().Capture(new PrototypeInputFrame { AttackDirection = Vector2.right });
                if (blocked) b.GetComponent<PrototypeFighterInput>().Capture(new PrototypeInputFrame { ForwardHeld = true, BackwardHeld = true });
                clock.Advance(0.1); // Entire active window lies between renders.
                Require(b.GetComponent<PrototypeDamageHealth>().AccumulatedDamage == (blocked ? 0 : 8), "catch-up hit/block");
                UnityEngine.Object.DestroyImmediate(move);
            }
            clock = Pair(out a, out b, 5);
            a.GetComponent<PrototypeFighterInput>().Capture(new PrototypeInputFrame { AttackDirection = Vector2.right });
            clock.Advance(0.5);
            Require(b.GetComponent<PrototypeDamageHealth>().AccumulatedDamage == 0, "whiff dealt damage");
        }

        private static void CheckMovementAndRecovery()
        {
            Vector3 reference = Vector3.zero;
            foreach (int fps in new[] { 30, 60, 120, 144 })
            {
                JinPrototypeController a, b;
                var clock = Pair(out a, out b, 10);
                a.GetComponent<PrototypeFighterInput>().Capture(new PrototypeInputFrame { ForwardHeld = true, ForwardPressed = true });
                for (int frame = 0; frame < fps; frame++) clock.Advance(1.0 / fps);
                Require(clock.TickIndex == 60, "tick count at " + fps + " FPS");
                if (fps == 30) reference = a.transform.position;
                else Require(Vector3.Distance(reference, a.transform.position) < 0.00001f, "movement differs at " + fps + " FPS");
                var health = b.GetComponent<PrototypeDamageHealth>();
                health.ReceiveDamage(8, false);
                clock.Advance(179.0 / 60);
                Require(health.AccumulatedDamage == 8, "damage recovered before delay");
                clock.Advance(1.0 / 60);
                Require(health.AccumulatedDamage < 8, "damage did not recover on shared time");
            }
        }
    }
}
