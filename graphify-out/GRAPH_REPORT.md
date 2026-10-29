# Graph Report - winthumbspreloader v3  (2026-10-29)

## Corpus Check
- 43 files · ~42,872 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 21 file(s) not represented in the graph (top: .resx 12, (none) 2, .bmp 2)

## Summary
- 1045 nodes · 2427 edges · 62 communities (33 shown, 29 thin omitted)
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 231 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `006ccc93`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Task Scheduler Form & Triggers
- Advanced Schedule Configuration UI
- Shell Thumbnail Cache Extraction
- CLI Options & UNC Resolver
- Application Settings & Properties
- Streaming Directory Scanner & Pipeline
- Progress Dialog Lifecycle & State
- Bulk Preloader & Presets UI
- Log Retention & File Cleanup
- Cache Tray Icon & Events
- Core Namespaces & Test Suites
- About Form & Update Checker
- Advanced Cache Settings UI
- Application Icons & Resources
- Main Settings Form & Extensions
- Cache Backup File Locking
- V3 xUnit Test Suite
- Admin Elevation & Entry Point
- Log Folder Path Validation
- Logger Configuration & Thresholds
- Native IProgressDialog COM Interface
- .NET 10 Project & Dependencies
- License Form Viewer
- Shell Image Factory Flags
- Bulk Preset Serialization Model
- Advanced Settings Form & Tabs
- COM Image & Cache Extraction
- Windows Thumbnail Extraction Flags
- Cache Operation Failure Reasons
- Log Folder Status Enum
- Progress Dialog Behavior Flags
- Shell Display Name Flags
- Log Folder Validation Result
- Shared Bitmap COM Interface
- Backup Folder Status Enum
- Window Long Index Constants
- Backup Folder Validation Result
- IShellItem COM Interface
- Extension List Sorting Modes
- Logging Verbosity Levels
- Application Update States
- Cache Operation Status Enum
- Progress Dialog Timer Actions
- Progress Dialog Run States
- Thumbnail Alpha Channel Types
- Thumbnail Cache Output Flags
- Inno Setup Installer & Docs

## God Nodes (most connected - your core abstractions)
1. `CacheForm` - 121 edges
2. `AdvancedSettingsForm` - 65 edges
3. `ScheduleForm` - 60 edges
4. `AdvancedScheduleForm` - 58 edges
5. `Logger` - 54 edges
6. `ProgressDialog` - 51 edges
7. `BulkPreloaderForm` - 50 edges
8. `Settings` - 49 edges
9. `AdvancedCacheForm` - 46 edges
10. `SettingsForm` - 41 edges

## Surprising Connections (you probably didn't know these)
- `Inno Setup Installer (v3.0.0)` --packages--> `WinThumbsPreloader V3`  [EXTRACTED]
  InnoSetup/installer.iss → README.md
- `BulkPreloaderForm` --defines--> `PresetsFilePath`  [EXTRACTED]
  WinThumbsPreloader/WinThumbsPreloader/Forms/BulkPreloaderForm.Designer.cs → WinThumbsPreloader/WinThumbsPreloader/Forms/BulkPreloaderForm.cs
- `CacheForm` --defines--> `LocalAppDataBackupPath`  [EXTRACTED]
  WinThumbsPreloader/WinThumbsPreloader/Forms/CacheForm.Designer.cs → WinThumbsPreloader/WinThumbsPreloader/Forms/CacheForm.cs
- `CacheForm` --defines--> `PortableBackupPath`  [EXTRACTED]
  WinThumbsPreloader/WinThumbsPreloader/Forms/CacheForm.Designer.cs → WinThumbsPreloader/WinThumbsPreloader/Forms/CacheForm.cs
- `ScheduleForm` --defines--> `Description`  [EXTRACTED]
  WinThumbsPreloader/WinThumbsPreloader/Forms/ScheduleForm.Designer.cs → WinThumbsPreloader/WinThumbsPreloader/Forms/ScheduleForm.cs

