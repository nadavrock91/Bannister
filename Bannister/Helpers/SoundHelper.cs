namespace Bannister.Helpers;

/// <summary>
/// Helper class for playing sound effects
/// </summary>
public static class SoundHelper
{
    public static async Task PlayExpGainSound()
    {
        try
        {
#if WINDOWS
            Console.Beep(800, 100); // 800 Hz for 100ms - short beep
#endif
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Sound error: {ex.Message}");
        }
    }

    public static async Task PlayLevelUpSound()
    {
        try
        {
#if WINDOWS
            Console.Beep(523, 100); // C note
            await Task.Delay(50);
            Console.Beep(659, 100); // E note
            await Task.Delay(50);
            Console.Beep(784, 200); // G note - longer
#endif
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Sound error: {ex.Message}");
        }
    }
}
