namespace KeyShot.Core;

/// <summary>
/// Picks a random sample index, avoiding the same sample twice in a row when
/// more than one is available (back-to-back repeats sound mechanical).
/// </summary>
public sealed class SampleSelector
{
    private readonly Random _random;
    private int _last = -1;

    public SampleSelector(Random? random = null) => _random = random ?? new Random();

    public int Next(int count)
    {
        if (count <= 0) return -1;
        if (count == 1) return _last = 0;

        int pick = _random.Next(count - 1);
        if (pick >= _last && _last >= 0) pick++;
        return _last = pick;
    }

    /// <summary>Uniform value in [-1, 1).</summary>
    public double NextSigned() => _random.NextDouble() * 2.0 - 1.0;
}
