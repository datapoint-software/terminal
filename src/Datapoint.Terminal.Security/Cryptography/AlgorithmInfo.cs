namespace Datapoint.Terminal.Security.Cryptography;

public sealed record AlgorithmInfo(string Name, CryptographySource Source, bool IsFipsApproved);