## Import Cycles
- None detected.

## Communities (62 total, 29 thin omitted)

### Community 0 - "Task Scheduler Form & Triggers"
Cohesion: 0.07
Nodes (3): ScheduleForm, Description, EffectiveDescription

### Community 2 - "Shell Thumbnail Cache Extraction"
Cohesion: 0.22
Nodes (4): Byte16, SIZE, ThumbnailPreloader, WTS_THUMBNAILID

### Community 3 - "CLI Options & UNC Resolver"
Cohesion: 0.08
Nodes (6): NetworkPathResolver, UNIVERSAL_NAME_INFO, Options, NetworkPathResolverTests, OptionsTests, PathFormattingTests

### Community 4 - "Application Settings & Properties"
Cohesion: 0.04
Nodes (45): Settings, Admin, AutoBackupAfterPreload, AutoBackupInterval, AutoBackupThumbs, AutoDeleteLogsByAge, AutoRestoreInterval, AutoRestoreThumbs (+37 more)

### Community 5 - "Streaming Directory Scanner & Pipeline"
Cohesion: 0.07
Nodes (11): DirectoryScanner, ScannedItem, DirectoryScannerTests, PreloaderPipelineIntegrationTests, ThumbnailsPreloader, ThumbnailsPreloaderState, Canceled, Done (+3 more)

### Community 6 - "Progress Dialog Lifecycle & State"
Cohesion: 0.07
Nodes (22): PBM, PBM_SETMARQUEE, PBS, PBS_MARQUEE, ProgressDialog, AutoClose, CancelButton, CancelMessage (+14 more)

### Community 8 - "Log Retention & File Cleanup"
Cohesion: 0.17
Nodes (5): LogCleanupResult, BytesFreed, DeletedByAge, FailedDeletes, TotalDeleted

### Community 9 - "Cache Tray Icon & Events"
Cohesion: 0.08
Nodes (3): CacheForm, LocalAppDataBackupPath, PortableBackupPath

### Community 10 - "Core Namespaces & Test Suites"
Cohesion: 0.10
Nodes (6): WinThumbsPreloader, WinThumbsPreloader.Properties, WinThumbsPreloader.Tests, WinThumbsPreloader.Forms, BulkPresetTests, NonParallelIntegrationCollection

### Community 13 - "Application Icons & Resources"
Cohesion: 0.10
Nodes (18): Resources, AboutForm_WinThumbsPreloader_BetaVersionAvailable, AboutForm_WinThumbsPreloader_IsUpToDate, AboutForm_WinThumbsPreloader_NewVersionAvailable, AboutForm_WinThumbsPreloader_UpdateCheckFailed, Culture, MainIcon, ResourceManager (+10 more)

### Community 15 - "Cache Backup File Locking"
Cohesion: 0.08
Nodes (17): AcquireResult, Acquired, AlreadyHeldByAnotherProcess, AlreadyHeldByThisProcess, Failed, InvalidPath, PermissionDenied, CacheBackupLock (+9 more)

### Community 21 - "Admin Elevation & Entry Point"
Cohesion: 0.16
Nodes (3): PreloaderApplicationContext, Program, AppOptions

### Community 24 - "Logger Configuration & Thresholds"
Cohesion: 0.20
Nodes (5): Logger, LastError, LocalAppDataLogPath, LogFrequency, PortableLogPath

### Community 28 - ".NET 10 Project & Dependencies"
Cohesion: 0.18
Nodes (10): coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.13.0), TaskScheduler (2.10.1), xunit (2.9.3), xunit.runner.visualstudio (3.0.2), WinThumbsPreloader.Tests, net10.0-windows7.0, Microsoft.NET.Sdk (+2 more)

