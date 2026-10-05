using FightingGame.Prototype;
using Combat;
using UnityEditor;
using UnityEditor.Animations;
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
        private const string JinKoreanPrefabPath = "Assets/Prototype/Prefabs/jin_but_korean.prefab";
        private const string JinKoreanModelPath = "Assets/Prototype/Characters/human_base.fbx";
        private const string JinKoreanControllerPath = "Assets/Prototype/Animations/jin_but_korean.controller";
        private const string JinKoreanIdlePath = "Assets/Animations/idle.fbx";
        private const string MiddleSideKickPath = "Assets/Animations/middlesidekicktrimmed_3ckKXwUzEUFtyFmE63gzFM.fbx";
        private const string CrescentKickPath = "Assets/Animations/crescentkicktrimmed_3ckKXwUzEUFtyFmE63gzFM.fbx";
        private const string LowSideKickPath = "Assets/Animations/lowsidekicktrimmed_3ckKXwUzEUFtyFmE63gzFM.fbx";
        private const string AxeKickPath = "Assets/Animations/axekicktrimmed_3ckKXwUzEUFtyFmE63gzFM.fbx";
        private const string OctagonMeshPath = "Assets/Prototype/Materials/Prototype_Octagon.asset";
        private const string MovesFolder = "Assets/Prototype/Moves";
        private const string PrototypeMovePackPath = MovesFolder + "/Prototype_MovePack.asset";
        private const string JinKoreanMovePackPath = MovesFolder + "/JinKorean/JinKorean_MovePack.asset";
        private const string AutoSetupSessionKey = "FightingGame.JinPrototypeSetup.v24";
        private const string HumanBaseMigrationSessionKey =
            "FightingGame.JinPrototypeSetup.HumanBaseKorean.v4";

        private sealed class PrototypeMoveSet
        {
            public MoveDefinition Punch;
            public MoveDefinition HighKick;
            public MoveDefinition LowKick;
            public MoveDefinition Teep;
            public MovePack PrototypePack;
            public MovePack KoreanPack;
        }

        static JinPrototypeSetup()
        {
            EditorApplication.delayCall += RunAutomaticSetupOnce;
            EditorApplication.delayCall += RebuildKoreanPrefabForHumanBaseOnce;
        }

        private static void RebuildKoreanPrefabForHumanBaseOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                SessionState.GetBool(HumanBaseMigrationSessionKey, false))
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(JinKoreanModelPath) == null ||
                AssetDatabase.LoadAssetAtPath<MovePack>(JinKoreanMovePackPath) == null)
            {
                Debug.LogWarning("Korean fighter migration is waiting for its model or move pack.");
                return;
            }

            RebuildJinKoreanPrefab();
            SessionState.SetBool(HumanBaseMigrationSessionKey, true);
        }

        private static void RunAutomaticSetupOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(AutoSetupSessionKey, false))
            {
                return;
            }

            SessionState.SetBool(AutoSetupSessionKey, true);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(JinKoreanPrefabPath) != null &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(TwoPlayerScenePath) != null)
            {
                return;
            }
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
            GameObject jinKoreanPrefab = CreateOrUpdateJinKoreanPrefab(moves);

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
            ConfigureMoves(jin, moves.PrototypePack);

            GameObject opponent = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, scene);
            opponent.name = "Prototype Opponent";
            opponent.transform.SetPositionAndRotation(
                new Vector3(2.6f, 0f, 0f),
                Quaternion.LookRotation(Vector3.left, Vector3.up));
            ApplyMaterials(opponent, bodyMaterial, faceMaterial, mouthMaterial);
            NormalizeCharacterSizeAndGround(opponent, 1.82f);

            JinPrototypeController opponentController = opponent.AddComponent<JinPrototypeController>();
            ConfigureMoves(opponent, moves.PrototypePack);
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
            BuildTwoPlayerScene(scene, jinKoreanPrefab);
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

        private static void BuildTwoPlayerScene(Scene sourceScene, GameObject jinKoreanPrefab)
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

            Object.DestroyImmediate(playerTwo);
            playerTwo = (GameObject)PrefabUtility.InstantiatePrefab(jinKoreanPrefab, twoPlayerScene);
            playerTwo.name = "jin_but_korean";
            Vector3 playerTwoPosition = playerTwo.transform.position;
            playerTwoPosition.x = 2.6f;
            playerTwoPosition.z = 0f;
            playerTwo.transform.SetPositionAndRotation(
                playerTwoPosition,
                Quaternion.LookRotation(Vector3.left, Vector3.up));

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

        [MenuItem("Tools/Jin Prototype/Rebuild Korean Fighter Prefab")]
        public static void RebuildJinKoreanPrefab()
        {
            AssetDatabase.ImportAsset(JinKoreanModelPath, ImportAssetOptions.ForceUpdate);
            PrototypeMoveSet moves = CreateOrUpdatePrototypeMoves();
            GameObject prefab = CreateOrUpdateJinKoreanPrefab(moves);
            if (prefab == null)
            {
                throw new System.InvalidOperationException(
                    "Could not rebuild jin_but_korean from " + JinKoreanModelPath);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Rebuilt jin_but_korean using " + JinKoreanModelPath);
        }

        private static GameObject CreateOrUpdateJinKoreanPrefab(PrototypeMoveSet moves)
        {
            EnsureFolder("Assets/Prototype/Animations");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(JinKoreanModelPath) == null)
            {
                Debug.LogError("Could not find the jin_but_korean model at: " + JinKoreanModelPath);
                return null;
            }
            EnsureHumanoidModelAndGetAvatar(JinKoreanModelPath);
            AnimationClip idle = EnsureHumanoidClip(JinKoreanIdlePath, true);
            AnimationClip middleSideKick = EnsureHumanoidClip(MiddleSideKickPath);
            AnimationClip crescentKick = EnsureHumanoidClip(CrescentKickPath);
            AnimationClip lowSideKick = EnsureHumanoidClip(LowSideKickPath);
            AnimationClip axeKick = EnsureHumanoidClip(AxeKickPath);
            AnimatorController animationController = CreateOrUpdateJinKoreanAnimator(
                idle, middleSideKick, crescentKick, lowSideKick, axeKick);

            // Korean Jin uses the more complete human_base skeleton. Its imported
            // materials stay intact; the original Jin textures target a different mesh.
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(JinKoreanModelPath);
            GameObject fighter = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            fighter.name = "jin_but_korean";
            fighter.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            NormalizeCharacterSizeAndGround(fighter, 1.82f);

            JinPrototypeController controller = fighter.GetComponent<JinPrototypeController>();
            if (controller == null) controller = fighter.AddComponent<JinPrototypeController>();
            controller.SetPlayerControlled(true);
            controller.SetControlProfile(PrototypeControlProfile.PlayerTwo);
            controller.SetShowControlHelp(false);
            // Imported clips drive Korean Jin's idle and attacks. The procedural
            // Humanoid pose remains available for locomotion and blocking.
            controller.SetUseHumanoidProceduralPose(true);
            ConfigureMoves(fighter, moves.KoreanPack);

            AddPrototypeHurtbox(fighter);
            fighter.AddComponent<PrototypeDamageHealth>();
            fighter.AddComponent<PrototypeDummyOpponent>();

            Animator animator = fighter.GetComponentInChildren<Animator>();
            animator.runtimeAnimatorController = animationController;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            PrototypeFighterCombat combat = fighter.GetComponent<PrototypeFighterCombat>();
            JinKoreanAnimationDriver animationDriver = fighter.AddComponent<JinKoreanAnimationDriver>();
            animationDriver.Configure(animator, combat, idle);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(fighter, JinKoreanPrefabPath);
            Object.DestroyImmediate(fighter);
            return prefab;
        }

        private static Avatar EnsureHumanoidModelAndGetAvatar(string assetPath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer != null && importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.SaveAndReimport();
                importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            }
            if (importer != null && importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
            }

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                Avatar avatar = assets[i] as Avatar;
                if (avatar != null) return avatar;
            }

            Debug.LogError("Could not create a humanoid avatar for jin_but_korean from: " + assetPath);
            return null;
        }

        private static AnimationClip EnsureHumanoidClip(string assetPath, bool loopTime = false)
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer != null)
            {
                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.SaveAndReimport();
                    importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                }
                if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
                {
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    importer.SaveAndReimport();
                    importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                }

                // These animation-only FBXs use hips/spine/etc. Their metadata may
                // retain Jin's root-hips mapping from an earlier copy-avatar import,
                // which prevents Unity from creating an Avatar or exposing a clip.
                HumanDescription description = importer.humanDescription;
                if (!HasHumanBoneMapping(description, "Hips", "hips"))
                {
                    importer.autoGenerateAvatarMappingIfUnspecified = true;
                    importer.humanDescription = new HumanDescription
                    {
                        human = new HumanBone[0],
                        skeleton = new SkeletonBone[0],
                        upperArmTwist = 0.5f,
                        lowerArmTwist = 0.5f,
                        upperLegTwist = 0.5f,
                        lowerLegTwist = 0.5f,
                        armStretch = 0.05f,
                        legStretch = 0.05f,
                        feetSpacing = 0f,
                        hasTranslationDoF = false
                    };
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    importer.SaveAndReimport();
                    importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                }

                bool changed = false;
                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                for (int i = 0; i < clips.Length; i++)
                {
                    ModelImporterClipAnimation clip = clips[i];
                    if (!clip.lockRootRotation || !clip.lockRootHeightY || !clip.lockRootPositionXZ ||
                        clip.loopTime != loopTime)
                    {
                        clip.lockRootRotation = true;
                        clip.lockRootHeightY = true;
                        clip.lockRootPositionXZ = true;
                        clip.loopTime = loopTime;
                        changed = true;
                    }
                }

                if (changed)
                {
                    importer.clipAnimations = clips;
                    importer.SaveAndReimport();
                }
            }

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                AnimationClip clip = assets[i] as AnimationClip;
                if (clip != null && !clip.name.StartsWith("__preview__")) return clip;
            }

            Debug.LogError("Could not load an animation clip from: " + assetPath);
            return null;
        }

        private static bool HasHumanBoneMapping(
            HumanDescription description,
            string humanName,
            string boneName)
        {
            if (description.human == null) return false;
            for (int i = 0; i < description.human.Length; i++)
            {
                HumanBone bone = description.human[i];
                if (bone.humanName == humanName && bone.boneName == boneName) return true;
            }
            return false;
        }

        private static AnimatorController CreateOrUpdateJinKoreanAnimator(
            AnimationClip idleClip,
            AnimationClip middleSideKick,
            AnimationClip crescentKick,
            AnimationClip lowSideKick,
            AnimationClip axeKick)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(JinKoreanControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(JinKoreanControllerPath);
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idle = GetOrCreateState(stateMachine, "Idle");
            idle.motion = idleClip;
            stateMachine.defaultState = idle;
            GetOrCreateState(stateMachine, JinKoreanAnimationDriver.MiddleSideKickState).motion = middleSideKick;
            GetOrCreateState(stateMachine, JinKoreanAnimationDriver.CrescentKickState).motion = crescentKick;
            GetOrCreateState(stateMachine, JinKoreanAnimationDriver.LowSideKickState).motion = lowSideKick;
            GetOrCreateState(stateMachine, JinKoreanAnimationDriver.AxeKickState).motion = axeKick;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorState GetOrCreateState(AnimatorStateMachine stateMachine, string stateName)
        {
            ChildAnimatorState[] states = stateMachine.states;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state.name == stateName) return states[i].state;
            }
            return stateMachine.AddState(stateName);
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            int slash = folderPath.LastIndexOf('/');
            string parent = folderPath.Substring(0, slash);
            string name = folderPath.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void AddPrototypeHurtbox(GameObject character)
        {
            Bounds bounds = CalculateRendererBounds(character);
            float rootScale = Mathf.Max(0.0001f, Mathf.Abs(character.transform.lossyScale.y));
            CapsuleCollider hurtbox = character.AddComponent<CapsuleCollider>();
            hurtbox.center = character.transform.InverseTransformPoint(bounds.center);
            hurtbox.direction = 1;
            hurtbox.radius = 0.23f / rootScale;
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
                new Vector3(0f, 1.25f, 0.72f), new Vector3(0.10f, 0.16f, 0.28f));
            set.HighKick = CreateOrUpdateMove(
                "Prototype_HighKick", "high_kick", "High Kick", "AttackUp",
                10, 12, 15, 16, HitLevel.High, KnockdownType.None,
                new Vector3(0f, 1.38f, 0.76f), new Vector3(0.13f, 0.23f, 0.31f));
            set.LowKick = CreateOrUpdateMove(
                "Prototype_LowKick", "low_kick", "Low Kick", "AttackDown",
                7, 10, 13, 11, HitLevel.Low, KnockdownType.None,
                new Vector3(0f, 0.46f, 0.68f), new Vector3(0.16f, 0.15f, 0.32f));
            set.Teep = CreateOrUpdateMove(
                "Prototype_StepTeep", "step_teep", "Stepping Teep", "ForwardHeld+Back,Neutral,Forward",
                14, 14, 15, 18, HitLevel.Mid, KnockdownType.HardKnockdown,
                new Vector3(0f, 0.94f, 0.88f), new Vector3(0.11f, 0.21f, 0.34f));
            set.PrototypePack = AssetDatabase.LoadAssetAtPath<MovePack>(PrototypeMovePackPath);
            set.KoreanPack = AssetDatabase.LoadAssetAtPath<MovePack>(JinKoreanMovePackPath);
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
            if (move != null) return move;

            move = ScriptableObject.CreateInstance<MoveDefinition>();
            AssetDatabase.CreateAsset(move, path);

            move.moveId = moveId;
            move.displayName = displayName;
            move.inputCommand = inputCommand;
            MoveInputToken token = MoveInputToken.AttackForward;
            if (moveId == "high_kick") token = MoveInputToken.AttackUp;
            else if (moveId == "low_kick") token = MoveInputToken.AttackDown;
            bool isStepTeep = moveId == "step_teep";
            move.command = isStepTeep
                ? new MoveCommand
                {
                    requireForwardHeld = true,
                    priority = 10,
                    steps = new[]
                    {
                        new MoveCommandStep { token = MoveInputToken.AttackBack, maxDelayFrames = 25 },
                        new MoveCommandStep { token = MoveInputToken.Neutral, maxDelayFrames = 25 },
                        new MoveCommandStep { token = MoveInputToken.AttackForward, maxDelayFrames = 25 }
                    }
                }
                : new MoveCommand
                {
                    steps = new[] { new MoveCommandStep { token = token, maxDelayFrames = 25 } }
                };
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
            if (isStepTeep)
            {
                move.movementStartFrame = 0;
                move.movementEndFrame = Mathf.Min(28, move.TotalFrames - 1);
                move.forwardMovementSpeed = 2.4f;
            }
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

        private static void ConfigureMoves(GameObject fighter, MovePack movePack)
        {
            FighterRig rig = fighter.GetComponent<FighterRig>();
            if (rig == null) rig = fighter.AddComponent<FighterRig>();
            if (movePack != null && movePack.fighterId == "jin_prototype")
            {
                rig.SetCustomBindings(new[]
                {
                    new HitboxAnchorBinding { anchor = HitboxAnchor.LeftHand, transformName = "arm left wrist" },
                    new HitboxAnchorBinding { anchor = HitboxAnchor.RightHand, transformName = "arm right wrist" },
                    new HitboxAnchorBinding { anchor = HitboxAnchor.LeftFoot, transformName = "leg left toe" },
                    new HitboxAnchorBinding { anchor = HitboxAnchor.RightFoot, transformName = "leg right toe" }
                });
            }
            else if (movePack != null && movePack.fighterId == "jin_but_korean")
            {
                rig.SetCustomBindings(new[]
                {
                    new HitboxAnchorBinding { anchor = HitboxAnchor.Hips, transformName = "hips" },
                    new HitboxAnchorBinding { anchor = HitboxAnchor.Chest, transformName = "chest" },
                    new HitboxAnchorBinding { anchor = HitboxAnchor.Head, transformName = "head" },
                    new HitboxAnchorBinding { anchor = HitboxAnchor.LeftHand, transformName = "hand.L" },
                    new HitboxAnchorBinding { anchor = HitboxAnchor.RightHand, transformName = "hand.R" },
                    new HitboxAnchorBinding { anchor = HitboxAnchor.LeftFoot, transformName = "toes.L" },
                    new HitboxAnchorBinding { anchor = HitboxAnchor.RightFoot, transformName = "toes.R" }
                });
            }
            PrototypeFighterCombat combat = fighter.GetComponent<PrototypeFighterCombat>();
            if (combat == null) combat = fighter.AddComponent<PrototypeFighterCombat>();
            combat.ConfigureMovePack(movePack);
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
