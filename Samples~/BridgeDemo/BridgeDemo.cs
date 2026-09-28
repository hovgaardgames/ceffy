using System;
using System.Threading.Tasks;
using Ceffy.Bridge;
using UnityEngine;

namespace Ceffy.Demos.Bridge
{
    // ---------- Shared models ----------

    [Serializable]
    public class Player
    {
        public string Id;
        public string Name;
        public int Level;
        public PlayerClass Class;
    }

    public enum PlayerClass
    {
        Warrior,
        Mage,
        Rogue
    }

    // ---------- Unity Methods (C# impl, called from JS) ----------

    [UnityMethods]
    public interface IDemoUnityMethods
    {
        Player GetPlayer();
        Player[] GetPartyMembers();
        Task<string> Greet(string name);
        void LogFromJs(string message);
    }

    // ---------- UI Methods (JS impl, called from C#) ----------

    [UiMethods]
    public interface IDemoUiMethods
    {
        void ShowNotification(string title, string message);
        void UpdatePlayerDisplay(Player player);
    }

    // ---------- Unity Methods implementation ----------

    public class DemoUnityMethodsImpl : IDemoUnityMethods
    {
        public Player GetPlayer()
        {
            return new Player { Id = "p1", Name = "Arthas", Level = 42, Class = PlayerClass.Warrior };
        }

        public Player[] GetPartyMembers()
        {
            return new[]
            {
                new Player { Id = "p1", Name = "Arthas", Level = 42, Class = PlayerClass.Warrior },
                new Player { Id = "p2", Name = "Jaina", Level = 38, Class = PlayerClass.Mage },
                new Player { Id = "p3", Name = "Valeera", Level = 35, Class = PlayerClass.Rogue },
            };
        }

        public Task<string> Greet(string name)
        {
            return Task.FromResult($"Hello from Unity, {name}!");
        }

        public void LogFromJs(string message)
        {
            Debug.Log($"[Bridge Demo] JS says: {message}");
        }
    }

    // ---------- MonoBehaviour wiring ----------

    public sealed class BridgeDemo : MonoBehaviour
    {
        [Tooltip("Optional. If not set, searches the scene.")]
        public CeffyInstance ceffyInstance;

        private CeffyBridge bridge;
        private IDemoUiMethods ui;
        private bool bridgeReady;

        private void Awake()
        {
            if (ceffyInstance == null)
            {
                ceffyInstance = GetComponent<CeffyInstance>();
                if (ceffyInstance == null)
                    ceffyInstance = FindAnyObjectByType<CeffyInstance>();
            }
        }

        private void Start()
        {
            bridge = new CeffyBridge(ceffyInstance);
            bridge.Bind<IDemoUnityMethods>(new DemoUnityMethodsImpl());
            ui = bridge.CreateProxy<IDemoUiMethods>();

            StartCoroutine(InjectWhenReady());
        }

        private System.Collections.IEnumerator InjectWhenReady()
        {
            yield return new WaitUntil(() => ceffyInstance.Texture != null);
            yield return new WaitForSeconds(0.5f);

            bridge.InjectBridgeRuntime();
            bridgeReady = true;
        }

        private void Update()
        {
            if (bridge != null)
                bridge.ProcessMessages();
        }

        private void OnDisable()
        {
            bridge?.Dispose();
        }

        private void OnGUI()
        {
            if (!bridgeReady) return;

            var y = 10f;
            if (GUI.Button(new Rect(10, y, 220, 30), "C# -> JS: Show Notification"))
            {
                ui.ShowNotification("Hello!", "This was sent from C# via the bridge.");
            }
            y += 35;

            if (GUI.Button(new Rect(10, y, 220, 30), "C# -> JS: Update Player"))
            {
                var player = new Player { Id = "p1", Name = "Arthas", Level = 42, Class = PlayerClass.Warrior };
                ui.UpdatePlayerDisplay(player);
            }
        }
    }
}
