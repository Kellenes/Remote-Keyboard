using InputSimulatorStandard;
using InputSimulatorStandard.Native;
using System.Text.Json;

namespace KeyboardController.Services;

public class InputHandler
{
    private readonly InputSimulator _simulator = new();

    public void ProcessCommand(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var action = root.GetProperty("action").GetString();

            if (action == "text")
            {
                var text = root.GetProperty("value").GetString();
                if (!string.IsNullOrEmpty(text))
                {
                    _simulator.Keyboard.TextEntry(text);
                }
                return;
            }

            var keyName = root.GetProperty("key").GetString();
            if (Enum.TryParse<VirtualKeyCode>(keyName, true, out var vkCode))
            {
                if (action == "down") _simulator.Keyboard.KeyDown(vkCode);
                else if (action == "up") _simulator.Keyboard.KeyUp(vkCode);
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"[Input Error] {ex.Message}");
        }
    }
}