using UnityEngine;

namespace FightingGame.Prototype
{
    /// <summary>
    /// Accumulated-damage condition display inspired by Buriki One and classic
    /// survival-horror ECG monitors. Damage rises from zero and may exceed 100%.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrototypeDamageHealth : MonoBehaviour
    {
        [Header("Accumulated damage")]
        [SerializeField, Min(1f)] private float dangerThreshold = 100f;
        [SerializeField, Min(0f)] private float accumulatedDamage;
        [SerializeField, Range(0f, 2f)] private float damageGrowthAtDanger = 0.75f;
        [SerializeField, Min(0f)] private float recoveryDelay = 3f;
        [SerializeField, Min(0f)] private float recoveryPerSecond = 1.5f;

        [Header("Condition monitor")]
        [SerializeField, Min(0.1f)] private float calmPulseSpeed = 1.15f;
        [SerializeField, Min(0.1f)] private float dangerPulseSpeed = 5.2f;
        [SerializeField, Range(4f, 24f)] private float waveAmplitude = 13f;
        [SerializeField] private string fighterLabel = "OPPONENT";
        [SerializeField] private bool alignRight = true;

        private Texture2D pixel;
        private GUIStyle titleStyle;
        private GUIStyle valueStyle;
        private double lastDamageTime = -100f;
        private float wavePhase;
        private float currentPulseSpeed;

        public float AccumulatedDamage { get { return accumulatedDamage; } }
        public float DamageRatio { get { return dangerThreshold > 0f ? accumulatedDamage / dangerThreshold : 0f; } }
        public float LastDamageApplied { get; private set; }

        public void ConfigureHud(string label, bool rightAligned)
        {
            fighterLabel = string.IsNullOrEmpty(label) ? "FIGHTER" : label.ToUpperInvariant();
            alignRight = rightAligned;
        }

        public bool ReceiveDamage(float baseAmount, bool strongAttack)
        {
            float vulnerability = Mathf.Clamp01(DamageRatio);
            float multiplier = 1f + vulnerability * damageGrowthAtDanger;
            LastDamageApplied = Mathf.Max(0f, baseAmount) * multiplier;
            accumulatedDamage = Mathf.Max(0f, accumulatedDamage + LastDamageApplied);
            lastDamageTime = CombatClock.TimeSeconds;
            return strongAttack && accumulatedDamage >= dangerThreshold;
        }

        public void ResetDamage()
        {
            accumulatedDamage = 0f;
            LastDamageApplied = 0f;
            lastDamageTime = -100f;
        }

        private void Update()
        {
            float danger = Mathf.Clamp01(DamageRatio);
            float targetPulseSpeed = Mathf.Lerp(
                calmPulseSpeed,
                dangerPulseSpeed,
                Mathf.SmoothStep(0f, 1f, danger));
            if (currentPulseSpeed <= 0f)
            {
                currentPulseSpeed = targetPulseSpeed;
            }
            float speedBlend = 1f - Mathf.Exp(-6f * Time.deltaTime);
            currentPulseSpeed = Mathf.Lerp(currentPulseSpeed, targetPulseSpeed, speedBlend);
            wavePhase = Mathf.Repeat(
                wavePhase + currentPulseSpeed * Mathf.PI * 2f * Time.deltaTime,
                Mathf.PI * 2f);

        }

        public void SimulateTick()
        {
            if (accumulatedDamage <= 0f || CombatClock.TimeSeconds - lastDamageTime < recoveryDelay)
            {
                return;
            }

            accumulatedDamage = Mathf.MoveTowards(
                accumulatedDamage,
                0f,
                recoveryPerSecond * CombatClock.StepSeconds);
        }

        private void OnGUI()
        {
            EnsureGuiResources();

            const float panelWidth = 320f;
            const float panelHeight = 118f;
            float panelX = alignRight ? Screen.width - panelWidth - 18f : 18f;
            Rect panel = new Rect(panelX, 18f, panelWidth, panelHeight);
            Color conditionColor = EvaluateConditionColor();

            Color oldColor = GUI.color;
            GUI.color = new Color(0.015f, 0.025f, 0.025f, 0.92f);
            GUI.DrawTexture(panel, pixel);
            GUI.color = new Color(conditionColor.r, conditionColor.g, conditionColor.b, 0.3f);
            DrawOutline(panel, 2f);

            titleStyle.normal.textColor = conditionColor;
            valueStyle.normal.textColor = conditionColor;
            GUI.Label(new Rect(panel.x + 12f, panel.y + 7f, 180f, 24f), fighterLabel + " CONDITION", titleStyle);
            GUI.Label(new Rect(panel.xMax - 106f, panel.y + 7f, 94f, 24f), GetConditionName(), valueStyle);

            Rect waveRect = new Rect(panel.x + 13f, panel.y + 36f, panel.width - 26f, 54f);
            DrawMonitorGrid(waveRect, conditionColor);
            DrawWave(waveRect, conditionColor);

            GUI.Label(
                new Rect(panel.x + 12f, panel.yMax - 24f, panel.width - 24f, 20f),
                "DAMAGE ACCUMULATED  " + accumulatedDamage.ToString("0") + "%",
                titleStyle);
            GUI.color = oldColor;
        }

        private void DrawWave(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            const float sampleSpacing = 3f;
            const float cycles = 3.25f;
            Vector2 previous = Vector2.zero;

            for (float x = 0f; x <= rect.width; x += sampleSpacing)
            {
                float normalizedX = x / rect.width;
                float signal = Mathf.Sin(normalizedX * cycles * Mathf.PI * 2f - wavePhase);
                Vector2 point = new Vector2(rect.x + x, rect.center.y - signal * waveAmplitude);
                if (x > 0f)
                {
                    DrawLine(previous, point, color, 2f);
                }
                previous = point;
            }
        }

        private void DrawMonitorGrid(Rect rect, Color color)
        {
            Color gridColor = new Color(color.r, color.g, color.b, 0.12f);
            for (int i = 0; i <= 4; i++)
            {
                float y = Mathf.Lerp(rect.y, rect.yMax, i / 4f);
                DrawLine(new Vector2(rect.x, y), new Vector2(rect.xMax, y), gridColor, 1f);
            }
            for (int i = 0; i <= 8; i++)
            {
                float x = Mathf.Lerp(rect.x, rect.xMax, i / 8f);
                DrawLine(new Vector2(x, rect.y), new Vector2(x, rect.yMax), gridColor, 1f);
            }
        }

        private void DrawOutline(Rect rect, float thickness)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), pixel);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), pixel);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), pixel);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), pixel);
        }

        private void DrawLine(Vector2 start, Vector2 end, Color color, float thickness)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            Vector2 difference = end - start;
            float length = difference.magnitude;
            if (length <= 0.001f)
            {
                return;
            }

            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg, start);
            GUI.DrawTexture(new Rect(start.x, start.y - thickness * 0.5f, length, thickness), pixel);
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private Color EvaluateConditionColor()
        {
            float ratio = Mathf.Clamp01(DamageRatio);
            if (ratio < 0.5f)
            {
                return Color.Lerp(new Color(0.12f, 1f, 0.22f), new Color(1f, 0.86f, 0.08f), ratio * 2f);
            }
            return Color.Lerp(new Color(1f, 0.86f, 0.08f), new Color(1f, 0.08f, 0.05f), (ratio - 0.5f) * 2f);
        }

        private string GetConditionName()
        {
            float ratio = DamageRatio;
            if (ratio >= 1f) return "CRITICAL";
            if (ratio >= 0.65f) return "DANGER";
            if (ratio >= 0.3f) return "CAUTION";
            return "FINE";
        }

        private void EnsureGuiResources()
        {
            if (pixel == null)
            {
                pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                pixel.name = "Condition Monitor Pixel";
                pixel.SetPixel(0, 0, Color.white);
                pixel.Apply();
            }

            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label);
                titleStyle.fontSize = 13;
                titleStyle.fontStyle = FontStyle.Bold;
                titleStyle.alignment = TextAnchor.MiddleLeft;
            }
            if (valueStyle == null)
            {
                valueStyle = new GUIStyle(titleStyle);
                valueStyle.alignment = TextAnchor.MiddleRight;
            }
        }

        private void OnDestroy()
        {
            if (pixel != null)
            {
                Destroy(pixel);
            }
        }
    }
}
