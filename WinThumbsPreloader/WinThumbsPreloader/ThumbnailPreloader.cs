using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using static WinThumbsPreloader.Logger;

#nullable enable

namespace WinThumbsPreloader
{
    [SupportedOSPlatform("windows")]
    sealed class ThumbnailPreloader
    {
        const int E_ACCESSDENIED = unchecked((int)0x80070005);
        const int RPC_E_DISCONNECTED = unchecked((int)0x80010108);
        const int MaxCachedParentDirsPerThread = 32;

        private static readonly Guid CLSID_LocalThumbnailCache =
            new Guid("50ef4544-ac9f-4a8e-b21b-8a26180db13f");

        private static readonly Guid IID_IShellItem =
            new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");

        private static readonly Lazy<Type?> ThumbnailCacheComType = new(() =>
            Type.GetTypeFromCLSID(CLSID_LocalThumbnailCache, throwOnError: false));

        // Synchronizes full-path SHCreateItemFromParsingName calls and ensures one-time local drive
        // shell extension initialization (prevents third-party Drive FolderExtensions such as
        // ExplorerPatcher from racing on IAT patching when multiple STA workers start on a network drive).
        private static readonly object ShellParseLock = new();
        private static bool _shellWarmupDone;

        // Per-thread LRU cache of parent directory IShellItem instances (avoids re-parsing deep paths or SMB shares per file)
        private static readonly ThreadLocal<Dictionary<string, IShellItem>> ThreadLocalParentItems =
            new ThreadLocal<Dictionary<string, IShellItem>>(() => new Dictionary<string, IShellItem>(StringComparer.OrdinalIgnoreCase));

        // Legacy single base item fallback
        private static readonly ThreadLocal<(string? basePath, IShellItem? baseItem)> ThreadLocalBaseItem =
            new ThreadLocal<(string?, IShellItem?)>(() => (null, null));

        // One thumbnail cache instance per thread (reused across calls)
        private static readonly ThreadLocal<IThumbnailCache?> ThreadLocalCache =
            new ThreadLocal<IThumbnailCache?>(CreateThumbnailCacheInstance);

        private static IThumbnailCache? CreateThumbnailCacheInstance()
        {
            var type = ThumbnailCacheComType.Value;
            if (type == null)
                return null;

            try
            {
                var instance = Activator.CreateInstance(type);
                if (instance == null)
                {
                    WriteLine("Activator.CreateInstance returned null for IThumbnailCache", LoggingFrequency.DebugLogging);
                    return null;
                }
                return (IThumbnailCache)instance;
            }
            catch (Exception ex)
            {
                WriteLine("Failed to create IThumbnailCache: " + ex.Message, LoggingFrequency.DebugLogging);
                return null;
            }
        }

        private static IThumbnailCache? GetOrCreateThreadLocalCache()
        {
            var cache = ThreadLocalCache.Value;
            if (cache == null)
            {
                cache = CreateThumbnailCacheInstance();
                ThreadLocalCache.Value = cache;
            }
            return cache;
        }

        public static long CachedHitsCount = 0;
        public static long ExtractedCount = 0;

        public static void ResetCounters()
        {
            Interlocked.Exchange(ref CachedHitsCount, 0);
            Interlocked.Exchange(ref ExtractedCount, 0);
        }

        /// <summary>
        /// Releases all per-thread cached COM objects on the current worker thread.
        /// </summary>
        public static void CleanupCurrentThreadComObjects()
        {
            try
            {
                if (ThreadLocalParentItems.IsValueCreated && ThreadLocalParentItems.Value is { Count: > 0 } dict)
                {
                    foreach (var kvp in dict)
                    {
                        try
                        {
                            if (kvp.Value != null && Marshal.IsComObject(kvp.Value))
                                Marshal.ReleaseComObject(kvp.Value);
                        }
                        catch { }
                    }
                    dict.Clear();
                }

                if (ThreadLocalBaseItem.IsValueCreated)
                {
                    var (_, baseItem) = ThreadLocalBaseItem.Value;
                    if (baseItem != null && Marshal.IsComObject(baseItem))
                    {
                        try { Marshal.ReleaseComObject(baseItem); } catch { }
                    }
                    ThreadLocalBaseItem.Value = (null, null);
                }

                if (ThreadLocalCache.IsValueCreated && ThreadLocalCache.Value is { } cache)
                {
                    if (Marshal.IsComObject(cache))
                    {
                        try { Marshal.ReleaseComObject(cache); } catch { }
                    }
                    ThreadLocalCache.Value = null;
                }
            }
            catch
            {
                // Ignore cleanup errors on thread exit
            }
        }

