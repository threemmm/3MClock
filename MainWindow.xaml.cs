using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ThreeMClock.Models;
using System.Runtime.InteropServices;

namespace ThreeMClock
{
    public partial class MainWindow : Window
    {
        private ClockSettings _settings;
        private readonly DispatcherTimer _clockTimer = new DispatcherTimer();
        private readonly DispatcherTimer _topmostTimer = new DispatcherTimer(); // Timer for the taskbar fix
        private System.Windows.Forms.NotifyIcon? _notifyIcon;

        // Win32 API for forcing topmost status
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);


        public MainWindow()
        {
            InitializeComponent();

            // Load settings in the constructor to ensure they're ready for use.
            _settings = ClockSettings.Load();

            // Configure timers
            _clockTimer.Interval = TimeSpan.FromMilliseconds(200);
            _clockTimer.Tick += (_, __) => UpdateClock();

            _topmostTimer.Interval = TimeSpan.FromMilliseconds(500);
            _topmostTimer.Tick += TopmostTimer_Tick;

            _notifyIcon = new System.Windows.Forms.NotifyIcon();

            // Display the application version automatically
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                VersionText.Text = $"v{version.Major}.{version.Minor}.{version.Build}";
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetupTrayIcon();
            if (_settings.Left != -1 && _settings.Top != -1)
            {
                this.Left = _settings.Left;
                this.Top = _settings.Top;
            }
            ApplySettings();
            _clockTimer.Start();
            UpdateClock(); // Call once to show time immediately
        }

