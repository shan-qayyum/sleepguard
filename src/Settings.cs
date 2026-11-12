// Settings and the PIN, kept in %AppData%\SleepGuard\settings.ini. The PIN is stored only as a salted
// PBKDF2 hash.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace SleepGuard
{
    public static class Settings
    {
        public static bool AutoEnabled = true;
        public static int AutoMinutes = 10;          // turn on after this long without keyboard or mouse input
        public static bool OnlyWhenPlaying = true;   // ...and only if sound has been playing in the last minute
        public static char HotkeyLetter = 'G';       // Ctrl+Alt+<letter> turns protection on
        public static int MaxWrongTries = 3;         // wrong PINs before Windows itself is locked
        public static bool ShowPrompt = true;        // show the PIN box when someone types
        public static bool SleepTimerEnabled = false;
        public static int SleepTimerMinutes = 60;    // pause the video after this long protected
        public static bool SleepTimerScreenOff = true;
        public static bool NotifyOnTurnOn = false;
        public static string PinHash = "";
        public static string PinSalt = "";
        // PBKDF2 rounds. Kept modest so a check stays well under a second on slow or throttled CPUs; guessing is
        // limited by the wrong-PIN lock, not by the hash cost. Stored per PIN so it can change later.
        public static int PinIterations = NewPinIterations;
        const int NewPinIterations = 4000;

        public static string Folder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SleepGuard"); } }
        static string FilePath { get { return Path.Combine(Folder, "settings.ini"); } }

        public static bool HasPin { get { return PinHash.Length > 0 && PinSalt.Length > 0; } }

        public static void Load()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0) map[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            catch (IOException) { return; }
            catch (UnauthorizedAccessException) { return; }

            AutoEnabled = Bool(map, "AutoEnabled", AutoEnabled);
            AutoMinutes = Int(map, "AutoMinutes", AutoMinutes, 1, 240);
            OnlyWhenPlaying = Bool(map, "OnlyWhenPlaying", OnlyWhenPlaying);
            string v;
            if (map.TryGetValue("HotkeyLetter", out v) && v.Length == 1 && char.ToUpperInvariant(v[0]) >= 'A' && char.ToUpperInvariant(v[0]) <= 'Z')
                HotkeyLetter = char.ToUpperInvariant(v[0]);
            MaxWrongTries = Int(map, "MaxWrongTries", MaxWrongTries, 1, 10);
            ShowPrompt = Bool(map, "ShowPrompt", ShowPrompt);
            SleepTimerEnabled = Bool(map, "SleepTimerEnabled", SleepTimerEnabled);
            SleepTimerMinutes = Int(map, "SleepTimerMinutes", SleepTimerMinutes, 1, 480);
            SleepTimerScreenOff = Bool(map, "SleepTimerScreenOff", SleepTimerScreenOff);
            NotifyOnTurnOn = Bool(map, "NotifyOnTurnOn", NotifyOnTurnOn);
            if (map.TryGetValue("PinHash", out v)) PinHash = v;
            if (map.TryGetValue("PinSalt", out v)) PinSalt = v;
            int it;
            PinIterations = map.TryGetValue("PinIterations", out v) && int.TryParse(v, out it) && it >= 1000 ? it : 20000;   // 20000 = first builds
        }

        public static void Save()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllLines(FilePath, new[]
            {
                "AutoEnabled=" + AutoEnabled,
                "AutoMinutes=" + AutoMinutes,
                "OnlyWhenPlaying=" + OnlyWhenPlaying,
                "HotkeyLetter=" + HotkeyLetter,
                "MaxWrongTries=" + MaxWrongTries,
                "ShowPrompt=" + ShowPrompt,
                "SleepTimerEnabled=" + SleepTimerEnabled,
                "SleepTimerMinutes=" + SleepTimerMinutes,
                "SleepTimerScreenOff=" + SleepTimerScreenOff,
                "NotifyOnTurnOn=" + NotifyOnTurnOn,
                "PinHash=" + PinHash,
                "PinSalt=" + PinSalt,
                "PinIterations=" + PinIterations,
            });
        }

        public static void SetPin(string pin)
        {
            byte[] salt = new byte[16];
            using (var rng = new RNGCryptoServiceProvider()) rng.GetBytes(salt);
            PinSalt = Convert.ToBase64String(salt);
            PinIterations = NewPinIterations;
            PinHash = Hash(pin.ToUpperInvariant(), salt);
            Save();
        }

        public static bool CheckPin(string pin)
        {
            if (!HasPin || pin == null) return false;
            pin = pin.ToUpperInvariant();              // PINs are stored in capitals; typing is case-insensitive
            byte[] salt;
            try { salt = Convert.FromBase64String(PinSalt); } catch (FormatException) { return false; }
            string attempt = Hash(pin, salt);
            int diff = attempt.Length ^ PinHash.Length;    // constant-time comparison
            for (int i = 0; i < Math.Min(attempt.Length, PinHash.Length); i++) diff |= attempt[i] ^ PinHash[i];
            return diff == 0;
        }

        static string Hash(string pin, byte[] salt)
        {
            using (var kdf = new Rfc2898DeriveBytes(pin, salt, PinIterations))
                return Convert.ToBase64String(kdf.GetBytes(32));
        }

        static bool Bool(Dictionary<string, string> m, string key, bool fallback)
        {
            string v; bool b;
            return m.TryGetValue(key, out v) && bool.TryParse(v, out b) ? b : fallback;
        }

        static int Int(Dictionary<string, string> m, string key, int fallback, int min, int max)
        {
            string v; int i;
            return m.TryGetValue(key, out v) && int.TryParse(v, out i) ? Math.Max(min, Math.Min(max, i)) : fallback;
        }
    }
}
