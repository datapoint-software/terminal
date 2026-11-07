namespace Datapoint.Terminal.Agent.Skills;

// SKILL.md's YAML header, as far as Agent Skills uses it: top-level "key: value" fields and the metadata map of strings. Every
// line is kept as it was, so fields this application doesn't know (license, allowed-tools…) survive an edit.
public sealed record SkillHeader(IReadOnlyList<(string Key, string Value)> Fields, IReadOnlyList<(string Key, string Value)> Metadata, string Body)
{
    public string? Field(string key) => Fields.FirstOrDefault(field => string.Equals(field.Key, key, StringComparison.Ordinal)).Value;

    public string? MetadataValue(string key) => Metadata.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal)).Value;
}
