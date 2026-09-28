using Core.Librarys;
using Core.Models;
using Core.Models.Config;
using Core.Servicers.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Servicers.Instances
{
    /// <summary>
    /// Loads the bundled catalog and synchronizes a validated catalog from GitHub.
    /// </summary>
    public sealed class CategoryCatalogService : ICategoryCatalogService
    {
        private const string RemoteCatalogUrl = "https://raw.githubusercontent.com/ethandong16/Tai.WinUI/master/default-categories.json";
        private const int MaxCatalogSize = 1024 * 1024;
        private const int MaxCategoryCount = 100;
        private const int MaxProcessesPerCategory = 500;
        private const int MaxAutomaticIntervalHours = 24 * 30;

        private readonly ICategorys _categoryService;
        private readonly IAppConfig _appConfig;
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _fetchLock = new SemaphoreSlim(1, 1);
        private readonly string _cachePath;
        private readonly object _initializationLock = new object();
        private readonly object _automaticUpdateLock = new object();
        private CancellationTokenSource _automaticUpdateCts;
        private Task _automaticUpdateTask;
        private volatile bool _initialized;
        private volatile bool _paused;

        public CategoryCatalogService(ICategorys categoryService, IAppConfig appConfig)
        {
            _categoryService = categoryService;
            _appConfig = appConfig;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(12)
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Tai-WinUI-CategorySync/1.0");
            _cachePath = Path.Combine(FileHelper.GetRootDirectory(), "Data", "default-categories.json");
            _appConfig.ConfigChanged += AppConfig_ConfigChanged;
        }

        public event EventHandler CatalogUpdated;

        public void Initialize()
        {
            lock (_initializationLock)
            {
                if (_initialized) return;
                if (_appConfig.GetConfig() == null)
                    throw new InvalidOperationException("应用配置尚未加载。");

                var definitions = TryReadCatalog(_cachePath)
                    ?? TryReadCatalog(Path.Combine(AppContext.BaseDirectory, "default-categories.json"))
                    ?? ReadEmbeddedCatalog();

                lock (DefaultCategoryCatalog.SyncRoot)
                {
                    var general = _appConfig.GetConfig().General;
                    var previousIds = general?.DefaultCategoryIds;
                    var previousInitialized = general?.DefaultCategoriesInitialized ?? false;
                    if (!_appConfig.UpdateAndSave(config => EnsureCategories(definitions, config)))
                    {
                        general.DefaultCategoryIds = previousIds;
                        general.DefaultCategoriesInitialized = previousInitialized;
                        throw new IOException("保存默认分类 ID 映射失败。");
                    }
                    DefaultCategoryCatalog.SetCategories(definitions);
                }
                _initialized = true;
            }
        }

        public async Task<CategoryCatalogUpdateResult> FetchLatestAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!_initialized)
            {
                Initialize();
            }

            await _fetchLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_paused) return Failure("分类获取已暂停。");
                string json;
                try
                {
                    json = await DownloadCatalogAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return Failure("获取分类已取消。");
                }
                catch (Exception exception)
                {
                    Logger.Error("获取 GitHub 分类失败：" + exception);
                    return Failure("无法连接 GitHub，已保留当前分类。");
                }

                if (!TryParseCatalog(json, out var definitions))
                {
                    return Failure("GitHub 返回的分类配置无效，已保留当前分类。");
                }
                if (cancellationToken.IsCancellationRequested || _paused)
                    return Failure("获取分类已取消。");

                var changed = !CatalogEquals(DefaultCategoryCatalog.Categories, definitions);
                try
                {
                    var categoryChanged = false;
                    var timestampSaved = false;
                    lock (DefaultCategoryCatalog.SyncRoot)
                    {
                        var general = _appConfig.GetConfig().General;
                        var previousIds = general?.DefaultCategoryIds;
                        var previousInitialized = general?.DefaultCategoriesInitialized ?? false;
                        var saved = _appConfig.UpdateAndSave(config =>
                        {
                            categoryChanged = EnsureCategories(definitions, config);
                            return true;
                        });
                        if (!saved)
                        {
                            general.DefaultCategoryIds = previousIds;
                            general.DefaultCategoriesInitialized = previousInitialized;
                            return Failure("保存分类设置失败，已保留当前分类目录。");
                        }
                        try
                        {
                            WriteCache(json);
                        }
                        catch (Exception exception)
                        {
                            Logger.Error("保存 GitHub 分类缓存失败：" + exception);
                            return Failure("分类映射已保存，但缓存写入失败，目录规则未切换；请重试。");
                        }
                        var previousUpdate = general.LastCategoryCatalogUpdateUtc;
                        timestampSaved = _appConfig.UpdateAndSave(config =>
                        {
                            config.General.LastCategoryCatalogUpdateUtc = DateTime.UtcNow;
                            return true;
                        });
                        if (!timestampSaved) general.LastCategoryCatalogUpdateUtc = previousUpdate;
                        DefaultCategoryCatalog.SetCategories(definitions);
                    }
                    var updatedAt = _appConfig.GetConfig().General.LastCategoryCatalogUpdateUtc;
                    var skippedDeletedCount = definitions.Count(definition =>
                        _appConfig.GetConfig().General.DefaultCategoryIds.TryGetValue(definition.Key, out var id) &&
                        _categoryService.GetCategory(id) == null);
                    try { CatalogUpdated?.Invoke(this, EventArgs.Empty); }
                    catch (Exception exception) { Logger.Error("重新分类失败：" + exception); }

                    return new CategoryCatalogUpdateResult
                    {
                        Succeeded = true,
                        Changed = changed || categoryChanged,
                        CategoryCount = definitions.Count,
                        UpdatedAtUtc = updatedAt,
                        Message = (changed || categoryChanged
                            ? $"已获取包含 {definitions.Count} 个分类的目录。"
                            : "分类目录已是最新。") +
                            (!timestampSaved ? "更新时间未能保存，稍后会重试。" : string.Empty) +
                            (skippedDeletedCount > 0
                                ? $"已删除的 {skippedDeletedCount} 个默认分类保持删除状态。"
                                : string.Empty)
                    };
                }
                catch (Exception exception)
                {
                    Logger.Error("应用 GitHub 分类失败：" + exception);
                    return Failure("无法完整应用新的分类配置，请查看启动日志。");
                }
            }
            finally
            {
                _fetchLock.Release();
            }
        }

        public void DeleteCategory(CategoryModel category)
        {
            lock (DefaultCategoryCatalog.SyncRoot)
            {
                var general = _appConfig.GetConfig().General;
                var previousIds = general.DefaultCategoryIds;
                var ids = new Dictionary<string, int>(previousIds, StringComparer.OrdinalIgnoreCase);
                var keys = ids.Where(pair => pair.Value == category.ID).Select(pair => pair.Key).ToArray();
                if (keys.Length > 0)
                {
                    foreach (var key in keys) ids[key] = -1;
                    if (!_appConfig.UpdateAndSave(config =>
                    {
                        config.General.DefaultCategoryIds = ids;
                        return true;
                    }))
                    {
                        general.DefaultCategoryIds = previousIds;
                        throw new IOException("保存默认分类删除状态失败。");
                    }
                }

                try
                {
                    _categoryService.Delete(category);
                }
                catch
                {
                    if (keys.Length > 0 && !_appConfig.UpdateAndSave(config =>
                    {
                        config.General.DefaultCategoryIds = previousIds;
                        return true;
                    }))
                    {
                        general.DefaultCategoryIds = previousIds;
                        Logger.Error("分类删除失败，恢复默认分类映射时无法保存配置。");
                    }
                    throw;
                }
            }
        }

        public void StartAutomaticUpdates()
        {
            var intervalHours = GetAutomaticIntervalHours();
            CancellationTokenSource previousCts;
            Task previousTask;
            lock (_automaticUpdateLock)
            {
                previousCts = _automaticUpdateCts;
                previousTask = _automaticUpdateTask;
                _automaticUpdateCts = null;
                _automaticUpdateTask = null;
                if (_initialized && !_paused && intervalHours > 0)
                {
                    var cts = new CancellationTokenSource();
                    var token = cts.Token;
                    _automaticUpdateCts = cts;
                    _automaticUpdateTask = Task.Run(() => RunAutomaticUpdatesAsync(token));
                }
            }
            CancelAndDispose(previousCts, previousTask);
        }

        public void ResumeAutomaticUpdates()
        {
            lock (_automaticUpdateLock) _paused = false;
            StartAutomaticUpdates();
        }

        public async Task PauseAndWaitAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            lock (_automaticUpdateLock) _paused = true;
            StopAutomaticUpdates();
            await _fetchLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            _fetchLock.Release();
        }

        public void StopAutomaticUpdates()
        {
            CancellationTokenSource cts;
            Task task;
            lock (_automaticUpdateLock)
            {
                cts = _automaticUpdateCts;
                task = _automaticUpdateTask;
                _automaticUpdateCts = null;
                _automaticUpdateTask = null;
            }

            CancelAndDispose(cts, task);
        }

        private static void CancelAndDispose(CancellationTokenSource cts, Task task)
        {
            if (cts == null) return;
            cts.Cancel();
            if (task == null) cts.Dispose();
            else _ = task.ContinueWith(_ => cts.Dispose());
        }

        private async Task RunAutomaticUpdatesAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var remaining = GetInitialDelay();
                    if (remaining > TimeSpan.Zero)
                    {
                        await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    var result = await FetchLatestAsync(cancellationToken).ConfigureAwait(false);
                    var intervalHours = GetAutomaticIntervalHours();
                    if (intervalHours <= 0) return;
                    if (!result.Succeeded || GetInitialDelay() <= TimeSpan.Zero)
                    {
                        await Task.Delay(TimeSpan.FromHours(Math.Min(intervalHours, 1)), cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The timer is intentionally cancelled when the setting changes or the app stops.
            }
            catch (Exception exception)
            {
                Logger.Error("自动获取分类失败：" + exception);
            }
        }

        private void AppConfig_ConfigChanged(ConfigModel oldConfig, ConfigModel newConfig)
        {
            if (_initialized && oldConfig?.General?.CategoryUpdateIntervalHours !=
                newConfig?.General?.CategoryUpdateIntervalHours)
            {
                StartAutomaticUpdates();
            }
        }

        private int GetAutomaticIntervalHours()
        {
            var value = _appConfig.GetConfig()?.General?.CategoryUpdateIntervalHours ?? 0;
            return Math.Max(0, Math.Min(MaxAutomaticIntervalHours, value));
        }

        private TimeSpan GetInitialDelay()
        {
            var intervalHours = GetAutomaticIntervalHours();
            var last = _appConfig.GetConfig()?.General?.LastCategoryCatalogUpdateUtc;
            if (intervalHours <= 0 || !last.HasValue) return TimeSpan.Zero;

            var remaining = TimeSpan.FromHours(intervalHours) - (DateTime.UtcNow - last.Value);
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        private bool EnsureCategories(IReadOnlyList<DefaultCategoryCatalog.Definition> definitions, ConfigModel config)
        {
            config.General ??= new GeneralModel();
            var ids = new Dictionary<string, int>(
                config.General.DefaultCategoryIds ?? new Dictionary<string, int>(),
                StringComparer.OrdinalIgnoreCase);
            var changed = false;

            foreach (var definition in definitions)
            {
                CategoryModel category = null;
                if (ids.TryGetValue(definition.Key, out var categoryId))
                {
                    category = _categoryService.GetCategory(categoryId);
                    if (category == null) continue;
                }

                // Reuse the name after a previous partial save, while keeping IDs as
                // the authority for renamed or deleted categories.
                if (category == null)
                {
                    category = _categoryService.GetCategories().FirstOrDefault(item =>
                        string.Equals(item.Name, definition.Name, StringComparison.OrdinalIgnoreCase));
                }

                if (category == null)
                {
                    category = _categoryService.Create(new CategoryModel
                    {
                        Name = definition.Name,
                        Color = definition.Color,
                        IconFile = string.Empty,
                        Directories = "[]"
                    });
                    changed = true;
                }

                if (!ids.TryGetValue(definition.Key, out var existingId) || existingId != category.ID)
                {
                    ids[definition.Key] = category.ID;
                    changed = true;
                }
            }

            if (!config.General.DefaultCategoriesInitialized)
            {
                config.General.DefaultCategoriesInitialized = true;
                changed = true;
            }

            config.General.DefaultCategoryIds = ids;
            return changed;
        }

        private void WriteCache(string json)
        {
            var directory = Path.GetDirectoryName(_cachePath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            var temporaryPath = _cachePath + ".tmp";
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _cachePath, true);
        }

        private async Task<string> DownloadCatalogAsync(CancellationToken cancellationToken)
        {
            using (var response = await _httpClient.GetAsync(
                new Uri(RemoteCatalogUrl), HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > MaxCatalogSize)
                    throw new InvalidDataException("分类配置超过大小限制。");

                using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                    .ConfigureAwait(false))
                using (var buffer = new MemoryStream())
                {
                    var chunk = new byte[8192];
                    int count;
                    while ((count = await stream.ReadAsync(chunk, 0, chunk.Length, cancellationToken)
                        .ConfigureAwait(false)) > 0)
                    {
                        if (buffer.Length + count > MaxCatalogSize)
                            throw new InvalidDataException("分类配置超过大小限制。");
                        buffer.Write(chunk, 0, count);
                    }

                    return new UTF8Encoding(false, true).GetString(buffer.ToArray());
                }
            }
        }

        private static CategoryCatalogUpdateResult Failure(string message)
        {
            return new CategoryCatalogUpdateResult
            {
                Succeeded = false,
                Changed = false,
                CategoryCount = DefaultCategoryCatalog.Categories.Count,
                Message = message
            };
        }

        private static List<DefaultCategoryCatalog.Definition> TryReadCatalog(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var info = new FileInfo(path);
                if (info.Length <= 0 || info.Length > MaxCatalogSize) return null;
                return TryParseCatalog(File.ReadAllText(path), out var definitions) ? definitions : null;
            }
            catch (Exception exception)
            {
                Logger.Error("读取本地分类失败：" + exception);
                return null;
            }
        }

        private static bool TryParseCatalog(string json, out List<DefaultCategoryCatalog.Definition> definitions)
        {
            definitions = null;
            try
            {
                var document = JsonConvert.DeserializeObject<CategoryCatalogDocument>(json);
                if (document == null || document.Version != 1 || document.Categories == null ||
                    document.Categories.Count == 0 || document.Categories.Count > MaxCategoryCount)
                {
                    return false;
                }

                var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var processes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var result = new List<DefaultCategoryCatalog.Definition>();
                foreach (var item in document.Categories)
                {
                    var key = item?.Key?.Trim();
                    var name = item?.Name?.Trim();
                    var color = item?.Color?.Trim();
                    if (string.IsNullOrWhiteSpace(key) || key.Length > 64 ||
                        !key.All(character => char.IsLetterOrDigit(character) || character == '-' || character == '_') ||
                        string.IsNullOrWhiteSpace(name) || name.Length > 80 || !names.Add(name) ||
                        string.IsNullOrWhiteSpace(color) || !IsColor(color) ||
                        item.Processes == null || item.Processes.Count == 0 ||
                        item.Processes.Count > MaxProcessesPerCategory || !keys.Add(key) ||
                        item.Processes.Any(process => string.IsNullOrWhiteSpace(process) ||
                            process.Length > 260 || process.Contains('/') || process.Contains('\\')))
                    {
                        return false;
                    }

                    var normalizedProcesses = item.Processes
                        .Select(process => NormalizeProcessName(process))
                        .ToArray();
                    if (normalizedProcesses.Any(process => process.Length == 0) ||
                        normalizedProcesses.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalizedProcesses.Length ||
                        normalizedProcesses.Any(process => !processes.Add(process)))
                    {
                        return false;
                    }

                    result.Add(new DefaultCategoryCatalog.Definition(key, name, color, normalizedProcesses));
                }

                definitions = result;
                return true;
            }
            catch (Exception exception)
            {
                Logger.Error("解析分类配置失败：" + exception);
                return false;
            }
        }

        private static bool IsColor(string value)
        {
            if (value.Length != 7 || value[0] != '#') return false;
            return value.Skip(1).All(character =>
                (character >= '0' && character <= '9') ||
                (character >= 'a' && character <= 'f') ||
                (character >= 'A' && character <= 'F'));
        }

        private static string NormalizeProcessName(string process)
        {
            var value = process.Trim();
            return value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? value.Substring(0, value.Length - 4)
                : value;
        }

        private static List<DefaultCategoryCatalog.Definition> ReadEmbeddedCatalog()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Core.default-categories.json"))
            {
                if (stream == null) throw new InvalidDataException("内置分类配置缺失。");
                using (var reader = new StreamReader(stream))
                {
                    if (!TryParseCatalog(reader.ReadToEnd(), out var definitions))
                        throw new InvalidDataException("内置分类配置无效。");
                    return definitions;
                }
            }
        }

        private static bool CatalogEquals(
            IEnumerable<DefaultCategoryCatalog.Definition> current,
            IEnumerable<DefaultCategoryCatalog.Definition> next)
        {
            var left = current.Select(ToSignature).ToArray();
            var right = next.Select(ToSignature).ToArray();
            return left.SequenceEqual(right, StringComparer.Ordinal);
        }

        private static string ToSignature(DefaultCategoryCatalog.Definition definition)
        {
            return definition.Key + "\n" + definition.Name + "\n" + definition.Color + "\n" +
                string.Join("\n", definition.Processes ?? Array.Empty<string>());
        }

        private sealed class CategoryCatalogDocument
        {
            [JsonProperty("version")]
            public int Version { get; set; }

            [JsonProperty("categories")]
            public List<CategoryCatalogItem> Categories { get; set; }
        }

        private sealed class CategoryCatalogItem
        {
            [JsonProperty("key")]
            public string Key { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("color")]
            public string Color { get; set; }

            [JsonProperty("processes")]
            public List<string> Processes { get; set; }
        }
    }
}
