using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TFY.CatAdventure.UI
{
    /// <summary>A full-screen page. Built once in code; shown/hidden by the UIManager.</summary>
    public abstract class UIScreen
    {
        public RectTransform Root { get; private set; }
        protected App App => App.I;
        public bool Visible => Root && Root.gameObject.activeSelf;

        public void Create(Transform parent)
        {
            Root = UIKit.Fill(parent, GetType().Name);
            var safe = UIKit.Fill(Root, "Safe"); safe.gameObject.AddComponent<SafeArea>();
            Build(safe);
            Root.gameObject.SetActive(false);
        }

        protected abstract void Build(RectTransform safe);
        public virtual void OnShow() { }
        public virtual void OnHide() { }
        public virtual void Tick(float dt) { }
        /// <summary>Unsubscribe from static events before the screen is destroyed.</summary>
        public virtual void Dispose() { }
        /// <summary>Android back button. Return true if handled.</summary>
        public virtual bool OnBack() => false;

        protected static RectTransform Header(RectTransform parent, string title, Action onBack)
        {
            var top = UIKit.Rect(parent, "Header", new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -190), new Vector2(-30, -30));
            UIKit.Label(top, title, 84, Color.white);
            if (onBack != null) { var b = UIKit.At(top, "Back", new Vector2(0, 0.5f), new Vector2(150, 150)); UIKit.Button(b, "", UIKit.Purple, onBack, "back"); }
            return top;
        }
    }

    /// <summary>
    /// Owns the canvases (screens, HUD, overlays), navigation, friendly toasts and dialogs. Error messages are always simple
    /// and kind ("Couldn't connect right now. You can keep playing offline.") — never technical.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        private Canvas main, overlay;
        private readonly Dictionary<Type, UIScreen> screens = new Dictionary<Type, UIScreen>();
        private UIScreen current;
        private RectTransform toastRoot, dialogRoot; private Text toastText; private float toastTimer;
        private readonly Queue<string> toasts = new Queue<string>();
        public UIScreen Current => current;

        public void Init()
        {
            main = UIKit.CreateCanvas(transform, "ScreensCanvas", 10);
            overlay = UIKit.CreateCanvas(transform, "OverlayCanvas", 50);
            foreach (var s in new UIScreen[] { new SplashScreen(), new LoadingScreen(), new HomeScreen(), new LevelsScreen(), new ShopScreen(), new AchievementsScreen(),
                                               new RewardsScreen(), new SettingsScreen(), new HudScreen(), new PauseScreen(), new CompleteScreen(), new FailScreen() })
            { s.Create(main.transform); screens[s.GetType()] = s; }

            toastRoot = UIKit.At(overlay.transform, "Toast", new Vector2(0.5f, 1f), new Vector2(940, 150), new Vector2(0, -200));
            UIKit.Panel(toastRoot, "Bg", new Color(0.17f, 0.12f, 0.3f, 0.92f), 50);
            toastText = UIKit.Label(toastRoot, "", 44, Color.white); ((RectTransform)toastText.transform).offsetMin = new Vector2(30, 10); ((RectTransform)toastText.transform).offsetMax = new Vector2(-30, -10);
            toastRoot.gameObject.SetActive(false);
            dialogRoot = UIKit.Fill(overlay.transform, "Dialog"); dialogRoot.gameObject.SetActive(false);
            GameEvents.Toast += Toast;
        }

        private void OnDestroy() { GameEvents.Toast -= Toast; foreach (var s in screens.Values) s.Dispose(); }

        /// <summary>Rebuilds every screen (after a language change).</summary>
        public void Rebuild()
        {
            GameEvents.Toast -= Toast;
            foreach (var s in screens.Values) s.Dispose();
            screens.Clear(); current = null; toasts.Clear(); toastTimer = 0;
            if (main) Destroy(main.gameObject);
            if (overlay) Destroy(overlay.gameObject);
            Init();
        }

        public T Get<T>() where T : UIScreen => (T)screens[typeof(T)];

        public T Show<T>() where T : UIScreen
        {
            var next = Get<T>();
            if (current != null && current != next) { current.OnHide(); current.Root.gameObject.SetActive(false); }
            current = next; next.Root.gameObject.SetActive(true); next.OnShow();
            return next;
        }

        /// <summary>Shows an extra screen on top (the HUD stays visible under Pause).</summary>
        public void ShowOverlay<T>(bool on) where T : UIScreen
        {
            var s = Get<T>(); s.Root.gameObject.SetActive(on); if (on) { s.Root.SetAsLastSibling(); s.OnShow(); } else s.OnHide();
        }

        public void Toast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (toastTimer > 0) { if (!toasts.Contains(message) && toastText.text != message) toasts.Enqueue(message); return; }
            toastText.text = message; toastRoot.gameObject.SetActive(true); toastTimer = 2.8f;
        }

        /// <summary>Simple dialog with up to two buttons. Used for confirmations and friendly errors.</summary>
        public void Dialog(string message, string okText, Action ok, string cancelText = null, Action cancel = null)
        {
            UIKit.Clear(dialogRoot); dialogRoot.gameObject.SetActive(true); dialogRoot.SetAsLastSibling();
            var dim = UIKit.Fill(dialogRoot, "Dim"); var dimImg = dim.gameObject.AddComponent<Image>(); dimImg.color = new Color(0, 0, 0, 0.55f);
            var box = UIKit.At(dialogRoot, "Box", new Vector2(0.5f, 0.5f), new Vector2(900, 700));
            UIKit.Panel(box, "Bg", UIKit.Light, 60);
            var msg = UIKit.Label(box, message, 50, UIKit.Dark, TextAnchor.MiddleCenter, false); ((RectTransform)msg.transform).offsetMin = new Vector2(50, 220); ((RectTransform)msg.transform).offsetMax = new Vector2(-50, -50);
            void Close() { dialogRoot.gameObject.SetActive(false); }
            if (cancelText != null)
            {
                UIKit.Button(UIKit.Rect(box, "Cancel", new Vector2(0.06f, 0.06f), new Vector2(0.47f, 0.27f)), cancelText, UIKit.Grey, () => { Close(); cancel?.Invoke(); }, null, 50);
                UIKit.Button(UIKit.Rect(box, "Ok", new Vector2(0.53f, 0.06f), new Vector2(0.94f, 0.27f)), okText, UIKit.Green, () => { Close(); ok?.Invoke(); }, null, 50);
            }
            else UIKit.Button(UIKit.Rect(box, "Ok", new Vector2(0.25f, 0.06f), new Vector2(0.75f, 0.27f)), okText, UIKit.Green, () => { Close(); ok?.Invoke(); }, null, 50);
        }

        public bool DialogOpen => dialogRoot && dialogRoot.gameObject.activeSelf;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (toastTimer > 0)
            {
                toastTimer -= dt;
                var cg = toastRoot.GetComponent<CanvasGroup>(); if (!cg) cg = toastRoot.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = Mathf.Clamp01(Mathf.Min(toastTimer * 3f, (2.8f - toastTimer) * 6f));
                if (toastTimer <= 0) { toastRoot.gameObject.SetActive(false); if (toasts.Count > 0) Toast(toasts.Dequeue()); }
            }
            current?.Tick(dt);
            foreach (var s in screens.Values) if (s != current && s.Visible) s.Tick(dt);
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame) Back();
        }

        public void Back()
        {
            if (DialogOpen) { dialogRoot.gameObject.SetActive(false); return; }
            foreach (var s in screens.Values) if (s != current && s.Visible && s.OnBack()) return;
            current?.OnBack();
        }

        public IEnumerator FadeIn(CanvasGroup g, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime) { g.alpha = t / seconds; yield return null; }
            g.alpha = 1;
        }
    }
}
