using NUnit.Framework;
using UnityEditor;
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
    public void Test2DCharacterSpritePrefabIsWiredForShared2DAnimation()
    {
        SpriteLibraryAsset library = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(HeroLibraryPath);
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(SharedControllerPath);
        Assert.NotNull(library);
        Assert.NotNull(controller);

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Assert.NotNull(root.GetComponent<SpriteRenderer>());
            SpriteLibrary spriteLibrary = root.GetComponent<SpriteLibrary>();
            SpriteResolver spriteResolver = root.GetComponent<SpriteResolver>();
            Animator animator = root.GetComponent<Animator>();

            Assert.NotNull(spriteLibrary);
            Assert.NotNull(spriteResolver);
            Assert.NotNull(animator);
            Assert.NotNull(root.GetComponent<Character2DAnimatorDriver>());
            Assert.NotNull(root.GetComponent<Player2DMovementAnimatorDriver>());
            Assert.That(spriteLibrary.spriteLibraryAsset, Is.EqualTo(library));
            Assert.That(animator.runtimeAnimatorController, Is.EqualTo(controller));
            Assert.That(spriteResolver.SetCategoryAndLabel(Category, "IdleDown_0"), Is.True);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
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
}
