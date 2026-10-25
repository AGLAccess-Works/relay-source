using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ControllerMouse.Core;
using Control = ControllerMouse.Core.Control;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private sealed record ChordRow(ButtonChord Chord, string Name);
    private void InitializeChordEditor()
    {
        ChordFirst.ItemsSource = ControlMapping.Controls; ChordFirst.SelectedIndex = 0;
        ChordSecond.ItemsSource = ControlMapping.Controls; ChordSecond.SelectedIndex = 1;
        ChordAction.ItemsSource = ((IEnumerable<BindingOption>)MappingAction.ItemsSource).Where(o =>
            !o.Binding.Repeat && (o.Binding.Kind == BindingKind.Keyboard || o.Binding.Kind == BindingKind.Shortcut && o.Binding.Shortcut != Shortcut.None)).ToList();
        ChordAction.SelectedItem = ((IEnumerable<BindingOption>)ChordAction.ItemsSource).First(o => o.Binding.Shortcut == Shortcut.OnScreenKeyboard);
        RefreshChordList();
    }
    private static string ChordName(ButtonChord chord) =>
        ControlMapping.Controls.First(c => c.Control == chord.First).Code + " + " + ControlMapping.Controls.First(c => c.Control == chord.Second).Code;
    private void RefreshChordList()
    {
        if (ChordList == null) return;
        ChordList.ItemsSource = (_settings.ButtonChords ?? []).Select(c => new ChordRow(c, ChordName(c) + " → " + BindingName(c.Action))).ToArray();
    }
    private void ChordSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ChordList.SelectedItem is not ChordRow row) return;
        ChordFirst.SelectedItem = ControlMapping.Controls.First(c => c.Control == row.Chord.First);
        ChordSecond.SelectedItem = ControlMapping.Controls.First(c => c.Control == row.Chord.Second);
        var options = ((IEnumerable<BindingOption>)ChordAction.ItemsSource).ToList();
        var option = options.FirstOrDefault(o => o.Binding == row.Chord.Action);
        if (option == null) { option = new(BindingName(row.Chord.Action), row.Chord.Action); options.Add(option); ChordAction.ItemsSource = options; }
        ChordAction.SelectedItem = option;
    }
    private void SaveChord(object sender, RoutedEventArgs e)
    {
        if (ChordFirst.SelectedItem is not ControlOption first || ChordSecond.SelectedItem is not ControlOption second || ChordAction.SelectedItem is not BindingOption action) return;
        var chord = new ButtonChord(first.Control, second.Control, action.Binding);
        if (!chord.IsValid) { ChordNotice.Text = "Choose two different controls. View + Menu is reserved for pause."; return; }
        var chords = (_settings.ButtonChords ?? []).ToList();
        int index = chords.FindIndex(c => c.Mask == chord.Mask);
        if (index >= 0) chords[index] = chord;
        else if (chords.Count >= 32) { ChordNotice.Text = "Remove a combination before adding another (maximum 32)."; return; }
        else chords.Add(chord);
        _settings = _settings with { ButtonChords = chords.ToArray() };
        ReadSettings(); SaveSettings(); RefreshChordList();
        ChordNotice.Text = ChordName(chord) + " saved. Release all controls to resume. Individual actions wait for the pairing window.";
    }
    private void RemoveChord(object sender, RoutedEventArgs e)
    {
        if (ChordList.SelectedItem is not ChordRow row) { ChordNotice.Text = "Select a saved combination first."; return; }
        _settings = _settings with { ButtonChords = (_settings.ButtonChords ?? []).Where(c => c.Mask != row.Chord.Mask).ToArray() };
        ReadSettings(); SaveSettings(); RefreshChordList(); ChordNotice.Text = "Combination removed. Release all controls to resume.";
    }
    private void AddExampleChords(object sender, RoutedEventArgs e)
    {
        var chords = (_settings.ButtonChords ?? []).ToList();
        foreach (var chord in new[] {
            new ButtonChord(Control.A, Control.B, new(BindingKind.Shortcut, Shortcut: Shortcut.OnScreenKeyboard)),
            new ButtonChord(Control.LeftShoulder, Control.RightShoulder, new(BindingKind.Shortcut, Shortcut: Shortcut.MagnifierView)) })
            if (!chords.Any(c => c.Mask == chord.Mask) && chords.Count < 32) chords.Add(chord);
        _settings = _settings with { ButtonChords = chords.ToArray() };
        ReadSettings(); SaveSettings(); RefreshChordList();
        ChordNotice.Text = "Added available examples: A + B opens Relay keyboard; LB + RB changes Magnifier view. Existing combinations were kept.";
    }
    private void RunChordSmokeChecks()
    {
        AddExampleChords(this, new RoutedEventArgs());
        if (_settings.ButtonChords?.Length != 2) throw new InvalidOperationException("Example combinations failed.");
        ChordList.SelectedIndex = 1;
        if (ChordFirst.SelectedItem is not ControlOption { Control: Control.LeftShoulder }) throw new InvalidOperationException("Combination selection failed.");
        SaveChord(this, new RoutedEventArgs());
        if (_settings.ButtonChords?.Length != 2) throw new InvalidOperationException("Combination update duplicated an entry.");
        ChordList.SelectedIndex = 0; RemoveChord(this, new RoutedEventArgs());
        if (_settings.ButtonChords?.Length != 1) throw new InvalidOperationException("Combination removal failed.");
        _settings = _settings with { ButtonChords = null }; RefreshChordList(); ReadSettings();
    }
}
