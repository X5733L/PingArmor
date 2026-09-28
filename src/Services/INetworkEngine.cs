using System.Collections.Generic;
using System.Threading;
using PingArmor.Models;

namespace PingArmor.Services;

public interface INetworkEngine
{
    List<NetworkAdapterInfo> GetAdapters(CancellationToken cancellationToken = default);
    OptimizationResult ApplyPlan(OptimizationPlan plan, bool force = false, CancellationToken cancellationToken = default);
}
