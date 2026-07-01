// RPMac - GUI (WPF, codigo puro, tema oscuro moderno)
// Copyright (C) 2026 golirt1 - GPL-2.0-only. Ver LICENSE y NOTICE.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace RPMac {

    public class MainWindow : Window {
        static Brush B(string hex) { return (Brush)new BrushConverter().ConvertFromString(hex); }
        // Paleta del tema: son brushes MUTABLES (mismo objeto, cambia .Color) para poder
        // cambiar de tema en vivo sin reconstruir la ventana.
        internal static readonly SolidColorBrush BG     = (SolidColorBrush)B("#1B1B1F");
        internal static readonly SolidColorBrush CARD   = (SolidColorBrush)B("#27272C");
        internal static readonly SolidColorBrush TXT    = (SolidColorBrush)B("#F2F2F4");
        internal static readonly SolidColorBrush SUB    = (SolidColorBrush)B("#8E8E96");
        internal static readonly SolidColorBrush ACCENT = (SolidColorBrush)B("#0A84FF");
        internal static readonly SolidColorBrush RED    = (SolidColorBrush)B("#FF453A");
        internal static readonly SolidColorBrush CHIP   = (SolidColorBrush)B("#37373E");
        internal static readonly SolidColorBrush BORDER = (SolidColorBrush)B("#3A3A42");
        internal static readonly SolidColorBrush BAR    = (SolidColorBrush)B("#202024"); // barra de estado
        internal static readonly SolidColorBrush OVBG   = (SolidColorBrush)B("#E61B1B1F"); // fondo del overlay (translucido)

        static void SetC(SolidColorBrush b, string hex) { b.Color = (Color)ColorConverter.ConvertFromString(hex); }
        static bool IsDark(string t) { return t != "light" && t != "japan"; }

        IntPtr hwnd = IntPtr.Zero;
        void SetTitleBar(bool dark) {
            try {
                if (hwnd == IntPtr.Zero) return;
                int on = dark ? 1 : 0;
                DwmSetWindowAttribute(hwnd, 20, ref on, 4); // dark mode
                DwmSetWindowAttribute(hwnd, 19, ref on, 4); // (build viejo de Win10)
            } catch { }
        }

        // Aplica una paleta cambiando el color de los brushes compartidos -> toda la UI
        // (ventana Y overlay) se repinta sola, porque comparten los mismos objetos brush.
        // Orden: BG, CARD, TXT, SUB, ACCENT, RED, CHIP, BORDER, BAR, OVBG
        void ApplyTheme(string name) {
            string[] p;
            switch (name) {
                case "light":  p = new[]{ "#F2F2F7","#FFFFFF","#1B1B1F","#6E6E73","#0A84FF","#FF3B30","#E5E5EA","#D1D1D6","#E8E8ED","#F2F2F2F7" }; break;
                case "nature": p = new[]{ "#14211A","#1E2E25","#EAF3EC","#8FB39B","#34C759","#FF6B57","#2A3D32","#34493C","#101A14","#E614211A" }; break;
                case "japan":  p = new[]{ "#F7F3EE","#FFFFFF","#1A1416","#8A7E78","#BC002D","#BC002D","#EFE7DE","#E0D5C8","#EFE7DE","#F2F7F3EE" }; break;
                default:       p = new[]{ "#1B1B1F","#27272C","#F2F2F4","#8E8E96","#0A84FF","#FF453A","#37373E","#3A3A42","#202024","#E61B1B1F" }; break; // dark
            }
            SetC(BG, p[0]); SetC(CARD, p[1]); SetC(TXT, p[2]); SetC(SUB, p[3]);
            SetC(ACCENT, p[4]); SetC(RED, p[5]); SetC(CHIP, p[6]); SetC(BORDER, p[7]); SetC(BAR, p[8]); SetC(OVBG, p[9]);
            SetTitleBar(IsDark(name));
        }

        // Sensor labels verified against VirtualSMC iStat.txt, KnownSMCKeys and applesmc.c.
        // TC0P is proximity (near socket) — NOT core temp; TC0D is the real die reading.
        // Dual-socket Mac Pro (4,1/5,1) uses TCAD/TCBD (die) and TCAH/TCBH (heatsink).
        // Only sensors present and plausible on this machine are shown, so extra entries
        // for other models (dual-CPU, extra GPUs) don't appear on single-CPU machines.
        static readonly string[][] CURATED = new string[][] {
            // CPU — single socket (most Intel Macs)
            new string[]{"TC0D","CPU (die)"},
            new string[]{"TC0H","CPU (heatsink)"},
            new string[]{"TC0P","CPU (proximity)"},   // near socket, runs hotter than die on some models
            new string[]{"TCXC","CPU (PECI)"},
            new string[]{"TCXc","CPU (PECI)"},
            new string[]{"TC0E","CPU"},
            new string[]{"TC0F","CPU"},
            // CPU — dual socket (Mac Pro 4,1 / 5,1, 2009-2012)
            new string[]{"TCAD","CPU A (die)"},
            new string[]{"TCAH","CPU A (heatsink)"},
            new string[]{"TCBD","CPU B (die)"},
            new string[]{"TCBH","CPU B (heatsink)"},
            // GPU
            new string[]{"TG0D","GPU 1 (die)"},
            new string[]{"TG0H","GPU 1 (heatsink)"},
            new string[]{"TG0P","GPU 1 (proximity)"},
            new string[]{"TG1D","GPU 2 (die)"},
            new string[]{"TG1H","GPU 2 (heatsink)"},
            new string[]{"TG1P","GPU 2 (proximity)"},
            new string[]{"TCGC","GPU (PECI)"},
            // System
            new string[]{"TM0P","Memory"},
            new string[]{"TM0S","Memory slot"},
            new string[]{"TM1P","Memory 2"},
            new string[]{"TA0P","Ambient"},
            new string[]{"TA1P","Ambient 2"},
            new string[]{"TPCD","Power (PCH)"},
            new string[]{"TH0P","Hard drive"},
            new string[]{"TN0H","Northbridge"},
            new string[]{"TI0P","Thunderbolt"},
            new string[]{"TB0T","Battery"},
            new string[]{"TW0P","Wi-Fi"},
        };

        // Estilos modernos (slider + scrollbar) cargados por XAML
        const string STYLES =
@"<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
  <Style TargetType=""Slider"">
    <Setter Property=""Template""><Setter.Value>
      <ControlTemplate TargetType=""Slider"">
        <Grid Height=""22"" VerticalAlignment=""Center"">
          <Border Height=""5"" CornerRadius=""2.5"" Background=""#3A3A42"" VerticalAlignment=""Center""/>
          <Track x:Name=""PART_Track"">
            <Track.DecreaseRepeatButton><RepeatButton Focusable=""False"" OverridesDefaultStyle=""True""><RepeatButton.Template><ControlTemplate TargetType=""RepeatButton""><Border Height=""5"" CornerRadius=""2.5"" Background=""#0A84FF"" VerticalAlignment=""Center""/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
            <Track.IncreaseRepeatButton><RepeatButton Focusable=""False"" OverridesDefaultStyle=""True""><RepeatButton.Template><ControlTemplate TargetType=""RepeatButton""><Border Background=""Transparent""/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>
            <Track.Thumb><Thumb OverridesDefaultStyle=""True""><Thumb.Template><ControlTemplate TargetType=""Thumb""><Ellipse Width=""16"" Height=""16"" Fill=""White""/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
          </Track>
        </Grid>
      </ControlTemplate>
    </Setter.Value></Setter>
  </Style>
  <Style TargetType=""ComboBox"">
    <Setter Property=""Foreground"" Value=""#EDEDED""/>
    <Setter Property=""Height"" Value=""30""/>
    <Setter Property=""Template""><Setter.Value>
      <ControlTemplate TargetType=""ComboBox"">
        <Grid>
          <ToggleButton Name=""ToggleButton"" Focusable=""false"" ClickMode=""Press"" OverridesDefaultStyle=""True""
              IsChecked=""{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}"">
            <ToggleButton.Template>
              <ControlTemplate TargetType=""ToggleButton"">
                <Border Background=""#2A2A32"" BorderBrush=""#3A3A42"" BorderThickness=""1"" CornerRadius=""8"">
                  <Path HorizontalAlignment=""Right"" VerticalAlignment=""Center"" Margin=""0,0,12,0""
                        Data=""M 0 0 L 8 0 L 4 5 Z"" Fill=""#EDEDED""/>
                </Border>
              </ControlTemplate>
            </ToggleButton.Template>
          </ToggleButton>
          <ContentPresenter IsHitTestVisible=""False"" Content=""{TemplateBinding SelectionBoxItem}""
              ContentTemplate=""{TemplateBinding SelectionBoxItemTemplate}""
              Margin=""12,0,28,0"" VerticalAlignment=""Center"" HorizontalAlignment=""Left""/>
          <Popup Name=""Popup"" Placement=""Bottom"" IsOpen=""{TemplateBinding IsDropDownOpen}""
                 AllowsTransparency=""True"" Focusable=""False"" PopupAnimation=""Slide"">
            <Border Background=""#2A2A32"" BorderBrush=""#3A3A42"" BorderThickness=""1"" CornerRadius=""8""
                    MinWidth=""{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}"" MaxHeight=""220"" Margin=""0,3,0,0"">
              <ScrollViewer><StackPanel IsItemsHost=""True""/></ScrollViewer>
            </Border>
          </Popup>
        </Grid>
      </ControlTemplate>
    </Setter.Value></Setter>
  </Style>
  <Style TargetType=""ComboBoxItem"">
    <Setter Property=""Foreground"" Value=""#EDEDED""/>
    <Setter Property=""Template""><Setter.Value>
      <ControlTemplate TargetType=""ComboBoxItem"">
        <Border Name=""Bd"" Background=""Transparent"" Padding=""12,8,12,8"" CornerRadius=""6"">
          <ContentPresenter/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property=""IsHighlighted"" Value=""True""><Setter TargetName=""Bd"" Property=""Background"" Value=""#0A84FF""/></Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value></Setter>
  </Style>
  <Style TargetType=""TextBox"">
    <Setter Property=""Foreground"" Value=""#EDEDED""/>
    <Setter Property=""CaretBrush"" Value=""#EDEDED""/>
    <Setter Property=""Background"" Value=""#2A2A32""/>
    <Setter Property=""BorderBrush"" Value=""#3A3A42""/>
    <Setter Property=""BorderThickness"" Value=""1""/>
    <Setter Property=""Padding"" Value=""9,7,9,7""/>
    <Setter Property=""VerticalContentAlignment"" Value=""Center""/>
    <Setter Property=""Template""><Setter.Value>
      <ControlTemplate TargetType=""TextBox"">
        <Border Background=""{TemplateBinding Background}"" BorderBrush=""{TemplateBinding BorderBrush}""
                BorderThickness=""{TemplateBinding BorderThickness}"" CornerRadius=""8"">
          <ScrollViewer x:Name=""PART_ContentHost"" Margin=""{TemplateBinding Padding}"" VerticalAlignment=""Center""/>
        </Border>
      </ControlTemplate>
    </Setter.Value></Setter>
  </Style>
  <Style TargetType=""ScrollBar"">
    <Setter Property=""Width"" Value=""10""/>
    <Setter Property=""Background"" Value=""Transparent""/>
    <Setter Property=""Template""><Setter.Value>
      <ControlTemplate TargetType=""ScrollBar"">
        <Grid Background=""Transparent"">
          <Track x:Name=""PART_Track"" IsDirectionReversed=""True"" Minimum=""{TemplateBinding Minimum}"" Maximum=""{TemplateBinding Maximum}"" Value=""{TemplateBinding Value}"" ViewportSize=""{TemplateBinding ViewportSize}"">
            <Track.DecreaseRepeatButton><RepeatButton Focusable=""False"" OverridesDefaultStyle=""True"" Command=""ScrollBar.PageUpCommand""><RepeatButton.Template><ControlTemplate TargetType=""RepeatButton""><Border Background=""Transparent""/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
            <Track.IncreaseRepeatButton><RepeatButton Focusable=""False"" OverridesDefaultStyle=""True"" Command=""ScrollBar.PageDownCommand""><RepeatButton.Template><ControlTemplate TargetType=""RepeatButton""><Border Background=""Transparent""/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>
            <Track.Thumb><Thumb OverridesDefaultStyle=""True"" MinHeight=""34""><Thumb.Template><ControlTemplate TargetType=""Thumb""><Border CornerRadius=""5"" Background=""#5AFFFFFF"" Margin=""2,0,2,0""/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
          </Track>
        </Grid>
      </ControlTemplate>
    </Setter.Value></Setter>
  </Style>
</ResourceDictionary>";

        class FanUi {
            public int Index;
            public double Max;
            public double Min;
            public TextBlock Rpm, Info, Mode;
            public Slider Slider;
            public TextBlock SliderVal;
            public Border Auto, MaxBtn, Manual, BarFill, Apply;
            public UIElement ManualRow;

            // Current mode: "auto" | "max" | "manual" | "curve". Tracked explicitly so
            // the refresh loop knows which fans to drive, without inspecting UI state.
            public volatile string CurMode = "auto";

            // Temperature-curve controls + cached values. The cached values are what the
            // refresh loop reads (UI controls are only touched on the UI thread).
            public Border CurveBtn, CurveApply;
            public UIElement CurveRow;
            public ComboBox CurveSensor;
            public Slider CtMinS, CtMaxS, CrMinS, CrMaxS;
            public TextBlock CtMinV, CtMaxV, CrMinV, CrMaxV;
            public string CurveSensorKey;
            public double CtMin = 40, CtMax = 80, CrMin, CrMax;
        }

        readonly List<FanUi> fans = new List<FanUi>();
        readonly Dictionary<string, TextBlock> curatedLabels = new Dictionary<string, TextBlock>();
        readonly Dictionary<string, TextBlock> allLabels = new Dictionary<string, TextBlock>();
        WrapPanel allPanel;
        Border allContainer;
        bool allLoaded = false;
        volatile bool showAll = false;
        TextBlock status;
        StackPanel presetChips;     // vertical list, one row per saved preset
        TextBox presetNameBox;      // name field for saving the current config
        TextBlock presetPlaceholder; // faux placeholder for the name field
        string activePreset;        // name of the preset currently applied (null = none / custom)
        System.Windows.Forms.ToolStripMenuItem trayPresetsItem;  // tray "Presets" submenu
        volatile bool running = true;
        const double BAR_W = 404;
        System.Windows.Forms.NotifyIcon tray;
        bool quitting = false;

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);

        public MainWindow() {
            Title = "RPMac";
            Width = 470; Height = 690;
            Background = BG; Foreground = TXT;
            FontFamily = new FontFamily("Segoe UI");
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);

            try { Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(STYLES)); } catch { }
            Settings.Load();
            ApplyTheme(Settings.Theme); // colorea la paleta antes de construir la UI

            // barra de titulo acorde al tema
            SourceInitialized += delegate {
                try {
                    hwnd = new WindowInteropHelper(this).Handle;
                    SetTitleBar(IsDark(Settings.Theme));
                } catch { }
            };

            var root = new DockPanel();
            Content = root;

            var header = new StackPanel { Margin = new Thickness(20, 18, 20, 6) };
            header.Children.Add(new TextBlock { Text = "RPMac", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = TXT });
            header.Children.Add(new TextBlock { Text = "The other app capable of controlling fans on Intel Macs in Windows — for free.", FontSize = 12, Foreground = SUB, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            var statusBar = new Border { Background = BAR, Child = (status = new TextBlock { Text = "Starting…", FontSize = 11, Foreground = SUB, Margin = new Thickness(20, 7, 20, 7) }) };
            DockPanel.SetDock(statusBar, Dock.Bottom);
            root.Children.Add(statusBar);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(14, 2, 8, 2) };
            var stack = new StackPanel();
            scroll.Content = stack;
            root.Children.Add(scroll);

            if (!Smc.IsInpOutDriverOpen())
                stack.Children.Add(Card(new TextBlock { Text = "Couldn't open the I/O driver (InpOut).\nRun the app as administrator.", Foreground = RED, TextWrapping = TextWrapping.Wrap }));

            // SALVAGUARDA: validar hardware Apple + coherencia del SMC antes de permitir escribir
            Smc.Validate();
            if (!Smc.WritesAllowed) {
                var warn = new StackPanel();
                warn.Children.Add(new TextBlock { Text = "⚠  Read-only mode", FontSize = 14, FontWeight = FontWeights.Bold, Foreground = B("#FFB340") });
                warn.Children.Add(new TextBlock { Text = Smc.SafetyReason, Foreground = TXT, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
                stack.Children.Add(Card(warn));
            }

            BuildFans(stack);
            BuildPresetsCard(stack);
            BuildTempsCard(stack);
            BuildSettingsCard(stack);

            Loaded += delegate {
                try {
                    SetupTray();
                    ApplySaved();
                    StartRefresh();
                    Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerChange;
                    if (Settings.Overlay) ShowOverlay();
                    if (Settings.StartMinimized) HideToTray();
                } catch (Exception ex) { App.LogError("Loaded", ex); }
            };
            StateChanged += delegate { if (WindowState == WindowState.Minimized) HideToTray(); };
            Closing += delegate (object s2, System.ComponentModel.CancelEventArgs e2) { if (!quitting) { e2.Cancel = true; HideToTray(); } };
            Closed += delegate { running = false; try { Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerChange; } catch { } };
        }

        Border Card(UIElement content) {
            return new Border {
                Background = CARD, CornerRadius = new CornerRadius(14),
                BorderBrush = BORDER, BorderThickness = new Thickness(1),
                Padding = new Thickness(18), Margin = new Thickness(6, 8, 6, 4),
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.35 },
                Child = content
            };
        }

        Border Chip(string text, Brush bg, Brush fg, MouseButtonEventHandler onClick) {
            var tb = new TextBlock { Text = text, Foreground = fg, FontSize = 13, FontWeight = FontWeights.SemiBold };
            var bd = new Border {
                Background = bg, CornerRadius = new CornerRadius(9),
                Padding = new Thickness(15, 8, 15, 8), Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand, Child = tb
            };
            bd.MouseEnter += delegate { bd.Opacity = 0.82; };
            bd.MouseLeave += delegate { bd.Opacity = 1.0; };
            bd.MouseLeftButtonUp += onClick;
            return bd;
        }

        bool Guard() {
            if (Smc.WritesAllowed) return true;
            status.Text = "Read-only on this hardware — " + Smc.SafetyReason;
            return false;
        }

        void SetMode(FanUi f, string mode) {
            f.CurMode = mode;
            f.Auto.Background = (mode == "auto") ? ACCENT : CHIP;
            f.MaxBtn.Background = (mode == "max") ? RED : CHIP;
            f.Manual.Background = (mode == "manual") ? ACCENT : CHIP;
            if (f.CurveBtn != null) f.CurveBtn.Background = (mode == "curve") ? ACCENT : CHIP;
            bool man = (mode == "manual");
            bool cur = (mode == "curve");
            f.Slider.IsEnabled = man;
            if (f.ManualRow != null) f.ManualRow.Visibility = man ? Visibility.Visible : Visibility.Collapsed;
            if (f.Apply != null) f.Apply.Visibility = man ? Visibility.Visible : Visibility.Collapsed;
            if (f.CurveRow != null) f.CurveRow.Visibility = cur ? Visibility.Visible : Visibility.Collapsed;
            if (f.CurveApply != null) f.CurveApply.Visibility = cur ? Visibility.Visible : Visibility.Collapsed;
            f.Mode.Text = mode == "auto" ? "Mode: automatic"
                        : mode == "max" ? "Mode: maximum"
                        : mode == "manual" ? "Mode: manual"
                        : "Mode: curve";
        }

        // Linear ramp: rpm_min below t_min, rpm_max above t_max, interpolated between.
        static double CurveRpm(double temp, double tMin, double tMax, double rMin, double rMax) {
            if (double.IsNaN(temp)) return rMin;
            if (tMax <= tMin) return rMin;
            if (temp <= tMin) return rMin;
            if (temp >= tMax) return rMax;
            return rMin + (rMax - rMin) * (temp - tMin) / (tMax - tMin);
        }

        // One labeled slider row used by the curve editor. Live-updates its value label.
        StackPanel CurveSliderRow(string label, Slider s, TextBlock val, string unit) {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
            row.Children.Add(new TextBlock { Text = label, Foreground = SUB, Width = 70, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 });
            s.Width = 200; s.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(s);
            val.Text = ((int)s.Value) + unit; val.Foreground = TXT; val.Width = 70;
            val.VerticalAlignment = VerticalAlignment.Center; val.Margin = new Thickness(10, 0, 0, 0);
            val.FontWeight = FontWeights.SemiBold; val.FontSize = 12;
            s.ValueChanged += delegate { val.Text = ((int)s.Value) + unit; };
            row.Children.Add(val);
            return row;
        }

        // Read the curve editor controls, validate, cache the values on the FanUi (so the
        // refresh loop can use them without touching UI), switch the fan to curve mode and
        // persist. The loop then drives the RPM each tick.
        void ApplyCurveFromUi(FanUi f) {
            double tmin = f.CtMinS.Value, tmax = f.CtMaxS.Value;
            double rmin = f.CrMinS.Value, rmax = f.CrMaxS.Value;
            if (tmax <= tmin) { status.Text = "Curve: max temp must be above min temp."; return; }
            if (rmax < rmin) { double t = rmin; rmin = rmax; rmax = t; }   // tolerate reversed RPM sliders
            string key = null;
            var item = f.CurveSensor.SelectedItem as ComboBoxItem;
            if (item != null) key = item.Tag as string;
            if (key == null) { status.Text = "Curve: pick a sensor first."; return; }
            f.CurveSensorKey = key; f.CtMin = tmin; f.CtMax = tmax; f.CrMin = rmin; f.CrMax = rmax;
            SetMode(f, "curve");
            Settings.SetFanCurve(f.Index, key, tmin, tmax, rmin, rmax);
            ClearActivePreset();
            status.Text = string.Format("Fan {0}: curve on · {1} {2:0}–{3:0}°C → {4:0}–{5:0} RPM",
                f.Index, key, tmin, tmax, rmin, rmax);
        }

        void BuildFans(Panel parent) {
            // Sensors offered in the curve dropdown: curated keys present with a plausible
            // reading (same criterion as the Temperatures card).
            var availSensors = new List<string[]>();
            foreach (var c in CURATED) {
                double v = Smc.ReadTemp(c[0]);
                if (!double.IsNaN(v) && v >= 5 && v <= 120) availSensors.Add(new[] { c[0], c[1] });
            }

            foreach (var fi in Smc.GetFans()) {
                double fmn = double.IsNaN(fi.Min) ? 0 : fi.Min;
                var f = new FanUi { Index = fi.Index, Max = double.IsNaN(fi.Max) ? 6000 : fi.Max, Min = fmn };
                var col = new StackPanel();
                col.Children.Add(new TextBlock { Text = "FAN " + fi.Index, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = SUB });

                // RPM grande + unidad
                var rpmRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                f.Rpm = new TextBlock { Text = "—", FontSize = 34, FontWeight = FontWeights.Bold, Foreground = TXT };
                rpmRow.Children.Add(f.Rpm);
                rpmRow.Children.Add(new TextBlock { Text = "RPM", FontSize = 13, Foreground = SUB, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(6, 0, 0, 7) });
                col.Children.Add(rpmRow);

                // barra visual de RPM
                var track = new Border { Background = CHIP, CornerRadius = new CornerRadius(4), Height = 8, Width = BAR_W, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 8) };
                f.BarFill = new Border { Background = ACCENT, CornerRadius = new CornerRadius(4), Height = 8, Width = 0, HorizontalAlignment = HorizontalAlignment.Left };
                track.Child = f.BarFill;
                col.Children.Add(track);

                f.Info = new TextBlock { Text = "", FontSize = 12, Foreground = SUB, Margin = new Thickness(0, 0, 0, 12) };
                col.Children.Add(f.Info);

                var chips = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
                f.Auto = Chip("Auto", CHIP, TXT, delegate { if (!Guard()) return; Smc.SetFanAuto(f.Index); SetMode(f, "auto"); Settings.SetFan(f.Index, "auto", 0); ClearActivePreset(); });
                f.MaxBtn = Chip("Max", CHIP, TXT, delegate { if (!Guard()) return; Smc.SetFanMax(f.Index); SetMode(f, "max"); Settings.SetFan(f.Index, "max", 0); ClearActivePreset(); });
                f.Manual = Chip("Manual", CHIP, TXT, delegate { if (!Guard()) return; SetMode(f, "manual"); });
                chips.Children.Add(f.Auto); chips.Children.Add(f.MaxBtn); chips.Children.Add(f.Manual);
                if (availSensors.Count > 0) {
                    f.CurveBtn = Chip("Curve", CHIP, TXT, delegate { if (!Guard()) return; SetMode(f, "curve"); });
                    chips.Children.Add(f.CurveBtn);
                }
                col.Children.Add(chips);

                var manualRow = new StackPanel { Orientation = Orientation.Horizontal };
                double mn = double.IsNaN(fi.Min) ? 0 : fi.Min;
                double tg = double.IsNaN(fi.Target) ? mn : fi.Target;
                f.Slider = new Slider { Minimum = mn, Maximum = f.Max, Value = tg, Width = 250, IsEnabled = false, VerticalAlignment = VerticalAlignment.Center };
                f.SliderVal = new TextBlock { Text = ((int)tg) + " RPM", Foreground = TXT, Width = 80, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), FontWeight = FontWeights.SemiBold };
                f.Slider.ValueChanged += delegate { f.SliderVal.Text = ((int)f.Slider.Value) + " RPM"; };
                manualRow.Children.Add(f.Slider);
                manualRow.Children.Add(f.SliderVal);
                col.Children.Add(manualRow);
                f.ManualRow = manualRow;

                var apply = Chip("Apply RPM", ACCENT, Brushes.White, delegate { if (!Guard()) return; Smc.SetFanRpm(f.Index, f.Slider.Value); SetMode(f, "manual"); Settings.SetFan(f.Index, "manual", (int)f.Slider.Value); ClearActivePreset(); });
                apply.Margin = new Thickness(0, 12, 0, 0);
                apply.HorizontalAlignment = HorizontalAlignment.Left;
                col.Children.Add(apply);
                f.Apply = apply;

                // ---- temperature curve controls (only if there are usable sensors) ----
                if (availSensors.Count > 0) {
                    var cv = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
                    var sensRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
                    sensRow.Children.Add(new TextBlock { Text = "Sensor", Foreground = SUB, Width = 70, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 });
                    f.CurveSensor = new ComboBox { Width = 200, VerticalAlignment = VerticalAlignment.Center };
                    foreach (var sg in availSensors) f.CurveSensor.Items.Add(new ComboBoxItem { Content = sg[1], Tag = sg[0] });
                    f.CurveSensor.SelectedIndex = 0;
                    sensRow.Children.Add(f.CurveSensor);
                    cv.Children.Add(sensRow);

                    f.CrMin = f.Min; f.CrMax = f.Max;
                    f.CtMinS = new Slider { Minimum = 0, Maximum = 110, Value = f.CtMin };
                    f.CtMaxS = new Slider { Minimum = 0, Maximum = 110, Value = f.CtMax };
                    f.CrMinS = new Slider { Minimum = f.Min, Maximum = f.Max, Value = f.Min };
                    f.CrMaxS = new Slider { Minimum = f.Min, Maximum = f.Max, Value = f.Max };
                    f.CtMinV = new TextBlock(); f.CtMaxV = new TextBlock(); f.CrMinV = new TextBlock(); f.CrMaxV = new TextBlock();
                    cv.Children.Add(CurveSliderRow("Temp min", f.CtMinS, f.CtMinV, " °C"));
                    cv.Children.Add(CurveSliderRow("Temp max", f.CtMaxS, f.CtMaxV, " °C"));
                    cv.Children.Add(CurveSliderRow("RPM min",  f.CrMinS, f.CrMinV, " RPM"));
                    cv.Children.Add(CurveSliderRow("RPM max",  f.CrMaxS, f.CrMaxV, " RPM"));
                    f.CurveRow = cv;
                    col.Children.Add(cv);

                    var fc = f;
                    var curveApply = Chip("Apply curve", ACCENT, Brushes.White, delegate { if (!Guard()) return; ApplyCurveFromUi(fc); });
                    curveApply.Margin = new Thickness(0, 10, 0, 0);
                    curveApply.HorizontalAlignment = HorizontalAlignment.Left;
                    f.CurveApply = curveApply;
                    col.Children.Add(curveApply);
                }

                if (!Smc.WritesAllowed) {
                    f.Auto.Opacity = 0.45; f.MaxBtn.Opacity = 0.45; f.Manual.Opacity = 0.45; apply.Opacity = 0.45;
                    if (f.CurveBtn != null) f.CurveBtn.Opacity = 0.45;
                    if (f.CurveApply != null) f.CurveApply.Opacity = 0.45;
                }

                f.Mode = new TextBlock { Text = "", FontSize = 11, Foreground = SUB, Margin = new Thickness(0, 10, 0, 0) };
                col.Children.Add(f.Mode);

                SetMode(f, fi.Forced ? "manual" : "auto");
                fans.Add(f);
                parent.Children.Add(Card(col));
            }
        }

        StackPanel TempRow(string name, string keyForLabel, Dictionary<string, TextBlock> store, double width) {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Width = width, Margin = new Thickness(0, 4, 0, 4) };
            row.Children.Add(new TextBlock { Text = name, Foreground = SUB, Width = width - 72, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 13 });
            var val = new TextBlock { Text = "—", Foreground = TXT, Width = 64, TextAlignment = TextAlignment.Right, FontWeight = FontWeights.SemiBold, FontSize = 13 };
            store[keyForLabel] = val;
            row.Children.Add(val);
            return row;
        }

        void BuildTempsCard(Panel parent) {
            var col = new StackPanel();
            col.Children.Add(new TextBlock { Text = "Temperatures", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = TXT, Margin = new Thickness(0, 0, 0, 10) });

            int shown = 0;
            foreach (var c in CURATED) {
                double v = Smc.ReadTemp(c[0]);
                if (double.IsNaN(v) || v < 5 || v > 120) continue;
                col.Children.Add(TempRow(c[1], c[0], curatedLabels, 404));
                shown++;
            }
            if (shown == 0) col.Children.Add(new TextBlock { Text = "No known sensors detected.", Foreground = SUB });

            var toggle = Chip("Show all sensors (raw)", CHIP, TXT, delegate { ToggleAll(); });
            toggle.Margin = new Thickness(0, 14, 0, 0);
            toggle.HorizontalAlignment = HorizontalAlignment.Left;
            col.Children.Add(toggle);

            allPanel = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            allContainer = new Border { Child = allPanel, Visibility = Visibility.Collapsed };
            col.Children.Add(allContainer);

            parent.Children.Add(Card(col));
        }

        void ToggleAll() {
            showAll = !showAll;
            allContainer.Visibility = showAll ? Visibility.Visible : Visibility.Collapsed;
            if (showAll && !allLoaded) {
                allLoaded = true;
                allPanel.Children.Add(new TextBlock { Text = "Detecting sensors… (raw, unverified list)", Foreground = SUB });
                new Thread(delegate () {
                    var keys = Smc.EnumTempKeys();
                    Dispatcher.Invoke((Action)delegate {
                        allPanel.Children.Clear();
                        foreach (var k in keys) allPanel.Children.Add(TempRow(k, k, allLabels, 200));
                    });
                }) { IsBackground = true }.Start();
            }
        }

        Border BuildToggle(bool initial, Action<bool> onChange) {
            var track = new Border { Width = 48, Height = 28, CornerRadius = new CornerRadius(14), Background = initial ? ACCENT : CHIP, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
            var knob = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Background = Brushes.White, HorizontalAlignment = initial ? HorizontalAlignment.Right : HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 3, 0) };
            track.Child = knob;
            bool state = initial;
            track.MouseLeftButtonUp += delegate {
                state = !state;
                track.Background = state ? ACCENT : CHIP;
                knob.HorizontalAlignment = state ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                onChange(state);
            };
            return track;
        }

        readonly Dictionary<string, Border> themeChips = new Dictionary<string, Border>();
        readonly Dictionary<string, TextBlock> themeChipLabels = new Dictionary<string, TextBlock>();

        // Etiqueta visible -> clave interna del tema
        static readonly string[][] THEMES = new string[][] {
            new string[]{ "dark",   "Dark"   },
            new string[]{ "light",  "Light"  },
            new string[]{ "nature", "Nature" },
            new string[]{ "japan",  "Japan"  },
        };

        void BuildThemeRow(Panel col) {
            col.Children.Add(new TextBlock { Text = "Theme", Foreground = TXT, FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 8) });
            var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var t in THEMES) {
                string key = t[0]; string label = t[1];
                var tb = new TextBlock { Text = label, FontSize = 13, FontWeight = FontWeights.SemiBold };
                var bd = new Border { CornerRadius = new CornerRadius(9), Padding = new Thickness(15, 8, 15, 8), Margin = new Thickness(0, 0, 8, 8), Cursor = Cursors.Hand, Child = tb };
                bd.MouseEnter += delegate { if (Settings.Theme != key) bd.Opacity = 0.82; };
                bd.MouseLeave += delegate { bd.Opacity = 1.0; };
                bd.MouseLeftButtonUp += delegate {
                    Settings.Theme = key; Settings.Save();
                    ApplyTheme(key);
                    SelectThemeChips();
                    status.Text = "Theme: " + label;
                };
                themeChips[key] = bd; themeChipLabels[key] = tb;
                wrap.Children.Add(bd);
            }
            col.Children.Add(wrap);
            SelectThemeChips();
        }

        void SelectThemeChips() {
            foreach (var kv in themeChips) {
                bool sel = (kv.Key == Settings.Theme);
                kv.Value.Background = sel ? ACCENT : CHIP;
                themeChipLabels[kv.Key].Foreground = sel ? Brushes.White : TXT;
            }
        }

        void BuildSettingsCard(Panel parent) {
            var col = new StackPanel();
            col.Children.Add(new TextBlock { Text = "Settings", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = TXT, Margin = new Thickness(0, 0, 0, 12) });

            var row = new DockPanel { LastChildFill = true };
            var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            labels.Children.Add(new TextBlock { Text = "Start with Windows", Foreground = TXT, FontSize = 13, FontWeight = FontWeights.SemiBold });
            labels.Children.Add(new TextBlock { Text = "Automatically opens the app at sign-in.", Foreground = SUB, FontSize = 11, Margin = new Thickness(0, 2, 0, 0) });

            string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
            bool enabled = false; try { enabled = Startup.IsEnabled(); } catch { }
            var toggle = BuildToggle(enabled, delegate (bool on) {
                try {
                    if (on) Startup.Enable(exe); else Startup.Disable();
                    status.Text = on ? "Start with Windows: enabled" : "Start with Windows: disabled";
                } catch (Exception ex) { status.Text = "Error: " + ex.Message; }
            });
            DockPanel.SetDock(toggle, Dock.Right);
            row.Children.Add(toggle);
            row.Children.Add(labels);
            col.Children.Add(row);

            var row2 = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 14, 0, 0) };
            var labels2 = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            labels2.Children.Add(new TextBlock { Text = "Start minimized to tray", Foreground = TXT, FontSize = 13, FontWeight = FontWeights.SemiBold });
            labels2.Children.Add(new TextBlock { Text = "Launch hidden in the system tray (next to the clock).", Foreground = SUB, FontSize = 11, Margin = new Thickness(0, 2, 0, 0) });
            var toggle2 = BuildToggle(Settings.StartMinimized, delegate (bool on) {
                Settings.StartMinimized = on; Settings.Save();
                status.Text = on ? "Start minimized: on" : "Start minimized: off";
            });
            DockPanel.SetDock(toggle2, Dock.Right);
            row2.Children.Add(toggle2);
            row2.Children.Add(labels2);
            col.Children.Add(row2);

            var row3 = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 14, 0, 0) };
            var labels3 = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            labels3.Children.Add(new TextBlock { Text = "Show temperatures in °F", Foreground = TXT, FontSize = 13, FontWeight = FontWeights.SemiBold });
            labels3.Children.Add(new TextBlock { Text = "Display temperatures in Fahrenheit instead of Celsius.", Foreground = SUB, FontSize = 11, Margin = new Thickness(0, 2, 0, 0) });
            var toggle3 = BuildToggle(Settings.Fahrenheit, delegate (bool on) {
                Settings.Fahrenheit = on; Settings.Save();
                ReformatTemps();
                status.Text = on ? "Temperatures: °F" : "Temperatures: °C";
            });
            DockPanel.SetDock(toggle3, Dock.Right);
            row3.Children.Add(toggle3);
            row3.Children.Add(labels3);
            col.Children.Add(row3);

            var row4 = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 14, 0, 0) };
            var labels4 = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            labels4.Children.Add(new TextBlock { Text = "On-screen overlay", Foreground = TXT, FontSize = 13, FontWeight = FontWeights.SemiBold });
            labels4.Children.Add(new TextBlock { Text = "Show fan RPM and temperatures on top of everything (top-right corner).", Foreground = SUB, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            var toggle4 = BuildToggle(Settings.Overlay, delegate (bool on) {
                Settings.Overlay = on; Settings.Save();
                if (on) ShowOverlay(); else HideOverlay();
                status.Text = on ? "Overlay: on" : "Overlay: off";
            });
            DockPanel.SetDock(toggle4, Dock.Right);
            row4.Children.Add(toggle4);
            row4.Children.Add(labels4);
            col.Children.Add(row4);

            BuildOverlayOptions(col);
            BuildThemeRow(col);

            parent.Children.Add(Card(col));
        }

        void BuildOverlayOptions(Panel col) {
            // --- Orientacion ---
            col.Children.Add(new TextBlock { Text = "Overlay layout", Foreground = TXT, FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 8) });
            var orient = new WrapPanel { Orientation = Orientation.Horizontal };
            var vtb = new TextBlock { Text = "Vertical", FontSize = 13, FontWeight = FontWeights.SemiBold };
            var htb = new TextBlock { Text = "Horizontal", FontSize = 13, FontWeight = FontWeights.SemiBold };
            var vbd = new Border { CornerRadius = new CornerRadius(9), Padding = new Thickness(15, 8, 15, 8), Margin = new Thickness(0, 0, 8, 8), Cursor = Cursors.Hand, Child = vtb };
            var hbd = new Border { CornerRadius = new CornerRadius(9), Padding = new Thickness(15, 8, 15, 8), Margin = new Thickness(0, 0, 8, 8), Cursor = Cursors.Hand, Child = htb };
            Action paintOrient = delegate {
                bool h = Settings.OverlayHorizontal;
                vbd.Background = h ? CHIP : ACCENT; vtb.Foreground = h ? TXT : Brushes.White;
                hbd.Background = h ? ACCENT : CHIP; htb.Foreground = h ? Brushes.White : TXT;
            };
            vbd.MouseLeftButtonUp += delegate { Settings.OverlayHorizontal = false; Settings.Save(); if (overlay != null) { overlay.SetHorizontal(false); overlay.Reposition(); } paintOrient(); };
            hbd.MouseLeftButtonUp += delegate { Settings.OverlayHorizontal = true; Settings.Save(); if (overlay != null) { overlay.SetHorizontal(true); overlay.Reposition(); } paintOrient(); };
            orient.Children.Add(vbd); orient.Children.Add(hbd);
            paintOrient();
            col.Children.Add(orient);

            // --- Que mostrar (ventiladores + sensores presentes) ---
            var items = new List<string[]>();
            foreach (var f in fans) items.Add(new[] { "fan" + f.Index, "Fan " + f.Index });
            foreach (var c in CURATED) if (curatedLabels.ContainsKey(c[0])) items.Add(new[] { c[0], c[1] });
            if (items.Count == 0) return;

            col.Children.Add(new TextBlock { Text = "Show in overlay", Foreground = TXT, FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 8) });
            var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var it in items) {
                string key = it[0]; string label = it[1];
                var tb = new TextBlock { Text = label, FontSize = 13, FontWeight = FontWeights.SemiBold };
                var bd = new Border { CornerRadius = new CornerRadius(9), Padding = new Thickness(13, 7, 13, 7), Margin = new Thickness(0, 0, 8, 8), Cursor = Cursors.Hand, Child = tb };
                Action paint = delegate { bool on = OverlaySel(key); bd.Background = on ? ACCENT : CHIP; tb.Foreground = on ? Brushes.White : TXT; };
                bd.MouseLeftButtonUp += delegate {
                    if (Settings.OverlayItems == null) { Settings.OverlayItems = new HashSet<string>(); foreach (var x in items) Settings.OverlayItems.Add(x[0]); }
                    if (Settings.OverlayItems.Contains(key)) Settings.OverlayItems.Remove(key); else Settings.OverlayItems.Add(key);
                    Settings.Save(); paint(); RefreshOverlayNow();
                };
                paint();
                wrap.Children.Add(bd);
            }
            col.Children.Add(wrap);
        }

        // ---- System tray ----
        void SetupTray() {
            try {
                tray = new System.Windows.Forms.NotifyIcon();
                tray.Icon = MakeIcon();
                tray.Text = "RPMac";
                tray.Visible = true;
                tray.DoubleClick += delegate { ShowFromTray(); };
                var menu = new System.Windows.Forms.ContextMenuStrip();
                menu.Items.Add("Open", null, delegate { ShowFromTray(); });
                trayPresetsItem = new System.Windows.Forms.ToolStripMenuItem("Presets");
                menu.Items.Add(trayPresetsItem);
                menu.Items.Add("Quit", null, delegate { QuitApp(); });
                tray.ContextMenuStrip = menu;
                UpdateTrayPresets();
            } catch { }
        }

        // Rebuild the tray "Presets" submenu so you can switch presets without opening the window.
        void UpdateTrayPresets() {
            if (trayPresetsItem == null) return;
            try {
                trayPresetsItem.DropDownItems.Clear();
                if (Settings.Presets.Count == 0) {
                    var none = new System.Windows.Forms.ToolStripMenuItem("(no presets yet)") { Enabled = false };
                    trayPresetsItem.DropDownItems.Add(none);
                    return;
                }
                foreach (var name in Settings.Presets.Keys) {
                    string n = name;
                    var item = new System.Windows.Forms.ToolStripMenuItem(n) { Checked = (n == activePreset) };
                    item.Click += delegate { if (Guard()) ApplyPreset(n); };
                    trayPresetsItem.DropDownItems.Add(item);
                }
            } catch { }
        }
        void ShowFromTray() { ForceShow(); }
        // Bring the window back and to the front. Public so a second launch (via the
        // single-instance guard in App.Main) can surface the already-running window.
        public void ForceShow() {
            try {
                Show();
                WindowState = WindowState.Normal;
                ShowInTaskbar = true;
                Activate();
                Topmost = true; Topmost = false;   // pop to front without staying on top
                Focus();
            } catch { }
        }
        // Hide to the tray — but if the tray icon couldn't be created, minimize instead of
        // vanishing, so the window can never become unreachable.
        void HideToTray() {
            if (tray == null) { WindowState = WindowState.Minimized; return; }
            Hide(); ShowInTaskbar = false;
        }
        void QuitApp() {
            quitting = true;
            try { if (tray != null) tray.Visible = false; } catch { }
            running = false;
            Smc.Cleanup();
            System.Windows.Application.Current.Shutdown();
        }
        System.Drawing.Icon MakeIcon() {
            try {
                var bmp = new System.Drawing.Bitmap(32, 32);
                using (var g = System.Drawing.Graphics.FromImage(bmp)) {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(System.Drawing.Color.Transparent);
                    using (var br = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(10, 132, 255)))
                        g.FillEllipse(br, 1, 1, 30, 30);
                    using (var f = new System.Drawing.Font("Segoe UI", 13, System.Drawing.FontStyle.Bold))
                    using (var wb = new System.Drawing.SolidBrush(System.Drawing.Color.White)) {
                        var sf = new System.Drawing.StringFormat {
                            Alignment = System.Drawing.StringAlignment.Center,
                            LineAlignment = System.Drawing.StringAlignment.Center
                        };
                        g.DrawString("R", f, wb, new System.Drawing.RectangleF(0, 0, 32, 32), sf);
                    }
                }
                return System.Drawing.Icon.FromHandle(bmp.GetHicon());
            } catch { return System.Drawing.SystemIcons.Application; }
        }

        // Aplica al abrir la última configuración guardada (si es seguro escribir)
        void ApplySaved() {
            if (!Smc.WritesAllowed) return;
            foreach (var f in fans) {
                if (!Settings.Fans.ContainsKey(f.Index)) continue;
                ApplyFanState(f, Settings.Fans[f.Index]);
            }
        }

        // Apply one saved fan state ([mode, rpm] or the curve form) to a fan: sets the SMC,
        // updates the UI controls and switches the mode. Shared by startup restore and presets.
        void ApplyFanState(FanUi f, string[] s) {
            if (s == null || s.Length < 1) return;
            string mode = s[0]; int rpm = 0; if (s.Length > 1) int.TryParse(s[1], out rpm);
            try {
                if (mode == "max") { Smc.SetFanMax(f.Index); SetMode(f, "max"); }
                else if (mode == "manual") { Smc.SetFanRpm(f.Index, rpm); f.Slider.Value = rpm; SetMode(f, "manual"); }
                else if (mode == "curve" && s.Length >= 7 && f.CurveSensor != null) {
                    double tmin, tmax, rmin, rmax;
                    double.TryParse(s[3], out tmin); double.TryParse(s[4], out tmax);
                    double.TryParse(s[5], out rmin); double.TryParse(s[6], out rmax);
                    f.CurveSensorKey = s[2]; f.CtMin = tmin; f.CtMax = tmax; f.CrMin = rmin; f.CrMax = rmax;
                    f.CtMinS.Value = tmin; f.CtMaxS.Value = tmax; f.CrMinS.Value = rmin; f.CrMaxS.Value = rmax;
                    foreach (var obj in f.CurveSensor.Items) {
                        var it = obj as ComboBoxItem;
                        if (it != null && (it.Tag as string) == f.CurveSensorKey) { f.CurveSensor.SelectedItem = it; break; }
                    }
                    SetMode(f, "curve");   // the refresh loop will start driving it
                }
                else { Smc.SetFanAuto(f.Index); SetMode(f, "auto"); }
            } catch { }
        }

        // Snapshot a fan's current mode + parameters into the saved-string form.
        string[] FanStateToArray(FanUi f) {
            switch (f.CurMode) {
                case "max":    return new[] { "max", "0" };
                case "manual": return new[] { "manual", ((int)f.Slider.Value).ToString() };
                case "curve":  return new[] { "curve", "0", f.CurveSensorKey ?? "",
                                   ((int)f.CtMin).ToString(), ((int)f.CtMax).ToString(),
                                   ((int)f.CrMin).ToString(), ((int)f.CrMax).ToString() };
                default:       return new[] { "auto", "0" };
            }
        }

        // ---- Presets: a named snapshot of every fan's mode + parameters ----
        void BuildPresetsCard(Panel parent) {
            if (fans.Count == 0) return;   // nothing to save on read-only hardware
            var col = new StackPanel();
            col.Children.Add(new TextBlock { Text = "Presets", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = TXT, Margin = new Thickness(0, 0, 0, 4) });
            col.Children.Add(new TextBlock { Text = "Save your current fan setup as a profile and switch with one click.", FontSize = 12, Foreground = SUB, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });

            presetChips = new StackPanel();   // vertical list of profile rows
            col.Children.Add(presetChips);

            // ---- "save current" area, visually separated from the list ----
            var saveWrap = new Border {
                Background = CHIP, CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 10, 12, 12), Margin = new Thickness(0, 6, 0, 0)
            };
            var saveCol = new StackPanel();
            saveCol.Children.Add(new TextBlock { Text = "SAVE CURRENT SETUP", FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = SUB, Margin = new Thickness(2, 0, 0, 7) });

            var saveRow = new StackPanel { Orientation = Orientation.Horizontal };
            // name field with a faux placeholder
            var nameHost = new Grid { Width = 210, VerticalAlignment = VerticalAlignment.Center };
            presetNameBox = new TextBox { VerticalAlignment = VerticalAlignment.Center, Background = BG };
            presetPlaceholder = new TextBlock { Text = "Profile name (e.g. Gaming)", Foreground = SUB, FontSize = 12.5, Margin = new Thickness(11, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
            presetNameBox.TextChanged += delegate { presetPlaceholder.Visibility = string.IsNullOrEmpty(presetNameBox.Text) ? Visibility.Visible : Visibility.Collapsed; };
            presetNameBox.KeyDown += delegate (object s, System.Windows.Input.KeyEventArgs e) {
                if (e.Key == System.Windows.Input.Key.Enter && Guard()) SaveCurrentAsPreset(presetNameBox.Text);
            };
            nameHost.Children.Add(presetNameBox);
            nameHost.Children.Add(presetPlaceholder);

            var saveBtn = Chip("Save", ACCENT, Brushes.White, delegate { if (!Guard()) return; SaveCurrentAsPreset(presetNameBox.Text); });
            saveBtn.Margin = new Thickness(10, 0, 0, 0);
            saveRow.Children.Add(nameHost);
            saveRow.Children.Add(saveBtn);
            saveCol.Children.Add(saveRow);
            saveWrap.Child = saveCol;
            col.Children.Add(saveWrap);

            if (!Smc.WritesAllowed) { presetNameBox.IsEnabled = false; saveBtn.Opacity = 0.45; saveWrap.Opacity = 0.6; }

            parent.Children.Add(Card(col));
            RebuildPresetChips();
        }

        void RebuildPresetChips() {
            if (presetChips == null) return;
            presetChips.Children.Clear();
            if (Settings.Presets.Count == 0) {
                presetChips.Children.Add(new TextBlock { Text = "No profiles yet. Set up your fans below and save one.", Foreground = SUB, FontSize = 12, Margin = new Thickness(2, 2, 0, 10), FontStyle = FontStyles.Italic });
                return;
            }
            foreach (var name in Settings.Presets.Keys) presetChips.Children.Add(PresetRow(name));
        }

        // A full-width profile row: name + a one-line summary of what it does, an Apply button
        // and a delete (×). The active profile is highlighted with an accent border.
        Border PresetRow(string name) {
            bool active = (name == activePreset);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // left: name (+ active dot) and summary line
            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var nameLine = new StackPanel { Orientation = Orientation.Horizontal };
            if (active) nameLine.Children.Add(new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(3.5), Background = ACCENT, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) });
            nameLine.Children.Add(new TextBlock { Text = name, Foreground = TXT, FontSize = 14, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            left.Children.Add(nameLine);
            left.Children.Add(new TextBlock { Text = PresetSummary(name, "  ·  "), Foreground = SUB, FontSize = 11.5, Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            // right: Apply + delete
            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            var applyBtn = new Border {
                Background = active ? ACCENT : BG, CornerRadius = new CornerRadius(7),
                Padding = new Thickness(14, 7, 14, 7), Cursor = Cursors.Hand,
                Child = new TextBlock { Text = active ? "Active" : "Apply", Foreground = active ? Brushes.White : TXT, FontSize = 12.5, FontWeight = FontWeights.SemiBold }
            };
            applyBtn.MouseEnter += delegate { applyBtn.Opacity = 0.82; };
            applyBtn.MouseLeave += delegate { applyBtn.Opacity = 1.0; };
            applyBtn.MouseLeftButtonUp += delegate { if (!Guard()) return; ApplyPreset(name); };
            var delBtn = new TextBlock { Text = "✕", Foreground = SUB, FontSize = 13, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 4, 0) };
            delBtn.MouseEnter += delegate { delBtn.Foreground = RED; };
            delBtn.MouseLeave += delegate { delBtn.Foreground = SUB; };
            delBtn.MouseLeftButtonUp += delegate { DeletePreset(name); };
            actions.Children.Add(applyBtn);
            actions.Children.Add(delBtn);
            Grid.SetColumn(actions, 1);
            grid.Children.Add(actions);

            var bd = new Border {
                Background = CHIP,
                BorderBrush = active ? ACCENT : BORDER,
                BorderThickness = new Thickness(active ? 1.4 : 1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 11, 12, 11),
                Margin = new Thickness(0, 0, 0, 8),
                Child = grid
            };
            if (!Smc.WritesAllowed) bd.Opacity = 0.45;
            return bd;
        }

        // One-line-per-fan summary of a preset, joined with the given separator.
        string PresetSummary(string name, string sep) {
            Dictionary<int, string[]> p;
            if (!Settings.Presets.TryGetValue(name, out p)) return name;
            var parts = new List<string>();
            foreach (var kv in p) {
                var s = kv.Value;
                string desc;
                switch (s.Length > 0 ? s[0] : "auto") {
                    case "max":    desc = "Max"; break;
                    case "manual": desc = (s.Length > 1 ? s[1] : "?") + " RPM"; break;
                    case "curve":  desc = s.Length >= 7 ? ("Curve " + s[2] + " " + s[3] + "-" + s[4] + "°C") : "Curve"; break;
                    default:       desc = "Auto"; break;
                }
                parts.Add("Fan " + kv.Key + " " + desc);
            }
            return string.Join(sep, parts.ToArray());
        }

        void ApplyPreset(string name) {
            Dictionary<int, string[]> preset;
            if (!Settings.Presets.TryGetValue(name, out preset)) return;
            foreach (var f in fans) {
                string[] s;
                if (!preset.TryGetValue(f.Index, out s)) continue;
                ApplyFanState(f, s);
                Settings.Fans[f.Index] = s;   // also becomes the current saved state
            }
            Settings.Save();
            activePreset = name;
            RebuildPresetChips();             // highlight the active one
            UpdateTrayPresets();
            status.Text = "Applied preset: " + name;
        }

        void SaveCurrentAsPreset(string name) {
            name = (name ?? "").Trim().Replace("|", " ");   // '|' is the config separator
            if (name == "") { status.Text = "Type a name for the preset first."; return; }
            var snap = new Dictionary<int, string[]>();
            foreach (var f in fans) snap[f.Index] = FanStateToArray(f);
            Settings.Presets[name] = snap;
            Settings.Save();
            presetNameBox.Text = "";
            activePreset = name;              // the just-saved config is now the active preset
            RebuildPresetChips();
            UpdateTrayPresets();
            status.Text = "Saved preset: " + name;
        }

        void DeletePreset(string name) {
            if (Settings.Presets.Remove(name)) {
                if (activePreset == name) activePreset = null;
                Settings.Save();
                RebuildPresetChips();
                UpdateTrayPresets();
                status.Text = "Deleted preset: " + name;
            }
        }

        // The user changed a fan by hand, so no saved preset matches anymore — clear the highlight.
        void ClearActivePreset() {
            if (activePreset == null) return;
            activePreset = null;
            RebuildPresetChips();
            UpdateTrayPresets();
        }

        // Al reanudar de suspension/hibernacion el SMC suele perder el modo forzado
        // (los ventiladores vuelven a automatico sin avisar). Reaplicamos la config guardada.
        void OnPowerChange(object s, Microsoft.Win32.PowerModeChangedEventArgs e) {
            if (e.Mode != Microsoft.Win32.PowerModes.Resume) return;
            new Thread(delegate () {
                Thread.Sleep(3000); // dar tiempo a que el SMC se estabilice tras reanudar
                try { Dispatcher.Invoke((Action)delegate {
                    if (!running) return;
                    ApplySaved();
                    status.Text = "Resumed — settings reapplied · " + DateTime.Now.ToString("HH:mm:ss");
                }); } catch { }
            }) { IsBackground = true }.Start();
        }

        void StartRefresh() {
            new Thread(delegate () {
                while (running) {
                    try {
                        var infos = Smc.GetFans();
                        var curated = new Dictionary<string, double>();
                        foreach (var k in new List<string>(curatedLabels.Keys)) curated[k] = Smc.ReadTemp(k);
                        Dictionary<string, double> all = null;
                        if (showAll) { all = new Dictionary<string, double>(); foreach (var k in new List<string>(allLabels.Keys)) all[k] = Smc.ReadTemp(k); }

                        // Drive any fan in curve mode. Runs on this background thread using the
                        // cached curve values (set on the UI thread); only touches fans whose
                        // CurMode is "curve", so auto/max/manual fans are never affected.
                        if (Smc.WritesAllowed) {
                            foreach (var f in fans) {
                                if (f.CurMode != "curve" || f.CurveSensorKey == null) continue;
                                double t = Smc.ReadTemp(f.CurveSensorKey);
                                if (double.IsNaN(t)) continue;
                                Smc.SetFanRpm(f.Index, CurveRpm(t, f.CtMin, f.CtMax, f.CrMin, f.CrMax));
                            }
                        }

                        Dispatcher.Invoke((Action)delegate {
                            foreach (var fi in infos) {
                                if (fi.Index >= fans.Count) continue;
                                var f = fans[fi.Index];
                                if (!double.IsNaN(fi.Actual)) {
                                    f.Rpm.Text = ((int)fi.Actual).ToString();
                                    double frac = (f.Max > 0) ? fi.Actual / f.Max : 0;
                                    if (frac < 0) frac = 0; if (frac > 1) frac = 1;
                                    f.BarFill.Width = BAR_W * frac;
                                    f.BarFill.Background = (frac > 0.9) ? RED : ACCENT;
                                }
                                f.Info.Text = string.Format("min {0:0} · max {1:0} · target {2:0} · {3}",
                                    fi.Min, fi.Max, fi.Target, fi.Forced ? "forced" : "auto");
                            }
                            UpdateTemps(curated, curatedLabels);
                            if (all != null) UpdateTemps(all, allLabels);
                            UpdateOverlay(infos, curated);
                            status.Text = "Driver OK · updated " + DateTime.Now.ToString("HH:mm:ss");
                        });
                    } catch { }
                    Thread.Sleep(2000);
                }
            }) { IsBackground = true }.Start();
        }

        void UpdateTemps(Dictionary<string, double> vals, Dictionary<string, TextBlock> labels) {
            foreach (var kv in vals) {
                TextBlock t;
                if (!double.IsNaN(kv.Value) && labels.TryGetValue(kv.Key, out t)) {
                    t.Tag = kv.Value;            // guardamos el valor crudo en °C para poder reformatear
                    t.Text = FormatTemp(kv.Value);
                }
            }
        }

        // El SMC siempre entrega °C; convertimos solo al mostrar segun la preferencia.
        static string FormatTemp(double c) {
            return Settings.Fahrenheit
                ? string.Format("{0:0.0} °F", c * 9.0 / 5.0 + 32.0)
                : string.Format("{0:0.0} °C", c);
        }

        // Reformatea las etiquetas ya visibles al cambiar C<->F (sin esperar al refresco).
        void ReformatTemps() {
            foreach (var t in curatedLabels.Values) if (t.Tag is double) t.Text = FormatTemp((double)t.Tag);
            foreach (var t in allLabels.Values) if (t.Tag is double) t.Text = FormatTemp((double)t.Tag);
        }

        // ---- Overlay en pantalla (estilo FRAPS, esquina superior derecha) ----
        Overlay overlay;
        void ShowOverlay() {
            if (overlay == null) overlay = new Overlay();
            overlay.Horizontal = Settings.OverlayHorizontal;
            overlay.Show();
            RefreshOverlayNow();
            overlay.Reposition();
            overlay.BringTopmost();
        }
        void HideOverlay() { if (overlay != null) overlay.Hide(); }

        // ¿Mostrar este item en el overlay? (null = todo)
        static bool OverlaySel(string key) {
            return Settings.OverlayItems == null || Settings.OverlayItems.Contains(key);
        }

        List<FanInfo> lastInfos;
        Dictionary<string, double> lastCurated;

        // Solo muestra sensores curados PRESENTES y con lectura plausible (los mismos de la
        // ventana): los valores salen tal cual del SMC, asi que son lecturas reales.
        void UpdateOverlay(List<FanInfo> infos, Dictionary<string, double> curated) {
            lastInfos = infos; lastCurated = curated;
            if (overlay == null || !overlay.IsVisible) return;
            var rows = new List<string[]>();
            foreach (var fi in infos)
                if (!double.IsNaN(fi.Actual) && OverlaySel("fan" + fi.Index))
                    rows.Add(new[] { "Fan " + fi.Index, ((int)fi.Actual) + " RPM" });
            foreach (var c in CURATED) {
                double v;
                if (curatedLabels.ContainsKey(c[0]) && OverlaySel(c[0]) && curated.TryGetValue(c[0], out v) && !double.IsNaN(v))
                    rows.Add(new[] { c[1], FormatTemp(v) });
            }
            overlay.Update(rows);
        }

        // Refresca el overlay al instante (al cambiar selección/orientación, sin esperar 2 s).
        void RefreshOverlayNow() {
            if (lastInfos != null && lastCurated != null) UpdateOverlay(lastInfos, lastCurated);
        }
    }

    // Ventana sin bordes, siempre encima y "click-through" (los clics la atraviesan).
    // Muestra RPM/temperaturas sobre cualquier app o juego, como FRAPS.
    // Usa los brushes compartidos del tema, asi que cambia de color con el tema en vivo.
    public class Overlay : Window {
        readonly StackPanel panel;
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TRANSPARENT = 0x20, WS_EX_LAYERED = 0x80000, WS_EX_TOOLWINDOW = 0x80;
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;

        // Re-assert top-most z-order. Borderless/windowed games often push their own
        // window to the top and cover the overlay; calling this each refresh keeps us above
        // them without stealing focus. (Exclusive-fullscreen games can't be drawn over by
        // any window-based overlay — use borderless/windowed mode for those.)
        public void BringTopmost() {
            try {
                IntPtr h = new WindowInteropHelper(this).Handle;
                if (h != IntPtr.Zero) SetWindowPos(h, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            } catch { }
        }

        public bool Horizontal = false;
        List<string[]> lastRows = new List<string[]>();
        readonly Border card;

        static DropShadowEffect Shadow() {
            return new DropShadowEffect { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 0, Opacity = 0.55 };
        }

        public Overlay() {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;            // no roba el foco al juego/app
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            FontFamily = new FontFamily("Segoe UI");

            panel = new StackPanel();

            card = new Border {
                Background = MainWindow.OVBG,
                BorderBrush = MainWindow.ACCENT,
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 10, 14, 11),
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.45 },
                Margin = new Thickness(10),     // hueco para que se vea la sombra
                Child = panel
            };
            Content = card;

            SourceInitialized += delegate {
                try {
                    IntPtr h = new WindowInteropHelper(this).Handle;
                    int ex = GetWindowLong(h, GWL_EXSTYLE);
                    SetWindowLong(h, GWL_EXSTYLE, ex | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW);
                } catch { }
            };
            SizeChanged += delegate { Reposition(); };
            Loaded += delegate { Reposition(); };
        }

        public void SetHorizontal(bool h) { Horizontal = h; Render(lastRows); }

        public void Reposition() {
            var wa = SystemParameters.WorkArea;          // siempre arriba a la derecha
            Left = wa.Right - ActualWidth + 2;
            Top = wa.Top + 2;
        }

        TextBlock Label(string t, double size) { return new TextBlock { Text = t, Foreground = MainWindow.SUB, FontSize = size, Effect = Shadow(), VerticalAlignment = VerticalAlignment.Center }; }
        TextBlock Value(string t, double size) { return new TextBlock { Text = t, Foreground = MainWindow.TXT, FontSize = size, FontWeight = FontWeights.SemiBold, Effect = Shadow(), VerticalAlignment = VerticalAlignment.Center }; }

        // rows: cada item es { etiqueta, valor }
        public void Update(List<string[]> rows) { lastRows = rows; Render(rows); BringTopmost(); }

        void Render(List<string[]> rows) {
            panel.Orientation = Horizontal ? Orientation.Horizontal : Orientation.Vertical;
            panel.MinWidth = Horizontal ? 0 : 128;
            panel.Children.Clear();

            // El modo horizontal es mas compacto (fuente menor, menos padding).
            card.Padding = Horizontal ? new Thickness(10, 5, 10, 6) : new Thickness(12, 8, 12, 9);
            double lblSize = Horizontal ? 10.5 : 12;
            double valSize = Horizontal ? 11.5 : 13;
            double dotSize = Horizontal ? 6 : 7;

            // cabecera: punto de acento (+ "RPMac" solo en vertical, para no ocupar de mas)
            var head = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = Horizontal ? new Thickness(0, 0, 12, 0) : new Thickness(0, 0, 0, 6) };
            head.Children.Add(new Border { Width = dotSize, Height = dotSize, CornerRadius = new CornerRadius(dotSize / 2), Background = MainWindow.ACCENT, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, Horizontal ? 0.0 : 7.0, 0) });
            if (!Horizontal) head.Children.Add(new TextBlock { Text = "RPMac", Foreground = MainWindow.TXT, FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Effect = Shadow() });
            panel.Children.Add(head);

            foreach (var r in rows) {
                if (Horizontal) {
                    var item = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 11, 0) };
                    var lbl = Label(r[0], lblSize); lbl.Margin = new Thickness(0, 0, 5, 0);
                    item.Children.Add(lbl);
                    item.Children.Add(Value(r[1], valSize));
                    panel.Children.Add(item);
                } else {
                    var line = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 1, 0, 1) };
                    var lbl = Label(r[0], lblSize); lbl.Margin = new Thickness(0, 0, 18, 0);
                    var val = Value(r[1], valSize);
                    DockPanel.SetDock(lbl, Dock.Left);
                    DockPanel.SetDock(val, Dock.Right);
                    line.Children.Add(lbl);
                    line.Children.Add(val);
                    panel.Children.Add(line);
                }
            }
        }
    }

    // Guarda/lee la configuración (modo por ventilador + iniciar minimizado) en %APPDATA%\RPMac
    static class Settings {
        static readonly string Dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RPMac");
        static readonly string FilePath = System.IO.Path.Combine(Dir, "config.txt");
        public static Dictionary<int, string[]> Fans = new Dictionary<int, string[]>();
        // preset name -> (fan index -> saved state), same state form as Fans
        public static Dictionary<string, Dictionary<int, string[]>> Presets = new Dictionary<string, Dictionary<int, string[]>>();
        public static bool StartMinimized = false;
        public static bool Fahrenheit = false;
        public static bool Overlay = false;
        public static bool OverlayHorizontal = false;
        public static HashSet<string> OverlayItems = null; // null = mostrar todo
        public static string Theme = "dark";

        public static void Load() {
            try {
                if (!System.IO.File.Exists(FilePath)) return;
                foreach (var line in System.IO.File.ReadAllLines(FilePath)) {
                    var s = line.Split('|');
                    if (s.Length >= 2 && s[0] == "min") StartMinimized = (s[1] == "1");
                    else if (s.Length >= 2 && s[0] == "tempf") Fahrenheit = (s[1] == "1");
                    else if (s.Length >= 2 && s[0] == "overlay") Overlay = (s[1] == "1");
                    else if (s.Length >= 2 && s[0] == "ovorient") OverlayHorizontal = (s[1] == "h");
                    else if (s.Length >= 2 && s[0] == "ovsel") OverlayItems = new HashSet<string>(s[1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                    else if (s.Length >= 2 && s[0] == "theme") Theme = s[1];
                    else if (s.Length >= 4 && s[0] == "fan") {
                        int idx;
                        if (int.TryParse(s[1], out idx)) {
                            // store everything after the index: [mode, rpm] or
                            // [curve, 0, sensor, tmin, tmax, rmin, rmax]
                            var arr = new string[s.Length - 2];
                            Array.Copy(s, 2, arr, 0, s.Length - 2);
                            Fans[idx] = arr;
                        }
                    }
                    // preset|<name>|<fanIndex>|<mode>|<rpm>[|curve fields...]
                    else if (s.Length >= 5 && s[0] == "preset") {
                        string pname = s[1];
                        int idx;
                        if (int.TryParse(s[2], out idx)) {
                            if (!Presets.ContainsKey(pname)) Presets[pname] = new Dictionary<int, string[]>();
                            var arr = new string[s.Length - 3];
                            Array.Copy(s, 3, arr, 0, s.Length - 3);
                            Presets[pname][idx] = arr;
                        }
                    }
                }
            } catch { }
        }
        public static void Save() {
            try {
                if (!System.IO.Directory.Exists(Dir)) System.IO.Directory.CreateDirectory(Dir);
                var lines = new List<string>();
                lines.Add("min|" + (StartMinimized ? "1" : "0"));
                lines.Add("tempf|" + (Fahrenheit ? "1" : "0"));
                lines.Add("overlay|" + (Overlay ? "1" : "0"));
                lines.Add("ovorient|" + (OverlayHorizontal ? "h" : "v"));
                if (OverlayItems != null) lines.Add("ovsel|" + string.Join(",", new List<string>(OverlayItems).ToArray()));
                lines.Add("theme|" + Theme);
                foreach (var kv in Fans) lines.Add("fan|" + kv.Key + "|" + string.Join("|", kv.Value));
                foreach (var p in Presets)
                    foreach (var kv in p.Value)
                        lines.Add("preset|" + p.Key + "|" + kv.Key + "|" + string.Join("|", kv.Value));
                System.IO.File.WriteAllLines(FilePath, lines.ToArray());
            } catch { }
        }
        public static void SetFan(int idx, string mode, int rpm) { Fans[idx] = new string[] { mode, rpm.ToString() }; Save(); }
        public static void SetFanCurve(int idx, string sensor, double tmin, double tmax, double rmin, double rmax) {
            Fans[idx] = new string[] { "curve", "0", sensor,
                ((int)tmin).ToString(), ((int)tmax).ToString(), ((int)rmin).ToString(), ((int)rmax).ToString() };
            Save();
        }
    }

    static class Startup {
        const string TASK = "RPMac";
        static int Run(string args) {
            var psi = new System.Diagnostics.ProcessStartInfo("schtasks.exe", args) {
                CreateNoWindow = true, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            var p = System.Diagnostics.Process.Start(psi);
            p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd();
            p.WaitForExit();
            return p.ExitCode;
        }
        public static bool IsEnabled() { return Run("/query /tn " + TASK) == 0; }
        public static void Enable(string exe) { Run("/create /tn " + TASK + " /tr \"\\\"" + exe + "\\\"\" /sc onlogon /rl highest /f"); }
        public static void Disable() { Run("/delete /tn " + TASK + " /f"); }
    }

    public class App {
        static Mutex mutex;

        [STAThread]
        public static void Main() {
            // Single instance: if RPMac is already running, ask that instance to surface its
            // window and exit — so a second launch never spawns a hidden duplicate fighting
            // over the SMC. (This is exactly what a user does when the window seems "gone".)
            bool createdNew;
            mutex = new Mutex(true, "RPMac_singleton_v1", out createdNew);
            if (!createdNew) {
                try { EventWaitHandle.OpenExisting("RPMac_show_v1").Set(); } catch { }
                return;
            }
            var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "RPMac_show_v1");

            var app = new Application();

            // Never let a stray UI-thread exception kill the window: log it and keep running.
            app.DispatcherUnhandledException += delegate (object s, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) {
                LogError("UI", e.Exception);
                e.Handled = true;
            };

            var win = new MainWindow();

            // Wait for other launches to ping us, then bring the window to the front.
            var waiter = new Thread(delegate () {
                while (true) {
                    try { showEvent.WaitOne(); } catch { break; }
                    try { app.Dispatcher.BeginInvoke(new Action(delegate { win.ForceShow(); })); } catch { }
                }
            }) { IsBackground = true };
            waiter.Start();

            app.Run(win);
            GC.KeepAlive(mutex);
        }

        internal static void LogError(string where, Exception ex) {
            try {
                string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RPMac");
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "error.log"), DateTime.Now + " [" + where + "] " + ex + "\r\n\r\n");
            } catch { }
        }
    }
}
