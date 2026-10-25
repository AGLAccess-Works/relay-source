using System;
using System.IO;
using System.Linq;
using System.Windows;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private bool _setupOpen;
    private void OpenSetup(object sender, RoutedEventArgs e) => RunSetup();
    internal void RunSetup()
    {
        if (_setupOpen) return;
        _setupOpen = true; _save.Stop();
        bool wasEnabled = _app.Service?.Status.Enabled == true;
        try
        {
            _app.Service?.UpdateSettings(SetupPlan.Navigation(_settings, SetupInputMethod.Switches), "setup-preview");
            if (!wasEnabled) _app.Service?.Toggle();
            var wizard = new SetupWindow(_settings, CompleteSetup, settings => _app.Service?.UpdateSettings(settings, "setup-preview"), _app.Service?.ControllerKind ?? ControllerKind.Unknown) { Owner = this };
            wizard.ShowDialog();
        }
        finally
        {
            _app.Service?.UpdateSettings(_settings, _app.Profiles.ActiveId);
            if (!wasEnabled && _app.Service?.Status.Enabled == true) _app.Service.Toggle();
            _app.Service?.SaveMotion(_app.Profiles.Profiles.Select(p => p.Id));
            _setupOpen = false; RefreshStatus();
            AccessibilitySupport.FocusHeading(PageHeading);
        }
    }
    private string? CompleteSetup(SetupChoices choices)
    {
        if (_app.Profiles.Profiles.Length >= 64) return "Your profile list is full. Remove an unused profile in Profiles, then run setup again.";
        var settings = SetupPlan.Build(choices, _settings);
        var profile = new Profile(Guid.NewGuid().ToString("N"), UniqueName("My setup"), settings);
        var current = _app.Profiles.Update(_settings);
        var candidate = current with { Profiles = [.. current.Profiles, profile], ActiveId = profile.Id, SetupCompleted = true };
        if (!_app.SmokeTest)
        {
            try { _app.Store.Save(settings); _app.ProfileStore.Save(candidate); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                try { _app.Store.Save(_settings); } catch (Exception rollback) when (rollback is IOException or UnauthorizedAccessException) { Log.Write(rollback); }
                Log.Write(ex); return "Could not save your setup. Your current profile is still active. Try Save and start again, or choose Set up later.";
            }
        }
        _app.Profiles = candidate; _settings = settings;
        PopulateControls(); PopulateProfiles(); LoadBinding(); RefreshChordList(); ReadSettings(); SaveSettings();
        Pages.SelectedIndex = 0;
        return null;
    }
    private void RunSetupSmokeChecks(string output)
    {
        var original = _app.Profiles;
        bool finished = false;
        var wizard = new SetupWindow(_settings, choices => { var result = CompleteSetup(choices); finished = result == null; return result; }, _ => { }) { Owner = this };
        wizard.Show(); wizard.UpdateLayout(); wizard.VerifyPages(output);
        if (!finished || !_app.Profiles.SetupCompleted || _settings.MotionLearning != LearningMode.Learn ||
            _settings.ButtonChords?.Length != 0 || !_settings.TremorStabilization ||
            !_settings.EffectiveVisualAccess.OneControlAtATime ||
            !_app.Profiles.Profiles.Any(p => p.Id == original.ActiveId)) throw new InvalidOperationException("Guided setup failed.");
        _app.Profiles = original; _settings = original.Active.Settings; PopulateControls(); PopulateProfiles(); ReadSettings();
        var failure = new SetupWindow(_settings, _ => "Could not save test setup.", _ => { }) { Owner = this };
        failure.Show(); failure.VerifyFailedSave();
        if (_app.Profiles != original) throw new InvalidOperationException("Cancelled setup modified existing profiles.");
        File.WriteAllText(Path.Combine(output, "setup.txt"), "PASS: four short setup pages rendered; accessible names and action patterns checked; large text at 480x480 remains reachable; independent reading/physical choices generated a new active profile with single-button keyboard/Magnifier tools, one-control presentation, motion assistance and selected learning; previous profiles preserved; setup completion stored in library; failed save remains open and cancel leaves profiles unchanged. No user settings changed.");
    }
}
