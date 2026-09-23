using CacheVault.Replication.Protocol;
using CacheVault.Replication.State;

namespace CacheVault.Replication.Abstractions;

public interface IPsyncDecisionService {
    PsyncDecision Decide(
        PsyncCommand command);
}