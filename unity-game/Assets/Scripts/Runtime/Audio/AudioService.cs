using System.Collections.Generic;
using TFY.CatAdventure.Domain;
using UnityEngine;

namespace TFY.CatAdventure.Audio
{
    /// <summary>
    /// Music (per world, crossfaded), ambience (birds, wind, water, traffic, forest, waves), sound effects (pooled voices,
    /// slight random pitch so repeats don't get tiring) and friendly cat sounds with a cooldown so they never spam.
    /// Respects the Music/Sound switches and volumes. Real recordings override synthesised ones (Resources/Audio/{id}).
    /// </summary>
    public class AudioService : MonoBehaviour
    {
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private AudioSource musicA, musicB, ambA, ambB; private bool musicOnA = true;
        private readonly List<AudioSource> voices = new List<AudioSource>();
        private int voiceIndex; private float lastMeow = -10f, coinPitch = 1f, lastCoin;
        private bool musicOn = true, soundOn = true; private float musicVol = 0.8f, soundVol = 1f;
        private string currentMusic, currentAmbience;

        private void Awake()
        {
            musicA = Source("MusicA", true); musicB = Source("MusicB", true); ambA = Source("AmbA", true); ambB = Source("AmbB", true);
            for (int i = 0; i < 10; i++) voices.Add(Source("Voice" + i, false));
        }

        private AudioSource Source(string n, bool loop) { var s = new GameObject(n).AddComponent<AudioSource>(); s.transform.SetParent(transform, false); s.loop = loop; s.playOnAwake = false; s.spatialBlend = 0; return s; }

        public void ApplySettings(PlayerSettings s)
        {
            musicOn = s.music; soundOn = s.sound; musicVol = s.musicVolume; soundVol = s.soundVolume;
            float mv = musicOn ? musicVol * 0.55f : 0f;
            (musicOnA ? musicA : musicB).volume = mv;
            ambA.volume = ambB.volume = soundOn ? soundVol * 0.35f : 0f;
        }

        // ------------------------------------------------------------------ clip library
        private AudioClip Get(string id)
        {
            if (clips.TryGetValue(id, out var c) && c) return c;
            c = Resources.Load<AudioClip>("Audio/" + id) ?? Generate(id);
            clips[id] = c; return c;
        }

        private static AudioClip Generate(string id)
        {
            switch (id)
            {
                case "sfx_jump": return Synth.Tone(id, 380, 760, 0.18f, "tri", 0.45f, 6);
                case "sfx_double_jump": return Synth.Tone(id, 560, 1180, 0.2f, "tri", 0.45f, 5, 0.03f);
                case "sfx_long_jump": return Synth.Tone(id, 300, 900, 0.38f, "tri", 0.45f, 3);
                case "sfx_land": return Synth.Noise(id, 0.12f, 900, 0.5f, 0.002f, 25);
                case "sfx_slide": return Synth.Noise(id, 0.35f, 2500, 0.3f, 0.02f, 5);
                case "sfx_lane": return Synth.Noise(id, 0.12f, 3500, 0.18f, 0.01f, 20);
                case "sfx_coin": return Synth.Chord(id, new[] { 1318.5f, 1975.5f }, 0.06f, 0.3f);
                case "sfx_fish": return Synth.Chord(id, new[] { 784f, 988f, 1175f }, 0.05f, 0.3f);
                case "sfx_gem": return Synth.Chord(id, new[] { 1046.5f, 1318.5f, 1568f, 2093f }, 0.05f, 0.28f);
                case "sfx_star": return Synth.Chord(id, new[] { 784f, 1046.5f, 1318.5f, 1568f, 2093f }, 0.07f, 0.3f);
                case "sfx_box": return Synth.Chord(id, new[] { 523f, 659f, 784f, 1046f }, 0.09f, 0.3f);
                case "sfx_checkpoint": return Synth.Chord(id, new[] { 659f, 784f, 988f, 1318f }, 0.08f, 0.32f);
                case "sfx_powerup": return Synth.Tone(id, 400, 1600, 0.45f, "square", 0.22f, 2, 0.05f);
                case "sfx_powerup_end": return Synth.Tone(id, 900, 450, 0.25f, "tri", 0.25f, 4);
                case "sfx_shield": return Synth.Tone(id, 1200, 600, 0.3f, "sine", 0.4f, 5, 0.1f);
                case "sfx_bump": return Synth.Tone(id, 260, 140, 0.3f, "sine", 0.55f, 5, 0.12f); // soft "boing", not a crash
                case "sfx_fall": return Synth.Tone(id, 700, 180, 0.6f, "tri", 0.35f, 1.5f, 0.02f);
                case "sfx_warning": return Synth.Noise(id, 0.9f, 180, 0.8f, 0.05f, 1.5f, false, 9);
                case "sfx_button": return Synth.Tone(id, 880, 1100, 0.07f, "sine", 0.35f, 20);
                case "sfx_level_complete": return Synth.Chord(id, new[] { 523f, 659f, 784f, 1046f, 1318f, 1568f }, 0.11f, 0.3f);
                case "sfx_fail": return Synth.Tone(id, 520, 330, 0.5f, "tri", 0.3f, 2);
                case "sfx_unlock": return Synth.Chord(id, new[] { 659f, 880f, 1108f, 1318f }, 0.1f, 0.3f);
                case "meow_happy": return Synth.Meow(id, 520, 0.55f, 0.5f);
                case "meow_excited": return Synth.Meow(id, 640, 0.4f, 1f);
                case "meow_jump": return Synth.Meow(id, 700, 0.22f, 0.8f);
                case "meow_victory": return Synth.Meow(id, 580, 0.75f, 1f);
                case "meow_surprised": return Synth.Meow(id, 760, 0.3f, 0.3f);
                case "amb_birds": return Synth.Birds(id, 8f);
                case "amb_wind": return Synth.Noise(id, 6f, 400, 0.35f, 0, 0, true, 0.15f);
                case "amb_forest": return Synth.Noise(id, 6f, 700, 0.25f, 0, 0, true, 0.1f);
                case "amb_stream": case "amb_waterfall": return Synth.Noise(id, 5f, 2200, 0.25f, 0, 0, true, 0.6f);
                case "amb_waves": return Synth.Noise(id, 8f, 900, 0.4f, 0, 0, true, 0.12f);
                case "amb_traffic": return Synth.Noise(id, 6f, 250, 0.35f, 0, 0, true, 0.25f);
                case "amb_gulls": return Synth.Birds(id, 9f);
                case "music_menu": return Synth.Music(id, 11, 96, new[] { 0, 2, 4, 5, 7, 9, 11 }, 220f, "tri", 0.5f);
                case "music_park": return Synth.Music(id, 21, 104, new[] { 0, 2, 4, 7, 9 }, 261.6f, "tri", 0.7f);       // happy, relaxing
                case "music_forest": return Synth.Music(id, 31, 100, new[] { 0, 2, 3, 5, 7, 8, 10 }, 220f, "sine", 0.8f); // nature adventure
                case "music_city": return Synth.Music(id, 41, 124, new[] { 0, 2, 4, 5, 7, 9, 11 }, 246.9f, "square", 1f); // energetic
                case "music_mountains": return Synth.Music(id, 51, 112, new[] { 0, 2, 4, 7, 9 }, 196f, "tri", 0.9f);     // adventure
                case "music_beach": return Synth.Music(id, 61, 98, new[] { 0, 4, 5, 7, 11 }, 293.7f, "sine", 0.6f);     // relaxing tropical
                case "music_snow": return Synth.Music(id, 71, 92, new[] { 0, 2, 4, 7, 9, 11 }, 329.6f, "tri", 0.55f);  // twinkly winter
                default: return Synth.Music(id, id.GetHashCode(), 104, new[] { 0, 2, 4, 7, 9 }, 261.6f, "tri", 0.6f);
            }
        }

