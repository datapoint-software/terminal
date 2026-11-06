namespace Datapoint.Terminal.Emulator.Parsing;

public readonly ref struct CsiParameters(ReadOnlySpan<int> values, ReadOnlySpan<bool> continuesWithSubParameter)
{
    private readonly ReadOnlySpan<int> _values = values;
    private readonly ReadOnlySpan<bool> _continuesWithSubParameter = continuesWithSubParameter;

    public int Count => _values.Length;

    public int this[int index] => _values[index];

    public int GetOrDefault(int index, int defaultValue)
    {
        if (index >= _values.Length || _values[index] == 0)
        {
            return defaultValue;
        }

        return _values[index];
    }

    public bool HasSubParameterAfter(int index) =>
        index < _continuesWithSubParameter.Length && _continuesWithSubParameter[index];
}
