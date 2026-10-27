using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace SlotMixer;

/// <summary>Help for people with a setup nobody on the project has: a diagnostic text to paste, and a way to report a problem.</summary>
internal sealed partial class WidgetWindow
{
    string BuildDiagnostics() => SlotMixer.Diagnostics.Build(_settings, _outputs, _inputs, All.Select(DescribeWindow));

    static string DescribeWindow(WidgetWindow window)
    {
        Native.GetWindowRect(window._hwnd, out var rect);
        return $"{(window._isPrimary ? "main" : "other")}: {rect.Left},{rect.Top} {rect.Width}x{rect.Height}, shown={window._shown}, strip scale {window._fit:0.00}";
    }

    /// <summary>Menu action: the diagnostic text goes to the clipboard, ready to paste.</summary>
    void CopyDiagnostics()
    {
        if (!TryCopy(BuildDiagnostics()))
        {
            _osd.Flash(null, "SlotMixer", L("Não foi possível copiar para a área de transferência."), null);
            return;
        }
        _osd.Flash(null, L("Diagnóstico copiado"), L("Cole na sua mensagem ou na issue do GitHub (Ctrl+V)."), null);
    }

    /// <summary>Menu action: copies the diagnostic text and opens the bug report form of the project.</summary>
    void ReportProblem()
    {
        var copied = TryCopy(BuildDiagnostics());
        try
        {
            Process.Start(new ProcessStartInfo($"https://github.com/{Updater.Repository}/issues/new?template=bug_report.yml") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
        _osd.Flash(null, L("Reportar um problema"),
            copied ? L("O diagnóstico foi copiado: cole no campo \"Diagnostics\".") : L("Abra a página e descreva o problema."), null);
    }

    /// <summary>The clipboard can be busy for a moment (another program is reading it): a few tries.</summary>
    static bool TryCopy(string text)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(text, copy: true);
                return true;
            }
            catch (COMException)
            {
                System.Threading.Thread.Sleep(60);
            }
        }
        return false;
    }
}
