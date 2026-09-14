using System.Collections.Generic;
using PingArmor.Models;

namespace PingArmor.Services;

public interface INetworkEngine
{
    List<NetworkAdapterInfo> GetAdapters();
    OptimizationResult ApplyPlan(OptimizationPlan plan, bool force = false);
}
