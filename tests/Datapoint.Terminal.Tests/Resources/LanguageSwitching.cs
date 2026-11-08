namespace Datapoint.Terminal.Tests.Resources;

// Tests that change the interface's language for the whole process run alone, so no other test sees the change.
[CollectionDefinition(nameof(LanguageSwitching), DisableParallelization = true)]
public sealed class LanguageSwitching;
