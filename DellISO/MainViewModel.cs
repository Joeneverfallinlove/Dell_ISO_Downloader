using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace DellISO
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        private readonly HttpClient _client = new();
        private Process _ariaProcess;
        private DateTime _startTime;
        private string _token;
        private LogWindow _logWindow;

        private string _currentDownloadPath;
        private bool _cancelFlag = false;

        // 日志
        [ObservableProperty] private string fullLog = "";
        private StringBuilder _logBuilder = new StringBuilder();

        // 详细日志开关
        [ObservableProperty] private bool isVerboseLogging = false;

        // UI 状态
        [ObservableProperty] private string serviceTag = "";
        [ObservableProperty] private string statusLog = "Ready.";
        [ObservableProperty] private bool isBusy = false;
        [ObservableProperty] private double progressValue = 0;

        // 全选控制
        [ObservableProperty] private bool hasIsoItems = false;
        [ObservableProperty] private bool hasDriverItems = false;
        [ObservableProperty] private bool hasWimItems = false;

        // 监控面板
        [ObservableProperty] private string currentFileName = "---";
        [ObservableProperty] private string downloadSpeed = "0 MB/S";
        [ObservableProperty] private string totalSizeDisplay = "---";
        [ObservableProperty] private string etaDisplay = "--:--:--";
        [ObservableProperty] private string elapsedTimeDisplay = "00:00:00";

        // 设置
        [ObservableProperty] private string isoPath;
        [ObservableProperty] private string driverPath;
        [ObservableProperty] private string wimPath;
        [ObservableProperty] private int selectedThreads = 0;
        public ObservableCollection<string> ThreadOptions { get; } = new();

        public ObservableCollection<DellFileItem> IsoList { get; } = new();
        public ObservableCollection<DellFileItem> DriverList { get; } = new();
        public ObservableCollection<DellFileItem> WimList { get; } = new();

        public event EventHandler LogUpdated;

        public MainViewModel()
        {
            // 自动计算最佳存储路径
            string baseDirectory = GetBestStoragePath();

            IsoPath = Path.Combine(baseDirectory, "Dell_Resource", "ISO");
            DriverPath = Path.Combine(baseDirectory, "Dell_Resource", "Drivers");
            WimPath = Path.Combine(baseDirectory, "Dell_Resource", "WIM_RE");

            ThreadOptions.Add("AUTO");
            int maxCpu = Environment.ProcessorCount;
            int limit = maxCpu > 16 ? 16 : maxCpu;
            for (int i = 1; i <= 16; i++) if (i <= limit) ThreadOptions.Add(i.ToString());

            AppendLog($"Application Started. Storage root: {baseDirectory}");
        }

        // 获取最佳存储路径（修复：去除了重复定义）
        private string GetBestStoragePath()
        {
            try
            {
                var allDrives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                    .ToList();

                // 如果只有一个盘（通常是C盘）
                if (allDrives.Count <= 1)
                {
                    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                }

                // 寻找除C盘以外，剩余容量最大的盘
                var bestDrive = allDrives
                    .Where(d => !d.Name.StartsWith("C:", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(d => d.AvailableFreeSpace)
                    .FirstOrDefault();

                if (bestDrive != null)
                {
                    return bestDrive.Name;
                }

                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Path calculation failed: {ex.Message}");
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            }
        }

        // 清理缓存（修复：添加了此方法，否则 SettingPage 会报错）
        [RelayCommand]
        public async Task CleanCache()
        {
            string rootPath = Path.GetDirectoryName(IsoPath); // 获取 Dell_Resource 目录

            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                AppendLog("Clean Cache: Target folder does not exist.");
                return;
            }

            try
            {
                IsBusy = true; // 锁定界面
                StatusLog = "Cleaning cache...";
                AppendLog($"[Manager] Cleaning all files in: {rootPath}");

                await Task.Run(() =>
                {
                    DirectoryInfo di = new DirectoryInfo(rootPath);
                    foreach (FileInfo file in di.GetFiles()) file.Delete();
                    foreach (DirectoryInfo dir in di.GetDirectories()) dir.Delete(true);
                });

                // 重建目录
                Directory.CreateDirectory(IsoPath);
                Directory.CreateDirectory(DriverPath);
                Directory.CreateDirectory(WimPath);

                StatusLog = "Cache cleared.";
                AppendLog("Clean Cache: Success.");
            }
            catch (Exception ex)
            {
                StatusLog = "Clean Failed.";
                AppendLog($"[Error] Failed to clean cache: {ex.Message}");
            }
            finally
            {
                IsBusy = false; // 解锁界面
            }
        }

        public void Cleanup()
        {
            _cancelFlag = true;
            if (_ariaProcess != null && !_ariaProcess.HasExited)
            {
                try { _ariaProcess.Kill(); } catch { }
            }
            try
            {
                foreach (var proc in Process.GetProcessesByName("aria2c")) proc.Kill();
            }
            catch { }
        }

        [RelayCommand]
        public void OpenLog()
        {
            if (_logWindow == null)
            {
                _logWindow = new LogWindow();
                _logWindow.Closed += (s, e) => _logWindow = null;
                _logWindow.Activate();
            }
            else
            {
                _logWindow.Activate();
            }
        }

        private void AppendLog(string msg)
        {
            if (_logBuilder.Length > 500000) _logBuilder.Clear();
            string time = DateTime.Now.ToString("HH:mm:ss");
            _logBuilder.AppendLine($"[{time}] {msg}");
            _dispatcherQueue.TryEnqueue(() => { FullLog = _logBuilder.ToString(); LogUpdated?.Invoke(this, EventArgs.Empty); });
        }

        [RelayCommand] public void ToggleIso(object isChecked) { bool v = isChecked is bool b && b; foreach (var i in IsoList) i.IsSelected = v; }
        [RelayCommand] public void ToggleDriver(object isChecked) { bool v = isChecked is bool b && b; foreach (var i in DriverList) i.IsSelected = v; }
        [RelayCommand] public void ToggleWim(object isChecked) { bool v = isChecked is bool b && b; foreach (var i in WimList) i.IsSelected = v; }

        [RelayCommand]
        public async Task FetchData()
        {
            if (string.IsNullOrWhiteSpace(ServiceTag)) { AppendLog("Error: Tag Empty"); return; }

            IsBusy = true;

            HasIsoItems = HasDriverItems = HasWimItems = false;
            IsoList.Clear(); DriverList.Clear(); WimList.Clear();
            _logBuilder.Clear();

            AppendLog($"Fetching for Tag: {ServiceTag}");

            try
            {
                AppendLog("Step 1: Getting Token...");
                var request = new HttpRequestMessage(HttpMethod.Post, "https://apigtwb2c.us.dell.com/auth/oauth/v2/token");
                request.Headers.Add("User-Agent", "RestSharp/106.12.0.0");
                request.Headers.Add("Authorization", "Basic bDcyMGQzMzQ3ZDIwYTg0ZmExYmIzNmMzZWYwMWU3MTliODo3M2ZkZTNmNTU1ODU0YjBiOGZmODcwOTBlMWY4YjU1Yg==");
                request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { { "grant_type", "client_credentials" }, { "scope", "read" } });

                var res = await _client.SendAsync(request);
                var jsonToken = await res.Content.ReadAsStringAsync();

                if (!res.IsSuccessStatusCode)
                {
                    AppendLog($"Token Failed: {jsonToken}");
                    IsBusy = false;
                    return;
                }

                var tokenObj = JsonSerializer.Deserialize<OAuthResponse>(jsonToken);
                _token = tokenObj.AccessToken;
                AppendLog("Token Acquired.");

                StatusLog = "Fetching Lists...";
                await Task.WhenAll(
                    GetIsoList($"https://apigtwb2c.us.dell.com/v1/osri/images/{ServiceTag}?Rev=production"),
                    GetComplexList($"https://apigtwb2c.us.dell.com/v1/osri/osimageparts/{ServiceTag}?oscode=WT64A&language=en&Rev=production", DriverList, "Driver"),
                    GetComplexList($"https://apigtwb2c.us.dell.com/v1/osri/sosimages/{ServiceTag}?Rev=production", WimList, "WIM")
                );

                HasIsoItems = IsoList.Count > 0;
                HasDriverItems = DriverList.Count > 0;
                HasWimItems = WimList.Count > 0;
                StatusLog = "Fetch Complete.";
                AppendLog("Fetch Done.");
            }
            catch (Exception ex) { StatusLog = "Error."; AppendLog($"CRITICAL: {ex.Message}"); }

            IsBusy = false;
        }

        private async Task GetIsoList(string url)
        {
            AppendLog($"[ISO] Req: {url}");
            try
            {
                var json = await CallApi(url);
                if (json == null) return;
                var data = JsonSerializer.Deserialize<DellIsoResponse>(json);
                if (data?.Images == null) { AppendLog("[ISO] No images."); return; }
                _dispatcherQueue.TryEnqueue(() => {
                    foreach (var item in data.Images) IsoList.Add(CreateItem(item.Title, item.DellVersion, item.ReleaseDate, item.Size, item.Url, "ISO"));
                });
                AppendLog($"[ISO] Added {data.Images.Count}");
            }
            catch (Exception ex) { AppendLog($"[ISO] Err: {ex.Message}"); }
        }

        private async Task GetComplexList(string url, ObservableCollection<DellFileItem> list, string cat)
        {
            AppendLog($"[{cat}] Req: {url}");
            try
            {
                var json = await CallApi(url);
                if (json == null) return;
                var data = JsonSerializer.Deserialize<DellComplexResponse>(json);
                var items = data.Swbs ?? data.Images;
                if (items == null) return;
                _dispatcherQueue.TryEnqueue(() => {
                    foreach (var item in items)
                    {
                        if (item.Files != null && item.Files.Count > 0)
                            list.Add(CreateItem(item.Title, item.VendorVersion, item.ReleaseDate, item.Files[0].Size, item.Files[0].Url, cat));
                    }
                });
                AppendLog($"[{cat}] Added {list.Count}");
            }
            catch (Exception ex) { AppendLog($"[{cat}] Err: {ex.Message}"); }
        }

        private DellFileItem CreateItem(string t, string v, string d, string s, string u, string c)
        {
            string safeTitle = string.Join("_", t.Split(Path.GetInvalidFileNameChars()));
            return new DellFileItem { Title = safeTitle, Version = v, ReleaseDate = FormatDate(d), OriginalSize = s, DownloadUrl = u, Category = c };
        }

        private async Task<string> CallApi(string url)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("User-Agent", "RestSharp/106.12.0.0");
            req.Headers.Add("Authorization", $"Bearer {_token}");
            req.Headers.Add("Accept", "application/json");
            var res = await _client.SendAsync(req);
            string content = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode) { AppendLog($"API Fail {res.StatusCode}: {content}"); return null; }
            return content;
        }

        private string FormatDate(string raw) => DateTime.TryParse(raw, out DateTime d) ? d.ToString("yyyy-MM-dd") : raw;

        [RelayCommand]
        public async Task StartDownload()
        {
            var queue = IsoList.Where(x => x.IsSelected).Concat(DriverList.Where(x => x.IsSelected)).Concat(WimList.Where(x => x.IsSelected)).ToList();
            if (queue.Count == 0) { StatusLog = "No selection."; AppendLog("No files selected."); return; }

            string ariaPath = await ExtractAria();

            IsBusy = true;

            _cancelFlag = false;
            _startTime = DateTime.Now;
            int threads = SelectedThreads == 0 ? Math.Min(Environment.ProcessorCount, 16) : int.Parse(ThreadOptions[SelectedThreads]);

            AppendLog($"--- Batch Started: {queue.Count} files, Threads: {threads} ---");

            foreach (var item in queue)
            {
                if (_cancelFlag) break;

                CurrentFileName = item.Title;
                TotalSizeDisplay = item.FormattedSize;
                ProgressValue = 0;

                string saveDir = "";
                if (item.Category == "ISO") saveDir = IsoPath;
                else if (item.Category == "Driver") saveDir = DriverPath;
                else saveDir = WimPath;

                Directory.CreateDirectory(saveDir);
                string safeName = item.Title;
                string extension = Path.GetExtension(new Uri(item.DownloadUrl).AbsolutePath);
                if (string.IsNullOrEmpty(extension)) extension = ".exe";
                if (!safeName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) safeName += extension;

                _currentDownloadPath = Path.Combine(saveDir, safeName);
                string cmdArgs = $"-x{threads} -s{threads} -d \"{saveDir}\" -o \"{safeName}\" \"{item.DownloadUrl}\" --summary-interval=1";

                AppendLog($"[Manager] Downloading: {safeName}");

                var psi = new ProcessStartInfo
                {
                    FileName = ariaPath,
                    Arguments = cmdArgs,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                _ariaProcess = new Process { StartInfo = psi };
                _ariaProcess.OutputDataReceived += (s, e) => AriaOutputHandler(e.Data, false);
                _ariaProcess.ErrorDataReceived += (s, e) => AriaOutputHandler(e.Data, true);

                _ariaProcess.Start();
                _ariaProcess.BeginOutputReadLine();
                _ariaProcess.BeginErrorReadLine();

                await _ariaProcess.WaitForExitAsync();

                if (_cancelFlag) { AppendLog("[Manager] Cancelled."); break; }
                if (_ariaProcess.ExitCode == 0) AppendLog($"[Manager] Finished: {safeName}");
                else AppendLog($"[Manager] Failed: {safeName} (Code: {_ariaProcess.ExitCode})");
            }

            IsBusy = false;

            StatusLog = _cancelFlag ? "Cancelled." : "All Done.";
            ResetMonitor();
            AppendLog("--- Batch Finished ---");
        }

        [RelayCommand]
        public void CancelDownload()
        {
            Cleanup(); // 杀进程

            if (!string.IsNullOrEmpty(_currentDownloadPath))
            {
                try
                {
                    if (File.Exists(_currentDownloadPath + ".aria2")) File.Delete(_currentDownloadPath + ".aria2");
                    if (File.Exists(_currentDownloadPath)) File.Delete(_currentDownloadPath);
                    AppendLog($"[Cancel] Cleaned: {_currentDownloadPath}");
                }
                catch (Exception ex) { AppendLog($"[Cancel] Clean Error: {ex.Message}"); }
            }

            IsBusy = false;

            StatusLog = "Cancelled & Cleaned.";
            ResetMonitor();
        }

        private void AriaOutputHandler(string line, bool isError)
        {
            if (string.IsNullOrEmpty(line)) return;
            if (isError) { AppendLog($"[Aria2c Error] {line}"); return; }

            _dispatcherQueue.TryEnqueue(() => {
                ElapsedTimeDisplay = (DateTime.Now - _startTime).ToString(@"hh\:mm\:ss");
                var match = Regex.Match(line, @"\((.*?)%\).*?DL:(.*?) .*?ETA:(.*?)]");
                if (match.Success)
                {
                    double.TryParse(match.Groups[1].Value, out double pct);
                    ProgressValue = pct;
                    DownloadSpeed = match.Groups[2].Value + "/S";
                    EtaDisplay = match.Groups[3].Value;
                }
            });

            if (IsVerboseLogging)
            {
                if (!line.Contains("%") && !line.Contains("ETA:")) AppendLog($"[Aria2c] {line}");
            }
        }

        private void ResetMonitor() { DownloadSpeed = "0 MB/S"; EtaDisplay = "--:--:--"; ProgressValue = 0; }

        private async Task<string> ExtractAria()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "aria2c.exe");
            if (!File.Exists(tempPath))
            {
                AppendLog("Extracting embedded aria2c...");
                using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("DellISO.Assets.aria2c.exe");
                using var fs = File.Create(tempPath); await s.CopyToAsync(fs);
            }
            return tempPath;
        }

        public async Task PickFolder(string type)
        {
            var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.Downloads };
            picker.FileTypeFilter.Add("*");
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle((Application.Current as App)?.MainWindowObj);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);
            var f = await picker.PickSingleFolderAsync();
            if (f != null)
            {
                if (type == "ISO") IsoPath = f.Path; else if (type == "Driver") DriverPath = f.Path; else WimPath = f.Path;
                AppendLog($"Path Change: {type} -> {f.Path}");
            }
        }
        [RelayCommand] public async Task PickIso() => await PickFolder("ISO");
        [RelayCommand] public async Task PickDriver() => await PickFolder("Driver");
        [RelayCommand] public async Task PickWim() => await PickFolder("WIM");


    }
}