using System.Collections.Generic;
using Kernel.Bullet;
using NUnit.Framework;
using UnityEngine;
using VocalithRandom = Vocalith.Random;

public sealed class CombatEntryTokenSelectionPlanTests
{
    private readonly List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
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
    public void TrySampleLibrary_UsesWeightedLibrariesWithinPlan()
    {
        CombatEntryTokenSelectionPlan plan = ScriptableObject.CreateInstance<CombatEntryTokenSelectionPlan>();
        createdObjects.Add(plan);

        BulletTokenLibrary weightedLibrary = CreateLibrary("WeightedLibrary", CreateToken<CoreTokenData>("fire", "Fire"));
        BulletTokenLibrary zeroWeightLibrary = CreateLibrary("ZeroWeightLibrary", CreateToken<CoreTokenData>("ice", "Ice"));

        plan.AddLibrary(weightedLibrary, 1f);
        plan.AddLibrary(zeroWeightLibrary, 0f);

        for (int i = 0; i < 8; i++)
        {
            bool success = plan.TrySampleLibrary(new VocalithRandom(1000 + i), out BulletTokenLibrary sampledLibrary);
            Assert.That(success, Is.True);
            Assert.That(sampledLibrary, Is.SameAs(weightedLibrary));
        }
    }

    [Test]
    public void TrySampleLibrary_ReturnsFalseWhenAllWeightsAreZero()
    {
        CombatEntryTokenSelectionPlan plan = ScriptableObject.CreateInstance<CombatEntryTokenSelectionPlan>();
        createdObjects.Add(plan);

        BulletTokenLibrary zeroWeightLibrary = CreateLibrary("ZeroWeightLibrary", CreateToken<CoreTokenData>("ice", "Ice"));
        plan.AddLibrary(zeroWeightLibrary, 0f);

        bool success = plan.TrySampleLibrary(new VocalithRandom(24680), out BulletTokenLibrary sampledLibrary);

        Assert.That(success, Is.False);
        Assert.That(sampledLibrary, Is.Null);
    }

    private BulletTokenLibrary CreateLibrary(string name, params PlaceableTokenData[] tokens)
    {
        BulletTokenLibrary library = ScriptableObject.CreateInstance<BulletTokenLibrary>();
        library.name = name;
        library.SetTokens(tokens);
        createdObjects.Add(library);
        return library;
    }

    private T CreateToken<T>(string tokenId, string displayText) where T : BaseTokenData
    {
        T token = ScriptableObject.CreateInstance<T>();
        token.TokenId = tokenId;
        token.DisplayText = displayText;
        token.name = tokenId;
        createdObjects.Add(token);
        return token;
    }
}
