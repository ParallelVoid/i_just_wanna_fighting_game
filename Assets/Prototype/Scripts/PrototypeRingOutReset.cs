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

        private JinPrototypeController playerController;
        private PrototypeDamageHealth opponentHealth;
        private PrototypeDummyOpponent opponentDummy;
        private TekkenPrototypeCamera fightCamera;
        private Vector3 playerSpawnPosition;
        private Quaternion playerSpawnRotation;
        private Vector3 opponentSpawnPosition;
        private Quaternion opponentSpawnRotation;
        private Transform fallingFighter;
        private float fallingTime;
        private bool initialized;

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
                playerController.SetControlsEnabled(true);
            }
            if (opponentHealth != null)
            {
                opponentHealth.ResetDamage();
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
            opponentHealth = opponent.GetComponent<PrototypeDamageHealth>();
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
            if (fallingFighter == null)
            {
                return;
            }

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 32;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = new Color(1f, 0.35f, 0.2f, 1f);
            GUI.Label(new Rect(0f, Screen.height * 0.18f, Screen.width, 50f), "RING OUT", style);
        }
    }
}
