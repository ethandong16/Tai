using Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Servicers.Instances
{
    internal static class DefaultCategoryCatalog
    {
        internal static readonly object SyncRoot = new object();

        internal sealed class Definition
        {
            internal string Key { get; }
            internal string Name { get; }
            internal string Color { get; }
            internal string[] Processes { get; }

            internal Definition(string key, string name, string color, params string[] processes)
            {
                Key = key;
                Name = name;
                Color = color;
                Processes = processes;
            }
        }

        private static volatile Definition[] _categories = Array.Empty<Definition>();
        private static volatile Dictionary<string, string> _processCategories =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal static IReadOnlyList<Definition> Categories => _categories;

        internal static void SetCategories(IEnumerable<Definition> categories)
        {
            var snapshot = Clone(categories);
            var processCategories = BuildProcessCategories(snapshot);
            _categories = snapshot;
            _processCategories = processCategories;
        }

        private static Definition[] Clone(IEnumerable<Definition> categories)
        {
            return categories
                .Select(category => new Definition(
                    category.Key,
                    category.Name,
                    category.Color,
                    category.Processes?.ToArray() ?? Array.Empty<string>()))
                .ToArray();
        }

        private static Dictionary<string, string> BuildProcessCategories(IEnumerable<Definition> categories)
        {
            return categories
                .SelectMany(category => category.Processes.Select(process => new { process, category.Key }))
                .ToDictionary(item => item.process, item => item.Key, StringComparer.OrdinalIgnoreCase);
        }

        internal static CategoryModel Find(string processName, IDictionary<string, int> ids, IEnumerable<CategoryModel> categories)
        {
            if (string.IsNullOrWhiteSpace(processName) || ids == null) return null;
            var name = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName.Substring(0, processName.Length - 4) : processName;
            if (!_processCategories.TryGetValue(name, out var key) || !ids.TryGetValue(key, out var id)) return null;
            return categories.FirstOrDefault(category => category.ID == id);
        }
    }
}
