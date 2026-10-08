using KeyShot.Core;

namespace KeyShot.Tests;

public class CooldownTests
{
    [Fact]
    public void First_trigger_always_passes()
    {
        long now = 0;
        var gate = new CooldownGate(100, () => now, 1000);
        Assert.True(gate.TryPass());
    }

    [Fact]
    public void Triggers_inside_window_are_dropped()
    {
        long now = 0;
        var gate = new CooldownGate(50, () => now, 1000);
        Assert.True(gate.TryPass());
        now = 49;
        Assert.False(gate.TryPass());
        now = 50;
        Assert.True(gate.TryPass());
    }

    [Fact]
    public void Dropped_triggers_do_not_extend_the_window()
    {
        long now = 0;
        var gate = new CooldownGate(50, () => now, 1000);
        gate.TryPass();
        now = 30;
        gate.TryPass(); // dropped
        now = 55;
        Assert.True(gate.TryPass()); // measured from the last *accepted* shot
    }

    [Fact]
    public void Zero_cooldown_allows_everything()
    {
        long now = 0;
        var gate = new CooldownGate(0, () => now, 1000);
        for (int i = 0; i < 100; i++) Assert.True(gate.TryPass());
    }

    [Fact]
    public void Cooldown_can_change_at_runtime()
    {
        long now = 0;
        var gate = new CooldownGate(0, () => now, 1000);
        gate.TryPass();
        gate.CooldownMs = 20;
        now = 10;
        Assert.False(gate.TryPass());
        Assert.Equal(20, gate.CooldownMs);
    }

    [Fact]
    public void Controller_applies_cooldown_between_presses()
    {
        long now = 0;
        var controller = new KeyShotController(new KeyShotSettings { CooldownMs = 40 }, () => now, 1000);

        Assert.True(controller.HandleKey(new KeyInput('A', true)));
        now = 10;
        Assert.False(controller.HandleKey(new KeyInput('B', true)));
        now = 45;
        Assert.True(controller.HandleKey(new KeyInput('C', true)));
    }
}
