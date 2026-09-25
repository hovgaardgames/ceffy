using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Ceffy
{
    /// <summary>
    /// One browser that hosts every shared <see cref="CeffyInstance"/> as an iframe placed at its
    /// own slot of a single texture. Created on demand by the first shared instance.
    /// </summary>
    internal sealed class CeffySharedHost : MonoBehaviour
    {
        private const int SlotPadding = 2;
        private const string HostPageResource = "CeffySharedHost.html";

        internal sealed class Slot
        {
            public readonly int Id;
            public readonly string IdString;
            public readonly CeffyInstance Owner;
            public RectInt Packed;
            public RectInt Content;
            public string Url;
            public bool Loaded;
            public readonly List<HostMessage> Pending = new();

            public Slot(int id, CeffyInstance owner)
            {
                Id = id;
                IdString = id.ToString();
                Owner = owner;
            }
        }

        private static CeffySharedHost instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private CeffyBrowser browser;
        private AtlasPacker packer;
        private readonly Dictionary<int, Slot> slots = new();
        private bool hostReady;
        private int nextSlotId = 1;
        private Slot pointerOwner;
        private Slot focusedSlot;

        public CeffyBrowser Browser => browser;
        public Texture2D Texture => browser?.Texture;

        public static CeffySharedHost GetOrCreate()
        {
            if (instance)
                return instance;

            var go = new GameObject("Ceffy Shared Host");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CeffySharedHost>();
            return instance;
        }

        private void Awake()
        {
            int width = Mathf.Max(64, CeffyInstance.SharedAtlasWidth);
            int height = Mathf.Max(64, CeffyInstance.SharedAtlasHeight);
            packer = new AtlasPacker(width, height);
            browser = new CeffyBrowser(width, height);
            browser.MessageReceived += HandleHostMessage;
            browser.DragStarted += HandleDragStart;

            var url = WriteHostPage();
            if (url != null)
                StartCoroutine(browser.Create(url));
            StartCoroutine(EndOfFrameRequestLoop());
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;

            browser.MessageReceived -= HandleHostMessage;
            browser.DragStarted -= HandleDragStart;
            browser.Close();
        }

        private void Update()
        {
            CeffyBrowser.PollCallbacks();
        }

        private IEnumerator EndOfFrameRequestLoop()
        {
            var waitForEndOfFrame = new WaitForEndOfFrame();
            while (true)
            {
                yield return waitForEndOfFrame;
                if (slots.Count > 0)
                    browser.RequestFrame();
            }
        }

        #region Slots

        /// <summary>
        /// Allocates a slot sized to the owner's Width x Height and loads its iframe once the host page is ready.
        /// Returns null when the shared texture has no room left.
        /// </summary>
        public Slot Register(CeffyInstance owner, string url)
        {
            var slot = new Slot(nextSlotId++, owner) { Url = url };
            if (!TryAllocate(owner.Width, owner.Height, out slot.Packed, out slot.Content))
            {
                Debug.LogError(
                    $"[Ceffy] Shared texture is full; cannot place '{owner.name}' ({owner.Width}x{owner.Height}). " +
                    "Increase CeffyInstance.SharedAtlasWidth/SharedAtlasHeight or disable Use Shared Instance.");
                return null;
            }

            slots[slot.Id] = slot;
            if (hostReady)
                SendAdd(slot);

            if (WebBrowserRuntime.VerboseLogging)
                Debug.Log($"[Ceffy] Shared slot {slot.Id} for '{owner.name}' at {slot.Content}");
            return slot;
        }

        public void Unregister(Slot slot)
        {
            if (!slots.Remove(slot.Id))
                return;

            if (pointerOwner == slot)
            {
                pointerOwner = null;
                browser.SendMouseLeave();
            }
            if (focusedSlot == slot)
                SetFocus(slot, false);

            if (hostReady)
                SendToHost(new HostMessage { type = "remove", id = slot.IdString });
            packer.Free(slot.Packed);
        }

        /// <summary>
        /// Moves the slot to a region of the new size, keeping the iframe (and its page state) alive.
        /// </summary>
        public void ResizeSlot(Slot slot, int width, int height)
        {
            if (!IsRegistered(slot) || (slot.Content.width == width && slot.Content.height == height))
                return;

            packer.Free(slot.Packed);
            if (!TryAllocate(width, height, out var packed, out var content))
            {
                Debug.LogError(
                    $"[Ceffy] Shared texture is full; cannot resize '{slot.Owner.name}' to {width}x{height}.");
                TryAllocate(slot.Content.width, slot.Content.height, out packed, out content);
            }

            slot.Packed = packed;
            slot.Content = content;
            if (hostReady)
                SendToHost(new HostMessage
                {
                    type = "layout", id = slot.IdString,
                    x = content.x, y = content.y, w = content.width, h = content.height
                });
        }

        private bool TryAllocate(int width, int height, out RectInt packed, out RectInt content)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            content = default;
            if (!packer.TryAllocate(width + SlotPadding * 2, height + SlotPadding * 2, out packed))
                return false;
            content = new RectInt(packed.x + SlotPadding, packed.y + SlotPadding, width, height);
            return true;
        }

        private bool IsRegistered(Slot slot) => slot != null && slots.ContainsKey(slot.Id);

        public void Navigate(Slot slot, string url)
        {
            slot.Url = url;
            slot.Loaded = false;
            if (hostReady)
                SendToHost(new HostMessage { type = "navigate", id = slot.IdString, src = slot.Url });
        }

        public void SendMessage(Slot slot, string data)
        {
            SendWhenLoaded(slot, new HostMessage { type = "msg", id = slot.IdString, data = data });
        }

        public void ExecuteJS(Slot slot, string code)
        {
            SendWhenLoaded(slot, new HostMessage { type = "exec", id = slot.IdString, data = code });
        }

        private void SendWhenLoaded(Slot slot, HostMessage message)
        {
            if (slot.Loaded)
                SendToHost(message);
            else
                slot.Pending.Add(message);
        }

        #endregion

        #region Host page

        private static string WriteHostPage()
        {
            var textAsset = Resources.Load<TextAsset>(HostPageResource);
            if (!textAsset)
            {
                Debug.LogError($"[Ceffy] Missing Resources/{HostPageResource}.txt");
                return null;
            }

            var dir = Path.Combine(Application.temporaryCachePath, "Ceffy");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, HostPageResource);
            File.WriteAllText(path, textAsset.text);
            return "file:///" + path.Replace("\\", "/").Replace(" ", "%20");
        }

        private void HandleHostMessage(string message)
        {
            var msg = JsonUtility.FromJson<HostMessage>(message);
            if (msg.type == "ready")
            {
                OnHostReady();
                return;
            }

            if (!int.TryParse(msg.id, out int id) || !slots.TryGetValue(id, out var slot))
                return;

            switch (msg.type)
            {
                case "loaded":
                    slot.Loaded = true;
                    foreach (var pending in slot.Pending)
                        SendToHost(pending);
                    slot.Pending.Clear();
                    break;
                case "msg":
                    slot.Owner.RaiseMessageFromCeffy(msg.data);
                    break;
                case "console":
                    slot.Owner.RaiseConsoleMessage((LogLevel)msg.level, msg.data, msg.src, msg.line);
                    break;
            }
        }

        private void OnHostReady()
        {
            hostReady = true;
            if (WebBrowserRuntime.VerboseLogging)
                Debug.Log("[Ceffy] Shared host page ready.");

            foreach (var slot in slots.Values)
                SendAdd(slot);
        }

        private void SendAdd(Slot slot)
        {
            var rect = slot.Content;
            SendToHost(new HostMessage
            {
                type = "add", id = slot.IdString, src = slot.Url,
                x = rect.x, y = rect.y, w = rect.width, h = rect.height
            });
        }

        private void SendToHost(HostMessage message)
        {
            browser.SendMessage(JsonUtility.ToJson(message));
        }

        #endregion

        #region Input

        /// <summary>
        /// Converts slot-local page coordinates to atlas pixels. False if the slot is no longer registered.
        /// </summary>
        private bool ToAtlas(Slot slot, ref int x, ref int y)
        {
            if (!IsRegistered(slot))
                return false;

            var rect = slot.Content;
            x = rect.x + Mathf.Clamp(x, 0, rect.width - 1);
            y = rect.y + Mathf.Clamp(y, 0, rect.height - 1);
            return true;
        }

        /// <summary>
        /// All slots share one Chromium pointer, so switching slots must leave the previous one first or
        /// it keeps its :hover state. Leaves are only honoured from the current owner, which makes the
        /// result independent of the order views update in.
        /// </summary>
        private void ClaimPointer(Slot slot)
        {
            if (pointerOwner == slot)
                return;
            if (pointerOwner != null)
                browser.SendMouseLeave();
            pointerOwner = slot;
        }

        public void SendMouseMove(Slot slot, int x, int y, EventFlags modifiers)
        {
            if (!ToAtlas(slot, ref x, ref y))
                return;
            ClaimPointer(slot);
            browser.SendMouseMove(x, y, modifiers);
        }

        public void SendMouseLeave(Slot slot)
        {
            if (slot == null || pointerOwner != slot)
                return;
            pointerOwner = null;
            browser.SendMouseLeave();
        }

        public void SendMouseClick(Slot slot, int x, int y, MouseButton button, bool isUp, int clickCount, EventFlags modifiers)
        {
            if (!ToAtlas(slot, ref x, ref y))
                return;
            ClaimPointer(slot);
            browser.SendMouseClick(x, y, button, isUp, clickCount, modifiers);
        }

        public void SendMouseWheel(Slot slot, int x, int y, int deltaX, int deltaY, EventFlags modifiers)
        {
            if (!ToAtlas(slot, ref x, ref y))
                return;
            ClaimPointer(slot);
            browser.SendMouseWheel(x, y, deltaX, deltaY, modifiers);
        }

        private void HandleDragStart(int x, int y, DragOperation allowedOps)
        {
            var owner = pointerOwner;
            if (owner == null)
            {
                // Nobody can drive this drag, so end it immediately rather than leave CEF mid-drag.
                browser.DragSourceEndedAt(x, y);
                browser.DragSourceSystemDragEnded();
                return;
            }

            owner.Owner.RaiseDragStart(x - owner.Content.x, y - owner.Content.y, allowedOps);
        }

        public void SendDragTargetEnter(Slot slot, int x, int y, EventFlags modifiers, DragOperation allowedOps)
        {
            if (ToAtlas(slot, ref x, ref y))
                browser.SendDragTargetEnter(x, y, modifiers, allowedOps);
        }

        public void SendDragTargetOver(Slot slot, int x, int y, EventFlags modifiers, DragOperation allowedOps)
        {
            if (ToAtlas(slot, ref x, ref y))
                browser.SendDragTargetOver(x, y, modifiers, allowedOps);
        }

        public void SendDragTargetLeave(Slot slot)
        {
            if (IsRegistered(slot))
                browser.SendDragTargetLeave();
        }

        public void SendDragTargetDrop(Slot slot, int x, int y, EventFlags modifiers)
        {
            if (ToAtlas(slot, ref x, ref y))
                browser.SendDragTargetDrop(x, y, modifiers);
        }

        public void DragSourceEndedAt(Slot slot, int x, int y)
        {
            if (ToAtlas(slot, ref x, ref y))
                browser.DragSourceEndedAt(x, y);
        }

        public void DragSourceSystemDragEnded(Slot slot)
        {
            if (IsRegistered(slot))
                browser.DragSourceSystemDragEnded();
        }

        /// <summary>
        /// Moves DOM focus into the slot's iframe so shared key events reach the right page.
        /// Blur is only honoured from the focused slot, for the same ordering reason as <see cref="ClaimPointer"/>.
        /// </summary>
        public void SetFocus(Slot slot, bool focused)
        {
            if (slot == null)
                return;

            if (focused)
            {
                focusedSlot = slot;
                if (hostReady)
                    SendToHost(new HostMessage { type = "focus", id = slot.IdString });
            }
            else if (focusedSlot == slot)
            {
                focusedSlot = null;
                if (hostReady)
                    SendToHost(new HostMessage { type = "blur" });
            }
        }

        #endregion

        [Serializable]
        internal class HostMessage
        {
            public string type;
            public string id;
            public string src;
            public string data;
            public int x;
            public int y;
            public int w;
            public int h;
            public int level;
            public int line;
        }
    }
}
