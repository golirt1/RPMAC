// RPMac - GUI (WPF, codigo puro, tema oscuro moderno)
// Copyright (C) 2026 golirt1 - GPL-2.0-only. Ver LICENSE y NOTICE.

using System;
using System.Drawing.Drawing2D;
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
            // The style templates (ComboBox/TextBox/Slider/ScrollBar) read theme colors through
            // DynamicResource. WPF FREEZES a brush once it's referenced by a sealed style, so those
            // resource brushes can't be mutated like the ones above — instead we swap in fresh brushes
            // here, which makes every DynamicResource re-resolve to the new color. (The brushes above
            // stay mutable precisely because they are NOT registered as resources.)
            SetThemeResources();
            SetTitleBar(IsDark(name));
        }

        // Swap the DynamicResource theme brushes for fresh instances matching the current palette.
        // Must be new objects each call: a brush used by a sealed style gets frozen and can't change.
        void SetThemeResources() {
            Resources["ThemeText"]      = new SolidColorBrush(TXT.Color);
            Resources["ThemeControlBg"] = new SolidColorBrush(CHIP.Color);
            Resources["ThemeBorder"]    = new SolidColorBrush(BORDER.Color);
            Resources["ThemeAccent"]    = new SolidColorBrush(ACCENT.Color);
            Resources["ThemeSub"]       = new SolidColorBrush(SUB.Color);
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
          <Border Height=""5"" CornerRadius=""2.5"" Background=""{DynamicResource ThemeControlBg}"" VerticalAlignment=""Center""/>
          <Track x:Name=""PART_Track"">
            <Track.DecreaseRepeatButton><RepeatButton Focusable=""False"" OverridesDefaultStyle=""True""><RepeatButton.Template><ControlTemplate TargetType=""RepeatButton""><Border Height=""5"" CornerRadius=""2.5"" Background=""{DynamicResource ThemeAccent}"" VerticalAlignment=""Center""/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
            <Track.IncreaseRepeatButton><RepeatButton Focusable=""False"" OverridesDefaultStyle=""True""><RepeatButton.Template><ControlTemplate TargetType=""RepeatButton""><Border Background=""Transparent""/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>
            <Track.Thumb><Thumb OverridesDefaultStyle=""True""><Thumb.Template><ControlTemplate TargetType=""Thumb""><Ellipse Width=""16"" Height=""16"" Fill=""{DynamicResource ThemeText}""/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
          </Track>
        </Grid>
      </ControlTemplate>
    </Setter.Value></Setter>
  </Style>
  <Style TargetType=""ComboBox"">
    <Setter Property=""Foreground"" Value=""{DynamicResource ThemeText}""/>
    <Setter Property=""Height"" Value=""30""/>
    <Setter Property=""Template""><Setter.Value>
      <ControlTemplate TargetType=""ComboBox"">
        <Grid>
          <ToggleButton Name=""ToggleButton"" Focusable=""false"" ClickMode=""Press"" OverridesDefaultStyle=""True""
              IsChecked=""{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}"">
            <ToggleButton.Template>
              <ControlTemplate TargetType=""ToggleButton"">
                <Border Background=""{DynamicResource ThemeControlBg}"" BorderBrush=""{DynamicResource ThemeBorder}"" BorderThickness=""1"" CornerRadius=""8"">
                  <Path HorizontalAlignment=""Right"" VerticalAlignment=""Center"" Margin=""0,0,12,0""
                        Data=""M 0 0 L 8 0 L 4 5 Z"" Fill=""{DynamicResource ThemeText}""/>
                </Border>
              </ControlTemplate>
            </ToggleButton.Template>
          </ToggleButton>
          <ContentPresenter IsHitTestVisible=""False"" Content=""{TemplateBinding SelectionBoxItem}""
              ContentTemplate=""{TemplateBinding SelectionBoxItemTemplate}""
              Margin=""12,0,28,0"" VerticalAlignment=""Center"" HorizontalAlignment=""Left""/>
          <Popup Name=""Popup"" Placement=""Bottom"" IsOpen=""{TemplateBinding IsDropDownOpen}""
                 AllowsTransparency=""True"" Focusable=""False"" PopupAnimation=""Slide"">
            <Border Background=""{DynamicResource ThemeControlBg}"" BorderBrush=""{DynamicResource ThemeBorder}"" BorderThickness=""1"" CornerRadius=""8""
                    MinWidth=""{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}"" MaxHeight=""220"" Margin=""0,3,0,0"">
              <ScrollViewer><StackPanel IsItemsHost=""True""/></ScrollViewer>
            </Border>
          </Popup>
        </Grid>
      </ControlTemplate>
    </Setter.Value></Setter>
  </Style>
  <Style TargetType=""ComboBoxItem"">
    <Setter Property=""Foreground"" Value=""{DynamicResource ThemeText}""/>
    <Setter Property=""Template""><Setter.Value>
      <ControlTemplate TargetType=""ComboBoxItem"">
        <Border Name=""Bd"" Background=""Transparent"" Padding=""12,8,12,8"" CornerRadius=""6"">
          <ContentPresenter/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property=""IsHighlighted"" Value=""True""><Setter TargetName=""Bd"" Property=""Background"" Value=""{DynamicResource ThemeAccent}""/></Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value></Setter>
  </Style>
  <Style TargetType=""TextBox"">
    <Setter Property=""Foreground"" Value=""{DynamicResource ThemeText}""/>
    <Setter Property=""CaretBrush"" Value=""{DynamicResource ThemeText}""/>
    <Setter Property=""Background"" Value=""{DynamicResource ThemeControlBg}""/>
    <Setter Property=""BorderBrush"" Value=""{DynamicResource ThemeBorder}""/>
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
            <Track.Thumb><Thumb OverridesDefaultStyle=""True"" MinHeight=""34""><Thumb.Template><ControlTemplate TargetType=""Thumb""><Border CornerRadius=""5"" Background=""{DynamicResource ThemeSub}"" Margin=""2,0,2,0""/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
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
        ComboBox trayModeCombo;     // "Show in tray" dropdown
        System.Drawing.Icon staticIcon;  // cached default "R" icon
        IntPtr staticIconHandle = IntPtr.Zero;
        // Icon currently shown in temperature mode, plus the HICON behind it. Icon.FromHandle
        // does NOT take ownership of the handle (not even Dispose frees it), so we destroy it
        // ourselves once the tray no longer needs it — see SetTrayTempIcon.
        System.Drawing.Icon tempIcon;
        IntPtr tempIconHandle = IntPtr.Zero;
        int tempIconValue = int.MinValue;   // value the current icon was drawn for
        string tempIconTheme;               // theme the current icon was drawn with
        System.Windows.Forms.NotifyIcon tray;
        bool quitting = false;

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);

        public MainWindow() {
            Title = "RPMac";
            Width = 470; Height = 690;
            Background = BG; Foreground = TXT;
            FontFamily = new FontFamily("Segoe UI");
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);

            // The STYLES XAML (ComboBox/TextBox/Slider/ScrollBar templates) pull their colors from
            // named theme resources via DynamicResource. Those resource brushes are set (and re-set on
            // every theme change) by ApplyTheme -> SetThemeResources with FRESH instances, because a
            // brush referenced by a sealed style gets frozen and could never change afterwards. They are
            // deliberately kept separate from the mutable BG/CARD/TXT/... brushes used directly in code.
            try { Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(STYLES)); } catch { }
            Settings.Load();
            ApplyTheme(Settings.Theme); // colorea la paleta (y crea los recursos de tema) antes de construir la UI

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
                // Each step is isolated so one failure can't take down startup or hide the
                // window, and the log tells us exactly which step failed.
                try { SetupTray(); }    catch (Exception ex) { App.LogError("SetupTray", ex); }
                try { ApplySaved(); }   catch (Exception ex) { App.LogError("ApplySaved", ex); }
                try { StartRefresh(); } catch (Exception ex) { App.LogError("StartRefresh", ex); }
                try { Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerChange; } catch (Exception ex) { App.LogError("PowerHook", ex); }
                try { if (Settings.Overlay) ShowOverlay(); } catch (Exception ex) { App.LogError("Overlay", ex); }
                try { if (Settings.StartMinimized) HideToTray(); } catch (Exception ex) { App.LogError("HideToTray", ex); }
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

        // Highlight a mode chip: colored background + white label when active, so the text
        // stays readable on the accent/red fill in the light themes too (not just dark).
        static void SetChipActive(Border chip, bool active, Brush activeBg) {
            if (chip == null) return;
            chip.Background = active ? activeBg : CHIP;
            var tb = chip.Child as TextBlock;
            if (tb != null) tb.Foreground = active ? Brushes.White : TXT;
        }

        void SetMode(FanUi f, string mode) {
            f.CurMode = mode;
            SetChipActive(f.Auto, mode == "auto", ACCENT);
            SetChipActive(f.MaxBtn, mode == "max", RED);
            SetChipActive(f.Manual, mode == "manual", ACCENT);
            SetChipActive(f.CurveBtn, mode == "curve", ACCENT);
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

        // Special curve "sensor" that tracks the hottest curated temperature instead of a
        // single fixed sensor, so a fan ramps up when ANY sensor gets hot (e.g. CPU or GPU).
        const string HIGHEST_SENSOR = "__highest__";

        // Highest valid temperature among the curated sensors (NaN if none). Reused by the
        // curve loop; same rule as the tray "Highest Temp" mode.
        static double HighestCurated(Dictionary<string, double> curated) {
            double temp = double.NaN;
            if (curated != null)
                foreach (var kv in curated)
                    if (!double.IsNaN(kv.Value) && (double.IsNaN(temp) || kv.Value > temp))
                        temp = kv.Value;
            return temp;
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
                f.Index, (key == HIGHEST_SENSOR ? "highest temp" : key), tmin, tmax, rmin, rmax);
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
                    // "Highest temp" (max of all sensors) first, so a fan can react to whichever
                    // sensor is hottest — useful on iMacs where CPU or GPU can each spike.
                    f.CurveSensor.Items.Add(new ComboBoxItem { Content = "Highest temp (any sensor)", Tag = HIGHEST_SENSOR });
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
                    ApplyTrayMode(null); // refresh tray digit color for the new theme
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

            // ---- Show in tray (dropdown) ----
            var rowTray = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 14, 0, 0) };
            var labelsTray = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            labelsTray.Children.Add(new TextBlock { Text = "Show in tray", Foreground = TXT, FontSize = 13, FontWeight = FontWeights.SemiBold });
            labelsTray.Children.Add(new TextBlock { Text = "Choose what the tray icon displays — the app icon, nothing, or a live temperature.", Foreground = SUB, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            trayModeCombo = new ComboBox { Width = 170, VerticalAlignment = VerticalAlignment.Center };
            trayModeCombo.Items.Add(new ComboBoxItem { Content = "App Icon", Tag = "icon" });
            trayModeCombo.Items.Add(new ComboBoxItem { Content = "None", Tag = "none" });
            trayModeCombo.Items.Add(new ComboBoxItem { Content = "Highest Temp", Tag = "highest" });
            foreach (var c in CURATED) {
                double v = Smc.ReadTemp(c[0]);
                if (!double.IsNaN(v) && v >= 5 && v <= 120)
                    trayModeCombo.Items.Add(new ComboBoxItem { Content = c[1], Tag = c[0] });
            }
            // Select the saved tray mode
            bool found = false;
            foreach (var obj in trayModeCombo.Items) {
                var ci = obj as ComboBoxItem;
                if (ci != null && (ci.Tag as string) == Settings.TrayMode) { trayModeCombo.SelectedItem = ci; found = true; break; }
            }
            if (!found) trayModeCombo.SelectedIndex = 0;
            trayModeCombo.SelectionChanged += delegate {
                var sel = trayModeCombo.SelectedItem as ComboBoxItem;
                if (sel != null) {
                    Settings.TrayMode = sel.Tag as string ?? "icon";
                    Settings.Save();
                    ApplyTrayMode(null);
                    status.Text = "Show in tray: " + (sel.Content as string);
                }
            };
            DockPanel.SetDock(trayModeCombo, Dock.Right);
            rowTray.Children.Add(trayModeCombo);
            rowTray.Children.Add(labelsTray);
            col.Children.Add(rowTray);

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
                // Cache it: MakeIcon records its HICON in staticIconHandle, so building a
                // second one here would orphan the first handle.
                if (staticIcon == null) staticIcon = MakeIcon();
                tray.Icon = staticIcon;
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
                // Clear() only detaches; dispose the old items so rebuilding the submenu
                // doesn't pile up abandoned ToolStrip items.
                var old = new List<System.Windows.Forms.ToolStripItem>();
                foreach (System.Windows.Forms.ToolStripItem it in trayPresetsItem.DropDownItems) old.Add(it);
                trayPresetsItem.DropDownItems.Clear();
                foreach (var it in old) { try { it.Dispose(); } catch { } }
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
        // Hide to the tray — but if the tray icon couldn't be created or tray is hidden
        // ("none" mode), minimize instead of vanishing, so the window can never become unreachable.
        void HideToTray() {
            if (tray == null || Settings.TrayMode == "none") { WindowState = WindowState.Minimized; return; }
            Hide(); ShowInTaskbar = false;
        }
        void QuitApp() {
            quitting = true;
            try { if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; } } catch { }
            // Free the icon handles we own (the tray no longer references them).
            ReleaseIcon(tempIcon, tempIconHandle);
            tempIcon = null; tempIconHandle = IntPtr.Zero;
            ReleaseIcon(staticIcon, staticIconHandle);
            staticIcon = null; staticIconHandle = IntPtr.Zero;
            running = false;
            Smc.Cleanup();
            System.Windows.Application.Current.Shutdown();
        }
        System.Drawing.Icon MakeIcon() {
            try {
                using (var bmp = new System.Drawing.Bitmap(32, 32)) {
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
                    IntPtr h = bmp.GetHicon();
                    if (h == IntPtr.Zero) return System.Drawing.SystemIcons.Application;
                    // Built once and kept for the life of the app; the handle is destroyed on quit.
                    staticIconHandle = h;
                    return System.Drawing.Icon.FromHandle(h);
                }
            } catch { return System.Drawing.SystemIcons.Application; }
        }

        // ---- Seven-segment temperature icon for the system tray ----
        // Segment map for digits 0-9.  Bits: g f e d c b a  (bit 0 = segment a = top)
        //   --a--
        //  |     |
        //  f     b
        //  |     |
        //   --g--
        //  |     |
        //  e     c
        //  |     |
        //   --d--
        static readonly byte[] SEG = { 0x3F,0x06,0x5B,0x4F,0x66,0x6D,0x7D,0x07,0x7F,0x6F };

        // Draw one seven-segment digit into a Graphics at the given x offset.
        // dw = digit cell width, dh = digit cell height, sw = segment bar thickness.
        static void DrawDigit(System.Drawing.Graphics g, int digit, float x, float y,
                              float dw, float dh, float sw, System.Drawing.Brush br) {
            if (digit < 0 || digit > 9) return;
            byte s = SEG[digit];
            float hw = dw;       // full width of horizontal segment
            float vh = (dh - sw) / 2f;  // height of a vertical half
            float midY = y + vh; // top of the middle segment

            // a - top horizontal
            if ((s & 0x01) != 0) g.FillRectangle(br, x, y, hw, sw);
            // b - top right vertical
            if ((s & 0x02) != 0) g.FillRectangle(br, x + hw - sw, y, sw, vh + sw);
            // c - bottom right vertical
            if ((s & 0x04) != 0) g.FillRectangle(br, x + hw - sw, midY, sw, vh + sw);
            // d - bottom horizontal
            if ((s & 0x08) != 0) g.FillRectangle(br, x, y + dh - sw, hw, sw);
            // e - bottom left vertical
            if ((s & 0x10) != 0) g.FillRectangle(br, x, midY, sw, vh + sw);
            // f - top left vertical
            if ((s & 0x20) != 0) g.FillRectangle(br, x, y, sw, vh + sw);
            // g - middle horizontal
            if ((s & 0x40) != 0) g.FillRectangle(br, x, midY, hw, sw);
        }

        // Digit color per app theme — chosen so the digits stay legible against the taskbar
        // for each theme's typical brightness (dark themes -> light digits, light themes -> dark digits).
        // Digit color per app theme — reversed from the app's own text contrast per user preference:
        // dark theme -> dark digits, light theme -> light digits.
        static System.Drawing.Color TrayDigitColor(string theme) {
            switch (theme) {
                case "light": return System.Drawing.Color.White; // light theme -> white digits
                case "japan": return System.Drawing.Color.FromArgb(0xBC, 0x00, 0x2D); // Japan theme accent red
                case "nature": return System.Drawing.Color.FromArgb(0x34, 0xC7, 0x59); // Nature theme accent green
                default: return System.Drawing.Color.FromArgb(0x1B, 0x1B, 0x1F); // dark theme -> near-black digits
            }
        }

        // Renders a temperature value as a tray icon using seven-segment-style digits.
        // Digit color follows the current app theme — legible on both dark and light taskbars.
        // Returns null on failure. 'handle' receives the HICON, which the CALLER owns and must
        // DestroyIcon once the tray is done with it (Icon.FromHandle never frees it).
        System.Drawing.Icon MakeTempIcon(int temp, out IntPtr handle) {
            handle = IntPtr.Zero;
            try {
                int sz = 32;  // standard tray icon canvas
                using (var bmp = new System.Drawing.Bitmap(sz, sz, System.Drawing.Imaging.PixelFormat.Format32bppArgb)) {
                    using (var g = System.Drawing.Graphics.FromImage(bmp)) {
                        g.SmoothingMode = SmoothingMode.Default;
                        g.Clear(System.Drawing.Color.Transparent);

                        // Clamp to displayable range
                        if (temp < 0) temp = 0;
                        if (temp > 199) temp = 199;
                        string digits = temp.ToString();

                        using (var br = new System.Drawing.SolidBrush(TrayDigitColor(Settings.Theme))) {
                            if (digits.Length == 1) {
                                // Single digit: centered, large
                                float dw = 18f, dh = 28f, sw = 4f;
                                float x0 = (sz - dw) / 2f;
                                DrawDigit(g, temp, x0, 2f, dw, dh, sw, br);
                            } else if (digits.Length == 2) {
                                // Two digits: fill the icon (like the reference Capture.PNG)
                                float dw = 13f, dh = 26f, sw = 3.5f;
                                float gap = 2f;
                                float totalW = dw * 2 + gap;
                                float x0 = (sz - totalW) / 2f;
                                DrawDigit(g, digits[0] - '0', x0, 3f, dw, dh, sw, br);
                                DrawDigit(g, digits[1] - '0', x0 + dw + gap, 3f, dw, dh, sw, br);
                            } else {
                                // Three digits (100+): narrower to fit
                                float dw = 9f, dh = 24f, sw = 2.5f;
                                float gap = 1f;
                                float totalW = dw * 3 + gap * 2;
                                float x0 = (sz - totalW) / 2f;
                                for (int i = 0; i < 3; i++)
                                    DrawDigit(g, digits[i] - '0', x0 + i * (dw + gap), 4f, dw, dh, sw, br);
                            }

                            // Tiny degree symbol in the top-right corner
                            using (var f = new System.Drawing.Font("Segoe UI", 6f, System.Drawing.FontStyle.Bold))
                                g.DrawString("°", f, br, sz - 9f, 0f);
                        }
                    }
                    IntPtr h = bmp.GetHicon();
                    if (h == IntPtr.Zero) return null;
                    handle = h;
                    return System.Drawing.Icon.FromHandle(h);
                }
            } catch { return null; }
        }

        // Show 'display' on the tray icon, redrawing only when the value (or theme) actually
        // changed, and freeing the previous icon's HICON afterwards. Without the DestroyIcon
        // every refresh tick leaked a USER/GDI handle: at one icon every 2 s the process hit
        // the 10.000-handle quota after a few hours, at which point GetHicon started failing
        // (tray fell back to the "R" icon) and the UI could no longer create windows.
        void SetTrayTempIcon(int display) {
            if (tempIcon != null && display == tempIconValue && Settings.Theme == tempIconTheme) {
                if (!ReferenceEquals(tray.Icon, tempIcon)) tray.Icon = tempIcon;
                tray.Visible = true;
                return;
            }
            IntPtr h;
            var icon = MakeTempIcon(display, out h);
            if (icon == null) { ShowStaticIcon(); return; }

            var oldIcon = tempIcon; IntPtr oldHandle = tempIconHandle;
            tempIcon = icon; tempIconHandle = h;
            tempIconValue = display; tempIconTheme = Settings.Theme;
            tray.Icon = icon;          // hand the new icon to the shell first…
            tray.Visible = true;
            ReleaseIcon(oldIcon, oldHandle);   // …then release the one it no longer uses
        }

        // Drop a generated icon and the HICON behind it. A null handle means the icon is a
        // shared system one (the MakeIcon fallback), which we must not touch.
        static void ReleaseIcon(System.Drawing.Icon icon, IntPtr handle) {
            if (handle == IntPtr.Zero) return;
            if (icon != null) { try { icon.Dispose(); } catch { } }
            try { DestroyIcon(handle); } catch { }
        }

        // Switch the tray back to the static "R" icon, releasing any temperature icon.
        void ShowStaticIcon() {
            if (staticIcon == null) staticIcon = MakeIcon();
            tray.Icon = staticIcon;
            tray.Visible = true;
            ReleaseIcon(tempIcon, tempIconHandle);
            tempIcon = null; tempIconHandle = IntPtr.Zero; tempIconValue = int.MinValue;
        }

        // Apply the current tray mode setting. Called on setting change and each refresh tick.
        // 'curated' may be null (on initial apply before the first refresh); in that case
        // temperature modes fall back to an immediate SMC read.
        void ApplyTrayMode(Dictionary<string, double> curated) {
            if (tray == null) return;
            string mode = Settings.TrayMode ?? "icon";

            if (mode == "icon") {
                ShowStaticIcon();
            } else if (mode == "none") {
                tray.Visible = false;
                ReleaseIcon(tempIcon, tempIconHandle);
                tempIcon = null; tempIconHandle = IntPtr.Zero; tempIconValue = int.MinValue;
            } else {
                // Temperature mode: "highest" or a specific sensor key
                double temp = double.NaN;
                if (mode == "highest") {
                    // Find the highest curated temperature
                    if (curated != null) {
                        foreach (var kv in curated)
                            if (!double.IsNaN(kv.Value) && (double.IsNaN(temp) || kv.Value > temp))
                                temp = kv.Value;
                    } else {
                        // Fallback: read sensors directly
                        foreach (var c in CURATED) {
                            double v = Smc.ReadTemp(c[0]);
                            if (!double.IsNaN(v) && v >= 5 && v <= 120 && (double.IsNaN(temp) || v > temp))
                                temp = v;
                        }
                    }
                } else {
                    // Specific sensor key
                    if (curated != null) {
                        double v;
                        if (curated.TryGetValue(mode, out v)) temp = v;
                    }
                    if (double.IsNaN(temp)) temp = Smc.ReadTemp(mode);
                }

                if (!double.IsNaN(temp) && temp >= 0 && temp <= 200) {
                    int display = Settings.Fahrenheit ? (int)(temp * 9.0 / 5.0 + 32.0) : (int)temp;
                    SetTrayTempIcon(display);
                } else {
                    // Sensor unavailable — show the static icon
                    ShowStaticIcon();
                }
            }
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
                                double t = (f.CurveSensorKey == HIGHEST_SENSOR)
                                    ? HighestCurated(curated)
                                    : Smc.ReadTemp(f.CurveSensorKey);
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
                            ApplyTrayMode(curated);
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

        // One frozen effect shared by every label: Render() rebuilds the whole panel each
        // refresh, so allocating a fresh (unfreezable) effect per TextBlock churned render
        // resources every 2 s for no reason.
        static readonly DropShadowEffect TEXT_SHADOW = Shadow();

        static DropShadowEffect Shadow() {
            var e = new DropShadowEffect { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 0, Opacity = 0.55 };
            e.Freeze();
            return e;
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

        TextBlock Label(string t, double size) { return new TextBlock { Text = t, Foreground = MainWindow.SUB, FontSize = size, Effect = TEXT_SHADOW, VerticalAlignment = VerticalAlignment.Center }; }
        TextBlock Value(string t, double size) { return new TextBlock { Text = t, Foreground = MainWindow.TXT, FontSize = size, FontWeight = FontWeights.SemiBold, Effect = TEXT_SHADOW, VerticalAlignment = VerticalAlignment.Center }; }

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
            if (!Horizontal) head.Children.Add(new TextBlock { Text = "RPMac", Foreground = MainWindow.TXT, FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Effect = TEXT_SHADOW });
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
        public static string TrayMode = "icon";  // "icon", "none", "highest", or a sensor key

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
                    else if (s.Length >= 2 && s[0] == "traymode") TrayMode = s[1];
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
                lines.Add("traymode|" + TrayMode);
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
        public static void Enable(string exe) {
            // `schtasks /create` defaults the task to "start only if on AC power" and "stop if
            // going on battery", so on a laptop RPMac won't launch at logon while on battery.
            // Register via an XML definition instead, where we can turn both conditions off.
            try {
                string xml =
                    "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n" +
                    "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\r\n" +
                    "  <RegistrationInfo><Description>RPMac fan control</Description></RegistrationInfo>\r\n" +
                    "  <Triggers><LogonTrigger><Enabled>true</Enabled></LogonTrigger></Triggers>\r\n" +
                    "  <Principals><Principal id=\"Author\"><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>\r\n" +
                    "  <Settings>\r\n" +
                    "    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>\r\n" +
                    "    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>\r\n" +
                    "    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>\r\n" +
                    "    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>\r\n" +
                    "    <Enabled>true</Enabled>\r\n" +
                    "  </Settings>\r\n" +
                    "  <Actions Context=\"Author\"><Exec><Command>" + System.Security.SecurityElement.Escape(exe) + "</Command></Exec></Actions>\r\n" +
                    "</Task>";
                string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RPMac_task.xml");
                System.IO.File.WriteAllText(tmp, xml, System.Text.Encoding.Unicode); // schtasks expects UTF-16
                int r = Run("/create /tn " + TASK + " /xml \"" + tmp + "\" /f");
                try { System.IO.File.Delete(tmp); } catch { }
                if (r == 0) return;
            } catch { }
            // Fallback to the simple form if XML registration failed for any reason.
            Run("/create /tn " + TASK + " /tr \"\\\"" + exe + "\\\"\" /sc onlogon /rl highest /f");
        }
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

            // Last-resort logger: catches unhandled exceptions on any thread (including the
            // constructor and native/corrupted-state failures) so a startup crash is recorded.
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs ea) {
                LogError("Fatal", ea.ExceptionObject as Exception);
            };

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
                string msg = (ex == null) ? "(no exception object)" : ex.ToString();
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "error.log"), DateTime.Now + " [" + where + "] " + msg + "\r\n\r\n");
            } catch { }
        }
    }
}
