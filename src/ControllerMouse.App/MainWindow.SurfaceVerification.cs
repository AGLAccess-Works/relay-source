using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ControllerMouse.Core;

namespace ControllerMouse.App;

public partial class MainWindow
{
    private void RunSurfaceChecks(string output)
    {
        if (!_app.SmokeTest || _app.Service != null)
            throw new InvalidOperationException("Controller surface checks require the isolated smoke preview, without a desktop output service.");

        var originalSettings = _settings;
        string originalSelection = _selectedInput;
        int originalPage = Pages.SelectedIndex;
        bool originalPorts = _portsView, originalLight = _app.LightTheme, originalContrast = _app.ContrastActive;
        double originalWidth = Width, originalHeight = Height;
        var originalWindowState = WindowState;
        bool originalTopmost = Topmost;
        var report = new StringBuilder();
        var artworkReport = new StringBuilder();
        var preview = new ControllerStatus(true, true, true, 0, "Preview", default, null, false);

        void SetPreview(Gamepad pad, bool connected = true)
        {
            RefreshStatus(preview with { State = pad, Connected = connected });
            UpdateLayout();
            DrainInvocations();
        }
        void DrainInvocations()
        {
            // Button's UIA Invoke dispatches its Click. Pump to ApplicationIdle so the
            // assertion observes the same completed invocation a screen reader requests.
            var frame = new DispatcherFrame();
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            UpdateLayout();
        }
        static AutomationPeer Peer(Button button) => UIElementAutomationPeer.CreatePeerForElement(button)
            ?? throw new InvalidOperationException("Controller input has no automation peer.");
        void InvokePart(string id)
        {
            var part = LiveSurface.GetPart(id) ?? throw new InvalidOperationException("Missing controller input " + id);
            var invoke = Peer(part).GetPattern(PatternInterface.Invoke) as IInvokeProvider
                ?? throw new InvalidOperationException("Controller input has no Invoke pattern: " + id);
            invoke.Invoke(); DrainInvocations();
            if (_selectedInput != id || LiveSurface.SelectedInput != id)
                throw new InvalidOperationException("UIA Invoke did not select the controller input: " + id);
        }
        static byte[] Pixels(FrameworkElement element)
        {
            element.UpdateLayout();
            int width = Math.Max(1, (int)Math.Ceiling(element.ActualWidth)), height = Math.Max(1, (int)Math.Ceiling(element.ActualHeight));
            var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            // A child visual retains its arranged offset. Paint it into an origin-based
            // drawing so the component snapshot cannot be cropped to transparent pixels.
            var drawing = new DrawingVisual();
            using (var context = drawing.RenderOpen())
                context.DrawRectangle(new VisualBrush(element), null, new Rect(0, 0, width, height));
            image.Render(drawing);
            var pixels = new byte[width * height * 4]; image.CopyPixels(pixels, width * 4, 0); return pixels;
        }
        static Rect Bounds(ControllerSurface surface, Button part) => new(part.TranslatePoint(new Point(), surface), part.RenderSize);
        static bool Direction(string id) => id is "Up" or "Down" or "Left" or "Right";
        static bool ActualPartHit(ControllerPartButton part, Point point)
        {
            if (part.InputHitTest(point) is not DependencyObject hit) return false;
            for (var element = hit; element != null; element = VisualTreeHelper.GetParent(element))
                if (ReferenceEquals(element, part)) return true;
            return false;
        }
        void CheckDirectionHitRegion(string id, ControllerPartButton part, string mode)
        {
            double minimumX = double.PositiveInfinity, minimumY = double.PositiveInfinity, maximumX = 0, maximumY = 0;
            int points = 0;
            for (double y = .5; y < part.ActualHeight; y += 2)
                for (double x = .5; x < part.ActualWidth; x += 2)
                {
                    var point = new Point(x, y);
                    bool expected = part.ContainsHitPoint(point);
                    if (expected != ActualPartHit(part, point))
                        throw new InvalidOperationException($"{mode} {id} declared hit region differs from its actual WPF hit test at {point}.");
                    if (!expected) continue;
                    points++; minimumX = Math.Min(minimumX, x); maximumX = Math.Max(maximumX, x);
                    minimumY = Math.Min(minimumY, y); maximumY = Math.Max(maximumY, y);
                }
            if (points < 16 || maximumX - minimumX < 20 || maximumY - minimumY < 20)
                throw new InvalidOperationException($"{mode} {id} has an empty or implausibly narrow directional hit region.");
            report.AppendLine($"{mode}: {id} shaped hit region; {points} two-pixel samples; span {maximumX - minimumX:0.0}×{maximumY - minimumY:0.0}.");
        }
        void CheckArtwork(string mode, string resource, bool shouldBeVisible)
        {
            var source = LiveSurface.ArtworkSource ?? throw new InvalidOperationException(mode + " has no decoded controller artwork.");
            var uri = LiveSurface.ArtworkResourceUri ?? throw new InvalidOperationException(mode + " has no artwork resource URI.");
            if (!uri.ToString().EndsWith(resource, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"{mode} loads the wrong artwork: {uri}.");
            var assembly = typeof(ControllerSurface).Assembly;
            string manifest = assembly.GetManifestResourceNames().Single(name => name.EndsWith(".g.resources", StringComparison.Ordinal));
            using (var stream = assembly.GetManifestResourceStream(manifest)!)
            using (var resources = new ResourceReader(stream))
                if (!resources.Cast<DictionaryEntry>().Any(entry => string.Equals(entry.Key as string, resource, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException(mode + " artwork is not embedded in the application.");
            var embedded = Application.GetResourceStream(uri) ?? throw new InvalidOperationException(mode + " embedded artwork cannot be opened.");
            using (embedded.Stream)
            {
                var decoded = BitmapDecoder.Create(embedded.Stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
                var original = source is CroppedBitmap cropped ? cropped.Source : source;
                if (original.PixelWidth != decoded.PixelWidth || original.PixelHeight != decoded.PixelHeight || source.PixelWidth < 768 || source.PixelHeight < 256)
                    throw new InvalidOperationException($"{mode} artwork is missing, downsampled or too small: {source.PixelWidth}×{source.PixelHeight}.");
            }
            bool enoughPixels = LiveSurface.ArtworkSupportsDpi(VisualTreeHelper.GetDpi(LiveSurface));
            if (LiveSurface.ArtworkVisible != (shouldBeVisible && enoughPixels))
                throw new InvalidOperationException(mode + " does not use artwork in regular themes and independent vector controls in high contrast.");
            var bounds = LiveSurface.ArtworkRenderedBounds;
            if (shouldBeVisible && enoughPixels)
            {
                if (bounds.IsEmpty || bounds.Width < 360 || bounds.Height < 120 || bounds.Left < -.5 || bounds.Top < -.5 ||
                    bounds.Right > LiveSurface.ActualWidth + .5 || bounds.Bottom > LiveSurface.ActualHeight + .5)
                    throw new InvalidOperationException($"{mode} artwork is tiny or clipped in the desktop hero: {bounds}; surface {LiveSurface.RenderSize}.");
                double intrinsicRatio = source.PixelWidth / (double)source.PixelHeight;
                if (Math.Abs(bounds.Width / bounds.Height / intrinsicRatio - 1) > .01)
                    throw new InvalidOperationException(mode + " stretches the controller artwork instead of preserving its aspect ratio.");
                var dpi = VisualTreeHelper.GetDpi(LiveSurface);
                if (bounds.Width * dpi.DpiScaleX > source.PixelWidth + 1 || bounds.Height * dpi.DpiScaleY > source.PixelHeight + 1)
                    throw new InvalidOperationException(mode + " artwork is upscaled past its intrinsic resolution at the current display scale.");
                var pixels = Pixels(LiveSurface);
                var colours = new HashSet<int>();
                for (int i = 0; i + 3 < pixels.Length; i += 16)
                    if (pixels[i + 3] > 0) colours.Add((pixels[i] >> 3) | ((pixels[i + 1] >> 3) << 5) | ((pixels[i + 2] >> 3) << 10));
                if (colours.Count < 64) throw new InvalidOperationException(mode + " rendered surface is blank or missing the shaded controller image.");
            }
            if (shouldBeVisible && !enoughPixels && !bounds.IsEmpty)
                throw new InvalidOperationException(mode + " retained blurry bitmap artwork instead of vector controls.");
            artworkReport.AppendLine($"{mode}: embedded {resource}; {source.PixelWidth}×{source.PixelHeight}; visible {LiveSurface.ArtworkVisible}; rendered bounds {bounds}; vector fallback for display resolution {shouldBeVisible && !enoughPixels}.");
        }
        void CheckParts(string mode, int count)
        {
            if (LiveSurface.Parts.Count != count) throw new InvalidOperationException($"Unexpected {mode} input count.");
            var bounds = new List<(string Id, Rect Rect, ControllerPartButton Part)>();
            foreach (var (id, part) in LiveSurface.Parts)
            {
                var peer = Peer(part);
                if (peer.GetAutomationControlType() != AutomationControlType.Button || peer.GetPattern(PatternInterface.Invoke) == null ||
                    string.IsNullOrWhiteSpace(peer.GetName()) || !peer.GetName().Contains(", ", StringComparison.Ordinal) ||
                    peer.GetAutomationId() != "ControllerPart_" + id || AutomationProperties.GetLiveSetting(part) != AutomationLiveSetting.Off)
                    throw new InvalidOperationException($"{mode} {id} does not expose a named input/action Button with Invoke.");
                if (part.ActualWidth < 43.99 || part.ActualHeight < 43.99)
                    throw new InvalidOperationException($"{mode} {id} has a target smaller than 44 logical pixels.");
                var rectangle = Bounds(LiveSurface, part);
                var renderedPart = (ControllerPartButton)part;
                if (Direction(id)) CheckDirectionHitRegion(id, renderedPart, mode);
                foreach (var previous in bounds)
                {
                    var intersection = Rect.Intersect(previous.Rect, rectangle);
                    if (intersection.IsEmpty || intersection.Width <= .01 || intersection.Height <= .01) continue;
                    if (!Direction(id) && !Direction(previous.Id))
                        throw new InvalidOperationException($"{mode} input targets overlap: {previous.Id} and {id}.");
                    // A compact D-pad can expose overlapping UIA bounding boxes, but its
                    // independently focusable direction buttons must never share a click.
                    for (double y = intersection.Top + .5; y < intersection.Bottom; y++)
                        for (double x = intersection.Left + .5; x < intersection.Right; x++)
                            if (renderedPart.ContainsHitPoint(new Point(x - rectangle.X, y - rectangle.Y)) &&
                                previous.Part.ContainsHitPoint(new Point(x - previous.Rect.X, y - previous.Rect.Y)))
                                throw new InvalidOperationException($"{mode} input click regions overlap: {previous.Id} and {id} at {x:0.0},{y:0.0}.");
                }
                bounds.Add((id, rectangle, renderedPart));
                report.AppendLine($"{mode}: {id}; {part.ActualWidth:0.0} × {part.ActualHeight:0.0}; {peer.GetName()}; {peer.GetItemStatus()}");
            }
            if (AccessViewport.ScrollableWidth > 1) throw new InvalidOperationException("Controller surface forces horizontal scrolling on the whole page.");
        }

        try
        {
            Topmost = true; Activate(); WindowState = WindowState.Normal; Width = 1440; Height = 900;
            _settings = new Settings { ShowControllerLabels = false, InterfaceScale = 1, ReduceInterfaceMotion = false, ControllerType = ControllerPreference.Standard };
            UpdateAccessibleViewport(); SetPortsView(false); SelectPage(0); SetPreview(default); DrainInvocations();
            CheckParts("Standard", 19);
            CheckArtwork("Standard", "Assets/Controllers/" + (_app.LightTheme ? "standard-front.png" : "standard-front-dark.png"), !_app.ContrastActive && !SystemParameters.HighContrast);
            var standardHeroSize = LiveSurface.RenderSize;
            var standardArtwork = LiveSurface.ArtworkRenderedBounds;
            if (LiveSurface.GetPart("Share") != null) throw new InvalidOperationException("Xbox One surface contains a Share button.");
            var xPart = (ControllerPartButton)LiveSurface.GetPart("X")!;
            var neutralX = Pixels(xPart);
            InvokePart("X"); PageHeading.Focus(); DrainInvocations();
            var selectedX = Pixels(xPart);
            if (!xPart.ConfigurationSelected || selectedX.SequenceEqual(neutralX) || xPart.HardwareActive)
                throw new InvalidOperationException("Configuration selection has no independent visible state.");
            if (!xPart.Focus()) throw new InvalidOperationException("Controller part cannot receive keyboard focus.");
            DrainInvocations();
            if (Pixels(xPart).SequenceEqual(selectedX) || !xPart.ConfigurationSelected || xPart.HardwareActive)
                throw new InvalidOperationException("Keyboard focus cannot be distinguished from configuration selection.");
            PageHeading.Focus(); SetPreview(new Gamepad(Buttons.X, 0, 0, 0, 0, 0, 0));
            if (!xPart.HardwareActive || !xPart.ConfigurationSelected || Pixels(xPart).SequenceEqual(selectedX))
                throw new InvalidOperationException("Physical press cannot be distinguished from selected state.");
            SetPreview(default);
            if (!Pixels(xPart).SequenceEqual(selectedX)) throw new InvalidOperationException("Released part did not immediately restore its selected drawing.");
            var partsBeforeLabels = LiveSurface.Parts.ToDictionary(entry => entry.Key, entry => entry.Value);
            _settings = _settings with { ShowControllerLabels = true }; SetPreview(default);
            if (!LiveSurface.ShowLabels || partsBeforeLabels.Any(entry => !ReferenceEquals(entry.Value, LiveSurface.GetPart(entry.Key))) || _selectedInput != "X")
                throw new InvalidOperationException("Optional labels rebuild controls or change configuration selection.");
            if (LiveSurface.ActualHeight < standardHeroSize.Height + 44)
                throw new InvalidOperationException("Optional full input labels do not reserve enough reachable space.");
            _settings = _settings with { ShowControllerLabels = false }; SetPreview(default);
            if (Math.Abs(LiveSurface.ActualHeight - standardHeroSize.Height) > .5 ||
                Math.Abs(LiveSurface.ArtworkRenderedBounds.Top - standardArtwork.Top) > .5)
                throw new InvalidOperationException("Hiding optional labels retains an enlarged or displaced controller hero.");
            InvokePart("A");
            string stableName = Peer(LiveSurface.GetPart("A")!).GetName();
            SetPreview(new Gamepad(Buttons.Y, 0, 0, 0, 0, 0, 0));
            if (_selectedInput != "A" || LiveSurface.SelectedInput != "A" || Peer(LiveSurface.GetPart("A")!).GetName() != stableName ||
                !((ControllerPartButton)LiveSurface.GetPart("Y")!).HardwareActive)
                throw new InvalidOperationException("Physical input changed configuration selection or its stable accessible name.");
            var selectedPart = LiveSurface.GetPart("A")!;
            if (!selectedPart.Focus() || !selectedPart.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)))
                throw new InvalidOperationException("Controller inputs cannot be reached and explored with keyboard focus.");
            DrainInvocations();
            if (_selectedInput != "A" || LiveSurface.SelectedInput != "A")
                throw new InvalidOperationException("Tab exploration changed the configured input.");
            SetPreview(default); PageHeading.Focus(); DrainInvocations();
            if (((ControllerPartButton)LiveSurface.GetPart("Y")!).HardwareActive || Peer(LiveSurface.GetPart("Y")!).GetItemStatus().Contains("Pressed", StringComparison.Ordinal))
                throw new InvalidOperationException("Released physical input retained its pressed state.");

            var stick = (ControllerPartButton)LiveSurface.GetPart("LeftAxis")!;
            var fixedSocket = Bounds(LiveSurface, stick); var centredPixels = Pixels(stick);
            SetPreview(new Gamepad(Buttons.None, 0, 0, 24000, -16000, 0, 0));
            bool unchangedPixels = Pixels(stick).SequenceEqual(centredPixels);
            var movedSocket = Bounds(LiveSurface, stick);
            if (stick.AxisPosition.X <= 0 || stick.AxisPosition.Y >= 0 || movedSocket != fixedSocket || unchangedPixels)
                throw new InvalidOperationException($"Stick cap did not move inside its stationary socket. Axis={stick.AxisPosition}; socket before={fixedSocket}; socket after={movedSocket}; unchanged pixels={unchangedPixels}.");
            SetPreview(default);
            if (stick.AxisPosition != default || stick.HardwareActive || !Pixels(stick).SequenceEqual(centredPixels))
                throw new InvalidOperationException("Stick did not return immediately to its neutral drawing.");
            var trigger = (ControllerPartButton)LiveSurface.GetPart("LeftTrigger")!;
            var releasedPixels = Pixels(trigger);
            SetPreview(new Gamepad(Buttons.None, 64, 0, 0, 0, 0, 0));
            double shallow = trigger.AnalogValue; var shallowPixels = Pixels(trigger);
            SetPreview(new Gamepad(Buttons.None, 192, 0, 0, 0, 0, 0));
            if (Math.Abs(shallow - 64 / 255d) > .0001 || Math.Abs(trigger.AnalogValue - 192 / 255d) > .0001 ||
                Pixels(trigger).SequenceEqual(shallowPixels))
                throw new InvalidOperationException("Trigger drawing does not show proportional analog input.");
            SetPreview(default);
            if (trigger.AnalogValue != 0 || trigger.HardwareActive || !Pixels(trigger).SequenceEqual(releasedPixels))
                throw new InvalidOperationException("Trigger retained a pressed state after release.");
            SetPreview(new Gamepad(Buttons.Up | Buttons.Right, 0, 0, 0, 0, 0, 0));
            if (!((ControllerPartButton)LiveSurface.GetPart("Up")!).HardwareActive ||
                !((ControllerPartButton)LiveSurface.GetPart("Right")!).HardwareActive ||
                ((ControllerPartButton)LiveSurface.GetPart("Down")!).HardwareActive || ((ControllerPartButton)LiveSurface.GetPart("Left")!).HardwareActive)
                throw new InvalidOperationException("The D-pad does not show both directions of a diagonal input truthfully.");
            SetPreview(new Gamepad(Buttons.A, 160, 120, 24000, -16000, -20000, 12000), false);
            if (LiveSurface.Parts.Values.OfType<ControllerPartButton>().Any(part => part.HardwareActive || part.AxisPosition != default || part.AnalogValue != 0))
                throw new InvalidOperationException("Disconnect retains a pressed or analog controller state.");
            _settings = _settings with { ReduceInterfaceMotion = true }; SetPreview(default);
            var aPart = (ControllerPartButton)LiveSurface.GetPart("A")!;
            var reducedNeutral = Pixels(aPart);
            SetPreview(new Gamepad(Buttons.A, 0, 0, 0, 0, 0, 0));
            if (!LiveSurface.ReducedMotion || !aPart.HardwareActive || Pixels(aPart).SequenceEqual(reducedNeutral))
                throw new InvalidOperationException("Reduced motion hides the active input state.");
            SetPreview(default);
            if (!Pixels(aPart).SequenceEqual(reducedNeutral)) throw new InvalidOperationException("Reduced-motion input fails to return immediately to neutral.");
            _settings = _settings with { ReduceInterfaceMotion = false }; SetPreview(default);
            InvokePart("Xbox");
            if (_presentedInputs["Xbox"].CanMap || !Peer(LiveSurface.GetPart("Xbox")!).GetName().Contains("Reserved by Windows", StringComparison.Ordinal) ||
                !Peer(LiveSurface.GetPart("Xbox")!).GetHelpText().Contains("not reported", StringComparison.Ordinal))
                throw new InvalidOperationException("Xbox button does not explain its Windows-reserved input limitation.");
            InvokePart("A"); PageHeading.Focus();

            var saved = new[] {
                new AdaptiveAccessory("A", "My foot switch", AccessoryKind.Pedal, [AccessorySignal.X]),
                new AdaptiveAccessory("X", "My hand switch", AccessoryKind.Switch, [AccessorySignal.B]) };
            foreach (string theme in new[] { "light", "dark", "contrast" })
            {
                _app.LightTheme = theme == "light"; _app.ApplyTheme(theme == "contrast");
                _settings = _settings with { ControllerType = ControllerPreference.Standard, AdaptiveAccessories = [] };
                SetPortsView(false); SetPreview(new Gamepad(Buttons.B, 64, 192, 14000, -9000, 0, 0));
                CheckArtwork("Standard " + theme, "Assets/Controllers/" + (_app.LightTheme ? "standard-front.png" : "standard-front-dark.png"), theme != "contrast" && !SystemParameters.HighContrast);
                Footer.Text = "Preview only · no Windows input sent"; CaptureRelay(output, "surface-standard-" + theme);
                _settings = _settings with { ControllerType = ControllerPreference.Adaptive, AdaptiveAccessories = saved };
                SetPreview(new Gamepad(Buttons.X, 0, 0, 0, 0, 0, 0)); UpdateLayout();
                CheckParts("Adaptive front " + theme, 10);
                CheckArtwork("Adaptive front " + theme, "Assets/Controllers/adaptive-front.png", theme != "contrast" && !SystemParameters.HighContrast);
                InvokePart("Profile");
                if (_presentedInputs["Profile"].CanMap || !Peer(LiveSurface.GetPart("Profile")!).GetHelpText().Contains("not reported", StringComparison.Ordinal))
                    throw new InvalidOperationException("Physical Profile button is incorrectly offered as reported or mappable input.");
                InvokePart("A"); PageHeading.Focus();
                Footer.Text = "Preview only · no Windows input sent"; CaptureRelay(output, "surface-adaptive-front-" + theme);
                var frontSize = LiveSurface.RenderSize;
                var frontArtwork = LiveSurface.ArtworkRenderedBounds;
                SetPortsView(true); UpdateLayout(); DrainInvocations();
                CheckParts("Adaptive ports " + theme, 21);
                CheckArtwork("Adaptive ports " + theme, "Assets/Controllers/adaptive-rear.png", theme != "contrast" && !SystemParameters.HighContrast);
                string aHelp = Peer(LiveSurface.GetPart("A")!).GetHelpText(), xHelp = Peer(LiveSurface.GetPart("X")!).GetHelpText();
                string bHelp = Peer(LiveSurface.GetPart("B")!).GetHelpText();
                if (!aHelp.Contains("My foot switch", StringComparison.Ordinal) || aHelp.Contains("My hand switch", StringComparison.Ordinal) ||
                    !xHelp.Contains("My hand switch", StringComparison.Ordinal) || xHelp.Contains("My foot switch", StringComparison.Ordinal) ||
                    bHelp.Contains("Saved accessory:", StringComparison.Ordinal) || !aHelp.Contains("connection is not reported", StringComparison.Ordinal))
                    throw new InvalidOperationException("Port accessory metadata does not match the exact saved physical port.");
                var aPort = (ControllerPartButton)LiveSurface.GetPart("A")!;
                if (!aPort.HardwareActive || ((ControllerPartButton)LiveSurface.GetPart("X")!).HardwareActive ||
                    !Peer(aPort).GetItemStatus().Contains("Matching logical input active", StringComparison.Ordinal) ||
                    !Peer(aPort).GetItemStatus().Contains("connection not reported", StringComparison.Ordinal))
                    throw new InvalidOperationException("Port activity incorrectly claims accessory connection or ignores its recorded logical signal.");
                Footer.Text = "Saved accessories · physical connection is not reported · preview only";
                CaptureRelay(output, "surface-adaptive-ports-" + theme);
                SetPortsView(false); UpdateLayout(); DrainInvocations();
                if (Math.Abs(LiveSurface.ActualHeight - frontSize.Height) > .5 ||
                    Math.Abs(LiveSurface.ArtworkRenderedBounds.Top - frontArtwork.Top) > .5)
                    throw new InvalidOperationException("Returning from the ports view retains an enlarged or displaced Adaptive front hero.");
                SetPortsView(true); UpdateLayout(); DrainInvocations();
            }
            SetPreview(default);
            if (LiveSurface.Parts.Values.OfType<ControllerPartButton>().Any(part => part.HardwareActive))
                throw new InvalidOperationException("Adaptive ports retained logical activity after release.");
            report.Insert(0, "PASS: independent named UIA Buttons and Invoke with stable IDs and no live announcement spam; at least 44-pixel bounding targets; rectangular targets do not overlap, and sampled directional D-pad click regions do not overlap. Hardware provides full-size input-list alternatives. Configuration selection, keyboard focus and physical activity have independent rendered states. Physical input and Tab focus preserve selection; label toggles preserve part instances; analog triggers/sticks and D-pad diagonals update truthfully; release and disconnect clear state; reduced motion keeps feedback visible. Reserved Xbox/Profile limitations, exact saved-port metadata and truthful logical activity remain. Nine 1440×900 previews cover standard, Adaptive front and Adaptive ports in light, dark and high contrast. No desktop output service or Windows input was used. Live screen-reader speech and physical hardware remain manual acceptance.\n\n");
            File.WriteAllText(Path.Combine(output, "controller-surfaces.txt"), report.ToString());
            artworkReport.Insert(0, "PASS: each surface loads its intended embedded controller bitmap at full intrinsic resolution. Regular themes render the bitmap only when its pixels cover the current display resolution; otherwise they retain sharp vector controls without shrinking input targets. High contrast uses vector controls. Image aesthetics, overlay alignment and physical hardware authenticity require visual review alongside these measurements.\n\n");
            File.WriteAllText(Path.Combine(output, "controller-artwork.txt"), artworkReport.ToString());
        }
        finally
        {
            _settings = originalSettings; _selectedInput = originalSelection; _app.LightTheme = originalLight; _app.ApplyTheme(originalContrast);
            SetPortsView(originalPorts); Width = originalWidth; Height = originalHeight; WindowState = originalWindowState;
            Topmost = originalTopmost; SelectPage(originalPage); UpdateAccessibleViewport(); RefreshStatus(); UpdateLayout();
        }
    }
}
