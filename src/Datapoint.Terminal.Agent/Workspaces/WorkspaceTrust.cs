namespace Datapoint.Terminal.Agent.Workspaces;

// Product decision 91. A folder with nothing to trust (no instruction files, no connections) is trusted without asking; the
// question comes when there is something to show.
public static class WorkspaceTrust
{
    public static TrustState Evaluate(bool isDefault, bool hasConnections, IReadOnlyList<string> instructionFiles, WorkspaceTrustRecord? record)
    {
        ArgumentNullException.ThrowIfNull(instructionFiles);
        if (isDefault)
        {
            return TrustState.Trusted;
        }

        if (record is not null && instructionFiles.All(record.Covers))
        {
            return record.IsTrusted ? TrustState.Trusted : TrustState.Untrusted;
        }

        return record is null && instructionFiles.Count == 0 && !hasConnections ? TrustState.Trusted : TrustState.Ask;
    }
}
