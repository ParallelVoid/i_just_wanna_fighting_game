using System.Collections.Generic;
using UnityEngine;

namespace FightingGame.Prototype
{
    public enum PrototypeControlProfile
    {
        PlayerOne,
        PlayerTwo
    }

    public struct PrototypeInputFrame
    {
        public bool ForwardHeld;
        public bool BackwardHeld;
        public bool ForwardPressed;
        public bool BackwardPressed;
        public float Side;
        public Vector2 AttackDirection;
    }

    /// <summary>
    /// Samples one fighter's controls. It intentionally contains no movement or
    /// combat rules, so keyboard/gamepad bindings can be replaced independently.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrototypeFighterInput : MonoBehaviour
    {
        [SerializeField] private PrototypeControlProfile controlProfile = PrototypeControlProfile.PlayerOne;

        private readonly List<PrototypeInputFrame> pending = new List<PrototypeInputFrame>();
        private PrototypeInputFrame lastCaptured;
        private PrototypeInputFrame held;

        // Queue changes, not every render sample: retain a tap or neutral transition between ticks.
        public void Capture() { Capture(Sample()); }

        public void Capture(PrototypeInputFrame frame)
        {
            bool transition = frame.ForwardHeld != lastCaptured.ForwardHeld ||
                frame.BackwardHeld != lastCaptured.BackwardHeld ||
                AttackRegion(frame.AttackDirection) != AttackRegion(lastCaptured.AttackDirection) ||
                frame.ForwardPressed || frame.BackwardPressed;
            if (transition) pending.Add(frame);
            else if (pending.Count > 0)
            {
                // Analog drift within a command region must not create a growing input delay.
                var previous = pending[pending.Count - 1];
                frame.ForwardPressed |= previous.ForwardPressed;
                frame.BackwardPressed |= previous.BackwardPressed;
                pending[pending.Count - 1] = frame;
            }
            else held = frame;
            lastCaptured = frame;
        }

        private static int AttackRegion(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.36f) return 0;
            return 1 | (direction.x < -0.45f ? 2 : 0) | (direction.x > 0.45f ? 4 : 0) |
                (direction.x > 0.35f ? 8 : 0) | (direction.y > 0.45f ? 16 : 0) |
                (direction.y < -0.45f ? 32 : 0);
        }

        public PrototypeInputFrame ConsumeTick()
        {
            if (pending.Count > 0)
            {
                held = pending[0];
                pending.RemoveAt(0);
            }
            PrototypeInputFrame result = held;
            held.ForwardPressed = false;
            held.BackwardPressed = false;
            return result;
        }

        public void ClearPendingInput()
        {
            pending.Clear();
            lastCaptured = default(PrototypeInputFrame);
            held = default(PrototypeInputFrame);
        }

        public PrototypeControlProfile ControlProfile { get { return controlProfile; } }

        public void SetControlProfile(PrototypeControlProfile profile)
        {
            controlProfile = profile;
        }

        public PrototypeInputFrame Sample()
        {
            bool playerOne = controlProfile == PrototypeControlProfile.PlayerOne;
            KeyCode forwardKey = playerOne ? KeyCode.D : KeyCode.L;
            KeyCode backwardKey = playerOne ? KeyCode.A : KeyCode.J;

            PrototypeInputFrame frame = new PrototypeInputFrame
            {
                ForwardHeld = Input.GetKey(forwardKey) || (playerOne && Input.GetKey(KeyCode.JoystickButton5)),
                BackwardHeld = Input.GetKey(backwardKey) || (playerOne && Input.GetKey(KeyCode.JoystickButton4)),
                ForwardPressed = Input.GetKeyDown(forwardKey) || (playerOne && Input.GetKeyDown(KeyCode.JoystickButton5)),
                BackwardPressed = Input.GetKeyDown(backwardKey) || (playerOne && Input.GetKeyDown(KeyCode.JoystickButton4)),
                Side = ReadSideInput(playerOne),
                AttackDirection = ReadAttackDirection(playerOne)
            };
            return frame;
        }

        private static float ReadSideInput(bool playerOne)
        {
            float side = 0f;
            KeyCode positiveKey = playerOne ? KeyCode.W : KeyCode.I;
            KeyCode negativeKey = playerOne ? KeyCode.S : KeyCode.K;
            if (Input.GetKey(positiveKey)) side += 1f;
            if (Input.GetKey(negativeKey)) side -= 1f;
            return side;
        }

        private Vector2 ReadAttackDirection(bool playerOne)
        {
            Vector2 direction = Vector2.zero;
            if (playerOne)
            {
                if (Input.GetKey(KeyCode.RightArrow)) direction.x += 1f;
                if (Input.GetKey(KeyCode.LeftArrow)) direction.x -= 1f;
                if (Input.GetKey(KeyCode.UpArrow)) direction.y += 1f;
                if (Input.GetKey(KeyCode.DownArrow)) direction.y -= 1f;
            }
            else
            {
                if (Input.GetKey(KeyCode.H)) direction.x += 1f;
                if (Input.GetKey(KeyCode.F)) direction.x -= 1f;
                if (Input.GetKey(KeyCode.T)) direction.y += 1f;
                if (Input.GetKey(KeyCode.G)) direction.y -= 1f;
            }

            if (direction.sqrMagnitude > 0f)
            {
                return Vector2.ClampMagnitude(direction, 1f);
            }

            if (!playerOne)
            {
                return Vector2.zero;
            }

            // The legacy Horizontal/Vertical axes also include WASD. Suppress that
            // contribution while movement keys are held so only a gamepad stick attacks.
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) ||
                Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D))
            {
                return Vector2.zero;
            }

            return Vector2.ClampMagnitude(
                new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
        }
    }
}
