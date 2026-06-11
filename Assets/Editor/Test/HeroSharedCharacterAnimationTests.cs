using NUnit.Framework;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.U2D.Animation;

public sealed class HeroSharedCharacterAnimationTests
{
    private const string Category = "Character";
    private const string HeroLibraryPath = "Assets/Animation/SharedCharacter/Libraries/Hero.spriteLib.asset";
    private const string SharedControllerPath = "Assets/Animation/SharedCharacter/Shared_4Dir.controller";
    private const string SharedWalkRightPath = "Assets/Animation/SharedCharacter/Clips/SharedWalkRight.anim";
    private const string SharedWalkLeftPath = "Assets/Animation/SharedCharacter/Clips/SharedWalkLeft.anim";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player/Test2DCharacterSprite.prefab";
    private const string FormalControllerPath = "Assets/Art/Player/PlayerAnimator/PlayerAnimator.controller";
    private const string FormalTexturePath = "Assets/Art/Player/PlayerAnimator/Player.png";

    [Test]
    public void HeroLibraryMapsDirectionalLabelsToExpectedSprites()
    {
        SpriteLibraryAsset library = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(HeroLibraryPath);
        Assert.NotNull(library);

        AssertLabelSpriteName(library, "IdleDown_0", "character_0");
        AssertLabelSpriteName(library, "IdleRight_0", "character_8");
        AssertLabelSpriteName(library, "IdleLeft_0", "character_28");
        AssertLabelSpriteName(library, "IdleUp_0", "character_19");
        AssertLabelSpriteName(library, "WalkDown_3", "character_3");
        AssertLabelSpriteName(library, "WalkRight_2", "character_10");
        AssertLabelSpriteName(library, "WalkLeft_2", "character_30");
        AssertLabelSpriteName(library, "WalkUp_3", "character_22");
    }

