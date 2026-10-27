using System.Runtime.InteropServices;

namespace SlotMixer.Audio;

/// <summary>Volume and mute of one process as the volume mixer shows them (the session that is playing wins over idle ones).</summary>
internal readonly record struct AppAudioState(float Level, bool Muted, bool Active);

/// <summary>The per-app volume slider and mute button of the Windows volume mixer, applied to every session of the app.</summary>
internal static class AppVolume
{
    sealed record AppSession(ISimpleAudioVolume Volume, bool Active);

    /// <summary>Current level (1.0 = 100 %), or null when the app has no audio session yet.</summary>
    public static float? Get(ICollection<uint> pids)
    {
        var sessions = Collect(pids);
        return sessions.Count == 0 ? null : Level(sessions);
    }

    /// <summary>
    /// Moves the volume by <paramref name="delta"/> (1.0 = 100 %) and returns the new level. Raising the volume also
    /// unmutes: whoever turns it up expects to hear something.
    /// </summary>
    public static float? Change(ICollection<uint> pids, float delta)
    {
        var sessions = Collect(pids);
        if (sessions.Count == 0)
            return null;

        var level = Math.Clamp(Level(sessions) + delta, 0f, 1f);
        var context = Guid.Empty;
        foreach (var session in sessions)
        {
            try
            {
                session.Volume.SetMasterVolume(level, ref context);
                if (delta > 0)
                    session.Volume.SetMute(false, ref context);
            }
            catch (COMException)
            {
                // The session ended between enumeration and now.
            }
        }
        return level;
    }

    /// <summary>Flips mute for every session of the app. Returns the new state, or null when the app has no audio session.</summary>
    public static bool? ToggleMute(ICollection<uint> pids)
    {
        var sessions = Collect(pids);
        if (sessions.Count == 0)
            return null;

        var reference = sessions.FirstOrDefault(s => s.Active) ?? sessions[0];
        reference.Volume.GetMute(out var muted);

        var target = !muted;
        var context = Guid.Empty;
        foreach (var session in sessions)
        {
            try
            {
                session.Volume.SetMute(target, ref context);
            }
            catch (COMException)
            {
                // The session ended between enumeration and now.
            }
        }
        return target;
    }

    /// <summary>The processes that are capturing from a microphone right now (an active session on a recording device).</summary>
    public static HashSet<uint> ReadCapturing()
    {
        var capturing = new HashSet<uint>();
        Endpoints.ForEachSession((pid, session) =>
        {
            session.GetState(out var state);
            if (state == 1)
                capturing.Add(pid);
        }, EDataFlow.Capture);
        return capturing;
    }

    /// <summary>One pass over every session on the system: the state of each process that has audio.</summary>
    public static Dictionary<uint, AppAudioState> ReadAll()
    {
        var result = new Dictionary<uint, AppAudioState>();
        Endpoints.ForEachSession((pid, session) =>
        {
            session.GetState(out var state);
            var volume = (ISimpleAudioVolume)session;
            volume.GetMasterVolume(out var level);
            volume.GetMute(out var muted);

            var active = state == 1;
            if (!result.TryGetValue(pid, out var known) || (active && !known.Active))
                result[pid] = new AppAudioState(level, muted, active);
        });
        return result;
    }

    /// <summary>
    /// The level meters of every session of these processes. Finding them walks every audio device, so it is done once in
    /// a while; <see cref="Peak"/> then reads the held meters, which costs next to nothing.
    /// </summary>
    public static List<IAudioMeterInformation> OpenMeters(ICollection<uint> pids)
    {
        var meters = new List<IAudioMeterInformation>();
        Endpoints.ForEachSession((pid, session) =>
        {
            if (pids.Contains(pid) && session is IAudioMeterInformation meter)
                meters.Add(meter);
        });
        return meters;
    }

    /// <summary>How loud the loudest of these meters is right now (0..1); null when there are none. A meter whose session ended is dropped.</summary>
    public static float? Peak(List<IAudioMeterInformation> meters)
    {
        float? loudest = null;
        for (var i = meters.Count - 1; i >= 0; i--)
        {
            int result;
            float peak;
            try
            {
                result = meters[i].GetPeakValue(out peak);
            }
            catch (COMException)
            {
                result = -1;
                peak = 0;
            }

            if (result != 0)
            {
                meters.RemoveAt(i);
                continue;
            }
            loudest = Math.Max(loudest ?? 0, peak);
        }
        return loudest;
    }

    static List<AppSession> Collect(ICollection<uint> pids)
    {
        var found = new List<AppSession>();
        Endpoints.ForEachSession((pid, session) =>
        {
            if (!pids.Contains(pid))
                return;
            session.GetState(out var state);
            found.Add(new AppSession((ISimpleAudioVolume)session, state == 1));
        });
        return found;
    }

    /// <summary>An app can have an idle session per device; the one that is playing is the one that counts.</summary>
    static float Level(List<AppSession> sessions)
    {
        var session = sessions.FirstOrDefault(s => s.Active) ?? sessions[0];
        session.Volume.GetMasterVolume(out var level);
        return level;
    }
}
