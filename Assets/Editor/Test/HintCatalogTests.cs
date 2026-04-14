using Kernel.UI;
using NUnit.Framework;

public sealed class HintCatalogTests
{
    [Test]
    public void TryDeserializeCatalogJson_ParsesCategoriesAndEntries()
    {
        const string json = "{\"categories\":[{\"id\":\"guide\",\"title\":\"指南\",\"entries\":[{\"id\":\"movement\",\"title\":\"移动\",\"content\":\"WASD\"}]}]}";

        bool success = HintCatalogUtility.TryDeserializeCatalogJson(json, out HintCatalogData catalog, out string errorMessage);

        Assert.That(success, Is.True, errorMessage);
        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.Categories.Count, Is.EqualTo(1));
        Assert.That(catalog.Categories[0].Id, Is.EqualTo("guide"));
        Assert.That(catalog.Categories[0].Entries.Count, Is.EqualTo(1));
        Assert.That(catalog.Categories[0].Entries[0].Id, Is.EqualTo("movement"));
    }

    [Test]
    public void Sanitize_TrimsAndDeduplicatesIds()
    {
        HintCatalogData raw = new()
        {
            Categories =
            {
                new HintCategoryData
                {
                    Id = " guide ",
                    Title = " 指南 ",
                    Entries =
                    {
                        new HintEntryData
                        {
                            Id = " e ",
                            Title = " 第一条 ",
                            Content = " 内容A "
                        },
                        new HintEntryData
                        {
                            Id = "e",
                            Title = "第二条",
                            Content = "内容B"
                        }
                    }
                },
                new HintCategoryData
                {
                    Id = "guide",
                    Title = "帮助"
                }
            }
        };

        HintCatalogData sanitized = HintCatalogUtility.Sanitize(raw);

        Assert.That(sanitized.Categories.Count, Is.EqualTo(2));
        Assert.That(sanitized.Categories[0].Id, Is.EqualTo("guide"));
        Assert.That(sanitized.Categories[1].Id, Is.EqualTo("guide_2"));
        Assert.That(sanitized.Categories[0].Entries[0].Id, Is.EqualTo("e"));
        Assert.That(sanitized.Categories[0].Entries[1].Id, Is.EqualTo("e_2"));
        Assert.That(sanitized.Categories[0].Entries[0].Title, Is.EqualTo("第一条"));
        Assert.That(sanitized.Categories[0].Entries[0].Content, Is.EqualTo("内容A"));
    }
}
