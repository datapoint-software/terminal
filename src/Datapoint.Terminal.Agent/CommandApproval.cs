namespace Datapoint.Terminal.Agent;

/// <summary>The user's answer to a proposal. <see cref="Command"/> is what to run, which the user may have edited.</summary>
public sealed record CommandApproval(bool Approved, string Command, string? Feedback = null)
{
    public static CommandApproval Approve(string command) => new(Approved: true, command);

    public static CommandApproval Reject(string command, string? feedback = null) => new(Approved: false, command, feedback);
}
