using UnityEngine;

namespace FightingGame.Prototype
{
    /// <summary>
    /// Keeps a classic 3D-fighter view perpendicular to the line between combatants.
    /// As the player circles, the camera orbits smoothly to maintain readable spacing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TekkenPrototypeCamera : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Transform opponent;
        [SerializeField, Min(1f)] private float baseDistance = 5.4f;
        [SerializeField, Min(0f)] private float height = 2.15f;
        [SerializeField, Min(0f)] private float lookHeight = 0.85f;
        [SerializeField, Min(0.1f)] private float positionSharpness = 7f;
        [SerializeField, Min(0.1f)] private float rotationSharpness = 10f;

        [Header("Round timer")]
        [SerializeField] private bool countdown;
        [SerializeField, Min(1f)] private float countdownSeconds = 90f;

        private Vector3 viewingSide;
        private float remainingSeconds;
        private bool previousCountdownState;
        private GUIStyle timerStyle;
        private GUIStyle timeUpStyle;

        public bool CountdownEnabled { get { return countdown; } }
        public float RemainingSeconds { get { return remainingSeconds; } }

        private void Awake()
        {
            ResetCountdown();
            previousCountdownState = countdown;
        }

        public void Configure(Transform playerTransform, Transform opponentTransform)
        {
            player = playerTransform;
            opponent = opponentTransform;

            Vector3 midpoint = (player.position + opponent.position) * 0.5f;
            viewingSide = transform.position - midpoint;
            viewingSide.y = 0f;
            if (viewingSide.sqrMagnitude < 0.001f)
            {
                Vector3 fightLine = opponent.position - player.position;
                fightLine.y = 0f;
                viewingSide = Vector3.Cross(Vector3.up, fightLine.normalized);
            }
            viewingSide.Normalize();
        }

        public void ResetCountdown()
        {
            remainingSeconds = Mathf.Max(1f, countdownSeconds);
        }

        public void SimulateCountdown()
        {
            if (countdown != previousCountdownState)
            {
                if (countdown)
                {
                    ResetCountdown();
                }
                previousCountdownState = countdown;
            }

            if (countdown && remainingSeconds > 0f)
            {
                remainingSeconds = Mathf.Max(0f, remainingSeconds - CombatClock.StepSeconds);
            }
        }

        private void LateUpdate()
        {
            if (player == null || opponent == null)
            {
                return;
            }

            Vector3 fightLine = opponent.position - player.position;
            fightLine.y = 0f;
            if (fightLine.sqrMagnitude < 0.001f)
            {
                return;
            }

            float separation = fightLine.magnitude;
            fightLine /= separation;
            Vector3 perpendicular = Vector3.Cross(Vector3.up, fightLine).normalized;

            // Keep the camera on its current side of the fight to prevent 180-degree flips.
            if (Vector3.Dot(perpendicular, viewingSide) < 0f)
            {
                perpendicular = -perpendicular;
            }
            viewingSide = perpendicular;

            Vector3 midpoint = (player.position + opponent.position) * 0.5f;
            float framingDistance = Mathf.Max(baseDistance, separation * 1.65f + 1.4f);
            Vector3 desiredPosition = midpoint + viewingSide * framingDistance + Vector3.up * height;
            Vector3 lookTarget = midpoint + Vector3.up * lookHeight;

            float positionBlend = 1f - Mathf.Exp(-positionSharpness * Time.deltaTime);
            float rotationBlend = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, positionBlend);

            Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationBlend);
        }

        private void OnGUI()
        {
            if (!countdown)
            {
                return;
            }

            EnsureTimerStyles();
            if (remainingSeconds <= 0f)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 110f, 18f, 220f, 55f), "TIME UP", timeUpStyle);
                return;
            }

            int displayedSeconds = Mathf.CeilToInt(remainingSeconds);
            timerStyle.normal.textColor = displayedSeconds <= 10
                ? new Color(1f, 0.2f, 0.12f, 1f)
                : Color.white;
            GUI.Label(
                new Rect(Screen.width * 0.5f - 70f, 18f, 140f, 55f),
                displayedSeconds.ToString("00"),
                timerStyle);
        }

        private void EnsureTimerStyles()
        {
            if (timerStyle == null)
            {
                timerStyle = new GUIStyle(GUI.skin.box);
                timerStyle.alignment = TextAnchor.MiddleCenter;
                timerStyle.fontSize = 34;
                timerStyle.fontStyle = FontStyle.Bold;
                timerStyle.normal.textColor = Color.white;
            }

            if (timeUpStyle == null)
            {
                timeUpStyle = new GUIStyle(timerStyle);
                timeUpStyle.fontSize = 30;
                timeUpStyle.normal.textColor = new Color(1f, 0.2f, 0.12f, 1f);
            }
        }
    }
}
