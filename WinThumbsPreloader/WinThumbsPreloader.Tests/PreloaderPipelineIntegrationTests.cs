using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace WinThumbsPreloader.Tests
{
    [CollectionDefinition("NonParallelIntegration", DisableParallelization = true)]
    public sealed class NonParallelIntegrationCollection { }

    [Collection("NonParallelIntegration")]
    public sealed class PreloaderPipelineIntegrationTests : IDisposable
    {
        private readonly string _tempDir;

        public PreloaderPipelineIntegrationTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "WTP_PipelineTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            Program.formOpen = true; // Prevent EndInstance from calling Application.Exit() during tests
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, recursive: true);
            }
            catch { }
        }

        private string CreateSampleBitmapFile(string fileName)
        {
            string filePath = Path.Combine(_tempDir, fileName);
            using var bmp = new Bitmap(64, 64);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.CornflowerBlue);
            }
            bmp.Save(filePath, ImageFormat.Bmp);
            return filePath;
        }

        [Fact]
        public void ResetCounters_SetsBothHitAndExtractedCountsToZero()
        {
            // Arrange
            Interlocked.Exchange(ref ThumbnailPreloader.CachedHitsCount, 42);
            Interlocked.Exchange(ref ThumbnailPreloader.ExtractedCount, 99);

            // Act
            ThumbnailPreloader.ResetCounters();

            // Assert
            Assert.Equal(0, Interlocked.Read(ref ThumbnailPreloader.CachedHitsCount));
            Assert.Equal(0, Interlocked.Read(ref ThumbnailPreloader.ExtractedCount));
        }

        [Fact]
        public async Task ThumbnailPreloader_OnStaThread_ExtractsRealImageAndRecreatesCacheAfterCleanup_Regression()
        {
            // Arrange: Regression test verifying that calling CleanupCurrentThreadComObjects()
            // on an STA thread sets ThreadLocalCache.Value = null and a subsequent PreloadThumbnail
            // call on the SAME thread cleanly recreates IThumbnailCache via GetOrCreateThreadLocalCache().
            string img1 = CreateSampleBitmapFile("reuse_test_1.bmp");
            string img2 = CreateSampleBitmapFile("reuse_test_2.bmp");
            ThumbnailPreloader.ResetCounters();

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var staThread = new Thread(() =>
            {
                try
                {
                    var item1 = new ScannedItem(img1, _tempDir, "reuse_test_1.bmp", IsDirectory: false, IsNetworkPath: false);
                    ThumbnailPreloader.PreloadThumbnail(in item1, [96, 256]);

                    // Release COM objects on this STA thread
                    ThumbnailPreloader.CleanupCurrentThreadComObjects();

                    // Immediately reuse the same STA thread to ensure GetOrCreateThreadLocalCache recreates COM instance
                    var item2 = new ScannedItem(img2, _tempDir, "reuse_test_2.bmp", IsDirectory: false, IsNetworkPath: false);
                    ThumbnailPreloader.PreloadThumbnail(in item2, [96, 256]);

                    ThumbnailPreloader.CleanupCurrentThreadComObjects();
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            })
            {
                IsBackground = true
            };
            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();

            // Act
            await tcs.Task;

            // Assert
            long totalProcessed = Interlocked.Read(ref ThumbnailPreloader.CachedHitsCount) +
                                  Interlocked.Read(ref ThumbnailPreloader.ExtractedCount);
            Assert.True(totalProcessed >= 2, $"Expected >= 2 thumbnail operations on STA thread, got {totalProcessed}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ThumbnailsPreloader_StartPreloaderAsync_SilentDirectoryMode_CompletesStreamingPipeline(bool multiThreaded)
        {
            // Arrange: End-to-end integration test for both single-threaded and multi-threaded STA pipelines
            CreateSampleBitmapFile("img_a.bmp");
            CreateSampleBitmapFile("img_b.bmp");
            CreateSampleBitmapFile("img_c.bmp");

            Interlocked.Exchange(ref Program.activeInstances, 1);
            var preloader = new ThumbnailsPreloader(
                path: _tempDir,
                includeNestedDirectories: true,
                silentMode: true,
                multiThreaded: multiThreaded,
                threadCount: 2);

            // Act
            await preloader.StartPreloaderAsync();

            // Assert
            Assert.Equal(ThumbnailsPreloader.ThumbnailsPreloaderState.Done, preloader.state);
            Assert.True(preloader.processedItemsCount >= 3);
            Assert.Equal(0, Volatile.Read(ref Program.activeInstances));
        }

        [Fact]
        public async Task ThumbnailsPreloader_StartPreloaderAsync_SingleFileMode_Regression_RunsOnStaAndCompletes()
        {
            // Arrange: Regression test for single-file preload path (!isDirectory)
            string singleFile = CreateSampleBitmapFile("single_mode.bmp");
            Interlocked.Exchange(ref Program.activeInstances, 1);

            var preloader = new ThumbnailsPreloader(
                path: singleFile,
                includeNestedDirectories: false,
                silentMode: true,
                multiThreaded: false,
                threadCount: 1);

            // Act
            await preloader.StartPreloaderAsync();

            // Assert
            Assert.Equal(ThumbnailsPreloader.ThumbnailsPreloaderState.Done, preloader.state);
            Assert.Equal(0, Volatile.Read(ref Program.activeInstances));
        }

        [Fact]
        public void ThumbnailPreloader_ConcurrentStaWorkers_SimultaneousShellParse_DoesNotRaceOrFault()
        {
            // Arrange: Regression test for 0xC0000005 access violation when 20 STA threads
            // simultaneously enter GetOrCreateParentShellItem -> SHCreateItemFromParsingName.
            const int workerCount = 20;
            string sampleFile = CreateSampleBitmapFile("concurrent_parse.bmp");
            var item = new ScannedItem(sampleFile, _tempDir, "concurrent_parse.bmp", IsDirectory: false, IsNetworkPath: true);
            using var startGate = new ManualResetEventSlim(false);
            var threads = new Thread[workerCount];
            Exception? capturedError = null;

            for (int i = 0; i < workerCount; i++)
            {
                threads[i] = new Thread(() =>
                {
                    try
                    {
                        startGate.Wait();
                        ThumbnailPreloader.PreloadThumbnail(in item, [96, 256]);
                    }
                    catch (Exception ex)
                    {
                        Interlocked.CompareExchange(ref capturedError, ex, null);
                    }
                    finally
                    {
                        ThumbnailPreloader.CleanupCurrentThreadComObjects();
                    }
                })
                {
                    IsBackground = true
                };
                threads[i].SetApartmentState(ApartmentState.STA);
                threads[i].Start();
            }

            // Act: release all 20 STA threads simultaneously
            startGate.Set();
            foreach (var t in threads)
                t.Join();

            // Assert
            Assert.Null(capturedError);
        }

        [Fact]
        public void ProgressDialog_SetProgress_UpdatesValueAndMaximumAtomically()
        {
            using var dialog = new ProgressDialog();
            dialog.SetProgress(825, 5173);

            Assert.Equal(825, dialog.Value);
            Assert.Equal(5173, dialog.Maximum);
        }

        [Fact]
        public void CacheForm_UpdateTimerTick_AfterFormDisposed_DoesNotThrowNullReferenceException()
        {
            var form = new CacheForm();
            var tickMethod = typeof(CacheForm).GetMethod(
                "UpdateTimer_Tick",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(tickMethod);

            form.Dispose();

            var ex = Record.Exception(() => tickMethod!.Invoke(form, [null, EventArgs.Empty]));
            Assert.Null(ex);
        }
    }
}
