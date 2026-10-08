using KeyShot.Core;

namespace KeyShot.Tests;

public class KeyShotControllerTests
{
    private sealed class RecordingEffect : IKeyEffect
    {
        public List<ShotKind> Shots { get; } = [];
        public string Name => "Recorder";
        public void Fire(ShotKind kind) => Shots.Add(kind);
    }

    private sealed class ThrowingEffect : IKeyEffect
    {
        public string Name => "Thrower";
        public void Fire(ShotKind kind) => throw new InvalidOperationException("boom");
    }

    private static (KeyShotController, RecordingEffect) Create(KeyShotSettings? settings = null)
    {
        // Frozen clock + zero cooldown keeps these tests about filtering only.
        var controller = new KeyShotController((settings ?? new KeyShotSettings()) with { CooldownMs = 0 }, () => 0, 1000);
        var effect = new RecordingEffect();
        controller.AddEffect(effect);
        return (controller, effect);
    }

    private static void Press(KeyShotController c, int vk, bool shortcut = false, bool injected = false)
    {
        c.HandleKey(new KeyInput(vk, true, injected, shortcut));
        c.HandleKey(new KeyInput(vk, false, injected));
    }

    [Fact]
    public void Letter_fires_normal_shot()
    {
        var (c, fx) = Create();
        Press(c, 'A');
        Assert.Equal([ShotKind.Normal], fx.Shots);
    }

    [Fact]
    public void Key_up_never_fires()
    {
        var (c, fx) = Create();
        c.HandleKey(new KeyInput('A', IsKeyDown: false));
        Assert.Empty(fx.Shots);
    }

    [Theory]
    [InlineData(VirtualKeys.LShift)]
    [InlineData(VirtualKeys.LControl)]
    [InlineData(VirtualKeys.LMenu)]
    [InlineData(VirtualKeys.LWin)]
    public void Modifiers_are_silent_by_default(int vk)
    {
        var (c, fx) = Create();
        Press(c, vk);
        Assert.Empty(fx.Shots);
    }

    [Fact]
    public void Modifiers_can_be_enabled()
    {
        var (c, fx) = Create(new KeyShotSettings { FireOnModifiers = true });
        Press(c, VirtualKeys.LShift);
        Assert.Single(fx.Shots);
    }

    [Fact]
    public void Shortcuts_like_ctrl_c_are_silent()
    {
        var (c, fx) = Create();
        Press(c, 'C', shortcut: true);
        Assert.Empty(fx.Shots);
    }

    [Fact]
    public void Shortcut_filter_can_be_disabled()
    {
        var (c, fx) = Create(new KeyShotSettings { IgnoreShortcuts = false });
        Press(c, 'C', shortcut: true);
        Assert.Single(fx.Shots);
    }

    [Fact]
    public void Injected_events_are_ignored_by_default()
    {
        var (c, fx) = Create();
        Press(c, 'A', injected: true);
        Assert.Empty(fx.Shots);

        var (c2, fx2) = Create(new KeyShotSettings { IgnoreInjected = false });
        Press(c2, 'A', injected: true);
        Assert.Single(fx2.Shots);
    }

    [Fact]
    public void Auto_repeat_fires_once_per_physical_press()
    {
        var (c, fx) = Create();
        for (int i = 0; i < 10; i++) c.HandleKey(new KeyInput('A', true)); // held key repeats key-down
        c.HandleKey(new KeyInput('A', false));
        c.HandleKey(new KeyInput('A', true));
        Assert.Equal(2, fx.Shots.Count);
    }

    [Fact]
    public void Auto_repeat_can_be_allowed()
    {
        var (c, fx) = Create(new KeyShotSettings { IgnoreAutoRepeat = false });
        for (int i = 0; i < 5; i++) c.HandleKey(new KeyInput('A', true));
        Assert.Equal(5, fx.Shots.Count);
    }

    [Fact]
    public void Space_and_enter_use_heavier_shots()
    {
        var (c, fx) = Create();
        Press(c, VirtualKeys.Space);
        Press(c, VirtualKeys.Return);
        Assert.Equal([ShotKind.Heavy, ShotKind.Strong], fx.Shots);
    }

    [Fact]
    public void Heavier_shots_can_be_turned_off()
    {
        var (c, fx) = Create(new KeyShotSettings { HeavySpace = false, StrongEnter = false });
        Press(c, VirtualKeys.Space);
        Press(c, VirtualKeys.Return);
        Assert.Equal([ShotKind.Normal, ShotKind.Normal], fx.Shots);
    }

    [Fact]
    public void Other_keys_are_opt_in()
    {
        var (c, fx) = Create();
        Press(c, VirtualKeys.Back);
        Press(c, 0x70);
        Assert.Empty(fx.Shots);

        var (c2, fx2) = Create(new KeyShotSettings { FireOnOtherKeys = true });
        Press(c2, VirtualKeys.Back);
        Assert.Single(fx2.Shots);
    }

    [Fact]
    public void Disabled_controller_is_silent_and_toggle_restores()
    {
        var (c, fx) = Create();
        var changes = new List<bool>();
        c.EnabledChanged += (_, on) => changes.Add(on);

        c.SetEnabled(false);
        Press(c, 'A');
        Assert.Empty(fx.Shots);
        Assert.False(c.IsEnabled);

        c.Toggle();
        Press(c, 'A');
        Assert.Single(fx.Shots);
        Assert.Equal([false, true], changes);
    }

    [Fact]
    public void Setting_same_state_does_not_raise_event()
    {
        var (c, _) = Create();
        int raised = 0;
        c.EnabledChanged += (_, _) => raised++;
        c.SetEnabled(true);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void Re_enabling_clears_stuck_key_state()
    {
        var (c, fx) = Create();
        c.HandleKey(new KeyInput('K', true)); // pressed...
        c.SetEnabled(false);                  // ...hook stops before the key-up arrives
        c.SetEnabled(true);
        c.HandleKey(new KeyInput('K', true));
        Assert.Equal(2, fx.Shots.Count);
    }

    [Fact]
    public void Failing_effect_never_escapes_and_others_still_fire()
    {
        var c = new KeyShotController(new KeyShotSettings { CooldownMs = 0 });
        var ok = new RecordingEffect();
        Exception? reported = null;
        c.EffectFailed += (_, ex) => reported = ex;
        c.AddEffect(new ThrowingEffect());
        c.AddEffect(ok);

        var fired = c.HandleKey(new KeyInput('A', true));

        Assert.True(fired);
        Assert.Single(ok.Shots);
        Assert.IsType<InvalidOperationException>(reported);
    }

    [Fact]
    public void Diagnostics_count_keys_and_shots()
    {
        var (c, _) = Create();
        Press(c, 'A');
        Press(c, VirtualKeys.LShift);
        Assert.Equal(2, c.KeysSeen);
        Assert.Equal(1, c.ShotsFired);
    }

    [Fact]
    public void Effects_can_be_removed()
    {
        var (c, fx) = Create();
        c.RemoveEffect(fx);
        Press(c, 'A');
        Assert.Empty(fx.Shots);
    }
}
