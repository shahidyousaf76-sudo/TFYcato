using System;
using System.Collections.Generic;
using System.Linq;
using TFY.CatAdventure.Domain;
using UnityEngine;

namespace TFY.CatAdventure.Gameplay
{
    /// <summary>
    /// Teaches controls inside play, not with text pages. The first time each situation appears, the game slows down,
    /// shows one big prompt ("TAP TO JUMP", "SWIPE LEFT", "SWIPE DOWN TO SLIDE", "DOUBLE TAP TO JUMP HIGHER", "JUMP AT THE ARROWS!")
    /// and waits. An early tap is remembered and performed at exactly the right moment, so the first try always succeeds.
    /// Level 1 always guides until it has been completed once.
    /// </summary>
    public class TutorialDirector
    {
        public static event Action<string, GameInput> Prompt;   // text, the gesture to animate
        public static event Action Hide, Great;

        private const string SeenKey = "tfy.hints.v1";
        private readonly HashSet<string> seen;
        private Row current; private string currentHint; private GameInput need; private bool queued, active, failed;
        private bool guideAll;
        public float TimeScale { get; private set; } = 1f;

        public TutorialDirector()
        {
            seen = new HashSet<string>((PlayerPrefs.GetString(SeenKey, "")).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        }

        public void Begin(LevelDef level, bool levelCompletedBefore)
        {
            guideAll = level.tutorial && !levelCompletedBefore;
            current = null; active = false; queued = false; TimeScale = 1f;
        }

        /// <summary>Returns true if the input was consumed (remembered to be performed at the right moment).</summary>
        public bool Intercept(GameInput input, float distance, CatController cat)
        {
            if (!active || current == null) return false;
            bool ok = input == need || (currentHint == "branch" && (input == GameInput.Tap || input == GameInput.SwipeDown))
                      || (currentHint == "bridge" && (input == GameInput.Tap || input == GameInput.SwipeUp))
                      || (currentHint == "wall" && (input == GameInput.SwipeLeft || input == GameInput.SwipeRight || input == GameInput.Tap));
            if (!ok) return false;
            MarkSeen(currentHint); Hide?.Invoke(); TimeScale = 1f;
            float ahead = current.z - distance;
            bool timed = input == GameInput.Tap || input == GameInput.SwipeUp || input == GameInput.SwipeDown;
            if (timed && currentHint != "wall" && ahead > JumpLead(input)) { queued = true; need = input; return true; }
            active = false;
            return false;
        }

        public void NotifyHit() { if (current != null) failed = true; }

        /// <summary>Per frame. `perform` executes a remembered input.</summary>
        public void Tick(float distance, LevelPlan plan, CatController cat, Action<GameInput> perform)
        {
            if (current != null)
            {
                float ahead = current.z - distance;
                if (queued && ahead <= JumpLead(need)) { queued = false; active = false; perform(need); }
                if (current.End < distance - 1f)
                {
                    if (!failed && currentHint != null && seen.Contains(currentHint)) Great?.Invoke(); else Hide?.Invoke();
                    current = null; active = false; queued = false; TimeScale = 1f; failed = false;
                }
                else if (active) TimeScale = Mathf.MoveTowards(TimeScale, 0.22f, Time.unscaledDeltaTime * 3f);
                return;
            }

            var row = plan.rows.FirstOrDefault(r => r.obstacleId != null && r.z > distance && r.z - distance < 13f);
            if (row == null) return;
            var (hint, gesture, text) = Classify(row, cat);
            if (hint == null) return;
            bool show = !seen.Contains(hint) || (guideAll && (hint == "jump" || hint == "lane" || hint == "branch"));
            if (!show) return;
            current = row; currentHint = hint; need = gesture; active = true; failed = false;
            Prompt?.Invoke(text, gesture);
        }

        private static float JumpLead(GameInput i) => i == GameInput.SwipeDown ? 2.8f : i == GameInput.SwipeUp ? 6.5f : 3.6f;

        private static (string hint, GameInput gesture, string text) Classify(Row row, CatController cat)
        {
            bool inMyLane = row.fullWidth || row.blocked[cat.Lane];
            switch (row.obstacleId)
            {
                case "broken_bridge": return ("bridge", GameInput.Tap, Loc.T("tut.long"));
                case "falling_rocks": return inMyLane ? ("rocks", cat.Lane == 0 ? GameInput.SwipeRight : GameInput.SwipeLeft, Loc.T("tut.rocks")) : (null, GameInput.Tap, null);
                case "moving_critter": return ("critter", GameInput.Tap, Loc.T("tut.critter"));
                case "branch": return inMyLane ? ("branch", GameInput.SwipeDown, Loc.T("tut.jumpOrSlide")) : (null, GameInput.Tap, null);
                case "wall": return inMyLane ? ("wall", GameInput.Tap, Loc.T("tut.double")) : (null, GameInput.Tap, null);
            }
            if (!inMyLane) return (null, GameInput.Tap, null);
            if (row.fullWidth) return ("jump", GameInput.Tap, Loc.T("tut.tap"));
            // A single blocked lane: teach changing lanes towards the nearest open lane.
            int open = row.OpenLane(cat.Lane);
            if (open != cat.Lane && !row.fullWidth) return ("lane", open < cat.Lane ? GameInput.SwipeLeft : GameInput.SwipeRight, Loc.T(open < cat.Lane ? "tut.left" : "tut.right"));
            return ("jump", GameInput.Tap, Loc.T("tut.tap"));
        }

        private void MarkSeen(string hint)
        {
            if (hint == null || !seen.Add(hint)) return;
            PlayerPrefs.SetString(SeenKey, string.Join(",", seen)); PlayerPrefs.Save();
        }

        public void ResetHints() { seen.Clear(); PlayerPrefs.DeleteKey(SeenKey); }
    }
}
