using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace TFY.CatAdventure.Data
{
    /// <summary>
    /// Crash-safe local files: write to .tmp, keep the previous version as .bak, then swap. Each file carries a SHA-256
    /// checksum line, so a corrupted save is detected and the backup is used instead (never a crash, never a wipe).
    /// </summary>
    public static class LocalFile
    {
        private static string PathOf(string name) => Path.Combine(Application.persistentDataPath, name);

        public static void Write(string name, string text)
        {
            var path = PathOf(name); var tmp = path + ".tmp"; var bak = path + ".bak";
            File.WriteAllText(tmp, Checksum(text) + "\n" + text, Encoding.UTF8);
            if (File.Exists(path)) File.Copy(path, bak, true);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        public enum ReadStatus { Ok, Missing, RecoveredFromBackup, Corrupt }

        public static string Read(string name, out ReadStatus status)
        {
            var path = PathOf(name);
            if (!File.Exists(path) && !File.Exists(path + ".bak")) { status = ReadStatus.Missing; return null; }
            var main = TryRead(path);
            if (main != null) { status = ReadStatus.Ok; return main; }
            var backup = TryRead(path + ".bak");
            if (backup != null) { status = ReadStatus.RecoveredFromBackup; return backup; }
            status = ReadStatus.Corrupt;
            return null;
        }

        public static void Delete(string name)
        {
            foreach (var p in new[] { PathOf(name), PathOf(name) + ".bak", PathOf(name) + ".tmp" }) if (File.Exists(p)) File.Delete(p);
        }

        private static string TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var all = File.ReadAllText(path, Encoding.UTF8);
                int nl = all.IndexOf('\n'); if (nl <= 0) return null;
                var sum = all.Substring(0, nl); var body = all.Substring(nl + 1);
                return sum == Checksum(body) ? body : null;
            }
            catch (Exception e) { Debug.LogWarning($"[LocalFile] {path}: {e.Message}"); return null; }
        }

        private static string Checksum(string s)
        {
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(s)));
        }
    }
}