        /// <summary>
        /// High-performance V3 entry point using a ScannedItem with direct parent directory IShellItem caching
        /// and network share fallback via IShellItemImageFactory.
        /// </summary>
        public static void PreloadThumbnail(in ScannedItem item, uint[] sizes)
        {
            if (string.IsNullOrWhiteSpace(item.FullPath) || sizes == null || sizes.Length == 0)
                return;

            IShellItem? shellItem = null;
            int hrCreate = 0;

            if (!string.IsNullOrWhiteSpace(item.ParentDirectory) && !string.IsNullOrWhiteSpace(item.RelativeName))
            {
                IShellItem? parentItem = GetOrCreateParentShellItem(item.ParentDirectory);
                if (parentItem != null)
                {
                    TryCreateShellItemFromRelativeName(parentItem, item.RelativeName, out shellItem, out hrCreate);
                }
            }

            // Fallback to full parsing name if relative resolution didn't succeed
            if (shellItem == null)
            {
                if (!TryCreateShellItem(item.FullPath, out shellItem, out hrCreate) || shellItem == null)
                {
                    if (currentLoggingFrequency == LoggingFrequency.DebugLogging)
                    {
                        string msg = Marshal.GetExceptionForHR(hrCreate)?.Message ?? "Unknown COM error";
                        WriteLine($"SHCreateItemFromParsingName failed (0x{hrCreate:X8}) for '{item.FullPath}': {msg}", LoggingFrequency.DebugLogging);
                    }
                    return;
                }
            }

            try
            {
                ExtractSizesForShellItem(shellItem, item.FullPath, sizes, item.IsNetworkPath);
            }
            finally
            {
                if (shellItem != null)
                    Marshal.ReleaseComObject(shellItem);
            }
        }

        /// <summary>
        /// Preloads thumbnails for a given file path and a set of sizes.
        /// </summary>
        public static void PreloadThumbnail(string filePath, uint[] sizes)
        {
            if (string.IsNullOrWhiteSpace(filePath) || sizes == null || sizes.Length == 0)
                return;

            string? resolved = NetworkPathResolver.NormalizeAndResolvePath(filePath) ?? filePath;
            bool isNetwork = NetworkPathResolver.IsNetworkOrMappedPath(resolved);

            if (!TryCreateShellItem(resolved, out IShellItem? shellItem, out int hrCreate) || shellItem == null)
            {
                if (currentLoggingFrequency == LoggingFrequency.DebugLogging)
                {
                    string msg = Marshal.GetExceptionForHR(hrCreate)?.Message ?? "Unknown COM error";
                    WriteLine($"SHCreateItemFromParsingName failed (0x{hrCreate:X8}) for '{resolved}': {msg}", LoggingFrequency.DebugLogging);
                }
                return;
            }

            try
            {
                ExtractSizesForShellItem(shellItem, resolved, sizes, isNetwork);
            }
            finally
            {
                Marshal.ReleaseComObject(shellItem);
            }
        }

        /// <summary>
        /// Preloads thumbnails using a base folder path and a relative file name.
        /// </summary>
        public static void PreloadThumbnail(string basePath, string relativePath, uint[] sizes)
        {
            if (string.IsNullOrWhiteSpace(basePath) || string.IsNullOrWhiteSpace(relativePath) || sizes == null || sizes.Length == 0)
                return;

            bool isNetwork = NetworkPathResolver.IsNetworkOrMappedPath(basePath);
            IShellItem? baseItem = GetOrCreateBaseShellItem(basePath);
            if (baseItem == null)
                return;

            if (!TryCreateShellItemFromRelativeName(baseItem, relativePath, out IShellItem? shellItem, out int hrCreate) || shellItem == null)
            {
                if (currentLoggingFrequency == LoggingFrequency.DebugLogging)
                {
                    string msg = Marshal.GetExceptionForHR(hrCreate)?.Message ?? "Unknown COM error";
                    WriteLine($"SHCreateItemFromRelativeName failed (0x{hrCreate:X8}) for '{relativePath}': {msg}", LoggingFrequency.DebugLogging);
                }
                return;
            }

            try
            {
                ExtractSizesForShellItem(shellItem, relativePath, sizes, isNetwork);
            }
            finally
            {
                Marshal.ReleaseComObject(shellItem);
            }
        }