        private void TopmostTimer_Tick(object? sender, EventArgs e)
        {
            // This timer continuously re-asserts the window's topmost status
            if (_settings.TopMostMode == "Above Taskbar")
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            }
        }

        private void ApplySettings()
        {
            switch (_settings.TopMostMode)
            {
                case "Above Taskbar":
                    Topmost = true;
                    _topmostTimer.Start();
                    break;
                case "Always on Top":
                    Topmost = true;
                    _topmostTimer.Stop();
                    break;
                case "Normal":
                default:
                    Topmost = false;
                    _topmostTimer.Stop();
                    break;
            }

            ClockRoot.Opacity = _settings.Opacity;
            ClockTextContainer.FontSize = _settings.FontSize;
            AmPmTextRun.FontSize = _settings.FontSize * 0.6; // This line ensures AM/PM text is smaller
            ClockTextContainer.FontFamily = new System.Windows.Media.FontFamily(_settings.FontFamily);

            var fontColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_settings.FontColor);
            ClockTextContainer.Foreground = new SolidColorBrush(fontColor);

            if (_settings.HasBackground)
            {
                BgBorder.Visibility = Visibility.Visible;
                var bgColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_settings.BackgroundColor);
                BgBorder.Background = new SolidColorBrush(bgColor);
            }
            else
            {
                BgBorder.Visibility = Visibility.Collapsed;
            }

            ClockTextContainer.Margin = new Thickness(_settings.BackgroundPaddingX, _settings.BackgroundPaddingY, _settings.BackgroundPaddingX, _settings.BackgroundPaddingY);

            switch (_settings.TextEffect)
            {
                case "Glow":
                    ClockTextContainer.Effect = new System.Windows.Media.Effects.DropShadowEffect { ShadowDepth = 0, BlurRadius = 8, Color = fontColor, Opacity = 0.9 };
                    break;
                case "None":
                    ClockTextContainer.Effect = null;
                    break;
                case "Shadow":
                default:
                    ClockTextContainer.Effect = new System.Windows.Media.Effects.DropShadowEffect { ShadowDepth = 0, BlurRadius = 4, Color = Colors.Black, Opacity = 0.7 };
                    break;
            }

            // Update UI controls
            foreach (ComboBoxItem item in TopMostComboBox.Items)
            {
                if (item.Content.ToString() == _settings.TopMostMode)
                {
                    TopMostComboBox.SelectedItem = item;
                    break;
                }
            }
            Use24HourCheck.IsChecked = _settings.Use24Hour;
            BackgroundCheck.IsChecked = _settings.HasBackground;
            BackgroundColorButton.IsEnabled = _settings.HasBackground;
            OpacitySlider.Value = _settings.Opacity;
            SizeSlider.Value = _settings.FontSize;
            BackgroundPaddingXSlider.Value = _settings.BackgroundPaddingX;
            BackgroundPaddingYSlider.Value = _settings.BackgroundPaddingY;

            foreach (ComboBoxItem item in FontComboBox.Items)
            {
                if (item.Content.ToString() == _settings.FontFamily)
                {
                    FontComboBox.SelectedItem = item;
                    break;
                }
            }
            foreach (ComboBoxItem item in TextEffectComboBox.Items)
            {
                if (item.Content.ToString() == _settings.TextEffect)
                {
                    TextEffectComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void Setting_Changed(object? sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            _settings.Use24Hour = Use24HourCheck.IsChecked ?? false;
            _settings.HasBackground = BackgroundCheck.IsChecked ?? false;

            ApplySettings();
            _settings.Save();
        }

        private void TopMostComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            if (TopMostComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                _settings.TopMostMode = selectedItem.Content.ToString() ?? "Normal";
                ApplySettings();
                _settings.Save();
            }
        }

        private void ResetButton_Click(object? sender, RoutedEventArgs e)
        {
            _settings = new ClockSettings();
            ApplySettings();
            _settings.Save();
        }

        private void UpdateClock()
        {
            var format = _settings.Use24Hour ? "HH:mm" : "h:mm";
            var amPmFormat = _settings.Use24Hour ? "" : " tt";

            TimeTextRun.Text = DateTime.Now.ToString(format, CultureInfo.CurrentCulture);

            if (string.IsNullOrEmpty(amPmFormat))
            {
                AmPmTextRun.Text = "";
            }
            else
            {
                AmPmTextRun.Text = DateTime.Now.ToString(amPmFormat, CultureInfo.CurrentCulture).ToUpper();
            }

            this.SizeToContent = SizeToContent.WidthAndHeight;
        }

        private void SetupTrayIcon()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = CreateIcon(),
                Text = "3M Clock",
                Visible = true
            };

            _notifyIcon.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            _notifyIcon.ContextMenuStrip.Items.Add("Settings", null, (s, e) => SettingsMenuItem_Click(null, null));
            _notifyIcon.ContextMenuStrip.Items.Add("Exit", null, (s, e) => Exit_Click(null, null));
            _notifyIcon.DoubleClick += (s, e) =>
            {
                this.Visibility = Visibility.Visible;
                this.Activate();
            };
        }

        private System.Drawing.Icon CreateIcon()
        {
            int size = 64;
            using (var bitmap = new System.Drawing.Bitmap(size, size))
            using (var g = System.Drawing.Graphics.FromImage(bitmap))
            {
                g.Clear(System.Drawing.Color.Transparent);

                using (var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(40, 40, 40)))
                {
                    g.FillEllipse(brush, 0, 0, size, size);
                }

                using (var pen = new System.Drawing.Pen(System.Drawing.Color.LightSkyBlue, 6))
                {
                    pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    g.DrawLine(pen, size / 2, size / 2, size / 2, 12);
                    g.DrawLine(pen, size / 2, size / 2, size - 16, size / 2);
                }

                return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _settings.Left = this.Left;
            _settings.Top = this.Top;
            _settings.Save();
            _notifyIcon?.Dispose();
            base.OnClosing(e);
        }

        private void SettingsMenuItem_Click(object? sender, RoutedEventArgs? e)
        {
            SettingsPopup.PlacementTarget = ClockRoot;
            SettingsPopup.IsOpen = true;
        }

        private void Exit_Click(object? sender, RoutedEventArgs? e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BackgroundColorButton_Click(object sender, RoutedEventArgs e)
        {
            var colorDialog = new System.Windows.Forms.ColorDialog();
            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var newColor = colorDialog.Color;
                _settings.BackgroundColor = System.Drawing.ColorTranslator.ToHtml(newColor);
                ApplySettings();
                _settings.Save();
            }
        }

        private void FontColorButton_Click(object sender, RoutedEventArgs e)
        {
            var colorDialog = new System.Windows.Forms.ColorDialog();
            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var newColor = colorDialog.Color;
                _settings.FontColor = System.Drawing.ColorTranslator.ToHtml(newColor);
                ApplySettings();
                _settings.Save();
            }
        }

        private void FontComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            if (FontComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                _settings.FontFamily = selectedItem.Content.ToString() ?? "Segoe UI Variable Display";
                ApplySettings();
                _settings.Save();
            }
        }

        private void TextEffectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            if (TextEffectComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                _settings.TextEffect = selectedItem.Content.ToString() ?? "Shadow";
                ApplySettings();
                _settings.Save();
            }
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded) return;
            _settings.Opacity = e.NewValue;
            ApplySettings();
            _settings.Save();
        }

        private void SizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded) return;
            _settings.FontSize = e.NewValue;
            ApplySettings();
            _settings.Save();
        }

        private void BackgroundPaddingXSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded) return;
            _settings.BackgroundPaddingX = e.NewValue;
            ApplySettings();
            _settings.Save();
        }

        private void BackgroundPaddingYSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded) return;
            _settings.BackgroundPaddingY = e.NewValue;
            ApplySettings();
            _settings.Save();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void CloseSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsPopup.IsOpen = false;
        }
    }
}

