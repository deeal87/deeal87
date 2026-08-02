using UnityEngine;

namespace SunnyStop.Game
{
    /// <summary>
    /// Builds the entire game at runtime - camera, lights, board, HUD and flow.
    ///
    /// It hooks itself in via RuntimeInitializeOnLoadMethod, so the project runs from
    /// any scene, including the empty default one. That means the repo contains no
    /// binary .unity file that nobody can review, and setup is: open an empty Unity
    /// project, drop Assets/ in, press Play.
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        private static Bootstrap _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (_instance != null) return;
            var go = new GameObject("SunnyStop");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<Bootstrap>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;

            SetUpCamera();
            SetUpLighting();
            UiBuilder.CreateEventSystem();

            var board = new GameObject("BoardView").AddComponent<BoardView>();
            board.transform.SetParent(transform, false);

            var hud = new GameObject("Hud").AddComponent<Hud>();
            hud.transform.SetParent(transform, false);

            var postcard = new GameObject("Postcard").AddComponent<PostcardView>();
            postcard.transform.SetParent(transform, false);

            var flow = gameObject.AddComponent<GameFlow>();
            flow.Initialise(board, hud, postcard);

            // The board is built behind the menu, so the departure call is
            // instant rather than a second load screen.
            flow.StartFromSave();

            var menu = new GameObject("Menu").AddComponent<MenuView>();
            menu.transform.SetParent(transform, false);
            // The route map and the album screens are not built in the Unity
            // prototype yet. Passing null omits those rows entirely rather than
            // shipping buttons that go nowhere; the browser build has both, and
            // they land here when the screens do.
            menu.Initialise(onPlay: flow.LoadLevel, onMap: null, onAlbum: null);
            menu.Show();
        }

        private void SetUpCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("MainCamera", typeof(Camera), typeof(AudioListener));
                go.tag = "MainCamera";
                camera = go.GetComponent<Camera>();
            }

            camera.transform.position = new Vector3(0f, 9.2f, -9.4f);
            camera.transform.rotation = Quaternion.Euler(46f, 0f, 0f);
            camera.fieldOfView = 42f;
            camera.clearFlags = CameraFlags();
            camera.backgroundColor = Palette.SkyForChapter(1);

            // Taps on 3D objects need a physics raycaster on the camera.
            if (camera.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>() == null)
            {
                camera.gameObject.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();
            }
        }

        private static CameraClearFlags CameraFlags() => CameraClearFlags.SolidColor;

        private void SetUpLighting()
        {
            if (FindFirstObjectByType<Light>() != null) return;

            var go = new GameObject("Sun", typeof(Light));
            go.transform.SetParent(transform, false);
            var light = go.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.97f, 0.90f);
            light.intensity = 1.05f;
            light.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.66f);
        }
    }
}
