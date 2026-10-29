using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WinThumbsPreloader.Properties;
using static WinThumbsPreloader.Logger;

#nullable enable

namespace WinThumbsPreloader
{
    public readonly record struct ScannedItem(
        string FullPath,
        string? ParentDirectory,
        string? RelativeName,
        bool IsDirectory,
        bool IsNetworkPath
    );

    [SupportedOSPlatform("windows")]
    sealed class DirectoryScanner
    {
        public volatile bool cancelled = false;
        public int totalItemsCount = 0;
        public volatile bool scanCompleted = false;

        private readonly List<ScannedItem> scannedList = new List<ScannedItem>();
        private readonly bool includeNestedDirectories;
        private readonly bool multiThreaded;
        private readonly string path;
        private readonly bool isNetworkPath;
        private readonly bool preloadAllFolders;
        private readonly bool preloadFolderIcons;
        private readonly int threadCount;
        private readonly HashSet<string> thumbnailExtensions;

        public DirectoryScanner(string path, bool includeNestedDirectories, bool multiThreaded, int threadCount)
        {
            WriteLine("Initializing Directory Scanner - DirectoryScanner(string, bool, bool, int)", LoggingFrequency.PreloaderLogging);
            thumbnailExtensions = LoadExtensions();
            this.path = NetworkPathResolver.NormalizeAndResolvePath(path) ?? path;
            this.isNetworkPath = NetworkPathResolver.IsNetworkOrMappedPath(this.path);
            this.includeNestedDirectories = includeNestedDirectories;
            this.multiThreaded = multiThreaded;
            this.threadCount = Math.Max(1, threadCount);
            this.preloadFolderIcons = Settings.Default.PreloadFolderIcons;
            this.preloadAllFolders = Settings.Default.PreloadAllFolders;
            WriteLine($"Resolved Scanner Path: {this.path} (Network/Mapped: {this.isNetworkPath})", LoggingFrequency.PreloaderLogging);
            WriteLine("IncludeNestedDirectories: " + includeNestedDirectories, LoggingFrequency.PreloaderLogging);
            WriteLine("preloadFolderIcons: " + preloadFolderIcons, LoggingFrequency.PreloaderLogging);
            WriteLine("preloadAllFolders: " + preloadAllFolders, LoggingFrequency.PreloaderLogging);
        }

