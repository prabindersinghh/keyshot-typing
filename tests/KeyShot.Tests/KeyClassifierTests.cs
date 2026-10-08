using KeyShot.Core;

namespace KeyShot.Tests;

public class KeyClassifierTests
{
    [Theory]
    [InlineData('A')]
    [InlineData('Z')]
    [InlineData('0')]
    [InlineData('9')]
    [InlineData(0x60)] // Numpad 0
    [InlineData(0x6A)] // Numpad *
    [InlineData(0xBA)] // ;
    [InlineData(0xBC)] // ,
    [InlineData(0xBE)] // .
    [InlineData(0xC0)] // `
    [InlineData(0xDB)] // [
    [InlineData(0xDE)] // '
    [InlineData(0xE2)] // 102-key \
    public void Printable_keys_are_printable(int vk) =>
        Assert.Equal(KeyCategory.Printable, KeyClassifier.Classify(vk));

    [Fact]
    public void Space_and_enter_have_their_own_categories()
    {
        Assert.Equal(KeyCategory.Space, KeyClassifier.Classify(VirtualKeys.Space));
        Assert.Equal(KeyCategory.Enter, KeyClassifier.Classify(VirtualKeys.Return));
    }

    [Theory]
    [InlineData(VirtualKeys.Shift)]
    [InlineData(VirtualKeys.LShift)]
    [InlineData(VirtualKeys.RShift)]
    [InlineData(VirtualKeys.Control)]
    [InlineData(VirtualKeys.LControl)]
    [InlineData(VirtualKeys.RControl)]
    [InlineData(VirtualKeys.Menu)]
    [InlineData(VirtualKeys.LMenu)]
    [InlineData(VirtualKeys.RMenu)]
    [InlineData(VirtualKeys.LWin)]
    [InlineData(VirtualKeys.RWin)]
    [InlineData(VirtualKeys.Capital)]
    public void Modifiers_are_modifiers(int vk)
    {
        Assert.Equal(KeyCategory.Modifier, KeyClassifier.Classify(vk));
        Assert.True(KeyClassifier.IsModifier(vk));
    }

    [Theory]
    [InlineData(VirtualKeys.Back)]
    [InlineData(VirtualKeys.Tab)]
    [InlineData(VirtualKeys.Escape)]
    [InlineData(0x25)] // Left arrow
    [InlineData(0x70)] // F1
    [InlineData(0x2E)] // Delete
    public void Navigation_and_function_keys_are_other(int vk) =>
        Assert.Equal(KeyCategory.Other, KeyClassifier.Classify(vk));

    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    [InlineData(int.MaxValue)]
    public void Out_of_range_codes_are_other(int vk) =>
        Assert.Equal(KeyCategory.Other, KeyClassifier.Classify(vk));
}