        /// <summary>Pre-generates the clips for a world while the loading screen is showing (so play never hitches).</summary>
        public void Preload(WorldDef w)
        {
            Get(w.music); foreach (var a in w.ambience) Get(a);
            foreach (var id in new[] { "sfx_jump", "sfx_double_jump", "sfx_long_jump", "sfx_land", "sfx_coin", "sfx_bump", "meow_jump", "meow_happy" }) Get(id);
        }

        // ------------------------------------------------------------------ playback
        public void PlayMusic(string id)
        {
            if (id == currentMusic) return; currentMusic = id;
            var from = musicOnA ? musicA : musicB; var to = musicOnA ? musicB : musicA; musicOnA = !musicOnA;
            to.clip = Get(id); to.volume = 0; to.Play();
            StopAllCoroutines(); StartCoroutine(Fade(from, to, 1.2f));
        }

        private System.Collections.IEnumerator Fade(AudioSource from, AudioSource to, float seconds)
        {
            float t = 0, fromStart = from.volume;
            while (t < seconds) { t += Time.unscaledDeltaTime; float k = t / seconds; from.volume = fromStart * (1 - k); to.volume = (musicOn ? musicVol * 0.55f : 0) * k; yield return null; }
            from.Stop();
        }

        public void PlayAmbience(IList<string> ids)
        {
            string key = ids == null ? "" : string.Join(",", ids);
            if (key == currentAmbience) return; currentAmbience = key;
            ambA.Stop(); ambB.Stop();
            if (ids != null && ids.Count > 0) { ambA.clip = Get(ids[0]); ambA.Play(); }
            if (ids != null && ids.Count > 1) { ambB.clip = Get(ids[1]); ambB.Play(); }
            ambA.volume = ambB.volume = soundOn ? soundVol * 0.35f : 0f;
        }

        public void Sfx(string id, float volume = 1f, float pitchJitter = 0.06f, float pitch = 1f)
        {
            if (!soundOn || soundVol <= 0) return;
            var v = voices[voiceIndex = (voiceIndex + 1) % voices.Count];
            v.pitch = pitch * (1 + Random.Range(-pitchJitter, pitchJitter));
            v.PlayOneShot(Get(id), volume * soundVol);
        }

        /// <summary>Coins rise in pitch when collected in a quick row — satisfying without being repetitive.</summary>
        public void Coin()
        {
            coinPitch = Time.time - lastCoin < 0.4f ? Mathf.Min(1.5f, coinPitch + 0.04f) : 1f; lastCoin = Time.time;
            Sfx("sfx_coin", 0.6f, 0.01f, coinPitch);
        }

        /// <summary>Cat sounds at most every few seconds (priority ones may interrupt the cooldown).</summary>
        public void Meow(string id, bool priority = false)
        {
            if (!priority && Time.time - lastMeow < 4f) return;
            lastMeow = Time.time; Sfx(id, 0.7f, 0.08f);
        }

        public void PauseAll(bool paused) { if (paused) { musicA.Pause(); musicB.Pause(); ambA.Pause(); ambB.Pause(); } else { musicA.UnPause(); musicB.UnPause(); ambA.UnPause(); ambB.UnPause(); } }
    }

    /// <summary>Short haptic taps on Android (respects the Vibration setting).</summary>
    public static class Haptics
    {
        public static bool Enabled = true;
        public static void Tap(long ms = 25)
        {
            if (!Enabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                    vibrator?.Call("vibrate", ms);
            }
            catch { Handheld.Vibrate(); }
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
    }
}
