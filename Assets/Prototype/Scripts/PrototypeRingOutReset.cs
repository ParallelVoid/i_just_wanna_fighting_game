using UnityEngine;

namespace FightingGame.Prototype
{
    /// <summary>
    /// Treats leaving the platform's X/Z bounds as a ring-out. The falling fighter
    /// drops briefly, then both combatants return to their captured spawn transforms.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrototypeRingOutReset : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Transform opponent;
        [SerializeField] private Renderer platformRenderer;
        [SerializeField, Min(0f)] private float edgeInset = 0.12f;
        [SerializeField, Min(0.1f)] private float fallDuration = 0.7f;
        [SerializeField, Min(0f)] private float initialFallSpeed = 1.2f;
        [SerializeField, Min(0f)] private float fallAcceleration = 8f;
        [SerializeField, Min(0.5f)] private float matchResetDelay = 2.25f;

        private JinPrototypeController playerController;
        private JinPrototypeController opponentController;
        private PrototypeDamageHealth playerHealth;
        private PrototypeDamageHealth opponentHealth;
        private PrototypeDummyOpponent playerDummy;
        private PrototypeDummyOpponent opponentDummy;
        private TekkenPrototypeCamera fightCamera;
        private Vector3 playerSpawnPosition;
        private Quaternion playerSpawnRotation;
        private Vector3 opponentSpawnPosition;
        private Quaternion opponentSpawnRotation;
        private Transform fallingFighter;
        private float fallingTime;
        private string matchWinner = string.Empty;
        private float matchEndTimer;
        private bool initialized;

        public bool ControlsLockedForResult
        {
            get { return fallingFighter != null || !string.IsNullOrEmpty(matchWinner); }
        }

        public void Configure(Transform playerTransform, Transform opponentTransform, Renderer platform)
        {
            player = playerTransform;
            opponent = opponentTransform;
            platformRenderer = platform;
            CaptureSpawnState();
        }

        private void Awake()
        {
            CaptureSpawnState();
        }

        private void Update()
        {
            if (!initialized || player == null || opponent == null || platformRenderer == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(matchWinner))
            {
                matchEndTimer += Time.deltaTime;
                if (matchEndTimer >= matchResetDelay)
                {
                    ResetBothFighters();
                }
                return;
            }

            if (fallingFighter == null)
            {
                if (IsOutsidePlatform(player))
                {
                    BeginRingOut(player);
                }
                else if (IsOutsidePlatform(opponent))
                {
                    BeginRingOut(opponent);
                }
                return;
            }

            fallingTime += Time.deltaTime;
            float fallSpeed = initialFallSpeed + fallingTime * fallAcceleration;
            fallingFighter.position += Vector3.down * fallSpeed * Time.deltaTime;

            if (fallingTime >= fallDuration)
            {
                ResetBothFighters();
            }
        }

        private bool IsOutsidePlatform(Transform fighter)
        {
            Vector3 position = fighter.position;
            Bounds worldBounds = platformRenderer.bounds;
            if (position.y < worldBounds.min.y - 0.5f)
            {
                return true;
            }

            MeshFilter meshFilter = platformRenderer.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return position.x < worldBounds.min.x + edgeInset ||
                       position.x > worldBounds.max.x - edgeInset ||
                       position.z < worldBounds.min.z + edgeInset ||
                       position.z > worldBounds.max.z - edgeInset;
            }

            Vector3 localPosition = platformRenderer.transform.InverseTransformPoint(position);
            Bounds localBounds = meshFilter.sharedMesh.bounds;
            float octagonApothem = Mathf.Max(
                0.01f,
                Mathf.Min(localBounds.extents.x, localBounds.extents.z) - edgeInset);
            float absoluteX = Mathf.Abs(localPosition.x);
            float absoluteZ = Mathf.Abs(localPosition.z);

            // A regular octagon with axis-aligned flat edges is the intersection of
            // a square and four diagonal half-planes.
            return absoluteX > octagonApothem ||
                   absoluteZ > octagonApothem ||
                   absoluteX + absoluteZ > octagonApothem * 1.41421356f;
        }

        private void BeginRingOut(Transform fighter)
        {
            fallingFighter = fighter;
            fallingTime = 0f;
            if (playerController != null)
            {
                playerController.SetControlsEnabled(false);
            }
            if (opponentController != null)
            {
                opponentController.SetControlsEnabled(false);
            }
        }

        public void DeclareWinner(string winnerLabel)
        {
            if (!string.IsNullOrEmpty(matchWinner))
            {
                return;
            }

            matchWinner = string.IsNullOrEmpty(winnerLabel) ? "FIGHTER" : winnerLabel.ToUpperInvariant();
            matchEndTimer = 0f;
            if (playerController != null)
            {
                playerController.SetControlsEnabled(false);
            }
            if (opponentController != null)
            {
                opponentController.SetControlsEnabled(false);
            }
        }

        private void ResetBothFighters()
        {
            player.SetPositionAndRotation(playerSpawnPosition, playerSpawnRotation);
            opponent.SetPositionAndRotation(opponentSpawnPosition, opponentSpawnRotation);

            ResetRigidbody(player);
            ResetRigidbody(opponent);

            if (playerController != null)
            {
                playerController.ResetRuntimeState();
                playerController.SetControlsEnabled(playerController.PlayerControlled);
            }
            if (opponentController != null)
            {
                opponentController.ResetRuntimeState();
                opponentController.SetControlsEnabled(opponentController.PlayerControlled);
            }
            if (playerHealth != null)
            {
                playerHealth.ResetDamage();
            }
            if (opponentHealth != null)
            {
                opponentHealth.ResetDamage();
            }
            if (playerDummy != null)
            {
                playerDummy.ResetReaction();
            }
            if (opponentDummy != null)
            {
                opponentDummy.ResetReaction();
            }
            if (fightCamera != null)
            {
                fightCamera.ResetCountdown();
            }

            fallingFighter = null;
            fallingTime = 0f;
            matchWinner = string.Empty;
            matchEndTimer = 0f;
        }

        private void CaptureSpawnState()
        {
            if (player == null || opponent == null || platformRenderer == null)
            {
                initialized = false;
                return;
            }

            playerSpawnPosition = player.position;
            playerSpawnRotation = player.rotation;
            opponentSpawnPosition = opponent.position;
            opponentSpawnRotation = opponent.rotation;
            playerController = player.GetComponent<JinPrototypeController>();
            opponentController = opponent.GetComponent<JinPrototypeController>();
            playerHealth = player.GetComponent<PrototypeDamageHealth>();
            opponentHealth = opponent.GetComponent<PrototypeDamageHealth>();
            playerDummy = player.GetComponent<PrototypeDummyOpponent>();
            opponentDummy = opponent.GetComponent<PrototypeDummyOpponent>();
            fightCamera = FindObjectOfType<TekkenPrototypeCamera>();
            initialized = true;
        }

        private static void ResetRigidbody(Transform fighter)
        {
            Rigidbody body = fighter.GetComponent<Rigidbody>();
            if (body == null)
            {
                return;
            }

            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private void OnGUI()
        {
            if (fallingFighter == null && string.IsNullOrEmpty(matchWinner))
            {
                return;
            }

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 32;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = string.IsNullOrEmpty(matchWinner)
                ? new Color(1f, 0.35f, 0.2f, 1f)
                : new Color(1f, 0.86f, 0.12f, 1f);
            string message = string.IsNullOrEmpty(matchWinner) ? "RING OUT" : matchWinner + " WINS";
            GUI.Label(new Rect(0f, Screen.height * 0.18f, Screen.width, 50f), message, style);
        }
    }
}
