using System.Collections.Generic;
using System.Reflection;
using Kernel.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vocalith.Localization;
using Object = UnityEngine.Object;

public sealed class GameExitUIUtilityTests
{
    private readonly List<Object> createdObjects = new();
    private Dictionary<string, string> savedLocalizationStrings;

    [SetUp]
    public void SetUp()
    {
        savedLocalizationStrings = new Dictionary<string, string>(GetStringTable());
        GetStringTable().Clear();
    }

    [TearDown]
    public void TearDown()
    {
        IDictionary<string, string> stringTable = GetStringTable();
        stringTable.Clear();
        foreach (KeyValuePair<string, string> entry in savedLocalizationStrings)
        {
            stringTable[entry.Key] = entry.Value;
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
    public void ConfigureExitConfirmation_ConfiguresPopupAndInvokesConfirmOnly()
    {
        LocalizationManager.RegisterString("ui.exit.confirm_message", "要离开吗？");
        LocalizationManager.RegisterString("ui.exit.confirm", "离开");
        LocalizationManager.RegisterString("ui.common.cancel", "留下");
        PopUpUIScreen popup = CreatePopup();
        InvokeNonPublic(popup, "OnInit");
        int confirmedCount = 0;

        GameExitUIUtility.ConfigureExitConfirmation(popup, () => confirmedCount++);

        Assert.That(popup.InfoText.text, Is.EqualTo("要离开吗？"));
        Assert.That(popup.ConfirmButtonText.text, Is.EqualTo("离开"));
        Assert.That(popup.CloseButtonText.text, Is.EqualTo("留下"));

        popup.CloseButton.onClick.Invoke();
        popup.TopCloseButton.onClick.Invoke();
        Assert.That(confirmedCount, Is.EqualTo(0));

        popup.ConfirmButton.onClick.Invoke();
        Assert.That(confirmedCount, Is.EqualTo(1));
    }

    private PopUpUIScreen CreatePopup()
    {
        GameObject root = CreateUiObject("Info Popup");
        root.AddComponent<CanvasGroup>();
        PopUpUIScreen popup = root.AddComponent<PopUpUIScreen>();

        GameObject topPanel = CreateUiObject("Top Panel", root.transform);
        CreateButton("Close Button", topPanel.transform);

        GameObject mainContent = CreateUiObject("Main Content", root.transform);
        GameObject infoPanel = CreateUiObject("Info", mainContent.transform);
        GameObject infoText = CreateUiObject("Text", infoPanel.transform);
        infoText.AddComponent<TextMeshProUGUI>();

        GameObject buttonPanel = CreateUiObject("Button", mainContent.transform);
        CreateButton("Confirm Buton", buttonPanel.transform);
        CreateButton("Close Button", buttonPanel.transform);

        return popup;
    }

    private Button CreateButton(string name, Transform parent)
    {
        GameObject buttonRoot = CreateUiObject(name, parent);
        GameObject edge = CreateUiObject("Edge", buttonRoot.transform);
        GameObject buttonObject = CreateUiObject("Button", edge.transform);
        buttonObject.AddComponent<Image>();
        Button button = buttonObject.AddComponent<Button>();

        GameObject labelObject = CreateUiObject("Text (TMP)", buttonObject.transform);
        labelObject.AddComponent<TextMeshProUGUI>();
        return button;
    }

    private GameObject CreateUiObject(string name, Transform parent = null)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        createdObjects.Add(gameObject);
        if (parent != null)
        {
            gameObject.transform.SetParent(parent, false);
        }

        return gameObject;
    }

    private static void InvokeNonPublic(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName} should exist.");
        method.Invoke(target, arguments);
    }

    private static IDictionary<string, string> GetStringTable()
    {
        FieldInfo field = typeof(LocalizationManager).GetField("_stringTable", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (IDictionary<string, string>)field.GetValue(null);
    }
}
