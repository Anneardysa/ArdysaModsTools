using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ArdysaModsTools.Core.Interfaces
{
    public interface IAutoexecService
    {
        Task<Dictionary<string, string>> LoadCurrentSettingsAsync(string? gamePath, CancellationToken cancellationToken = default);
        Task ApplySettingsAsync(string? gamePath, Dictionary<string, string> settings, CancellationToken cancellationToken = default);
        Task ExportCfgAsync(string exportPath, Dictionary<string, string> settings, CancellationToken cancellationToken = default);
    }
}