    [Test]
    public void HeroSharedHorizontalWalkClipsResolveExpectedSprites()
    {
        SpriteLibraryAsset library = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(HeroLibraryPath);
        AnimationClip walkRightClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(SharedWalkRightPath);
        AnimationClip walkLeftClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(SharedWalkLeftPath);

        Assert.NotNull(library);
        Assert.NotNull(walkRightClip);
        Assert.NotNull(walkLeftClip);

        GameObject gameObject = new("HeroSharedCharacterAnimationTest");
        try
        {
            SpriteRenderer spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            SpriteLibrary spriteLibrary = gameObject.AddComponent<SpriteLibrary>();
            spriteLibrary.spriteLibraryAsset = library;
            SpriteResolver spriteResolver = gameObject.AddComponent<SpriteResolver>();

            AssertClipSample(walkRightClip, 0f, "character_8", gameObject, spriteResolver, spriteRenderer);
            AssertClipSample(walkRightClip, 1f / 6f, "character_9", gameObject, spriteResolver, spriteRenderer);
            AssertClipSample(walkRightClip, 2f / 6f, "character_10", gameObject, spriteResolver, spriteRenderer);
            AssertClipSample(walkRightClip, 3f / 6f, "character_11", gameObject, spriteResolver, spriteRenderer);

            AssertClipSample(walkLeftClip, 0f, "character_28", gameObject, spriteResolver, spriteRenderer);
            AssertClipSample(walkLeftClip, 1f / 6f, "character_29", gameObject, spriteResolver, spriteRenderer);
            AssertClipSample(walkLeftClip, 2f / 6f, "character_30", gameObject, spriteResolver, spriteRenderer);
            AssertClipSample(walkLeftClip, 3f / 6f, "character_31", gameObject, spriteResolver, spriteRenderer);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void Test2DCharacterSpritePrefabIsWiredForFormal2DAnimation()
    {
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(FormalControllerPath);
        Sprite initialSprite = AssetDatabase.LoadAllAssetsAtPath(FormalTexturePath)
            .OfType<Sprite>()
            .FirstOrDefault(sprite => sprite.name == "Player_0");
        Assert.NotNull(controller);
        Assert.NotNull(initialSprite);

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            SpriteRenderer spriteRenderer = root.GetComponent<SpriteRenderer>();
            Animator animator = root.GetComponent<Animator>();

            Assert.NotNull(spriteRenderer);
            Assert.NotNull(animator);
            Character2DAnimatorDriver driver = root.GetComponent<Character2DAnimatorDriver>();
            Assert.NotNull(driver);
            Assert.NotNull(root.GetComponent<Player2DMovementAnimatorDriver>());
            Assert.That(animator.runtimeAnimatorController, Is.EqualTo(controller));
            Assert.That(spriteRenderer.sprite, Is.EqualTo(initialSprite));
            Assert.That(GetPrivateField<bool>(driver, "flipHorizontalWhenFacingLeft"), Is.True);
            Assert.That(root.GetComponent<SpriteLibrary>(), Is.Null);
            Assert.That(root.GetComponent<SpriteResolver>(), Is.Null);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [Test]
    public void FormalPlayerAnimatorBackStatesAreWiredForMouseFacing()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(FormalControllerPath);
        AnimationClip idleBack = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Player/PlayerAnimator/Idle_Back.anim");
        AnimationClip walkBack = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Player/PlayerAnimator/Walk_Back.anim");
        AnimationClip dodgeBack = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Player/PlayerAnimator/Dodge_Back.anim");
        Assert.NotNull(controller);
        Assert.NotNull(idleBack);
        Assert.NotNull(walkBack);
        Assert.NotNull(dodgeBack);

        AnimatorStateMachine root = controller.layers[0].stateMachine;
        AnimatorStateMachine frontStates = FindChildStateMachine(root, "FrontStates");
        AnimatorStateMachine backStates = FindChildStateMachine(root, "BackStates");
        Assert.NotNull(frontStates);
        Assert.NotNull(backStates);
        Assert.NotNull(backStates.defaultState);
        Assert.That(backStates.defaultState.name, Is.EqualTo("Idle_Back"));

        AssertStateMotion(backStates, "Idle_Back", idleBack);
        AssertStateMotion(backStates, "Walk_Back", walkBack);
        AssertStateMotion(backStates, "Dodge_Back", dodgeBack);
        AssertStateMachineTransition(root.GetStateMachineTransitions(frontStates), backStates, AnimatorConditionMode.If);
        AssertStateMachineTransition(root.GetStateMachineTransitions(backStates), frontStates, AnimatorConditionMode.IfNot);
        AssertStateTransition(frontStates, "Idle_Front", FindState(backStates, "Idle_Back"), AnimatorConditionMode.If);
        AssertStateTransition(frontStates, "Walk_Front", FindState(backStates, "Walk_Back"), AnimatorConditionMode.If);
        AssertStateTransition(frontStates, "Dodge_Front", FindState(backStates, "Dodge_Back"), AnimatorConditionMode.If);
        AssertStateTransition(backStates, "Idle_Back", FindState(frontStates, "Idle_Front"), AnimatorConditionMode.IfNot);
        AssertStateTransition(backStates, "Walk_Back", FindState(frontStates, "Walk_Front"), AnimatorConditionMode.IfNot);
        AssertStateTransition(backStates, "Dodge_Back", FindState(frontStates, "Dodge_Front"), AnimatorConditionMode.IfNot);
    }

    [Test]
    public void FormalPlayerAnimatorRuntimeSwitchesBetweenFrontAndBackSprites()
    {
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(FormalControllerPath);
        Assert.NotNull(controller);

        GameObject gameObject = new("FormalPlayerAnimatorRuntimeProbe");
        try
        {
            SpriteRenderer spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            Animator animator = gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Update(0f);

            AssertSpriteNameIndexInRange(spriteRenderer.sprite, 0, 7);

            animator.SetBool("IsFacingBack", true);
            for (int i = 0; i < 10; i++)
            {
                animator.Update(0.1f);
            }

            AssertSpriteNameIndexInRange(spriteRenderer.sprite, 36, 43);

            animator.SetBool("IsFacingBack", false);
            for (int i = 0; i < 10; i++)
            {
                animator.Update(0.1f);
            }

            AssertSpriteNameIndexInRange(spriteRenderer.sprite, 0, 7);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    private static void AssertLabelSpriteName(SpriteLibraryAsset library, string label, string expectedSpriteName)
    {
        Sprite sprite = library.GetSprite(Category, label);
        Assert.NotNull(sprite, label);
        Assert.That(sprite.name, Is.EqualTo(expectedSpriteName), label);
    }

    private static void AssertClipSample(
        AnimationClip clip,
        float time,
        string expectedSpriteName,
        GameObject gameObject,
        SpriteResolver spriteResolver,
        SpriteRenderer spriteRenderer)
    {
        clip.SampleAnimation(gameObject, time);
        spriteResolver.ResolveSpriteToSpriteRenderer();
        Assert.NotNull(spriteRenderer.sprite, clip.name);
        Assert.That(spriteRenderer.sprite.name, Is.EqualTo(expectedSpriteName), $"{clip.name} @ {time:0.###}");
    }

    private static AnimatorStateMachine FindChildStateMachine(AnimatorStateMachine root, string stateMachineName)
    {
        foreach (ChildAnimatorStateMachine child in root.stateMachines)
        {
            if (child.stateMachine.name == stateMachineName)
            {
                return child.stateMachine;
            }
        }

        return null;
    }

    private static void AssertStateMotion(AnimatorStateMachine stateMachine, string stateName, Motion expectedMotion)
    {
        AnimatorState state = FindState(stateMachine, stateName);
        Assert.NotNull(state, stateName);
        Assert.That(state.motion, Is.EqualTo(expectedMotion), stateName);
    }

    private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
    {
        return stateMachine.states
            .Select(child => child.state)
            .FirstOrDefault(candidate => candidate.name == stateName);
    }

    private static void AssertStateMachineTransition(
        AnimatorTransition[] transitions,
        AnimatorStateMachine expectedDestination,
        AnimatorConditionMode expectedMode)
    {
        AnimatorTransition transition = transitions.FirstOrDefault(candidate => candidate.destinationStateMachine == expectedDestination);
        Assert.NotNull(transition, expectedDestination.name);
        Assert.That(transition.conditions.Length, Is.EqualTo(1), expectedDestination.name);
        Assert.That(transition.conditions[0].parameter, Is.EqualTo("IsFacingBack"), expectedDestination.name);
        Assert.That(transition.conditions[0].mode, Is.EqualTo(expectedMode), expectedDestination.name);
    }

    private static void AssertStateTransition(
        AnimatorStateMachine stateMachine,
        string sourceStateName,
        AnimatorState expectedDestination,
        AnimatorConditionMode expectedMode)
    {
        Assert.NotNull(expectedDestination, sourceStateName);
        AnimatorState source = FindState(stateMachine, sourceStateName);
        Assert.NotNull(source, sourceStateName);
        AnimatorStateTransition transition = source.transitions.FirstOrDefault(candidate => candidate.destinationState == expectedDestination);
        Assert.NotNull(transition, $"{sourceStateName} -> {expectedDestination.name}");
        Assert.That(transition.hasExitTime, Is.False, sourceStateName);
        Assert.That(transition.duration, Is.EqualTo(0f).Within(0.0001f), sourceStateName);
        Assert.That(transition.conditions.Length, Is.EqualTo(1), sourceStateName);
        Assert.That(transition.conditions[0].parameter, Is.EqualTo("IsFacingBack"), sourceStateName);
        Assert.That(transition.conditions[0].mode, Is.EqualTo(expectedMode), sourceStateName);
    }

    private static void AssertSpriteNameIndexInRange(Sprite sprite, int inclusiveMin, int inclusiveMax)
    {
        Assert.NotNull(sprite);
        Assert.That(sprite.name.StartsWith("Player_"), Is.True, sprite.name);
        Assert.That(int.TryParse(sprite.name.Substring("Player_".Length), out int index), Is.True, sprite.name);
        Assert.That(index, Is.InRange(inclusiveMin, inclusiveMax), sprite.name);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field, fieldName);
        return (T)field.GetValue(target);
    }
}
