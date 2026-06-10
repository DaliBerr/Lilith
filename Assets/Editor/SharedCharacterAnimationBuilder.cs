using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.U2D.Animation;

internal static class SharedCharacterAnimationBuilder
{
    public const string Root = "Assets/Animation/SharedCharacter";
    public const string ClipsFolder = Root + "/Clips";
    public const string LibrariesFolder = Root + "/Libraries";
    public const string HeroLibraryPath = LibrariesFolder + "/Hero.spriteLib.asset";
    public const string ControllerPath = Root + "/Shared_4Dir.controller";
    public const string SourceTexturePath = "Assets/Animation/character.png";
    public const string PlayerPrefabPath = "Assets/Prefabs/Player/Test2DCharacterSprite.prefab";
    public const string Category = "Character";

    private const string SpriteHashProperty = "m_SpriteHash";
    private const string ActorSortingLayerName = "Default";
    private const int ActorSortingOrder = 20;

    private static readonly (string Label, string SpriteName)[] LabelSprites =
    {
        ("IdleDown_0", "character_0"),
        ("IdleRight_0", "character_8"),
        ("IdleLeft_0", "character_28"),
        ("IdleUp_0", "character_19"),
        ("WalkDown_0", "character_0"),
        ("WalkDown_1", "character_1"),
        ("WalkDown_2", "character_2"),
        ("WalkDown_3", "character_3"),
        ("WalkRight_0", "character_8"),
        ("WalkRight_1", "character_9"),
        ("WalkRight_2", "character_10"),
        ("WalkRight_3", "character_11"),
        ("WalkLeft_0", "character_28"),
        ("WalkLeft_1", "character_29"),
        ("WalkLeft_2", "character_30"),
        ("WalkLeft_3", "character_31"),
        ("WalkUp_0", "character_19"),
        ("WalkUp_1", "character_20"),
        ("WalkUp_2", "character_21"),
        ("WalkUp_3", "character_22"),
    };

    [MenuItem("Tools/Lilith/2D/Build Shared Character Animation")]
    public static void Build()
    {
        EnsureFolder(Root);
        EnsureFolder(ClipsFolder);
        EnsureFolder(LibrariesFolder);

        Dictionary<string, Sprite> sprites = LoadSourceSprites();
        SpriteLibraryAsset heroLibrary = CreateOrUpdateHeroLibrary(sprites);
        AnimationClip[] clips = CreateOrUpdateClips();
        AnimatorController controller = CreateOrUpdateController(clips);
        WireTestPlayerPrefab(heroLibrary, controller);
        ValidateResolverLookup(heroLibrary, sprites);
        ValidateClipSampling(heroLibrary, sprites, clips);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Lilith shared 2D character animation assets generated and Test2DCharacterSprite prefab wired.");
    }