        private static void ExtractSizesForShellItem(IShellItem shellItem, string displayPath, uint[] sizes, bool isNetworkPath)
        {
            IThumbnailCache? tbCache = GetOrCreateThreadLocalCache();

            foreach (uint size in sizes)
            {
                if (size == 0)
                    continue;

                bool succeeded = false;

                if (tbCache != null)
                {
                    // 1. Fast check if high-quality thumbnail is already in Windows Thumbnail Cache
                    int hrCheck = tbCache.GetThumbnail(
                        shellItem,
                        size,
                        WTS_FLAGS.WTS_INCACHEONLY,
                        IntPtr.Zero,
                        out WTS_CACHEFLAGS outFlags,
                        IntPtr.Zero);

                    if (hrCheck >= 0 &&
                        (outFlags & WTS_CACHEFLAGS.WTS_CACHED) != 0 &&
                        (outFlags & WTS_CACHEFLAGS.WTS_LOWQUALITY) == 0)
                    {
                        Interlocked.Increment(ref CachedHitsCount);
                        continue;
                    }

                    // 2. Try fast in-proc extraction first; if in-proc fails, fall back to WTS_EXTRACT
                    int hrInProc = tbCache.GetThumbnail(
                        shellItem,
                        size,
                        WTS_FLAGS.WTS_EXTRACTINPROC,
                        IntPtr.Zero,
                        out _,
                        IntPtr.Zero);

                    if (hrInProc >= 0)
                    {
                        succeeded = true;
                        Interlocked.Increment(ref ExtractedCount);
                    }
                    else if (hrInProc == E_ACCESSDENIED || hrInProc == RPC_E_DISCONNECTED)
                    {
                        break;
                    }

                    if (!succeeded)
                    {
                        int hrExtract = tbCache.GetThumbnail(
                            shellItem,
                            size,
                            WTS_FLAGS.WTS_EXTRACT,
                            IntPtr.Zero,
                            out _,
                            IntPtr.Zero);

                        if (hrExtract >= 0)
                        {
                            succeeded = true;
                            Interlocked.Increment(ref ExtractedCount);
                        }
                        else if (hrExtract == E_ACCESSDENIED)
                        {
                            break;
                        }
                    }
                }

                // 3. Fallback via IShellItemImageFactory (critical for network shares \\server\share, mapped drives, and out-of-proc handlers)
                if (!succeeded)
                {
                    if (TryExtractViaShellItemImageFactory(shellItem, size, isNetworkPath, out bool wasCacheHit))
                    {
                        if (wasCacheHit)
                            Interlocked.Increment(ref CachedHitsCount);
                        else
                            Interlocked.Increment(ref ExtractedCount);
                    }
                    else if (currentLoggingFrequency == LoggingFrequency.DebugLogging)
                    {
                        WriteLine($"GetThumbnail failed on path '{displayPath}' for size {size}", LoggingFrequency.DebugLogging);
                    }
                }
            }
        }

        private static bool TryExtractViaShellItemImageFactory(IShellItem shellItem, uint size, bool isNetworkPath, out bool wasCacheHit)
        {
            wasCacheHit = false;
            if (shellItem is not IShellItemImageFactory imageFactory)
                return false;

            var requestedSize = new SIZE((int)size, (int)size);

            // Check cache first via SIIGBF_INCACHEONLY | SIIGBF_THUMBNAILONLY
            int hrCache = imageFactory.GetImage(
                requestedSize,
                SIIGBF.SIIGBF_INCACHEONLY | SIIGBF.SIIGBF_THUMBNAILONLY,
                out IntPtr hbmCached);

            if (hbmCached != IntPtr.Zero)
            {
                DeleteObject(hbmCached);
                if (hrCache >= 0)
                {
                    wasCacheHit = true;
                    return true;
                }
            }

            // Extract and populate Explorer / network folder cache
            SIIGBF extractFlags = isNetworkPath
                ? SIIGBF.SIIGBF_RESIZETOFIT
                : SIIGBF.SIIGBF_THUMBNAILONLY;

            int hr = imageFactory.GetImage(requestedSize, extractFlags, out IntPtr hbm);
            if (hbm != IntPtr.Zero)
            {
                DeleteObject(hbm);
                if (hr >= 0)
                    return true;
            }

            if (!isNetworkPath)
            {
                hr = imageFactory.GetImage(requestedSize, SIIGBF.SIIGBF_RESIZETOFIT, out hbm);
                if (hbm != IntPtr.Zero)
                {
                    DeleteObject(hbm);
                    if (hr >= 0)
                        return true;
                }
            }

            return false;
        }

