using Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Servicers.Interfaces
{
    /// <summary>
    /// 负责加载、同步和应用默认应用分类目录。
    /// </summary>
    public interface ICategoryCatalogService
    {
        event EventHandler CatalogUpdated;

        /// <summary>
        /// 加载本地缓存或内置目录，并确保目录对应的数据库分类存在。
        /// </summary>
        void Initialize();

        /// <summary>
        /// 从 GitHub 获取并校验最新目录；下载或校验失败时保留当前目录。
        /// </summary>
        Task<CategoryCatalogUpdateResult> FetchLatestAsync(CancellationToken cancellationToken = default(CancellationToken));

        void DeleteCategory(CategoryModel category);

        void StartAutomaticUpdates();
        void StopAutomaticUpdates();
        void ResumeAutomaticUpdates();
        Task PauseAndWaitAsync(CancellationToken cancellationToken = default(CancellationToken));
    }

    public sealed class CategoryCatalogUpdateResult
    {
        public bool Succeeded { get; internal set; }
        public bool Changed { get; internal set; }
        public int CategoryCount { get; internal set; }
        public string Message { get; internal set; }
        public DateTime? UpdatedAtUtc { get; internal set; }
    }
}
