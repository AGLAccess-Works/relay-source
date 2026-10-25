using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private readonly Dictionary<string, ProfileRowButton> _profileRows = new(StringComparer.Ordinal);

    private void PopulateProfileRows()
    {
        var ids = _app.Profiles.Profiles.Select(profile => profile.Id).ToHashSet(StringComparer.Ordinal);
        foreach (string removed in _profileRows.Keys.Where(id => !ids.Contains(id)).ToArray())
        {
            ProfileList.Children.Remove(_profileRows[removed]);
            _profileRows.Remove(removed);
        }
        for (int index = 0; index < _app.Profiles.Profiles.Length; index++)
        {
            var profile = _app.Profiles.Profiles[index];
            if (!_profileRows.TryGetValue(profile.Id, out var row))
            {
                row = new ProfileRowButton(profile.Id);
                row.Click += (_, _) => ActivateProfile(profile.Id);
                _profileRows.Add(profile.Id, row);
            }
            row.UpdateProfile(profile.Name, profile.Id == _app.Profiles.ActiveId);
            int current = ProfileList.Children.IndexOf(row);
            if (current == index) continue;
            if (current >= 0) ProfileList.Children.Remove(row);
            ProfileList.Children.Insert(index, row);
        }
    }

    private sealed class ProfileRowButton : Button
    {
        private readonly TextBlock _name = new() { FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock _current = new()
        {
            Text = "✓ Current profile",
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0)
        };

        internal ProfileRowButton(string id)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            MinWidth = 44; MinHeight = 88; Padding = new Thickness(16);
            Margin = new Thickness(0, 0, 0, 8);
            Style = (Style)Application.Current.FindResource("InputTile");
            _name.SetResourceReference(TextBlock.FontSizeProperty, "MainFontSize");
            _current.SetResourceReference(TextBlock.FontSizeProperty, "CaptionFontSize");
            var text = new StackPanel(); text.Children.Add(_name); text.Children.Add(_current);
            var icon = new DecorativeGlyph
            {
                Text = "\uE7F4",
                FontFamily = new("Segoe MDL2 Assets"),
                FontSize = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            icon.SetResourceReference(TextBlock.ForegroundProperty, "AccentSecondaryBrush");
            var badge = new Border
            {
                Child = icon,
                Width = 36,
                Height = 36,
                CornerRadius = new(8),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            badge.SetResourceReference(Border.BackgroundProperty, "SelectionSurfaceBrush");
            var iconColumn = new ColumnDefinition { Width = new GridLength(48) };
            var grid = new Grid(); grid.ColumnDefinitions.Add(iconColumn); grid.ColumnDefinitions.Add(new());
            Grid.SetColumn(text, 1); grid.Children.Add(badge); grid.Children.Add(text); Content = grid;
            void UpdateDecoration()
            {
                bool hide = ActualWidth < 480 && _name.FontSize >= 24;
                badge.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
                iconColumn.Width = new GridLength(hide ? 0 : 48);
            }
            SizeChanged += (_, _) => UpdateDecoration(); Loaded += (_, _) => UpdateDecoration();
            _name.SizeChanged += (_, _) => UpdateDecoration();
            AutomationProperties.SetAutomationId(this, "ProfileRow_" + id);
            AutomationProperties.SetHelpText(this, "Choose to make this profile active. Moving focus here keeps your current controls.");
        }

        internal void UpdateProfile(string name, bool current)
        {
            _name.Text = name;
            _current.Visibility = current ? Visibility.Visible : Visibility.Collapsed;
            InputTileState.SetIsSelected(this, current);
            SetResourceReference(BackgroundProperty, current ? "SelectionSurfaceBrush" : "PanelBrush");
            SetResourceReference(BorderBrushProperty, "BorderDefaultBrush");
            AutomationProperties.SetName(this, name + ". " + (current ? "Current profile" : "Activate profile"));
            AutomationProperties.SetItemStatus(this, current ? "Current profile" : "Available profile");
        }
    }
}
