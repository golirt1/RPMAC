// Fan Control Open - GUI (WPF, codigo puro, tema oscuro moderno)
// Copyright (C) 2026 - GPL-2.0-only. Ver LICENSE y NOTICE.

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
        static readonly Brush BG     = B("#1B1B1F");
        static readonly Brush CARD   = B("#27272C");
        static readonly Brush TXT    = B("#F2F2F4");
        static readonly Brush SUB    = B("#8E8E96");
        static readonly Brush ACCENT = B("#0A84FF");
        static readonly Brush RED    = B("#FF453A");
        static readonly Brush CHIP   = B("#37373E");
        static readonly Brush BORDER = B("#3A3A42");

        static readonly string[][] CURATED = new string[][] {
            new string[]{"TC0P","CPU"},
            new string[]{"TCXc","CPU (PECI)"},
            new string[]{"TC0c","CPU (core)"},
            new string[]{"TG0P","GPU 1"},
            new string[]{"TG0D","GPU 1 (core)"},
            new string[]{"TG1P","GPU 2"},
            new string[]{"TG1D","GPU 2 (core)"},
            new string[]{"TM0P","Memory"},
            new string[]{"TM1P","Memory 2"},
            new string[]{"TA0P","Ambient"},
            new string[]{"TPCD","Power"},
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
            public Border Auto, MaxBtn, Manual, BarFill;
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

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);

        public MainWindow() {
            Title = "RPMac";
            Width = 470; Height = 690;
            Background = BG; Foreground = TXT;
            FontFamily = new FontFamily("Segoe UI");
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);

            try { Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(STYLES)); } catch { }

            // barra de titulo oscura
            SourceInitialized += delegate {
                try {
                    IntPtr h = new WindowInteropHelper(this).Handle; int on = 1;
                    DwmSetWindowAttribute(h, 20, ref on, 4);
                    DwmSetWindowAttribute(h, 19, ref on, 4);
                } catch { }
            };

            var root = new DockPanel();
            Content = root;

            var header = new StackPanel { Margin = new Thickness(20, 18, 20, 6) };
            header.Children.Add(new TextBlock { Text = "RPMac", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = TXT });
            header.Children.Add(new TextBlock { Text = "The other app capable of controlling fans on Intel Macs in Windows — for free.", FontSize = 12, Foreground = SUB, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            var statusBar = new Border { Background = B("#202024"), Child = (status = new TextBlock { Text = "Starting…", FontSize = 11, Foreground = SUB, Margin = new Thickness(20, 7, 20, 7) }) };
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

            Loaded += delegate { StartRefresh(); };
            Closed += delegate { running = false; };
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
            f.Slider.IsEnabled = (mode == "manual");
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
                f.Auto = Chip("Auto", CHIP, TXT, delegate { if (!Guard()) return; Smc.SetFanAuto(f.Index); SetMode(f, "auto"); });
                f.MaxBtn = Chip("Max", CHIP, TXT, delegate { if (!Guard()) return; Smc.SetFanMax(f.Index); SetMode(f, "max"); });
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

                var apply = Chip("Apply RPM", ACCENT, Brushes.White, delegate { if (!Guard()) return; Smc.SetFanRpm(f.Index, f.Slider.Value); SetMode(f, "manual"); });
                apply.Margin = new Thickness(0, 12, 0, 0);
                apply.HorizontalAlignment = HorizontalAlignment.Left;
                col.Children.Add(apply);

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

            parent.Children.Add(Card(col));
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
                if (!double.IsNaN(kv.Value) && labels.TryGetValue(kv.Key, out t)) t.Text = string.Format("{0:0.0} °C", kv.Value);
            }
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
        [STAThread]
        public static void Main() {
            var app = new Application();
            app.Run(new MainWindow());
        }
    }
}