        private HashSet<string> LoadExtensions()
        {
            try
            {
                var exts = Settings.Default.ExtensionsText
                    .Split([',', ' ', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(ext => ext.Trim())
                    .Where(ext => ext.Length > 0)
                    .Select(ext =>
                    {
                        ext = ext.TrimStart('.');
                        return "." + ext.ToLowerInvariant();
                    })
                    .Distinct();

                return new HashSet<string>(exts, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                WriteLine("Failed to load extensions, using defaults: " + ex.Message, LoggingFrequency.PreloaderLogging);

                return new HashSet<string>(
                [
                    ".avi",".avif",".bmp",".gif",".heic",".heif",".jpg",".jpeg",
                    ".mkv",".mov",".mp4",".png",".svg",".tif",".tiff",".webp"
                ], StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Streams discovered items directly into a bounded/unbounded ChannelWriter so consumers can start
        /// extracting thumbnails immediately while directory enumeration is still running.
        /// </summary>
        public void ScanToChannel(ChannelWriter<ScannedItem> writer)
        {
            try
            {
                if (!includeNestedDirectories)
                {
                    ScanFirstLevelToChannel(writer);
                }
                else if (multiThreaded)
                {
                    ScanNestedParallelToChannel(writer);
                }
                else
                {
                    ScanNestedSequentialToChannel(writer);
                }
            }
            finally
            {
                scanCompleted = true;
                writer.TryComplete();
            }
        }

        public string[] GetItems()
        {
            ScannedItem[] items = GetScannedItems();
            string[] result = new string[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                result[i] = items[i].FullPath;
            }
            return result;
        }

        public ScannedItem[] GetScannedItems()
        {
            var channel = Channel.CreateUnbounded<ScannedItem>(new UnboundedChannelOptions
            {
                SingleWriter = !multiThreaded || !includeNestedDirectories,
                SingleReader = true
            });

            ScanToChannel(channel.Writer);

            scannedList.Clear();
            while (channel.Reader.TryRead(out ScannedItem item))
            {
                scannedList.Add(item);
            }

            if (multiThreaded && includeNestedDirectories)
            {
                scannedList.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.FullPath, b.FullPath));
            }

            ScannedItem[] result = scannedList.ToArray();
            scannedList.Clear();
            scannedList.Capacity = 0;
            return result;
        }

        private bool ShouldIncludeFile(FileSystemInfo file)
        {
            string ext = file.Extension;

            return !string.IsNullOrEmpty(ext) &&
                   (thumbnailExtensions.Count == 0 || thumbnailExtensions.Contains(ext));
        }

        private void ScanFirstLevelToChannel(ChannelWriter<ScannedItem> writer)
        {
            WriteLine("Scanning items for only first level", LoggingFrequency.PreloaderLogging);

            var enumOptions = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                AttributesToSkip = 0,
                RecurseSubdirectories = false
            };

            try
            {
                var rootInfo = new DirectoryInfo(path);
                string rootFullName = rootInfo.FullName;

                foreach (FileSystemInfo entry in rootInfo.EnumerateFileSystemInfos("*", enumOptions))
                {
                    if (cancelled)
                        break;

                    bool isDirectory = (entry.Attributes & FileAttributes.Directory) != 0;

                    if (isDirectory)
                    {
                        if (!preloadFolderIcons)
                            continue;

                        var dirInfo = (DirectoryInfo)entry;

                        if (preloadAllFolders)
                        {
                            writer.TryWrite(new ScannedItem(dirInfo.FullName, rootFullName, dirInfo.Name, true, isNetworkPath));
                            Interlocked.Increment(ref totalItemsCount);
                            continue;
                        }

                        try
                        {
                            foreach (FileSystemInfo subFile in dirInfo.EnumerateFiles("*", enumOptions))
                            {
                                if (cancelled)
                                    break;

                                if (ShouldIncludeFile(subFile))
                                {
                                    writer.TryWrite(new ScannedItem(dirInfo.FullName, rootFullName, dirInfo.Name, true, isNetworkPath));
                                    Interlocked.Increment(ref totalItemsCount);
                                    break;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            WriteLine("Exception thrown while scanning subdirectory: " + e.Message, LoggingFrequency.DebugLogging);
                        }
                    }
                    else if (ShouldIncludeFile(entry))
                    {
                        writer.TryWrite(new ScannedItem(entry.FullName, rootFullName, entry.Name, false, isNetworkPath));
                        Interlocked.Increment(ref totalItemsCount);
                    }
                }
            }
            catch (Exception e)
            {
                WriteLine("Exception thrown while scanning directory: " + e.Message, LoggingFrequency.DebugLogging);
            }
        }

        private void ScanNestedSequentialToChannel(ChannelWriter<ScannedItem> writer)
        {
            WriteLine("Scanning items for nested directories sequentially", LoggingFrequency.PreloaderLogging);

            var enumOptions = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0 };
            var queue = new Queue<(string FullPath, string? ParentPath, string? FolderName)>();
            queue.Enqueue((path, Path.GetDirectoryName(path), Path.GetFileName(path)));

            while (queue.Count > 0 && !cancelled)
            {
                var (currentPath, parentPath, folderName) = queue.Dequeue();
                bool directoryContainsThumbnail = false;

                try
                {
                    var dirInfo = new DirectoryInfo(currentPath);
                    string dirFullName = dirInfo.FullName;

                    foreach (FileSystemInfo entry in dirInfo.EnumerateFileSystemInfos("*", enumOptions))
                    {
                        if (cancelled) break;

                        if ((entry.Attributes & FileAttributes.Directory) != 0)
                        {
                            queue.Enqueue((entry.FullName, dirFullName, entry.Name));
                        }
                        else if (ShouldIncludeFile(entry))
                        {
                            writer.TryWrite(new ScannedItem(entry.FullName, dirFullName, entry.Name, false, isNetworkPath));
                            Interlocked.Increment(ref totalItemsCount);
                            directoryContainsThumbnail = true;
                        }
                    }

                    if (!cancelled && preloadFolderIcons && (directoryContainsThumbnail || preloadAllFolders))
                    {
                        writer.TryWrite(new ScannedItem(currentPath, parentPath, folderName, true, isNetworkPath));
                        Interlocked.Increment(ref totalItemsCount);
                    }
                }
                catch (Exception e)
                {
                    WriteLine("Exception thrown while scanning directory: " + e.Message, LoggingFrequency.DebugLogging);
                }
            }
        }

        private void ScanNestedParallelToChannel(ChannelWriter<ScannedItem> writer)
        {
            // Cap SMB directory enumeration concurrency to avoid overwhelming network shares while keeping local NVMe at full speed
            int scannerDegree = isNetworkPath ? Math.Clamp(threadCount, 2, 8) : threadCount;
            WriteLine($"Scanning items for nested directories in parallel (degree={scannerDegree})", LoggingFrequency.PreloaderLogging);

            var enumOptions = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0 };
            using var directoriesToProcess = new BlockingCollection<(string FullPath, string? ParentPath, string? FolderName)>
            {
                (path, Path.GetDirectoryName(path), Path.GetFileName(path))
            };
            int pendingWork = 1;

            void AbortRemainingQueue()
            {
                try
                {
                    directoriesToProcess.CompleteAdding();
                    while (directoriesToProcess.TryTake(out _))
                    {
                        Interlocked.Decrement(ref pendingWork);
                    }
                }
                catch { }
            }

            Parallel.ForEach(
                Partitioner.Create(directoriesToProcess.GetConsumingEnumerable(), EnumerablePartitionerOptions.NoBuffering),
                new ParallelOptions { MaxDegreeOfParallelism = scannerDegree },
                (currentDir, loopState) =>
                {
                    try
                    {
                        if (cancelled || loopState.ShouldExitCurrentIteration)
                        {
                            AbortRemainingQueue();
                            loopState.Stop();
                            return;
                        }

                        bool directoryContainsThumbnail = false;

                        try
                        {
                            var dirInfo = new DirectoryInfo(currentDir.FullPath);
                            string dirFullName = dirInfo.FullName;

                            foreach (FileSystemInfo entry in dirInfo.EnumerateFileSystemInfos("*", enumOptions))
                            {
                                if (cancelled || loopState.ShouldExitCurrentIteration)
                                {
                                    AbortRemainingQueue();
                                    loopState.Stop();
                                    return;
                                }

                                if ((entry.Attributes & FileAttributes.Directory) != 0)
                                {
                                    Interlocked.Increment(ref pendingWork);
                                    try
                                    {
                                        directoriesToProcess.Add((entry.FullName, dirFullName, entry.Name));
                                    }
                                    catch (InvalidOperationException)
                                    {
                                        Interlocked.Decrement(ref pendingWork);
                                    }
                                }
                                else if (ShouldIncludeFile(entry))
                                {
                                    writer.TryWrite(new ScannedItem(entry.FullName, dirFullName, entry.Name, false, isNetworkPath));
                                    Interlocked.Increment(ref totalItemsCount);
                                    directoryContainsThumbnail = true;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            WriteLine("Exception thrown while scanning directory: " + e.Message, LoggingFrequency.DebugLogging);
                        }

                        if (!cancelled && preloadFolderIcons && (directoryContainsThumbnail || preloadAllFolders))
                        {
                            writer.TryWrite(new ScannedItem(currentDir.FullPath, currentDir.ParentPath, currentDir.FolderName, true, isNetworkPath));
                            Interlocked.Increment(ref totalItemsCount);
                        }
                    }
                    finally
                    {
                        if (Interlocked.Decrement(ref pendingWork) == 0)
                        {
                            try
                            {
                                directoriesToProcess.CompleteAdding();
                            }
                            catch { }
                        }
                    }
                });
        }
    }
}
