// Build-time-only stand-in for the slice of the Unity API that Assets/Scripts/Game
// uses. It is NOT part of the game and must never be placed under Assets/ - Unity
// would then find two definitions of every type.
//
// Why it exists: the Unity layer cannot be compiled without the editor, so ~1200
// lines of gameplay code would otherwise be completely unverified until somebody
// opens the project. Compiling against this stub catches what actually goes wrong
// in practice - typos, wrong member names, bad signatures, unbalanced braces -
// on every commit, with nothing but a C# compiler.
//
// It deliberately mirrors real Unity signatures. If the real API changes, this
// fails to compile against the real thing, not silently diverges. It proves the
// code compiles; it does not prove the game behaves - that is what the level
// cross-check and the in-editor tests are for.

using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object
    {
        public string name { get; set; }
        public static void Destroy(Object obj) { }
        public static void DontDestroyOnLoad(Object target) { }
        public static T FindFirstObjectByType<T>() where T : Object => null;
        public static bool operator ==(Object a, Object b) => ReferenceEquals(a, b);
        public static bool operator !=(Object a, Object b) => !ReferenceEquals(a, b);
        public override bool Equals(object other) => base.Equals(other);
        public override int GetHashCode() => base.GetHashCode();
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 one => new Vector2(1, 1);
        public float magnitude => 0f;
        public static Vector2 operator *(Vector2 a, float b) => new Vector2(a.x * b, a.y * b);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 right => new Vector3(1, 0, 0);
        public static Vector3 left => new Vector3(-1, 0, 0);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public static Vector3 back => new Vector3(0, 0, -1);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a;
        public static Vector3 operator +(Vector3 a, Vector3 b) => a;
        public static Vector3 operator -(Vector3 a, Vector3 b) => a;
        public static Vector3 operator *(Vector3 a, float b) => a;
        public static Vector3 operator /(Vector3 a, float b) => a;
    }

    public struct Quaternion
    {
        public static Quaternion identity => new Quaternion();
        public static Quaternion Euler(float x, float y, float z) => new Quaternion();
        public static Quaternion LookRotation(Vector3 forward, Vector3 upwards) => new Quaternion();
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => a;
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; this.a = 1f; }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1);
        public static Color gray => new Color(.5f, .5f, .5f);
        public static Color Lerp(Color a, Color b, float t) => a;
        public static bool operator ==(Color a, Color b) => true;
        public static bool operator !=(Color a, Color b) => false;
        public override bool Equals(object o) => base.Equals(o);
        public override int GetHashCode() => base.GetHashCode();
    }

    public static class Mathf
    {
        public const float PI = 3.14159265f;
        public static float Sin(float f) => 0f;
        public static float Clamp01(float f) => f;
        public static int Clamp(int v, int lo, int hi) => v;
        public static float Max(float a, float b) => a;
        public static int Max(int a, int b) => a;
        public static float Abs(float f) => f;
        public static int Abs(int v) => v;
        public static float Atan2(float y, float x) => 0f;
        public const float Rad2Deg = 57.29578f;
        public static float Min(float a, float b) => a;
        public static int Min(int a, int b) => a;
        public static int CeilToInt(float f) => 0;
    }

    public class Transform : Component, IEnumerable
    {
        public Vector3 position { get; set; }
        public Vector3 localPosition { get; set; }
        public Vector3 localScale { get; set; }
        public Quaternion rotation { get; set; }
        public Quaternion localRotation { get; set; }
        public Transform parent { get; set; }
        public void SetParent(Transform p, bool worldPositionStays) { }
        public void SetParent(Transform p) { }
        public Vector3 InverseTransformPoint(Vector3 position) => position;
        public Vector3 TransformPoint(Vector3 position) => position;
        public int childCount { get; }
        public Transform GetChild(int index) => null;
        public IEnumerator GetEnumerator() => null;
    }

    public class Component : Object
    {
        public Transform transform { get; }
        public GameObject gameObject { get; }
        public T GetComponent<T>() where T : class => null;
        public T GetComponentInChildren<T>() where T : class => null;
        public T AddComponent<T>() where T : Component, new() => null;
    }

    public class Behaviour : Component
    {
        public bool enabled { get; set; }
    }

    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator routine) => null;
        public void StopAllCoroutines() { }
    }

    public sealed class Coroutine { }
    public sealed class WaitForSeconds { public WaitForSeconds(float seconds) { } }

    public enum PrimitiveType { Cube, Sphere, Capsule, Cylinder, Plane, Quad }

    public sealed class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string name) { }
        public GameObject(string name, params Type[] components) { }
        public Transform transform { get; }
        public string tag { get; set; }
        public T AddComponent<T>() where T : Component => null;
        public Component AddComponent(Type type) => null;
        public T GetComponent<T>() where T : class => null;
        public T GetComponentInChildren<T>() where T : class => null;
        public void SetActive(bool value) { }
        public bool activeSelf => true;
        public static GameObject CreatePrimitive(PrimitiveType type) => null;
    }

    public class Renderer : Component
    {
        public bool enabled { get; set; }
        public Material material { get; set; }
        public Material sharedMaterial { get; set; }
    }

    public class MeshRenderer : Renderer { }
    public class Collider : Component { }
    public class BoxCollider : Collider { public Vector3 size { get; set; } }

    public sealed class Shader : Object { public static Shader Find(string name) => null; }

    public class Material : Object
    {
        public Material(Shader shader) { }
        public bool HasProperty(string name) => false;
        public void SetColor(string name, Color value) { }
        public void SetFloat(string name, float value) { }
    }

    public class Font : Object { public Material material { get; } }

    public class TextMesh : Component
    {
        public string text { get; set; }
        public Font font { get; set; }
        public float characterSize { get; set; }
        public int fontSize { get; set; }
        public TextAnchor anchor { get; set; }
        public Color color { get; set; }
    }

    public class TextAsset : Object { public string text => ""; }

    public static class Resources
    {
        public static T Load<T>(string path) where T : Object => null;
        public static T[] LoadAll<T>(string path) where T : Object => new T[0];
        public static T GetBuiltinResource<T>(string path) where T : Object => null;
    }

    public enum CameraClearFlags { Skybox, Color, SolidColor, Depth, Nothing }

    public class Camera : Behaviour
    {
        public static Camera main => null;
        public float fieldOfView { get; set; }
        public CameraClearFlags clearFlags { get; set; }
        public Color backgroundColor { get; set; }
    }

    public enum LightType { Spot, Directional, Point, Area }
    public enum LightShadows { None, Hard, Soft }

    public class Light : Behaviour
    {
        public LightType type { get; set; }
        public Color color { get; set; }
        public float intensity { get; set; }
        public LightShadows shadows { get; set; }
    }

    public static class RenderSettings
    {
        public static Rendering.AmbientMode ambientMode { get; set; }
        public static Color ambientLight { get; set; }
    }

    public static class Time { public static float deltaTime => 0f; public static float time => 0f; }

    // Real Unity declares these as const ints on a static class, not an enum,
    // and Screen.sleepTimeout is an int. Mirroring that exactly matters: an enum
    // here would reject the perfectly valid assignment the game makes.
    public static class SleepTimeout
    {
        public const int NeverSleep = -1;
        public const int SystemSetting = -2;
    }

    public static class Screen { public static int sleepTimeout { get; set; } }

    public static class Application { public static int targetFrameRate { get; set; } }

    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogError(object message) { }
        public static void LogWarning(object message) { }
    }

    public static class PlayerPrefs
    {
        public static int GetInt(string key, int defaultValue = 0) => defaultValue;
        public static void SetInt(string key, int value) { }
        public static string GetString(string key, string defaultValue = "") => defaultValue;
        public static void SetString(string key, string value) { }
        public static void DeleteKey(string key) { }
        public static void DeleteAll() { }
        public static void Save() { }
    }

    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }

    public enum TextAnchor
    {
        UpperLeft, UpperCenter, UpperRight,
        MiddleLeft, MiddleCenter, MiddleRight,
        LowerLeft, LowerCenter, LowerRight
    }

    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }

    public class AudioListener : Behaviour { }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RequireComponent : Attribute { public RequireComponent(Type type) { } }

    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, SubsystemRegistration }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { }
    }

    namespace Rendering
    {
        public enum AmbientMode { Skybox, Trilight, Flat, Custom }
    }

    namespace EventSystems
    {
        public class UIBehaviour : MonoBehaviour { }
        public class EventSystem : UIBehaviour { }
        public class BaseInputModule : UIBehaviour { }
        public class PointerInputModule : BaseInputModule { }
        public class StandaloneInputModule : PointerInputModule { }
        public class PhysicsRaycaster : Component { }
        public class AbstractEventData { }
        public class BaseEventData : AbstractEventData { }
        public class PointerEventData : BaseEventData { }
        public interface IEventSystemHandler { }
        public interface IPointerClickHandler : IEventSystemHandler
        {
            void OnPointerClick(PointerEventData eventData);
        }
    }

    namespace UI
    {
        public class Graphic : UnityEngine.EventSystems.UIBehaviour
        {
            public Color color { get; set; }
        }

        public class MaskableGraphic : Graphic { }
        public class Image : MaskableGraphic
        {
            public bool raycastTarget { get; set; }
        }

        /// <summary>Clips children to this rect without a stencil buffer.</summary>
        public class RectMask2D : UnityEngine.EventSystems.UIBehaviour { }

        public class ScrollRect : UnityEngine.EventSystems.UIBehaviour
        {
            public enum MovementType { Unrestricted, Elastic, Clamped }
            public RectTransform content { get; set; }
            public RectTransform viewport { get; set; }
            public bool horizontal { get; set; }
            public bool vertical { get; set; }
            public MovementType movementType { get; set; }
            public float elasticity { get; set; }
            public float scrollSensitivity { get; set; }
            public Vector2 normalizedPosition { get; set; }
            public float verticalNormalizedPosition { get; set; }
        }

        public class Text : MaskableGraphic
        {
            public string text { get; set; }
            public Font font { get; set; }
            public int fontSize { get; set; }
            public TextAnchor alignment { get; set; }
            public HorizontalWrapMode horizontalOverflow { get; set; }
            public VerticalWrapMode verticalOverflow { get; set; }
            public bool supportRichText { get; set; }
            public FontStyle fontStyle { get; set; }
            public RectTransform rectTransform { get; }
        }

        public class Selectable : UnityEngine.EventSystems.UIBehaviour
        {
            public enum Transition { None, ColorTint, SpriteSwap, Animation }
            public Transition transition { get; set; }
            public bool interactable { get; set; }
            public Graphic targetGraphic { get; set; }
        }

        public class ButtonClickedEvent
        {
            public void AddListener(Action call) { }
            public void RemoveAllListeners() { }
        }

        public class Button : Selectable
        {
            public ButtonClickedEvent onClick { get; }
        }

        public class Canvas : Behaviour
        {
            public RenderMode renderMode { get; set; }
            public int sortingOrder { get; set; }
        }

        public class CanvasScaler : UnityEngine.EventSystems.UIBehaviour
        {
            public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
            public ScaleMode uiScaleMode { get; set; }
            public Vector2 referenceResolution { get; set; }
            public float matchWidthOrHeight { get; set; }
        }

        public class GraphicRaycaster : Component { }
    }

    public class RectTransform : Transform
    {
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 pivot { get; set; }
        public Vector2 anchoredPosition { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Vector2 offsetMin { get; set; }
        public Vector2 offsetMax { get; set; }
    }

    public class CanvasRenderer : Component { }
    public class CanvasGroup : Behaviour { public float alpha { get; set; } }
}
