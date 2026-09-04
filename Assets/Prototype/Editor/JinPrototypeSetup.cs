using FightingGame.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FightingGame.EditorTools
{
    /// <summary>
    /// Builds a reproducible sample scene around the imported Jin FBX.
    /// The one-shot automatic setup lets the project configure itself after script import;
    /// it can also be rerun from Tools/Jin Prototype/Rebuild Sample Scene.
    /// </summary>
    [InitializeOnLoad]
    public static class JinPrototypeSetup
    {
        private const string ModelPath = "Assets/jin-kazama/source/Jin Kazama From King Of Fighters All Star/Jin Kazama From King Of Fighters All Star.fbx";
        private const string BodyTexturePath = "Assets/jin-kazama/textures/Ch_Jin_Body_D.png";
        private const string FaceTexturePath = "Assets/jin-kazama/textures/Ch_Jin_Face_D.png";
        private const string MouthTexturePath = "Assets/jin-kazama/textures/mouthinn_d.png";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string PrefabPath = "Assets/Prototype/Prefabs/JinPrototype.prefab";
        private const string OctagonMeshPath = "Assets/Prototype/Materials/Prototype_Octagon.asset";
        private const string AutoSetupSessionKey = "FightingGame.JinPrototypeSetup.v14";

        static JinPrototypeSetup()
        {
            EditorApplication.delayCall += RunAutomaticSetupOnce;
        }

        private static void RunAutomaticSetupOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(AutoSetupSessionKey, false))
            {
                return;
            }

            SessionState.SetBool(AutoSetupSessionKey, true);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null)
            {
                Debug.LogWarning("Jin prototype setup could not find the FBX at: " + ModelPath);
                return;
            }

            BuildSampleScene(false);
        }

        [MenuItem("Tools/Jin Prototype/Rebuild Sample Scene")]
        public static void RebuildSampleScene()
        {
            BuildSampleScene(true);
        }

        private static void BuildSampleScene(bool showConfirmation)
        {
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            Texture2D bodyTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(BodyTexturePath);
            Texture2D faceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(FaceTexturePath);
            Texture2D mouthTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(MouthTexturePath);

            if (modelAsset == null || bodyTexture == null || faceTexture == null || mouthTexture == null)
            {
                Debug.LogError("Jin prototype setup is missing the model or one of its three textures.");
                return;
            }

            Material bodyMaterial = CreateOrUpdateMaterial("Assets/Prototype/Materials/Jin_Body.mat", bodyTexture, 0.18f);
            Material faceMaterial = CreateOrUpdateMaterial("Assets/Prototype/Materials/Jin_Face.mat", faceTexture, 0.2f);
            Material mouthMaterial = CreateOrUpdateMaterial("Assets/Prototype/Materials/Jin_Mouth.mat", mouthTexture, 0.05f);
            Material groundMaterial = CreateOrUpdateGroundMaterial();
            Material opponentMaterial = CreateOrUpdateColorMaterial(
                "Assets/Prototype/Materials/Prototype_Opponent.mat",
                new Color(0.28f, 0.07f, 0.08f, 1f));

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            RemoveExisting("Jin Prototype");
            RemoveExisting("Prototype Ground");
            RemoveExisting("Prototype Opponent");

            GameObject jin = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, scene);
            jin.name = "Jin Prototype";
            jin.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 180f, 0f));

            ApplyMaterials(jin, bodyMaterial, faceMaterial, mouthMaterial);
            NormalizeCharacterSizeAndGround(jin, 1.82f);

            if (jin.GetComponent<JinPrototypeController>() == null)
            {
                jin.AddComponent<JinPrototypeController>();
            }

            GameObject opponent = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            opponent.name = "Prototype Opponent";
            opponent.transform.position = new Vector3(2.6f, 0.9f, 0f);
            opponent.transform.localScale = new Vector3(0.55f, 0.9f, 0.55f);
            opponent.GetComponent<Renderer>().sharedMaterial = opponentMaterial;
            opponent.AddComponent<PrototypeDamageHealth>();
            opponent.AddComponent<PrototypeDummyOpponent>();

            Mesh octagonMesh = CreateOrUpdateOctagonMesh();
            GameObject ground = new GameObject(
                "Prototype Ground",
                typeof(MeshFilter),
                typeof(MeshRenderer),
                typeof(MeshCollider));
            ground.transform.position = new Vector3(0f, -0.075f, 0f);
            ground.GetComponent<MeshFilter>().sharedMesh = octagonMesh;
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
            ground.GetComponent<MeshCollider>().sharedMesh = octagonMesh;

            JinPrototypeController controller = jin.GetComponent<JinPrototypeController>();
            controller.SetOpponent(opponent.transform);
            ConfigureCamera(jin, opponent);
            ConfigureLight();

            PrefabUtility.SaveAsPrefabAssetAndConnect(jin, PrefabPath, InteractionMode.AutomatedAction);
            jin.GetComponent<JinPrototypeController>().SetOpponent(opponent.transform);

            PrototypeRingOutReset ringOutReset = ground.GetComponent<PrototypeRingOutReset>();
            if (ringOutReset == null)
            {
                ringOutReset = ground.AddComponent<PrototypeRingOutReset>();
            }
            ringOutReset.Configure(jin.transform, opponent.transform, ground.GetComponent<Renderer>());

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EnsureSceneInBuildSettings();
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = jin;
            Debug.Log("Jin prototype ready. Enter Play Mode and use A/D or the arrow keys.");

            if (showConfirmation)
            {
                EditorUtility.DisplayDialog("Jin Prototype", "The textured Jin prefab and movement sample scene were rebuilt.", "OK");
            }
        }

        private static Mesh CreateOrUpdateOctagonMesh()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(OctagonMeshPath);
            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.name = "Prototype Octagon Platform";
                AssetDatabase.CreateAsset(mesh, OctagonMeshPath);
            }

            const int sides = 8;
            const float radius = 5f;
            const float halfHeight = 0.075f;
            Vector3[] vertices = new Vector3[2 + sides * 2];
            int[] triangles = new int[sides * 12];
            vertices[0] = Vector3.up * halfHeight;
            vertices[1] = Vector3.down * halfHeight;

            for (int i = 0; i < sides; i++)
            {
                float angle = (22.5f + i * 45f) * Mathf.Deg2Rad;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                vertices[2 + i] = new Vector3(x, halfHeight, z);
                vertices[2 + sides + i] = new Vector3(x, -halfHeight, z);
            }

            int triangleIndex = 0;
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                int top = 2 + i;
                int topNext = 2 + next;
                int bottom = 2 + sides + i;
                int bottomNext = 2 + sides + next;

                // Top, bottom, then two outward-facing side triangles.
                triangles[triangleIndex++] = 0;
                triangles[triangleIndex++] = topNext;
                triangles[triangleIndex++] = top;
                triangles[triangleIndex++] = 1;
                triangles[triangleIndex++] = bottom;
                triangles[triangleIndex++] = bottomNext;
                triangles[triangleIndex++] = top;
                triangles[triangleIndex++] = topNext;
                triangles[triangleIndex++] = bottomNext;
                triangles[triangleIndex++] = top;
                triangles[triangleIndex++] = bottomNext;
                triangles[triangleIndex++] = bottom;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material CreateOrUpdateMaterial(string path, Texture2D texture, float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Lit");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateOrUpdateGroundMaterial()
        {
            const string path = "Assets/Prototype/Materials/Prototype_Ground.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = new Color(0.075f, 0.085f, 0.105f, 1f);
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.12f);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateOrUpdateColorMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.16f);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ApplyMaterials(GameObject root, Material body, Material face, Material mouth)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                Material[] source = renderer.sharedMaterials;
                Material[] replacements = new Material[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    string materialName = source[i] != null ? source[i].name.ToLowerInvariant() : renderer.name.ToLowerInvariant();
                    replacements[i] = ChooseMaterial(materialName, body, face, mouth);
                }
                renderer.sharedMaterials = replacements;
            }
        }

        private static Material ChooseMaterial(string name, Material body, Material face, Material mouth)
        {
            if (name.Contains("mouth") || name.Contains("teeth") || name.Contains("tongue"))
            {
                return mouth;
            }

            if (name.Contains("face") || name.Contains("skin") || name.Contains("fringe") ||
                name.Contains("eyeball") || name.Contains("eye"))
            {
                return face;
            }

            // Costume, hood, neck strip, belt, shoes, hair, and any unknown body
            // slots use the packed body/costume texture.
            return body;
        }

        private static void NormalizeCharacterSizeAndGround(GameObject character, float targetHeight)
        {
            Bounds bounds = CalculateRendererBounds(character);
            if (bounds.size.y > 0.001f)
            {
                float scale = targetHeight / bounds.size.y;
                character.transform.localScale = Vector3.one * scale;
                bounds = CalculateRendererBounds(character);
            }

            character.transform.position += Vector3.up * -bounds.min.y;
        }

        private static Bounds CalculateRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(root.transform.position, Vector3.one);
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }

        private static void ConfigureCamera(GameObject character, GameObject opponent)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            Bounds bounds = CalculateRendererBounds(character);
            Vector3 target = new Vector3(0f, bounds.center.y, 0f);
            camera.transform.position = target + new Vector3(0f, 0.15f, -4.4f);
            camera.transform.LookAt(target + Vector3.up * 0.05f);
            camera.fieldOfView = 38f;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.065f, 1f);

            TekkenPrototypeCamera fightCamera = camera.GetComponent<TekkenPrototypeCamera>();
            if (fightCamera == null)
            {
                fightCamera = camera.gameObject.AddComponent<TekkenPrototypeCamera>();
            }
            fightCamera.Configure(character.transform, opponent.transform);
        }

        private static void ConfigureLight()
        {
            Light light = Object.FindObjectOfType<Light>();
            if (light == null)
            {
                GameObject lightObject = new GameObject("Directional Light");
                light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
            }

            light.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
        }

        private static void RemoveExisting(string objectName)
        {
            GameObject existing = GameObject.Find(objectName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }
        }

        private static void EnsureSceneInBuildSettings()
        {
            EditorBuildSettingsScene[] currentScenes = EditorBuildSettings.scenes;
            for (int i = 0; i < currentScenes.Length; i++)
            {
                if (currentScenes[i].path == ScenePath)
                {
                    currentScenes[i].enabled = true;
                    EditorBuildSettings.scenes = currentScenes;
                    return;
                }
            }

            EditorBuildSettingsScene[] updatedScenes = new EditorBuildSettingsScene[currentScenes.Length + 1];
            currentScenes.CopyTo(updatedScenes, 0);
            updatedScenes[updatedScenes.Length - 1] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = updatedScenes;
        }
    }
}
