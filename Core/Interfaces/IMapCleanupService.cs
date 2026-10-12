using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ArdysaModsTools.Core.Interfaces
{
    public enum MapCleanupGroup { Foliage, Props }

    public sealed record MapCleanupStatus(bool Foliage, bool Props, bool Mounted);

    public interface IMapCleanupService
    {
        Task<MapCleanupStatus> GetStatusAsync(string? gamePath, CancellationToken cancellationToken = default);

        Task<bool> SetEnabledAsync(string? gamePath, IReadOnlySet<MapCleanupGroup> enabled, CancellationToken cancellationToken = default);
    }
}
