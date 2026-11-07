namespace Datapoint.Terminal.ThirdPartyNotices;

// One shipped package, or other third-party material, with the license it's used under.
public sealed record Component(string Name, string License, string? Project, string LicenseText, IReadOnlyList<string> Notices);