### Community 30 - "Shell Image Factory Flags"
Cohesion: 0.18
Nodes (11): SIIGBF, SIIGBF_BIGGERSIZEOK, SIIGBF_CROPTOSQUARE, SIIGBF_ICONBACKGROUND, SIIGBF_ICONONLY, SIIGBF_INCACHEONLY, SIIGBF_MEMORYONLY, SIIGBF_RESIZETOFIT (+3 more)

### Community 32 - "Bulk Preset Serialization Model"
Cohesion: 0.20
Nodes (8): BulkPreset, MaximumInstances, Method, MultiThreaded, Name, Paths, Recursive, Threads

### Community 35 - "Windows Thumbnail Extraction Flags"
Cohesion: 0.20
Nodes (10): WTS_FLAGS, WTS_EXTRACT, WTS_EXTRACTDONOTCACHE, WTS_EXTRACTINPROC, WTS_FASTEXTRACT, WTS_FORCEEXTRACTION, WTS_INCACHEONLY, WTS_SCALETOREQUESTEDSIZE (+2 more)

### Community 36 - "Cache Operation Failure Reasons"
Cohesion: 0.22
Nodes (9): CacheOperationFailureReason, AlreadyInProgress, BackupPathNotConfigured, InvalidBackupPath, NoFilesCopied, None, NoSourceFiles, PermissionDenied (+1 more)

### Community 37 - "Log Folder Status Enum"
Cohesion: 0.22
Nodes (9): LogFolderStatus, DefaultFolder, InvalidPath, NotSelected, NotWritable, WillBeCreated, WillBeCreatedAdmin, Writable (+1 more)

### Community 38 - "Progress Dialog Behavior Flags"
Cohesion: 0.22
Nodes (9): PROGDLG, AutoTime, MarqueeProgress, Modal, NoCancel, NoMinimize, NoProgressBar, Normal (+1 more)

### Community 39 - "Shell Display Name Flags"
Cohesion: 0.22
Nodes (9): SIGDN, DESKTOPABSOLUTEEDITING, DESKTOPABSOLUTEPARSING, FILESYSPATH, NORMALDISPLAY, PARENTRELATIVEEDITING, PARENTRELATIVEFORADDRESSBAR, PARENTRELATIVEPARSING (+1 more)

### Community 40 - "Log Folder Validation Result"
Cohesion: 0.25
Nodes (7): LogFolderStatusInfo, CanUseForLogging, IsRunningAsAdmin, Message, NormalizedPath, Status, UsesDefaultPath

### Community 43 - "Backup Folder Status Enum"
Cohesion: 0.25
Nodes (8): BackupFolderStatus, InvalidPath, NotSelected, NotWritable, WillBeCreated, WillBeCreatedAdmin, Writable, WritableAdmin

### Community 45 - "Window Long Index Constants"
Cohesion: 0.25
Nodes (8): GWL, GWL_EXSTYLE, GWL_HINSTANCE, GWL_HWNDPARENT, GWL_ID, GWL_STYLE, GWL_USERDATA, GWL_WNDPROC

### Community 46 - "Backup Folder Validation Result"
Cohesion: 0.29
Nodes (6): BackupFolderStatusInfo, CanUseForBackup, IsRunningAsAdmin, Message, NormalizedPath, Status

### Community 49 - "Extension List Sorting Modes"
Cohesion: 0.33
Nodes (6): SortingMethod, Commas, CommasAndSpaces, Disabled, Spaces, Vertically

### Community 50 - "Logging Verbosity Levels"
Cohesion: 0.33
Nodes (6): LoggingFrequency, AllLogging, DebugLogging, GUILogging, NoLogging, PreloaderLogging

### Community 51 - "Application Update States"
Cohesion: 0.40
Nodes (5): UpdateState, BetaAvailable, Error, UpdateAvailable, Updated

### Community 52 - "Cache Operation Status Enum"
Cohesion: 0.50
Nodes (4): Success, Failure, PartialSuccess, Success

### Community 53 - "Progress Dialog Timer Actions"
Cohesion: 0.50
Nodes (4): PDTIMER, Pause, Reset, Resume

