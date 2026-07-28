// Build-time-only stand-in for the slice of the UnityEditor API that
// Assets/Editor uses. Same rules as UnityStub.cs: never place under Assets/,
// and keep the signatures honest.

using System;

namespace UnityEngine
{
    public enum ColorSpace { Uninitialized = -1, Gamma = 0, Linear = 1 }
}

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItemAttribute : Attribute
    {
        public MenuItemAttribute(string itemName) { }
        public MenuItemAttribute(string itemName, bool isValidateFunction) { }
        public MenuItemAttribute(string itemName, bool isValidateFunction, int priority) { }
        public int priority;
    }

    public enum UIOrientation
    {
        Portrait, PortraitUpsideDown, LandscapeRight, LandscapeLeft, AutoRotation
    }

    public enum AndroidSdkVersions
    {
        AndroidApiLevel26 = 26, AndroidApiLevel29 = 29,
        AndroidApiLevel31 = 31, AndroidApiLevel33 = 33
    }

    public static class PlayerSettings
    {
        public static string companyName { get; set; }
        public static string productName { get; set; }
        public static string applicationIdentifier { get; set; }
        public static UIOrientation defaultInterfaceOrientation { get; set; }
        public static bool allowedAutorotateToPortrait { get; set; }
        public static bool allowedAutorotateToPortraitUpsideDown { get; set; }
        public static bool allowedAutorotateToLandscapeLeft { get; set; }
        public static bool allowedAutorotateToLandscapeRight { get; set; }
        public static UnityEngine.ColorSpace colorSpace { get; set; }

        public static class Android
        {
            public static AndroidSdkVersions minSdkVersion { get; set; }
        }

        public static class iOS
        {
            public static string targetOSVersionString { get; set; }
        }
    }

    public static class AssetDatabase
    {
        public static void SaveAssets() { }
        public static void Refresh() { }
    }

    public static class EditorUtility
    {
        public static bool DisplayDialog(string title, string message, string ok) => false;
        public static bool DisplayDialog(string title, string message, string ok, string cancel) => false;
    }
}
