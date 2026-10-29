using System;
using System.IO;
using Xunit;

namespace WinThumbsPreloader.Tests
{
    [Collection("NonParallelIntegration")]
    public sealed class CacheBackupAndLockTests : IDisposable
    {
        private readonly string _tempDir;

        public CacheBackupAndLockTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "WTP_BackupTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            CacheBackupLock.Release();
        }

        public void Dispose()
        {
            CacheBackupLock.Release();
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, recursive: true);
            }
            catch { }
        }

        [Fact]
        public void CacheBackupLock_TryAcquire_WhenNotHeld_AcquiresAndPreventsReacquisitionUntilReleased()
        {
            // Act 1: First acquisition succeeds
            var firstResult = CacheBackupLock.TryAcquire(_tempDir, out Exception firstEx);

            // Assert 1
            Assert.Equal(CacheBackupLock.AcquireResult.Acquired, firstResult);
            Assert.Null(firstEx);

            // Act 2: Second acquisition in same process returns AlreadyHeldByThisProcess
            var secondResult = CacheBackupLock.TryAcquire(_tempDir, out _);
            Assert.Equal(CacheBackupLock.AcquireResult.AlreadyHeldByThisProcess, secondResult);

            // Act 3: Release and re-acquire succeeds
            CacheBackupLock.Release();
            var thirdResult = CacheBackupLock.TryAcquire(_tempDir, out _);
            Assert.Equal(CacheBackupLock.AcquireResult.Acquired, thirdResult);
        }

        [Fact]
        public void CacheBackupLock_TryAcquire_WithEmptyPath_ReturnsInvalidPath()
        {
            // Act
            var result = CacheBackupLock.TryAcquire("", out Exception ex);

            // Assert
            Assert.Equal(CacheBackupLock.AcquireResult.InvalidPath, result);
            Assert.NotNull(ex);
        }

        [Fact]
        public void GetBackupFolderStatusInfo_WithExplorerThumbcacheFolder_ReturnsInvalidPath()
        {
            // Arrange: Using the actual Explorer cache directory as a backup target must be rejected
            string explorerCacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Microsoft\Windows\Explorer");

            // Act
            var status = CacheForm.GetBackupFolderStatusInfo(explorerCacheDir);

            // Assert
            Assert.Equal(CacheForm.BackupFolderStatus.InvalidPath, status.Status);
            Assert.False(status.CanUseForBackup);
        }

        [Fact]
        public void GetBackupFolderStatusInfo_WithWritableTempDirectory_ReturnsWritable()
        {
            // Act
            var status = CacheForm.GetBackupFolderStatusInfo(_tempDir);

            // Assert
            Assert.True(status.CanUseForBackup);
            Assert.Equal(Path.GetFullPath(_tempDir), status.NormalizedPath);
        }

        [Fact]
        public void GetLastBackupDateTimeForFolder_WhenInfoFileAndDbFileExist_ReturnsSavedTimestamp()
        {
            // Arrange
            File.WriteAllBytes(Path.Combine(_tempDir, "thumbcache_256.db"), [1, 2, 3, 4]);
            var expectedTime = new DateTimeOffset(2026, 10, 29, 12, 30, 0, TimeSpan.Zero);
            File.WriteAllText(
                Path.Combine(_tempDir, ".WinThumbsPreloader_BackupInfo.txt"),
                expectedTime.ToString("O"));

            // Act
            DateTimeOffset? actual = CacheForm.GetLastBackupDateTimeForFolder(_tempDir);

            // Assert
            Assert.NotNull(actual);
            Assert.Equal(expectedTime.ToLocalTime(), actual!.Value);
        }

        [Fact]
        public void GetLastBackupDateTimeForFolder_WhenOnlyDbFilesExist_InfersTimestampFromLastWriteTime()
        {
            // Arrange
            string dbFile = Path.Combine(_tempDir, "thumbcache_96.db");
            File.WriteAllBytes(dbFile, [1, 2, 3, 4]);
            DateTime expectedUtc = new DateTime(2026, 5, 15, 8, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(dbFile, expectedUtc);

            // Act
            DateTimeOffset? actual = CacheForm.GetLastBackupDateTimeForFolder(_tempDir);

            // Assert
            Assert.NotNull(actual);
            Assert.Equal(new DateTimeOffset(expectedUtc, TimeSpan.Zero).ToLocalTime(), actual!.Value);
        }
    }
}
