using KeyShot.Core;

namespace KeyShot.Tests;

public class SampleSelectorTests
{
    [Fact]
    public void Empty_pool_returns_minus_one() => Assert.Equal(-1, new SampleSelector(new Random(1)).Next(0));

    [Fact]
    public void Single_sample_is_always_chosen()
    {
        var s = new SampleSelector(new Random(1));
        for (int i = 0; i < 20; i++) Assert.Equal(0, s.Next(1));
    }

    [Fact]
    public void Never_repeats_the_same_sample_back_to_back()
    {
        var s = new SampleSelector(new Random(42));
        int last = s.Next(3);
        for (int i = 0; i < 1000; i++)
        {
            int next = s.Next(3);
            Assert.NotEqual(last, next);
            last = next;
        }
    }

    [Fact]
    public void All_samples_are_used_roughly_evenly()
    {
        var s = new SampleSelector(new Random(7));
        var counts = new int[4];
        for (int i = 0; i < 4000; i++) counts[s.Next(4)]++;
        Assert.All(counts, c => Assert.InRange(c, 800, 1200));
    }

    [Fact]
    public void Indices_stay_in_range_when_pool_shrinks()
    {
        var s = new SampleSelector(new Random(3));
        for (int i = 0; i < 50; i++) s.Next(5);
        for (int i = 0; i < 50; i++) Assert.InRange(s.Next(2), 0, 1);
    }

    [Fact]
    public void Signed_values_are_within_unit_range()
    {
        var s = new SampleSelector(new Random(9));
        for (int i = 0; i < 1000; i++) Assert.InRange(s.NextSigned(), -1.0, 1.0);
    }
}