### Community 54 - "Progress Dialog Run States"
Cohesion: 0.50
Nodes (4): ProgressDialogState, Paused, Running, Stopped

### Community 55 - "Thumbnail Alpha Channel Types"
Cohesion: 0.50
Nodes (4): WTS_ALPHATYPE, WTSAT_ARGB, WTSAT_RGB, WTSAT_UNKNOWN

### Community 56 - "Thumbnail Cache Output Flags"
Cohesion: 0.50
Nodes (4): WTS_CACHEFLAGS, WTS_CACHED, WTS_DEFAULT, WTS_LOWQUALITY

## Knowledge Gaps
- **235 isolated node(s):** `net10.0-windows7.0`, `coverlet.collector (6.0.4)`, `Microsoft.NET.Test.Sdk (17.13.0)`, `xunit (2.9.3)`, `xunit.runner.visualstudio (3.0.2)` (+230 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 326 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **29 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `CacheForm` connect `Cache Tray Icon & Events` to `Advanced Schedule Configuration UI`, `Streaming Directory Scanner & Pipeline`, `Core Namespaces & Test Suites`, `About Form & Update Checker`, `Advanced Cache Settings UI`, `Main Settings Form & Extensions`, `Cache Backup File Locking`, `Settings Persistence & Startup`, `Automated Cache Backup & Polling`, `Explorer Cache Copy & Rollback`, `Admin Elevation & Entry Point`, `Form Keyboard Shortcut Handlers`, `Backup Folder Path Validation`, `License Form Viewer`, `Backup Timestamp Metadata`, `Cache Operation Failure Reasons`, `Form Close Button Handlers`, `Backup Folder Status Enum`, `Cache Folder & Explorer Actions`, `Backup Folder Validation Result`, `Cache Operation Status Enum`, `Start Minimized Tray Behavior`?**
  _High betweenness centrality (0.217) - this node is a cross-community bridge._
- **Are the 3 inferred relationships involving `CacheForm` (e.g. with `.CacheButton_Click()` and `.StartMinimized()`) actually correct?**
  _`CacheForm` has 3 INFERRED edges - model-reasoned connections that need verification._
- **What connects `net10.0-windows7.0`, `coverlet.collector (6.0.4)`, `Microsoft.NET.Test.Sdk (17.13.0)` to the rest of the system?**
  _235 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Task Scheduler Form & Triggers` be split into smaller, more focused modules?**
  _Cohesion score 0.06976744186046512 - nodes in this community are weakly interconnected._
- **Why does `ProgressDialog` connect `Progress Dialog Lifecycle & State` to `Streaming Directory Scanner & Pipeline`, `Progress Dialog Behavior Flags`, `Core Namespaces & Test Suites`, `Form Close Button Handlers`, `Window Long Index Constants`, `Win32 Window Handle Interop`, `Progress Dialog Timer Actions`, `Progress Dialog Run States`, `Native IProgressDialog COM Interface`?**
  _High betweenness centrality (0.122) - this node is a cross-community bridge._
- **Should `Advanced Schedule Configuration UI` be split into smaller, more focused modules?**
  _Cohesion score 0.08139534883720931 - nodes in this community are weakly interconnected._
- **Why does `ThumbnailPreloader` connect `Shell Thumbnail Cache Extraction` to `COM Image & Cache Extraction`, `CLI Options & UNC Resolver`, `Windows Thumbnail Extraction Flags`, `Streaming Directory Scanner & Pipeline`, `Progress Dialog Lifecycle & State`, `Shell Display Name Flags`, `Shared Bitmap COM Interface`, `Core Namespaces & Test Suites`, `IShellItem COM Interface`, `Win32 Window Handle Interop`, `Thumbnail Alpha Channel Types`, `Logger Configuration & Thresholds`, `Thumbnail Cache Output Flags`, `Shell Image Factory Flags`?**
  _High betweenness centrality (0.107) - this node is a cross-community bridge._