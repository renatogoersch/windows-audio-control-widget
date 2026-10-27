using System.Windows.Threading;
using SlotMixer.Audio;

namespace SlotMixer;

/// <summary>The thin line on a slot while its app is playing sound: which app is making the noise, at a glance.</summary>
internal sealed partial class WidgetWindow
{
    readonly DispatcherTimer _peakTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };

    /// <summary>True while lines may be showing, so that turning the indicator off clears them once.</summary>
    bool _activityShown;

    void InitActivity()
    {
        _peakTimer.Tick += (_, _) => UpdateActivity();
        _peakTimer.Start();
    }

    void UpdateActivity()
    {
        if (!_settings.ShowActivity || !_shown)
        {
            if (_activityShown)
            {
                foreach (var view in _views)
                {
                    view.SetActivity(null, enabled: false);
                    view.SetInputLevel(null, enabled: false);
                }
                _system.SetActivity(null, enabled: false);
                _activityShown = false;
            }
            return;
        }

        for (var i = 0; i < _views.Count && i < _runtime.Count; i++)
        {
            _views[i].SetActivity(AppVolume.Peak(_runtime[i].Meters), enabled: true);
            _views[i].SetInputLevel(AppVolume.Peak(_runtime[i].InputMeters), enabled: true);
        }

        _system.SetActivity(_masterMeter == null ? null : AppVolume.Peak(new List<IAudioMeterInformation> { _masterMeter }), enabled: true);
        _activityShown = true;
    }
}
