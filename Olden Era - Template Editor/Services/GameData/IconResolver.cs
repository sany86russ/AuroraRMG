using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Olden_Era___Template_Editor.Services.GameData
{
    /// <summary>
    /// Resolves a game icon (by its sprite SID) into a WPF <see cref="ImageSource"/> from the local
    /// icon cache (<c>%LOCALAPPDATA%\AuroraRMG\catalog\icons\&lt;sid&gt;.png</c>). Returns null when no
    /// icon has been extracted yet, so callers fall back to the coloured category dot.
    /// Results are memoised (including misses) and frozen for cross-thread use.
    /// </summary>
    public static class IconResolver
    {
        private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.Ordinal);
        private static readonly Queue<string> CacheOrder = new();
        private const int MaxCachedIcons = 512;
        private static readonly object Sync = new();

        public static string IconDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "AuroraRMG", "catalog", "icons");

        /// <summary>True if at least one icon PNG exists in the cache.</summary>
        public static bool HasAnyIcons
        {
            get
            {
                try { return Directory.Exists(IconDirectory) && Directory.EnumerateFiles(IconDirectory, "*.png").Any(); }
                catch { return false; }
            }
        }

        public static ImageSource? Resolve(string? iconSid)
        {
            if (string.IsNullOrWhiteSpace(iconSid)) return null;

            lock (Sync)
            {
                if (Cache.TryGetValue(iconSid, out var hit)) return hit;

                ImageSource? img = null;
                try
                {
                    string path = Path.Combine(IconDirectory, iconSid + ".png");
                    if (File.Exists(path))
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption  = BitmapCacheOption.OnLoad;   // load fully so the file isn't locked
                        bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                        bmp.DecodePixelWidth = 64; // picker icons are at most 32 DIPs, including 200% DPI
                        bmp.UriSource    = new Uri(path, UriKind.Absolute);
                        bmp.EndInit();
                        bmp.Freeze();
                        img = bmp;
                    }
                }
                catch { img = null; }

                if (Cache.Count >= MaxCachedIcons) Cache.Remove(CacheOrder.Dequeue());
                Cache[iconSid] = img;
                CacheOrder.Enqueue(iconSid);
                return img;
            }
        }

        /// <summary>Clears the memo (call after a fresh extraction so new icons are picked up).</summary>
        public static void Invalidate()
        {
            lock (Sync) { Cache.Clear(); CacheOrder.Clear(); }
        }
    }
}
