using UnityEngine;
using UnityEngine.UI;

namespace FightingGame.CharacterSelect
{
    /// <summary>Renders real particle flames into a UI background without changing the authored selection layout.</summary>
    public sealed class CharacterSelectFireBackground : MonoBehaviour
    {
        [SerializeField] private Canvas targetCanvas = null;
        [SerializeField] private Material particleMaterial = null;
        [SerializeField, Range(0, 1)] private float opacity = .7f;
        [SerializeField, Min(0)] private float flamesPerSecond = 100;
        [SerializeField, Min(0)] private float embersPerSecond = 18;
        [SerializeField, Min(.1f)] private float flameSize = 1;
        [SerializeField] private Color hotColor = new Color(1, .65f, .12f, .8f);
        [SerializeField] private Color flameColor = new Color(1, .18f, .025f, .55f);
        private GameObject renderRoot;
        private RawImage backdrop;
        private RenderTexture renderTexture;
        private ParticleSystem flames;
        private ParticleSystem embers;

        private void OnEnable()
        {
            if (targetCanvas == null || particleMaterial == null)
            {
                Debug.LogWarning("Fire background needs its canvas and particle material assigned.", this);
                return;
            }
            renderTexture = new RenderTexture(960, 540, 16, RenderTextureFormat.ARGB32) { name = "Character Select Fire" };
            renderTexture.Create();
            // Render away from the scene's main camera. No project layers or main camera settings are changed.
            renderRoot = new GameObject("Fire Particle Renderer");
            renderRoot.transform.SetParent(transform, false);
            renderRoot.transform.position = new Vector3(10000, 0, 0);
            var cameraObject = new GameObject("Fire Camera", typeof(Camera));
            cameraObject.transform.SetParent(renderRoot.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 0, -10);
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            camera.aspect = 16f / 9f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 20;
            camera.targetTexture = renderTexture;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            flames = CreateParticles("Flames", false);
            embers = CreateParticles("Embers", true);
            var imageObject = new GameObject("Fire Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            var rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(targetCanvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var background = targetCanvas.transform.Find("Background");
            rect.SetSiblingIndex(background == null ? 0 : background.GetSiblingIndex() + 1);
            backdrop = imageObject.GetComponent<RawImage>();
            backdrop.texture = renderTexture;
            backdrop.raycastTarget = false;
            ApplySettings();
            // Start with a populated fire bed instead of waiting for the first particles to rise.
            flames.Simulate(2.5f, true, true);
            embers.Simulate(3.5f, true, true);
            flames.Play();
            embers.Play();
        }

        private ParticleSystem CreateParticles(string name, bool sparks)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            go.transform.SetParent(renderRoot.transform, false);
            go.transform.localPosition = new Vector3(0, -5.2f, sparks ? -1 : 0);
            var particles = go.GetComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = sparks ? 160 : 500;
            main.startLifetime = new ParticleSystem.MinMaxCurve(sparks ? 2 : 1.2f, sparks ? 4.5f : 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(sparks ? 1.1f : .5f, sparks ? 2 : 1.3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-.2f, .2f);
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(20, .15f, .05f);
            shape.rotation = new Vector3(-90, 0, 0);
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = sparks ? .2f : .3f;
            noise.frequency = .7f;
            noise.scrollSpeed = .4f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .35f), new Keyframe(.25f, 1), new Keyframe(1, .05f)));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = sparks ? 1 : 0;
            return particles;
        }

        private void OnValidate()
        {
            if (Application.isPlaying) ApplySettings();
        }
        private void ApplySettings()
        {
            if (backdrop != null) backdrop.color = new Color(1, 1, 1, opacity);
            Configure(flames, flamesPerSecond, false);
            Configure(embers, embersPerSecond, true);
        }
        private void Configure(ParticleSystem particles, float rate, bool sparks)
        {
            if (particles == null) return;
            var emission = particles.emission;
            emission.rateOverTime = rate;
            var main = particles.main;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve((sparks ? .025f : .4f) * flameSize, (sparks ? .07f : 1f) * flameSize);
            main.startSizeY = new ParticleSystem.MinMaxCurve((sparks ? .04f : .8f) * flameSize, (sparks ? .13f : 2f) * flameSize);
            main.startSizeZ = 1;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(hotColor, 0), new GradientColorKey(flameColor, .55f), new GradientColorKey(new Color(.35f,.025f,.005f), 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(sparks ? .9f : hotColor.a, .12f), new GradientAlphaKey(sparks ? .7f : flameColor.a, .55f), new GradientAlphaKey(0, 1) });
            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(gradient);
        }
        private void OnDisable()
        {
            if (renderRoot != null) { renderRoot.SetActive(false); Destroy(renderRoot); }
            if (backdrop != null) Destroy(backdrop.gameObject);
            if (renderTexture != null) { renderTexture.Release(); Destroy(renderTexture); }
        }
    }
}
