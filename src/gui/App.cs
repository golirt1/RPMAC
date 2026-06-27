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
            public TextBlock Rpm, Info, Mode;
            public Slider Slider;
            public TextBlock SliderVal;
            public Border Auto, MaxBtn, Manual, BarFill, Apply;
            public UIElement ManualRow;
        }

        readonly List<FanUi> fans = new List<FanUi>();
        readonly Dictionary<string, TextBlock> curatedLabels = new Dictionary<string, TextBlock>();
        readonly Dictionary<string, TextBlock> allLabels = new Dictionary<string, TextBlock>();
        WrapPanel allPanel;
        Border allContainer;
        bool allLoaded = false;
        volatile bool showAll = false;
        TextBlock status;
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
            BuildTempsCard(stack);
            BuildSettingsCard(stack);

            Loaded += delegate {
                SetupTray();
                ApplySaved();
                StartRefresh();
                Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerChange;
                if (Settings.Overlay) ShowOverlay();
                if (Settings.StartMinimized) HideToTray();
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
            f.Auto.Background = (mode == "auto") ? ACCENT : CHIP;
            f.MaxBtn.Background = (mode == "max") ? RED : CHIP;
            f.Manual.Background = (mode == "manual") ? ACCENT : CHIP;
            bool man = (mode == "manual");
            f.Slider.IsEnabled = man;
            if (f.ManualRow != null) f.ManualRow.Visibility = man ? Visibility.Visible : Visibility.Collapsed;
            if (f.Apply != null) f.Apply.Visibility = man ? Visibility.Visible : Visibility.Collapsed;
            f.Mode.Text = mode == "auto" ? "Mode: automatic" : mode == "max" ? "Mode: maximum" : "Mode: manual";
        }

        void BuildFans(Panel parent) {
            foreach (var fi in Smc.GetFans()) {
                var f = new FanUi { Index = fi.Index, Max = double.IsNaN(fi.Max) ? 6000 : fi.Max };
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
                f.Auto = Chip("Auto", CHIP, TXT, delegate { if (!Guard()) return; Smc.SetFanAuto(f.Index); SetMode(f, "auto"); Settings.SetFan(f.Index, "auto", 0); });
                f.MaxBtn = Chip("Max", CHIP, TXT, delegate { if (!Guard()) return; Smc.SetFanMax(f.Index); SetMode(f, "max"); Settings.SetFan(f.Index, "max", 0); });
                f.Manual = Chip("Manual", CHIP, TXT, delegate { if (!Guard()) return; SetMode(f, "manual"); });
                chips.Children.Add(f.Auto); chips.Children.Add(f.MaxBtn); chips.Children.Add(f.Manual);
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

                var apply = Chip("Apply RPM", ACCENT, Brushes.White, delegate { if (!Guard()) return; Smc.SetFanRpm(f.Index, f.Slider.Value); SetMode(f, "manual"); Settings.SetFan(f.Index, "manual", (int)f.Slider.Value); });
                apply.Margin = new Thickness(0, 12, 0, 0);
                apply.HorizontalAlignment = HorizontalAlignment.Left;
                col.Children.Add(apply);
                f.Apply = apply;

                if (!Smc.WritesAllowed) { f.Auto.Opacity = 0.45; f.MaxBtn.Opacity = 0.45; f.Manual.Opacity = 0.45; apply.Opacity = 0.45; }

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
                menu.Items.Add("Quit", null, delegate { QuitApp(); });
                tray.ContextMenuStrip = menu;
            } catch { }
        }
        void ShowFromTray() { Show(); WindowState = WindowState.Normal; ShowInTaskbar = true; Activate(); }
        void HideToTray() { Hide(); ShowInTaskbar = false; }
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
                var s = Settings.Fans[f.Index];
                string mode = s[0]; int rpm = 0; int.TryParse(s[1], out rpm);
                try {
                    if (mode == "max") { Smc.SetFanMax(f.Index); SetMode(f, "max"); }
                    else if (mode == "manual") { Smc.SetFanRpm(f.Index, rpm); f.Slider.Value = rpm; SetMode(f, "manual"); }
                    else { Smc.SetFanAuto(f.Index); SetMode(f, "auto"); }
                } catch { }
            }
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
        public void Update(List<string[]> rows) { lastRows = rows; Render(rows); }

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
                    else if (s.Length >= 4 && s[0] == "fan") { int idx; if (int.TryParse(s[1], out idx)) Fans[idx] = new string[] { s[2], s[3] }; }
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
                foreach (var kv in Fans) lines.Add("fan|" + kv.Key + "|" + kv.Value[0] + "|" + kv.Value[1]);
                System.IO.File.WriteAllLines(FilePath, lines.ToArray());
            } catch { }
        }
        public static void SetFan(int idx, string mode, int rpm) { Fans[idx] = new string[] { mode, rpm.ToString() }; Save(); }
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
        [STAThread]
        public static void Main() {
            var app = new Application();
            app.Run(new MainWindow());
        }
    }
}
