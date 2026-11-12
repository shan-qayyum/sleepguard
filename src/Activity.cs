// A small activity log (%AppData%\SleepGuard\activity.log): when protection turned on and off, and every
// wrong PIN, so you can see in the morning whether someone tried.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SleepGuard
{
    public static class Activity
    {
        const int KeepLines = 1000;
        static string FilePath { get { return Path.Combine(Settings.Folder, "activity.log"); } }

        public static void Add(string text)
        {
            try
            {
                Directory.CreateDirectory(Settings.Folder);
                File.AppendAllText(FilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text + Environment.NewLine);
                var info = new FileInfo(FilePath);
                if (info.Length > 200000)
                {
                    string[] lines = File.ReadAllLines(FilePath);
                    File.WriteAllLines(FilePath, lines.Skip(Math.Max(0, lines.Length - KeepLines)).ToArray());
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        // Newest first.
        public static IList<string> Recent(int count)
        {
            try { return File.ReadAllLines(FilePath).Reverse().Take(count).ToList(); }
            catch (IOException) { return new List<string>(); }
            catch (UnauthorizedAccessException) { return new List<string>(); }
        }

        public static void Clear()
        {
            try { File.Delete(FilePath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