        private static bool TryCreateShellItem(string filePath, out IShellItem? shellItem, out int hr)
        {
            shellItem = null;
            lock (ShellParseLock)
            {
                if (!_shellWarmupDone)
                {
                    _shellWarmupDone = true;
                    string warmupPath = Environment.SystemDirectory;
                    if (!string.IsNullOrWhiteSpace(warmupPath) &&
                        SHCreateItemFromParsingName(warmupPath, IntPtr.Zero, IID_IShellItem, out IShellItem? warmupItem) >= 0 &&
                        warmupItem != null)
                    {
                        try { Marshal.ReleaseComObject(warmupItem); } catch { }
                    }
                }

                hr = SHCreateItemFromParsingName(filePath, IntPtr.Zero, IID_IShellItem, out shellItem);
            }
            return hr >= 0 && shellItem != null;
        }

        private static bool TryCreateShellItemFromRelativeName(IShellItem baseItem, string relativePath, out IShellItem? shellItem, out int hr)
        {
            shellItem = null;
            hr = SHCreateItemFromRelativeName(baseItem, relativePath, IntPtr.Zero, IID_IShellItem, out shellItem);
            return hr >= 0 && shellItem != null;
        }

        private static IShellItem? GetOrCreateParentShellItem(string parentPath)
        {
            var dict = ThreadLocalParentItems.Value;
            if (dict == null)
                return null;

            if (dict.TryGetValue(parentPath, out IShellItem? existing) && existing != null)
                return existing;

            if (dict.Count >= MaxCachedParentDirsPerThread)
            {
                string? oldestKey = null;
                IShellItem? oldestVal = null;
                foreach (var kvp in dict)
                {
                    oldestKey = kvp.Key;
                    oldestVal = kvp.Value;
                    break;
                }

                if (oldestKey != null)
                {
                    dict.Remove(oldestKey);
                    try
                    {
                        if (oldestVal != null && Marshal.IsComObject(oldestVal))
                            Marshal.ReleaseComObject(oldestVal);
                    }
                    catch { }
                }
            }

            if (!TryCreateShellItem(parentPath, out IShellItem? newItem, out int hr) || newItem == null)
            {
                if (currentLoggingFrequency == LoggingFrequency.DebugLogging)
                {
                    string msg = Marshal.GetExceptionForHR(hr)?.Message ?? "Unknown COM error";
                    WriteLine($"SHCreateItemFromParsingName failed for parent folder (0x{hr:X8}) '{parentPath}': {msg}", LoggingFrequency.DebugLogging);
                }
                return null;
            }

            dict[parentPath] = newItem;
            return newItem;
        }

