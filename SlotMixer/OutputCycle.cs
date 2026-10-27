using SlotMixer.Audio;

namespace SlotMixer;

/// <summary>Which output a "switch output" shortcut moves to: the next one the slot allows, wrapping around.</summary>
internal static class OutputCycle
{
    /// <param name="outputs">The outputs in the order the cycle walks them (by name).</param>
    /// <param name="currentId">Where the app plays now; may be missing from the list (a device that went away).</param>
    /// <param name="allowed">Whether the slot lets the shortcut use that output.</param>
    /// <returns>The next allowed output after the current one, or null when there is none other than the current one.</returns>
    public static AudioDevice? Next(IReadOnlyList<AudioDevice> outputs, string? currentId, Func<AudioDevice, bool> allowed)
    {
        var start = -1;
        for (var i = 0; i < outputs.Count; i++)
        {
            if (string.Equals(outputs[i].Id, currentId, StringComparison.OrdinalIgnoreCase))
            {
                start = i;
                break;
            }
        }

        for (var step = 1; step <= outputs.Count; step++)
        {
            var candidate = outputs[(start + step) % outputs.Count];
            if (!allowed(candidate))
                continue;

            return string.Equals(candidate.Id, currentId, StringComparison.OrdinalIgnoreCase) ? null : candidate;
        }
        return null;
    }
}