    private static Dictionary<string, Sprite> LoadSourceSprites()
    {
        Dictionary<string, Sprite> sprites = AssetDatabase.LoadAllAssetsAtPath(SourceTexturePath)
            .OfType<Sprite>()
            .ToDictionary(sprite => sprite.name, sprite => sprite);
        if (sprites.Count == 0)
        {
            throw new InvalidOperationException($"No sprites loaded from {SourceTexturePath}.");
        }

        string[] missing = LabelSprites
            .Select(mapping => mapping.SpriteName)
            .Where(spriteName => !sprites.ContainsKey(spriteName))
            .Distinct()
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"Missing sprites in {SourceTexturePath}: {string.Join(", ", missing)}.");
        }

        return sprites;
    }

    private static SpriteLibraryAsset CreateOrUpdateHeroLibrary(IReadOnlyDictionary<string, Sprite> sprites)
    {
        SpriteLibraryAsset library = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(HeroLibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<SpriteLibraryAsset>();
            library.name = "Hero";
            AssetDatabase.CreateAsset(library, HeroLibraryPath);
        }
        else
        {
            SerializedObject serializedLibrary = new(library);
            serializedLibrary.FindProperty("m_Labels").ClearArray();
            serializedLibrary.ApplyModifiedPropertiesWithoutUndo();
        }

        foreach ((string label, string spriteName) in LabelSprites)
        {
            library.AddCategoryLabel(sprites[spriteName], Category, label);
        }

        SerializedObject libraryHashObject = new(library);
        libraryHashObject.FindProperty("m_ModificationHash").longValue = DateTime.UtcNow.Ticks;
        libraryHashObject.FindProperty("m_Version").intValue = 1;
        libraryHashObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(library);
        return library;
    }

    private static AnimationClip[] CreateOrUpdateClips()
    {
        return new[]
        {
            CreateOrUpdateClip("SharedIdleDown", 60f, "IdleDown_0"),
            CreateOrUpdateClip("SharedIdleRight", 60f, "IdleRight_0"),
            CreateOrUpdateClip("SharedIdleLeft", 60f, "IdleLeft_0"),
            CreateOrUpdateClip("SharedIdleUp", 60f, "IdleUp_0"),
            CreateOrUpdateClip("SharedWalkDown", 6f, "WalkDown_0", "WalkDown_1", "WalkDown_2", "WalkDown_3"),
            CreateOrUpdateClip("SharedWalkRight", 6f, "WalkRight_0", "WalkRight_1", "WalkRight_2", "WalkRight_3"),
            CreateOrUpdateClip("SharedWalkLeft", 6f, "WalkLeft_0", "WalkLeft_1", "WalkLeft_2", "WalkLeft_3"),
            CreateOrUpdateClip("SharedWalkUp", 6f, "WalkUp_0", "WalkUp_1", "WalkUp_2", "WalkUp_3"),
        };
    }

    private static AnimationClip CreateOrUpdateClip(string clipName, float frameRate, params string[] labels)
    {
        string path = $"{ClipsFolder}/{clipName}.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { name = clipName };
            AssetDatabase.CreateAsset(clip, path);
        }
        else
        {
            clip.ClearCurves();
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
            }

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                AnimationUtility.SetEditorCurve(clip, binding, null);
            }

            clip.name = clipName;
        }

        clip.frameRate = frameRate;
        Keyframe[] keyframes = new Keyframe[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            keyframes[i] = new Keyframe(i / frameRate, ConvertIntToFloat(ResolveSpriteHash(labels[i])))
            {
                inTangent = float.PositiveInfinity,
                outTangent = float.PositiveInfinity,
            };
        }

        EditorCurveBinding resolverBinding = EditorCurveBinding.DiscreteCurve(
            string.Empty,
            typeof(SpriteResolver),
            SpriteHashProperty);
        AnimationUtility.SetEditorCurve(clip, resolverBinding, new AnimationCurve(keyframes));

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimatorController CreateOrUpdateController(IReadOnlyList<AnimationClip> clips)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        ResetController(controller);
        AddSharedParameters(controller);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        stateMachine.entryPosition = new Vector3(-210f, 110f, 0f);
        stateMachine.anyStatePosition = new Vector3(-200f, 10f, 0f);
        stateMachine.exitPosition = new Vector3(800f, 120f, 0f);

        AnimatorState idleState = stateMachine.AddState("Idle", new Vector3(0f, 50f, 0f));
        AnimatorState walkState = stateMachine.AddState("Walk", new Vector3(0f, 130f, 0f));
        stateMachine.defaultState = idleState;

        BlendTree idleTree = CreateBlendTree(controller, "Idle");
        idleTree.AddChild(clips[0], new Vector2(0f, -1f));
        idleTree.AddChild(clips[1], new Vector2(1f, 0f));
        idleTree.AddChild(clips[2], new Vector2(-1f, 0f));
        idleTree.AddChild(clips[3], new Vector2(0f, 1f));
        idleState.motion = idleTree;

        BlendTree walkTree = CreateBlendTree(controller, "Walk");
        walkTree.AddChild(clips[4], new Vector2(0f, -1f));
        walkTree.AddChild(clips[7], new Vector2(0f, 1f));
        walkTree.AddChild(clips[5], new Vector2(1f, 0f));
        walkTree.AddChild(clips[6], new Vector2(-1f, 0f));
        walkState.motion = walkTree;

        AnimatorStateTransition idleToWalk = idleState.AddTransition(walkState);
        idleToWalk.hasExitTime = false;
        idleToWalk.duration = 0f;
        idleToWalk.offset = 0f;
        idleToWalk.hasFixedDuration = true;
        idleToWalk.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");

        AnimatorStateTransition walkToIdle = walkState.AddTransition(idleState);
        walkToIdle.hasExitTime = false;
        walkToIdle.duration = 0f;
        walkToIdle.offset = 0f;
        walkToIdle.hasFixedDuration = true;
        walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static BlendTree CreateBlendTree(AnimatorController controller, string name)
    {
        BlendTree tree = new()
        {
            name = name,
            blendType = BlendTreeType.SimpleDirectional2D,
            blendParameter = "MoveX",
            blendParameterY = "MoveY",
            useAutomaticThresholds = true,
        };
        AssetDatabase.AddObjectToAsset(tree, controller);
        return tree;
    }

    private static void ResetController(AnimatorController controller)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters.ToArray())
        {
            controller.RemoveParameter(parameter);
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState childState in stateMachine.states.ToArray())
        {
            stateMachine.RemoveState(childState.state);
        }

        foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines.ToArray())
        {
            stateMachine.RemoveStateMachine(childStateMachine.stateMachine);
        }

        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
        {
            if (asset is BlendTree)
            {
                UnityEngine.Object.DestroyImmediate(asset, true);
            }
        }
    }

    private static void AddSharedParameters(AnimatorController controller)
    {
        controller.AddParameter("Blend", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
    }

    private static void WireTestPlayerPrefab(SpriteLibraryAsset heroLibrary, RuntimeAnimatorController controller)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            SpriteRenderer spriteRenderer = GetOrAddComponent<SpriteRenderer>(root);
            SpriteLibrary spriteLibrary = GetOrAddComponent<SpriteLibrary>(root);
            SpriteResolver spriteResolver = GetOrAddComponent<SpriteResolver>(root);
            Animator animator = GetOrAddComponent<Animator>(root);
            Player2DMovementController movementController = GetOrAddComponent<Player2DMovementController>(root);
            Character2DAnimatorDriver characterDriver = GetOrAddComponent<Character2DAnimatorDriver>(root);
            Player2DMovementAnimatorDriver movementDriver = GetOrAddComponent<Player2DMovementAnimatorDriver>(root);

            spriteLibrary.spriteLibraryAsset = heroLibrary;
            if (!spriteResolver.SetCategoryAndLabel(Category, "IdleDown_0"))
            {
                throw new InvalidOperationException("Test2DCharacterSprite SpriteResolver could not resolve Character/IdleDown_0.");
            }

            spriteResolver.ResolveSpriteToSpriteRenderer();
            spriteRenderer.sortingLayerName = ActorSortingLayerName;
            spriteRenderer.sortingOrder = ActorSortingOrder;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            SetObjectReference(characterDriver, "animator", animator);
            characterDriver.RefreshAnimatorParameters();
            SetObjectReference(movementDriver, "movementController", movementController);
            SetObjectReference(movementDriver, "animatorDriver", characterDriver);

            EditorUtility.SetDirty(spriteRenderer);
            EditorUtility.SetDirty(spriteLibrary);
            EditorUtility.SetDirty(spriteResolver);
            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(characterDriver);
            EditorUtility.SetDirty(movementDriver);
            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ValidateResolverLookup(SpriteLibraryAsset heroLibrary, IReadOnlyDictionary<string, Sprite> sprites)
    {
        GameObject validationObject = new("SpriteLibraryResolverValidation");
        try
        {
            SpriteRenderer spriteRenderer = validationObject.AddComponent<SpriteRenderer>();
            SpriteLibrary spriteLibrary = validationObject.AddComponent<SpriteLibrary>();
            spriteLibrary.spriteLibraryAsset = heroLibrary;
            SpriteResolver spriteResolver = validationObject.AddComponent<SpriteResolver>();

            if (!spriteResolver.SetCategoryAndLabel(Category, "WalkLeft_2"))
            {
                throw new InvalidOperationException("Temporary resolver could not resolve Character/WalkLeft_2.");
            }

            spriteResolver.ResolveSpriteToSpriteRenderer();
            if (spriteRenderer.sprite != sprites["character_30"])
            {
                throw new InvalidOperationException("Temporary resolver selected an unexpected sprite for WalkLeft_2.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(validationObject);
        }
    }

    private static void ValidateClipSampling(
        SpriteLibraryAsset heroLibrary,
        IReadOnlyDictionary<string, Sprite> sprites,
        IReadOnlyList<AnimationClip> clips)
    {
        GameObject validationObject = new("SharedCharacterClipValidation");
        try
        {
            SpriteRenderer spriteRenderer = validationObject.AddComponent<SpriteRenderer>();
            SpriteLibrary spriteLibrary = validationObject.AddComponent<SpriteLibrary>();
            spriteLibrary.spriteLibraryAsset = heroLibrary;
            SpriteResolver spriteResolver = validationObject.AddComponent<SpriteResolver>();

            ValidateSample(clips[0], 0f, "character_0", validationObject, spriteResolver, spriteRenderer, sprites);
            ValidateSample(clips[4], 1f / 6f, "character_1", validationObject, spriteResolver, spriteRenderer, sprites);
            ValidateSample(clips[5], 2f / 6f, "character_10", validationObject, spriteResolver, spriteRenderer, sprites);
            ValidateSample(clips[6], 2f / 6f, "character_30", validationObject, spriteResolver, spriteRenderer, sprites);
            ValidateSample(clips[7], 3f / 6f, "character_22", validationObject, spriteResolver, spriteRenderer, sprites);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(validationObject);
        }
    }

    private static void ValidateSample(
        AnimationClip clip,
        float time,
        string expectedSpriteName,
        GameObject validationObject,
        SpriteResolver spriteResolver,
        SpriteRenderer spriteRenderer,
        IReadOnlyDictionary<string, Sprite> sprites)
    {
        clip.SampleAnimation(validationObject, time);
        spriteResolver.ResolveSpriteToSpriteRenderer();
        if (spriteRenderer.sprite != sprites[expectedSpriteName])
        {
            string actualSpriteName = spriteRenderer.sprite != null ? spriteRenderer.sprite.name : "<null>";
            throw new InvalidOperationException(
                $"Clip {clip.name} sampled at {time:0.###} resolved {actualSpriteName}, expected {expectedSpriteName}.");
        }
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        SerializedObject serializedObject = new(target);
        serializedObject.FindProperty(propertyName).objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static int ResolveSpriteHash(string label)
    {
        MethodInfo hashMethod = typeof(SpriteLibrary).GetMethod(
            "GetHashForCategoryAndEntry",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (hashMethod == null)
        {
            throw new InvalidOperationException("SpriteLibrary.GetHashForCategoryAndEntry was not found.");
        }

        return (int)hashMethod.Invoke(null, new object[] { Category, label });
    }

    private static float ConvertIntToFloat(int value)
    {
        return BitConverter.ToSingle(BitConverter.GetBytes(value), 0);
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