        private static IShellItem? GetOrCreateBaseShellItem(string basePath)
        {
            var (currentPath, currentItem) = ThreadLocalBaseItem.Value;

            if (string.Equals(currentPath, basePath, StringComparison.OrdinalIgnoreCase) && currentItem != null)
                return currentItem;

            if (currentItem != null)
            {
                try { Marshal.ReleaseComObject(currentItem); } catch { }
            }

            if (!TryCreateShellItem(basePath, out IShellItem? newItem, out int hr))
            {
                if (currentLoggingFrequency == LoggingFrequency.DebugLogging)
                {
                    string msg = Marshal.GetExceptionForHR(hr)?.Message ?? "Unknown COM error";
                    WriteLine($"SHCreateItemFromParsingName failed for base path (0x{hr:X8}) '{basePath}': {msg}", LoggingFrequency.DebugLogging);
                }
                ThreadLocalBaseItem.Value = (basePath, null);
                return null;
            }

            ThreadLocalBaseItem.Value = (basePath, newItem);
            return newItem;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHCreateItemFromParsingName(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            [In] IntPtr pbc,
            [In][MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [Out][MarshalAs(UnmanagedType.Interface, IidParameterIndex = 2)] out IShellItem? ppv);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHCreateItemFromRelativeName(
            [In][MarshalAs(UnmanagedType.Interface)] IShellItem psiParent,
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszName,
            [In] IntPtr pbc,
            [In][MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [Out][MarshalAs(UnmanagedType.Interface, IidParameterIndex = 3)] out IShellItem? ppv);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("F676C15D-596A-4ce2-8234-33996F445DB1")]
        private interface IThumbnailCache
        {
            [PreserveSig]
            int GetThumbnail(
                [In] IShellItem pShellItem,
                [In] uint cxyRequestedThumbSize,
                [In] WTS_FLAGS flags,
                [In] IntPtr ppvThumb,
                [Out] out WTS_CACHEFLAGS pOutFlags,
                [In] IntPtr pThumbnailID
            );
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        private interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage(
                [In, MarshalAs(UnmanagedType.Struct)] SIZE size,
                [In] SIIGBF flags,
                [Out] out IntPtr phbm);
        }

        [Flags]
        private enum SIIGBF : int
        {
            SIIGBF_RESIZETOFIT = 0x00000000,
            SIIGBF_BIGGERSIZEOK = 0x00000001,
            SIIGBF_MEMORYONLY = 0x00000002,
            SIIGBF_ICONONLY = 0x00000004,
            SIIGBF_THUMBNAILONLY = 0x00000008,
            SIIGBF_INCACHEONLY = 0x00000010,
            SIIGBF_CROPTOSQUARE = 0x00000020,
            SIIGBF_WIDETHUMBNAILS = 0x00000040,
            SIIGBF_ICONBACKGROUND = 0x00000080,
            SIIGBF_SCALEUP = 0x00000100
        }

        [Flags]
        private enum WTS_FLAGS : uint
        {
            WTS_EXTRACT = 0x00000000,
            WTS_INCACHEONLY = 0x00000001,
            WTS_FASTEXTRACT = 0x00000002,
            WTS_FORCEEXTRACTION = 0x00000004,
            WTS_SLOWRECLAIM = 0x00000008,
            WTS_EXTRACTDONOTCACHE = 0x00000020,
            WTS_SCALETOREQUESTEDSIZE = 0x00000040,
            WTS_SKIPFASTEXTRACT = 0x00000080,
            WTS_EXTRACTINPROC = 0x00000100
        }

        [Flags]
        private enum WTS_CACHEFLAGS : uint
        {
            WTS_DEFAULT = 0x00000000,
            WTS_LOWQUALITY = 0x00000001,
            WTS_CACHED = 0x00000002
        }

        [InlineArray(16)]
        private struct Byte16
        {
            private byte _element0;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WTS_THUMBNAILID
        {
            public Byte16 rgbKey;
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        public interface IShellItem
        {
            void BindToHandler(
                IntPtr pbc,
                [MarshalAs(UnmanagedType.LPStruct)] Guid bhid,
                [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
                out IntPtr ppv);

            void GetParent(out IShellItem ppsi);

            void GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);

            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);

            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        public enum SIGDN : uint
        {
            NORMALDISPLAY = 0,
            PARENTRELATIVEPARSING = 0x80018001,
            PARENTRELATIVEFORADDRESSBAR = 0x8001c001,
            DESKTOPABSOLUTEPARSING = 0x80028000,
            PARENTRELATIVEEDITING = 0x80031001,
            DESKTOPABSOLUTEEDITING = 0x8004c000,
            FILESYSPATH = 0x80058000,
            URL = 0x80068000
        }

        [ComImport]
        [Guid("091162a4-bc96-411f-aae8-c5122cd03363")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface ISharedBitmap
        {
            uint Detach([Out] out IntPtr phbm);
            uint GetFormat([Out] out WTS_ALPHATYPE pat);
            uint GetSharedBitmap([Out] out IntPtr phbm);
            uint GetSize([Out, MarshalAs(UnmanagedType.Struct)] out SIZE pSize);
            uint InitializeBitmap([In] IntPtr hbm, [In] WTS_ALPHATYPE wtsAT);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE
        {
            public int cx;
            public int cy;

            public SIZE(int cx, int cy)
            {
                this.cx = cx;
                this.cy = cy;
            }
        }

        public enum WTS_ALPHATYPE : uint
        {
            WTSAT_UNKNOWN = 0,
            WTSAT_RGB = 1,
            WTSAT_ARGB = 2
        }
    }
}
