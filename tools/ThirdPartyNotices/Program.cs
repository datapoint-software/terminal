using Datapoint.Terminal.ThirdPartyNotices;

// Writes THIRD-PARTY-NOTICES.txt at the repository's root; with --check, only reports whether it's up to date.
var repository = new DirectoryInfo(AppContext.BaseDirectory);
while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "Datapoint.Terminal.slnx")))
{
    repository = repository.Parent;
}

if (repository is null)
{
    Console.Error.WriteLine("Run this from inside the repository.");
    return 2;
}

var path = Path.Combine(repository.FullName, "THIRD-PARTY-NOTICES.txt");
try
{
    var text = NoticesFile.Generate(repository.FullName);
    if (args.Contains("--check", StringComparer.Ordinal))
    {
        var isCurrent = File.Exists(path) && string.Equals(File.ReadAllText(path), text, StringComparison.Ordinal);
        Console.WriteLine(isCurrent ? "THIRD-PARTY-NOTICES.txt is up to date." : "THIRD-PARTY-NOTICES.txt is out of date: run dotnet run --project tools/ThirdPartyNotices.");
        return isCurrent ? 0 : 1;
    }

    File.WriteAllText(path, text);
    Console.WriteLine($"Wrote {path}.");
    return 0;
}
catch (NoticeException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
