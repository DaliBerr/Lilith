using System.Collections.Generic;
using System.Reflection;
using Kernel.Bullet;
using Kernel.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class BackPackAttackPreviewControllerTests
{
    private readonly List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        BackPackAttackPreviewRig[] previewRigs = Object.FindObjectsByType<BackPackAttackPreviewRig>(FindObjectsSortMode.None);
        for (int i = 0; i < previewRigs.Length; i++)
        {
            if (previewRigs[i] != null)
            {
                Object.DestroyImmediate(previewRigs[i].gameObject);
            }
        }

        CharBullet[] bullets = Object.FindObjectsByType<CharBullet>(FindObjectsSortMode.None);
        for (int i = 0; i < bullets.Length; i++)
        {
            if (bullets[i] != null)
            {
                Object.DestroyImmediate(bullets[i].gameObject);
            }
        }

        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void RefreshPreview_BindsSceneRigCameraTexture()
    {
        BackPackAttackPreviewController controller = CreateController();
        BackPackAttackPreviewRig sceneRig = CreateSceneRig();
        CharBullet bulletPrefab = CreateBulletPrefab();
        PlayerPlaneMovement player = CreatePlayer(bulletPrefab);
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });

        controller.RefreshPreview(player, compiledAttack);

        BackPackAttackPreviewRig boundRig = GetPrivateField<BackPackAttackPreviewRig>(controller, "previewRig");
        Camera previewCamera = GetPrivateField<Camera>(controller, "previewCamera");
        RenderTexture previewTexture = GetPrivateField<RenderTexture>(controller, "previewTexture");
        RawImage previewImage = GetPrivateField<RawImage>(controller, "previewImage");

        Assert.That(boundRig, Is.SameAs(sceneRig));
        Assert.That(previewCamera, Is.Not.Null);
        Assert.That(previewCamera.gameObject.activeSelf, Is.True);
        Assert.That(previewTexture, Is.Not.Null);
        Assert.That(previewCamera.targetTexture, Is.SameAs(previewTexture));
        Assert.That(previewImage.texture, Is.SameAs(previewTexture));
    }

    [Test]
    public void RefreshPreview_ReusesAuthoredSceneRigCameraPoseAndLensSettings()
    {
        Vector3 authoredLocalPosition = new(6f, 28f, -9f);
        Quaternion authoredLocalRotation = Quaternion.Euler(90f, 18f, 0f);
        float authoredOrthographicSize = 23f;
        float authoredFieldOfView = 37f;
        CreateSceneRig(authoredLocalPosition, authoredLocalRotation, authoredOrthographicSize, authoredFieldOfView);
        BackPackAttackPreviewController controller = CreateController();
        CharBullet bulletPrefab = CreateBulletPrefab();
        PlayerPlaneMovement player = CreatePlayer(bulletPrefab);
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });

        controller.RefreshPreview(player, compiledAttack);

        Camera previewCamera = GetPrivateField<Camera>(controller, "previewCamera");
        Assert.That(previewCamera, Is.Not.Null);
        Assert.That((previewCamera.transform.localPosition - authoredLocalPosition).sqrMagnitude, Is.LessThan(0.000001f));
        Assert.That(Quaternion.Angle(previewCamera.transform.localRotation, authoredLocalRotation), Is.LessThan(0.001f));
        Assert.That(previewCamera.orthographicSize, Is.EqualTo(authoredOrthographicSize).Within(0.001f));
        Assert.That(previewCamera.fieldOfView, Is.EqualTo(authoredFieldOfView).Within(0.001f));
    }

    [Test]
    public void RefreshPreview_WithSpreadAttack_ParentsProjectilesUnderSceneRigRoot()
    {
        BackPackAttackPreviewController controller = CreateController();
        BackPackAttackPreviewRig sceneRig = CreateSceneRig();
        CharBullet bulletPrefab = CreateBulletPrefab();
        PlayerPlaneMovement player = CreatePlayer(bulletPrefab);
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        BehaviorTokenData spreadToken = CreateBehaviorToken("spread", "Spread", AttackBehaviorType.Spread, acceptsNumericValue: true, defaultProjectileCount: 2, spreadAngleStep: 12f);
        ValueTokenData valueThree = CreateValueToken("three", "3", 3f);
        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            spreadToken,
            valueThree,
        });

        controller.RefreshPreview(player, compiledAttack);

        List<CharBullet> activePreviewBullets = GetPrivateField<List<CharBullet>>(controller, "activePreviewBullets");
        Assert.That(activePreviewBullets.Count, Is.EqualTo(3));
        for (int i = 0; i < activePreviewBullets.Count; i++)
        {
            Assert.That(activePreviewBullets[i].transform.parent, Is.SameAs(sceneRig.ProjectileRoot));
        }
    }

    [Test]
    public void RefreshPreview_WithDummyFormation_TracksAllDummiesAndPrefersMiddleAsPrimary()
    {
        BackPackAttackPreviewController controller = CreateController();
        CreateSceneRig(dummyNames: new[] { "PreviewDummy-L", "PreviewDummy-M", "PreviewDummy-R" });
        CharBullet bulletPrefab = CreateBulletPrefab();
        PlayerPlaneMovement player = CreatePlayer(bulletPrefab);
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });

        controller.RefreshPreview(player, compiledAttack);

        BackPackPreviewDummyEnemy previewDummy = GetPrivateField<BackPackPreviewDummyEnemy>(controller, "previewDummy");
        List<BackPackPreviewDummyEnemy> previewDummies = GetPrivateField<List<BackPackPreviewDummyEnemy>>(controller, "previewDummies");

        Assert.That(previewDummy, Is.Not.Null);
        Assert.That(previewDummy.name, Is.EqualTo("PreviewDummy-M"));
        Assert.That(previewDummies.Count, Is.EqualTo(3));
        Assert.That(previewDummies[0].name, Is.EqualTo("PreviewDummy-L"));
        Assert.That(previewDummies[1].name, Is.EqualTo("PreviewDummy-M"));
        Assert.That(previewDummies[2].name, Is.EqualTo("PreviewDummy-R"));
    }

    [Test]
    public void RefreshPreview_WithInvalidFormula_ShowsFirstErrorMessageAndKeepsRig()
    {
        BackPackAttackPreviewController controller = CreateController();
        BackPackAttackPreviewRig sceneRig = CreateSceneRig();
        CharBullet bulletPrefab = CreateBulletPrefab();
        PlayerPlaneMovement player = CreatePlayer(bulletPrefab);
        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[0]);

        controller.RefreshPreview(player, compiledAttack);

        TMP_Text statusLabel = GetPrivateField<TMP_Text>(controller, "statusLabel");
        RenderTexture previewTexture = GetPrivateField<RenderTexture>(controller, "previewTexture");
        List<CharBullet> activePreviewBullets = GetPrivateField<List<CharBullet>>(controller, "activePreviewBullets");
        RawImage previewImage = GetPrivateField<RawImage>(controller, "previewImage");

        Assert.That(statusLabel.text, Does.Contain("does not contain a core token"));
        Assert.That(GetPrivateField<BackPackAttackPreviewRig>(controller, "previewRig"), Is.SameAs(sceneRig));
        Assert.That(previewImage.texture, Is.SameAs(previewTexture));
        Assert.That(activePreviewBullets.Count, Is.EqualTo(0));
    }

    [Test]
    public void RefreshPreview_WithMissingSceneRig_ShowsExplicitMessage()
    {
        BackPackAttackPreviewController controller = CreateController();
        CharBullet bulletPrefab = CreateBulletPrefab();
        PlayerPlaneMovement player = CreatePlayer(bulletPrefab);
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });

        controller.RefreshPreview(player, compiledAttack);

        TMP_Text statusLabel = GetPrivateField<TMP_Text>(controller, "statusLabel");
        RawImage previewImage = GetPrivateField<RawImage>(controller, "previewImage");

        Assert.That(statusLabel.text, Does.Contain("no BackPackAttackPreviewRig"));
        Assert.That(GetPrivateField<BackPackAttackPreviewRig>(controller, "previewRig") == null, Is.True);
        Assert.That(previewImage.texture == null, Is.True);
    }

    [Test]
    public void RefreshPreview_WithMultipleSceneRigs_ShowsExplicitMessage()
    {
        BackPackAttackPreviewController controller = CreateController();
        CreateSceneRig();
        CreateSceneRig();
        CharBullet bulletPrefab = CreateBulletPrefab();
        PlayerPlaneMovement player = CreatePlayer(bulletPrefab);
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });

        controller.RefreshPreview(player, compiledAttack);

        TMP_Text statusLabel = GetPrivateField<TMP_Text>(controller, "statusLabel");
        RawImage previewImage = GetPrivateField<RawImage>(controller, "previewImage");

        Assert.That(statusLabel.text, Does.Contain("expected exactly one"));
        Assert.That(previewImage.texture == null, Is.True);
    }

    [Test]
    public void PreviewDummyDamage_WithExplosionAttack_ShowsExplosionHint()
    {
        BackPackAttackPreviewController controller = CreateController();
        CreateSceneRig();
        CharBullet bulletPrefab = CreateBulletPrefab();
        PlayerPlaneMovement player = CreatePlayer(bulletPrefab);
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        ResultTokenData explosionToken = CreateResultToken("explosion", "Boom", AttackResultType.Explosion, acceptsNumericValue: true, defaultExplosionRadius: 1f);
        ValueTokenData radiusValue = CreateValueToken("radius", "2", 2f);
        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            explosionToken,
            radiusValue,
        });

        controller.RefreshPreview(player, compiledAttack);

        BackPackPreviewDummyEnemy previewDummy = GetPrivateField<BackPackPreviewDummyEnemy>(controller, "previewDummy");
        LineRenderer explosionHint = GetPrivateField<LineRenderer>(controller, "explosionHint");
        Assert.That(previewDummy, Is.Not.Null);
        Assert.That(explosionHint, Is.Not.Null);

        bool applied = previewDummy.TryApplyDamage(1f, out _, out _);

        Assert.That(applied, Is.True);
        Assert.That(explosionHint.enabled, Is.True);
        Assert.That(new Vector2(explosionHint.GetPosition(0).x, explosionHint.GetPosition(0).z).magnitude,
            Is.EqualTo(compiledAttack.ExplosionRadius).Within(0.01f));
    }

    [Test]
    public void PreviewDummyHitFeedback_WithTextMeshProStyleMaterial_DoesNotLogColorErrors()
    {
        Shader tmpShader = Shader.Find("TextMeshPro/Distance Field");
        Assert.That(tmpShader, Is.Not.Null);

        Material tmpMaterial = new(tmpShader);
        createdObjects.Add(tmpMaterial);

        GameObject dummyObject = CreateGameObject("PreviewDummy");
        dummyObject.tag = "Enemy_Object";
        dummyObject.AddComponent<BoxCollider>();
        BackPackPreviewDummyEnemy previewDummy = dummyObject.AddComponent<BackPackPreviewDummyEnemy>();

        GameObject glyphObject = CreateGameObject("Glyph");
        glyphObject.transform.SetParent(dummyObject.transform, false);
        glyphObject.AddComponent<MeshFilter>();
        MeshRenderer glyphRenderer = glyphObject.AddComponent<MeshRenderer>();
        glyphRenderer.sharedMaterial = tmpMaterial;

        previewDummy.ResetPreviewState(12f, 0);
        bool applied = previewDummy.TryApplyDamage(1f, out _, out _);

        Assert.That(applied, Is.True);
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void PreviewDummy_ResetPreviewState_DoesNotAccumulateHitScale()
    {
        GameObject dummyObject = CreateGameObject("PreviewDummy");
        dummyObject.tag = "Enemy_Object";
        dummyObject.transform.localScale = new Vector3(2f, 2f, 2f);
        dummyObject.AddComponent<BoxCollider>();
        BackPackPreviewDummyEnemy previewDummy = dummyObject.AddComponent<BackPackPreviewDummyEnemy>();

        previewDummy.ResetPreviewState(12f, 0);
        Vector3 authoredScale = dummyObject.transform.localScale;

        bool firstApplied = previewDummy.TryApplyDamage(1f, out _, out _);
        Vector3 firstHitScale = dummyObject.transform.localScale;

        previewDummy.ResetPreviewState(12f, 0);
        Vector3 resetScale = dummyObject.transform.localScale;

        bool secondApplied = previewDummy.TryApplyDamage(1f, out _, out _);
        Vector3 secondHitScale = dummyObject.transform.localScale;

        Assert.That(firstApplied, Is.True);
        Assert.That(secondApplied, Is.True);
        Assert.That(resetScale, Is.EqualTo(authoredScale).Using(Vector3EqualityComparerWithTolerance(0.001f)));
        Assert.That(secondHitScale, Is.EqualTo(firstHitScale).Using(Vector3EqualityComparerWithTolerance(0.001f)));
    }

    [Test]
    public void ClearPreview_ClearsTextureDisablesCameraAndPreviewBullets()
    {
        BackPackAttackPreviewController controller = CreateController();
        BackPackAttackPreviewRig sceneRig = CreateSceneRig();
        CharBullet bulletPrefab = CreateBulletPrefab();
        PlayerPlaneMovement player = CreatePlayer(bulletPrefab);
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });

        controller.RefreshPreview(player, compiledAttack);
        controller.ClearPreview();

        Camera previewCamera = sceneRig.PreviewCamera;
        RenderTexture previewTexture = GetPrivateField<RenderTexture>(controller, "previewTexture");
        List<CharBullet> activePreviewBullets = GetPrivateField<List<CharBullet>>(controller, "activePreviewBullets");
        RawImage previewImage = GetPrivateField<RawImage>(controller, "previewImage");

        Assert.That(previewTexture == null, Is.True);
        Assert.That(activePreviewBullets.Count, Is.EqualTo(0));
        Assert.That(previewImage.texture == null, Is.True);
        Assert.That(previewCamera.targetTexture == null, Is.True);
        Assert.That(previewCamera.gameObject.activeSelf, Is.False);
        Assert.That(sceneRig.ProjectileRoot.childCount, Is.EqualTo(0));
        Assert.That(Object.FindObjectsByType<CharBullet>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
    }

    [Test]
    public void Emit_WithParentOverride_CollectsSpawnedBulletsUnderParent()
    {
        CharBullet bulletPrefab = CreateBulletPrefab();
        GameObject owner = CreateGameObject("Owner");
        GameObject projectileRoot = CreateGameObject("ProjectileRoot");
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        BehaviorTokenData spreadToken = CreateBehaviorToken("spread", "Spread", AttackBehaviorType.Spread, acceptsNumericValue: true, defaultProjectileCount: 2, spreadAngleStep: 12f);
        ValueTokenData valueThree = CreateValueToken("three", "3", 3f);
        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            spreadToken,
            valueThree,
        });

        List<CharBullet> spawnedBullets = new();
        int emittedCount = AttackProjectileEmitter.Emit(
            bulletPrefab,
            owner.transform,
            Vector3.zero,
            Vector3.forward,
            compiledAttack,
            projectileRoot.transform,
            spawnedBullets);

        Assert.That(emittedCount, Is.EqualTo(3));
        Assert.That(spawnedBullets.Count, Is.EqualTo(3));
        for (int i = 0; i < spawnedBullets.Count; i++)
        {
            Assert.That(spawnedBullets[i], Is.Not.Null);
            Assert.That(spawnedBullets[i].transform.parent, Is.SameAs(projectileRoot.transform));
        }
    }

    private BackPackAttackPreviewController CreateController()
    {
        GameObject root = CreateUiObject("BackPackUI");
        GameObject mainContent = CreateUiObject("MainContent");
        mainContent.transform.SetParent(root.transform, false);

        GameObject leftPanel = CreateUiObject("Left Panel");
        leftPanel.transform.SetParent(mainContent.transform, false);

        GameObject previewAnimation = CreateUiObject("Preview Animation");
        previewAnimation.transform.SetParent(leftPanel.transform, false);

        GameObject previewRender = CreateUiObject("Preview Render");
        previewRender.transform.SetParent(previewAnimation.transform, false);
        previewRender.AddComponent<RawImage>();

        GameObject previewStatus = CreateUiObject("Preview Status");
        previewStatus.transform.SetParent(previewAnimation.transform, false);
        previewStatus.AddComponent<TextMeshProUGUI>();

        return root.AddComponent<BackPackAttackPreviewController>();
    }

    private BackPackAttackPreviewRig CreateSceneRig(
        Vector3? cameraLocalPosition = null,
        Quaternion? cameraLocalRotation = null,
        float orthographicSize = 18f,
        float fieldOfView = 60f,
        string[] dummyNames = null)
    {
        GameObject rigObject = CreateGameObject("BackPackAttackPreviewRig");
        BackPackAttackPreviewRig rig = rigObject.AddComponent<BackPackAttackPreviewRig>();

        GameObject floor = CreateGameObject("Floor");
        floor.transform.SetParent(rigObject.transform, false);

        GameObject previewBullets = CreateGameObject("PreviewBullets");
        previewBullets.transform.SetParent(rigObject.transform, false);

        GameObject previewPlayer = CreateGameObject("PreviewPlayer");
        previewPlayer.transform.SetParent(rigObject.transform, false);
        GameObject spawnAnchor = CreateGameObject("SpawnAnchor");
        spawnAnchor.transform.SetParent(previewPlayer.transform, false);
        spawnAnchor.transform.localPosition = new Vector3(0f, 0f, 4f);
        spawnAnchor.transform.localRotation = Quaternion.identity;

        string[] resolvedDummyNames = dummyNames ?? new[] { "PreviewDummy" };
        BackPackPreviewDummyEnemy[] previewDummies = new BackPackPreviewDummyEnemy[resolvedDummyNames.Length];
        for (int i = 0; i < resolvedDummyNames.Length; i++)
        {
            GameObject previewDummyObject = CreateGameObject(resolvedDummyNames[i]);
            previewDummyObject.transform.SetParent(rigObject.transform, false);
            previewDummyObject.transform.localPosition = BuildPreviewDummyLocalPosition(i, resolvedDummyNames.Length);
            previewDummyObject.tag = "Enemy_Object";
            BoxCollider dummyCollider = previewDummyObject.AddComponent<BoxCollider>();
            dummyCollider.size = new Vector3(18f, 10f, 2f);
            previewDummies[i] = previewDummyObject.AddComponent<BackPackPreviewDummyEnemy>();
        }

        GameObject explosionHintObject = CreateGameObject("ExplosionHint");
        explosionHintObject.transform.SetParent(rigObject.transform, false);
        LineRenderer explosionHint = explosionHintObject.AddComponent<LineRenderer>();
        explosionHint.loop = true;
        explosionHint.useWorldSpace = false;
        explosionHint.positionCount = 48;
        explosionHint.enabled = false;

        GameObject previewCameraObject = CreateGameObject("PreviewCamera");
        previewCameraObject.transform.SetParent(rigObject.transform, false);
        previewCameraObject.transform.localPosition = cameraLocalPosition ?? new Vector3(0f, 30f, 6f);
        previewCameraObject.transform.localRotation = cameraLocalRotation ?? Quaternion.Euler(90f, 0f, 0f);
        Camera previewCamera = previewCameraObject.AddComponent<Camera>();
        previewCamera.orthographic = true;
        previewCamera.orthographicSize = orthographicSize;
        previewCamera.fieldOfView = fieldOfView;
        previewCameraObject.SetActive(false);

        SetPrivateField(rig, "previewCamera", previewCamera);
        SetPrivateField(rig, "spawnAnchor", spawnAnchor.transform);
        SetPrivateField(rig, "projectileRoot", previewBullets.transform);
        SetPrivateField(rig, "previewPlayerRoot", previewPlayer.transform);
        SetPrivateField(rig, "previewDummy", ResolvePrimaryPreviewDummy(previewDummies));
        SetPrivateField(rig, "previewDummies", previewDummies);
        SetPrivateField(rig, "explosionHint", explosionHint);
        SetPrivateField(rig, "floorRoot", floor.transform);
        return rig;
    }

    private static Vector3 BuildPreviewDummyLocalPosition(int index, int count)
    {
        float spacing = 12f;
        float startX = -((count - 1) * spacing) * 0.5f;
        return new Vector3(startX + (index * spacing), 0f, 24f);
    }

    private static BackPackPreviewDummyEnemy ResolvePrimaryPreviewDummy(BackPackPreviewDummyEnemy[] previewDummies)
    {
        for (int i = 0; i < previewDummies.Length; i++)
        {
            if (previewDummies[i] != null && previewDummies[i].name == "PreviewDummy")
            {
                return previewDummies[i];
            }
        }

        for (int i = 0; i < previewDummies.Length; i++)
        {
            if (previewDummies[i] != null && previewDummies[i].name.EndsWith("-M"))
            {
                return previewDummies[i];
            }
        }

        return previewDummies[0];
    }

    private PlayerPlaneMovement CreatePlayer(CharBullet bulletPrefab)
    {
        GameObject playerObject = CreateGameObject("Player");
        PlayerPlaneMovement playerMovement = playerObject.AddComponent<PlayerPlaneMovement>();
        SetPrivateField(playerMovement, "bulletPrefab", bulletPrefab);
        return playerMovement;
    }

    private CharBullet CreateBulletPrefab()
    {
        GameObject bulletObject = CreateGameObject("BulletPrefab");
        SphereCollider sphereCollider = bulletObject.AddComponent<SphereCollider>();
        sphereCollider.isTrigger = true;
        sphereCollider.radius = 0.5f;

        GameObject glyphObject = CreateGameObject("BulletGlyph");
        glyphObject.transform.SetParent(bulletObject.transform, false);
        TextMeshPro textMeshPro = glyphObject.AddComponent<TextMeshPro>();
        textMeshPro.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 20f);
        textMeshPro.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 20f);

        return bulletObject.AddComponent<CharBullet>();
    }

    private CoreTokenData CreateCoreToken(string tokenId, string displayText, AttackCoreType coreType)
    {
        CoreTokenData token = CreateToken<CoreTokenData>(tokenId, displayText);
        token.CoreType = coreType;
        token.Damage = 1f;
        token.ProjectileLife = 1;
        token.ImpactLifeCost = 1;
        token.ProjectileSpeed = 320f;
        token.MaxLifetime = 2f;
        token.MaxTravelDistance = 512f;
        token.ImpactMask = ~0;
        return token;
    }

    private BehaviorTokenData CreateBehaviorToken(string tokenId, string displayText, AttackBehaviorType behaviorType, bool acceptsNumericValue, int defaultProjectileCount, float spreadAngleStep)
    {
        BehaviorTokenData token = CreateToken<BehaviorTokenData>(tokenId, displayText);
        token.BehaviorType = behaviorType;
        token.AcceptsNumericValue = acceptsNumericValue;
        token.DefaultProjectileCount = defaultProjectileCount;
        token.SpreadAngleStep = spreadAngleStep;
        return token;
    }

    private ResultTokenData CreateResultToken(string tokenId, string displayText, AttackResultType resultType, bool acceptsNumericValue, float defaultExplosionRadius)
    {
        ResultTokenData token = CreateToken<ResultTokenData>(tokenId, displayText);
        token.ResultType = resultType;
        token.AcceptsNumericValue = acceptsNumericValue;
        token.DefaultExplosionRadius = defaultExplosionRadius;
        return token;
    }

    private ValueTokenData CreateValueToken(string tokenId, string displayText, float numericValue)
    {
        ValueTokenData token = CreateToken<ValueTokenData>(tokenId, displayText);
        token.NumericValue = numericValue;
        return token;
    }

    private T CreateToken<T>(string tokenId, string displayText) where T : BaseTokenData
    {
        T token = ScriptableObject.CreateInstance<T>();
        createdObjects.Add(token);
        token.TokenId = tokenId;
        token.DisplayText = displayText;
        return token;
    }

    private GameObject CreateUiObject(string name)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' was not found on {target.GetType().Name}.");
        return (T)field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' was not found on {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    private static IEqualityComparer<Vector3> Vector3EqualityComparerWithTolerance(float tolerance)
    {
        return new Vector3EqualityComparer(tolerance);
    }

    private sealed class Vector3EqualityComparer : IEqualityComparer<Vector3>
    {
        private readonly float tolerance;

        public Vector3EqualityComparer(float tolerance)
        {
            this.tolerance = tolerance;
        }

        public bool Equals(Vector3 expected, Vector3 actual)
        {
            return (expected - actual).sqrMagnitude <= tolerance * tolerance;
        }

        public int GetHashCode(Vector3 obj)
        {
            return obj.GetHashCode();
        }
    }
}
