namespace Datapoint.Terminal.Agent.Skills;

// A skill as read from its SKILL.md: what the agent sees, and where and how it runs.
public sealed record Skill(string Name, string Title, string Description, IReadOnlyList<string> Labels, SkillRollout Rollout, string Body, string Path)
{
    // A host the skill runs on has every label the skill lists; a skill that lists none runs on every host.
    public bool RunsOn(IReadOnlyList<string>? hostLabels) =>
        Labels.All(label => hostLabels?.Contains(label, StringComparer.OrdinalIgnoreCase) == true);
}
