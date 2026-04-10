namespace AsanaClaudeAgent.Services;

public interface IGitService
{
    Task EnsureCleanWorkingTreeAsync(CancellationToken ct);
    Task<string> CreateBranchAsync(string branchName, CancellationToken ct);
    Task CheckoutAsync(string branchName, CancellationToken ct);
    Task DeleteLocalBranchAsync(string branchName, CancellationToken ct);
}
