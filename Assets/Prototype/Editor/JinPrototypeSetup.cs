using FightingGame.Prototype;
using Combat;
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
        private const string TwoPlayerScenePath = "Assets/Scenes/TwoPlayerSampleScene.unity";
        private const string PrefabPath = "Assets/Prototype/Prefabs/JinPrototype.prefab";
        private const string OctagonMeshPath = "Assets/Prototype/Materials/Prototype_Octagon.asset";
        private const string MovesFolder = "Assets/Prototype/Moves";
        private const string AutoSetupSessionKey = "FightingGame.JinPrototypeSetup.v19";

        private sealed class PrototypeMoveSet
        {
            public MoveDefinition Punch;
            public MoveDefinition HighKick;
            public MoveDefinition LowKick;
            public MoveDefinition Teep;
        }

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
            PrototypeMoveSet moves = CreateOrUpdatePrototypeMoves();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            RemoveExisting("Jin Prototype");
            RemoveExisting("Prototype Ground");
            RemoveExisting("Prototype Opponent");

            GameObject jin = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, scene);
            jin.name = "Jin Prototype";
            jin.transform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.LookRotation(Vector3.right, Vector3.up));

            ApplyMaterials(jin, bodyMaterial, faceMaterial, mouthMaterial);
            NormalizeCharacterSizeAndGround(jin, 1.82f);

            if (jin.GetComponent<JinPrototypeController>() == null)
            {
                jin.AddComponent<JinPrototypeController>();
            }
            ConfigureMoves(jin, moves);

            GameObject opponent = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, scene);
            opponent.name = "Prototype Opponent";
            opponent.transform.SetPositionAndRotation(
                new Vector3(2.6f, 0f, 0f),
                Quaternion.LookRotation(Vector3.left, Vector3.up));
            ApplyMaterials(opponent, bodyMaterial, faceMaterial, mouthMaterial);
            NormalizeCharacterSizeAndGround(opponent, 1.82f);

            JinPrototypeController opponentController = opponent.AddComponent<JinPrototypeController>();
            ConfigureMoves(opponent, moves);
            opponentController.SetPlayerControlled(false);
            opponentController.SetOpponent(jin.transform);
            AddPrototypeHurtbox(opponent);
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
            BuildTwoPlayerScene(scene);
            EnsureSceneInBuildSettings(ScenePath);
            EnsureSceneInBuildSettings(TwoPlayerScenePath);
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = GameObject.Find("Jin Prototype");
            Debug.Log("Jin prototypes ready. The two-player scene is open; use WASD + arrows and IJKL + TFGH.");

            if (showConfirmation)
            {
                EditorUtility.DisplayDialog(
                    "Jin Prototype",
                    "The single-player scene and local two-player scene were rebuilt. TwoPlayerSampleScene is open.",
                    "OK");
            }
        }

        private static void BuildTwoPlayerScene(Scene sourceScene)
        {
            if (!EditorSceneManager.SaveScene(sourceScene, TwoPlayerScenePath, true))
            {
                Debug.LogError("Could not create the two-player scene copy at: " + TwoPlayerScenePath);
                return;
            }

            Scene twoPlayerScene = EditorSceneManager.OpenScene(TwoPlayerScenePath, OpenSceneMode.Single);
            GameObject playerOne = GameObject.Find("Jin Prototype");
            GameObject playerTwo = GameObject.Find("Prototype Opponent");
            GameObject ground = GameObject.Find("Prototype Ground");
            if (playerOne == null || playerTwo == null || ground == null)
            {
                Debug.LogError("The copied scene is missing one or more generated prototype objects.");
                return;
            }

            JinPrototypeController playerOneController = playerOne.GetComponent<JinPrototypeController>();
            JinPrototypeController playerTwoController = playerTwo.GetComponent<JinPrototypeController>();
            playerOneController.SetPlayerControlled(true);
            playerOneController.SetControlProfile(PrototypeControlProfile.PlayerOne);
            playerOneController.SetShowControlHelp(false);
            playerOneController.SetOpponent(playerTwo.transform);
            playerTwoController.SetPlayerControlled(true);
            playerTwoController.SetControlProfile(PrototypeControlProfile.PlayerTwo);
            playerTwoController.SetShowControlHelp(false);
            playerTwoController.SetOpponent(playerOne.transform);

            if (playerOne.GetComponent<CapsuleCollider>() == null)
            {
                AddPrototypeHurtbox(playerOne);
            }

            PrototypeDamageHealth playerOneHealth = playerOne.GetComponent<PrototypeDamageHealth>();
            if (playerOneHealth == null)
            {
                playerOneHealth = playerOne.AddComponent<PrototypeDamageHealth>();
            }
            playerOneHealth.ConfigureHud("PLAYER 1", false);

            PrototypeDamageHealth playerTwoHealth = playerTwo.GetComponent<PrototypeDamageHealth>();
            playerTwoHealth.ConfigureHud("PLAYER 2", true);

            if (playerOne.GetComponent<PrototypeDummyOpponent>() == null)
            {
                playerOne.AddComponent<PrototypeDummyOpponent>();
            }

            TekkenPrototypeCamera fightCamera = Camera.main.GetComponent<TekkenPrototypeCamera>();
            fightCamera.Configure(playerOne.transform, playerTwo.transform);

            PrototypeRingOutReset ringOutReset = ground.GetComponent<PrototypeRingOutReset>();
            ringOutReset.Configure(playerOne.transform, playerTwo.transform, ground.GetComponent<Renderer>());

            EditorSceneManager.MarkSceneDirty(twoPlayerScene);
            EditorSceneManager.SaveScene(twoPlayerScene);
        }

        private static void AddPrototypeHurtbox(GameObject character)
        {
            Bounds bounds = CalculateRendererBounds(character);
            float rootScale = Mathf.Max(0.0001f, Mathf.Abs(character.transform.lossyScale.y));
            CapsuleCollider hurtbox = character.AddComponent<CapsuleCollider>();
            hurtbox.center = character.transform.InverseTransformPoint(bounds.center);
            hurtbox.direction = 1;
            hurtbox.radius = 0.3f / rootScale;
            hurtbox.height = Mathf.Max(
                hurtbox.radius * 2f,
                bounds.size.y * 0.94f / rootScale);
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

        private static PrototypeMoveSet CreateOrUpdatePrototypeMoves()
        {
            if (!AssetDatabase.IsValidFolder(MovesFolder))
            {
                AssetDatabase.CreateFolder("Assets/Prototype", "Moves");
            }

            PrototypeMoveSet set = new PrototypeMoveSet();
            set.Punch = CreateOrUpdateMove(
                "Prototype_StraightPunch", "straight_punch", "Straight Punch", "AttackRight",
                5, 8, 12, 8, HitLevel.High, KnockdownType.None,
                new Vector3(0f, 1.25f, 0.72f), new Vector3(0.25f, 0.16f, 0.28f));
            set.HighKick = CreateOrUpdateMove(
                "Prototype_HighKick", "high_kick", "High Kick", "AttackUp",
                10, 12, 15, 16, HitLevel.High, KnockdownType.None,
                new Vector3(0f, 1.38f, 0.76f), new Vector3(0.275f, 0.23f, 0.31f));
            set.LowKick = CreateOrUpdateMove(
                "Prototype_LowKick", "low_kick", "Low Kick", "AttackDown",
                7, 10, 13, 11, HitLevel.Low, KnockdownType.None,
                new Vector3(0f, 0.46f, 0.68f), new Vector3(0.29f, 0.15f, 0.32f));
            set.Teep = CreateOrUpdateMove(
                "Prototype_StepTeep", "step_teep", "Stepping Teep", "ForwardHeld+Back,Neutral,Forward",
                14, 14, 15, 18, HitLevel.Mid, KnockdownType.HardKnockdown,
                new Vector3(0f, 0.94f, 0.88f), new Vector3(0.29f, 0.21f, 0.34f));
            return set;
        }

        private static MoveDefinition CreateOrUpdateMove(
            string assetName,
            string moveId,
            string displayName,
            string inputCommand,
            int startupFrames,
            int activeFrames,
            int recoveryFrames,
            int damage,
            HitLevel hitLevel,
            KnockdownType knockdown,
            Vector3 localPosition,
            Vector3 halfExtents)
        {
            string path = MovesFolder + "/" + assetName + ".asset";
            MoveDefinition move = AssetDatabase.LoadAssetAtPath<MoveDefinition>(path);
            if (move == null)
            {
                move = ScriptableObject.CreateInstance<MoveDefinition>();
                AssetDatabase.CreateAsset(move, path);
            }

            move.moveId = moveId;
            move.displayName = displayName;
            move.inputCommand = inputCommand;
            move.requiredFighterStates = new[]
            {
                FighterState.Idle,
                FighterState.Walk,
                FighterState.Sidestep,
                FighterState.Run
            };
            move.startupFrames = startupFrames;
            move.activeFrames = activeFrames;
            move.recoveryFrames = recoveryFrames;
            move.damage = damage;
            move.chipDamage = 0;
            move.defaultHitLevel = hitLevel;
            move.knockdownType = knockdown;
            move.animationStateName = displayName.Replace(" ", string.Empty);
            if (move.hitboxes == null) move.hitboxes = new System.Collections.Generic.List<HitboxDefinition>();
            move.hitboxes.Clear();
            move.hitboxes.Add(new HitboxDefinition
            {
                label = moveId,
                shape = HitVolumeShape.Box,
                size = halfExtents,
                localPosition = localPosition,
                startFrame = startupFrames,
                endFrame = startupFrames + activeFrames - 1,
                hitLevel = hitLevel
            });
            EditorUtility.SetDirty(move);
            return move;
        }

        private static void ConfigureMoves(GameObject fighter, PrototypeMoveSet moves)
        {
            PrototypeFighterCombat combat = fighter.GetComponent<PrototypeFighterCombat>();
            if (combat == null) combat = fighter.AddComponent<PrototypeFighterCombat>();
            combat.ConfigureMoves(moves.Punch, moves.HighKick, moves.LowKick, moves.Teep);
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

        private static void EnsureSceneInBuildSettings(string scenePath)
        {
            EditorBuildSettingsScene[] currentScenes = EditorBuildSettings.scenes;
            for (int i = 0; i < currentScenes.Length; i++)
            {
                if (currentScenes[i].path == scenePath)
                {
                    currentScenes[i].enabled = true;
                    EditorBuildSettings.scenes = currentScenes;
                    return;
                }
            }

            EditorBuildSettingsScene[] updatedScenes = new EditorBuildSettingsScene[currentScenes.Length + 1];
            currentScenes.CopyTo(updatedScenes, 0);
            updatedScenes[updatedScenes.Length - 1] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = updatedScenes;
        }
    }
}
