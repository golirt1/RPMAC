// RPMac - GUI (WPF, codigo puro, tema oscuro moderno)
// Copyright (C) 2026 golirt1 - GPL-2.0-only. Ver LICENSE y NOTICE.

using System;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
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
        // Colores de estado fijos (no cambian por tema: legibles sobre claro y oscuro)
        internal static readonly SolidColorBrush WARN   = (SolidColorBrush)B("#FFB340"); // ámbar: temperatura alta / read-only
        internal static readonly SolidColorBrush GOOD   = (SolidColorBrush)B("#34C759"); // verde: todo bien

        const string VERSION = "1.6.1";

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

        // Un punto de la curva: temperatura (°C) -> velocidad (RPM)
        class CurvePt {
            public double T, R;
            public CurvePt(double t, double r) { T = t; R = r; }
        }

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
            public UIElement CurveRow, CurveApplyRow;
            public ComboBox CurveSensor;
            public string CurveSensorKey;

            // Curva como lista de puntos (temperatura -> RPM), ordenada por temperatura y
            // con 2 puntos como mínimo. 'Pts' es el modelo que edita la UI; 'Flat' es una
            // instantánea plana [t0,r0,t1,r1,...] que lee el hilo de refresco sin bloqueos:
            // al editar se reemplaza el array entero, así que el lector nunca ve un estado
            // a medias (una asignación de referencia es atómica).
            // 'Pts' se mantiene siempre ordenada por temperatura y sin bajadas de RPM:
            // se inserta en su sitio al añadir y se limita contra los vecinos al arrastrar,
            // así que aquí solo hay que aplanarla.
            public List<CurvePt> Pts = new List<CurvePt>();
            public volatile double[] Flat;
            public void SyncFlat() {
                var f = new double[Pts.Count * 2];
                for (int i = 0; i < Pts.Count; i++) { f[i * 2] = Pts[i].T; f[i * 2 + 1] = Pts[i].R; }
                Flat = f;
            }

            // Editor gráfico de la curva
            public Canvas CurveCv;
            public Polyline CurveLine;
            public Polygon CurveFill;
            public Ellipse CurveLive;
            public List<Ellipse> Thumbs = new List<Ellipse>();
            public TextBlock CurveReadout, CvYMin, CvYMax;
            public TextBlock[] CvTicks;
            public int CurveDrag = -1;                  // índice del punto que se arrastra, -1 = ninguno
            public volatile float LastCurveTemp = float.NaN;   // temp actual del sensor de la curva
            public double LastCurveRpm = double.NaN;    // último RPM aplicado (para el suavizado)

            // Marca de RPM objetivo sobre la barra
            public Border TargetTick;
        }

        readonly List<FanUi> fans = new List<FanUi>();
        readonly Dictionary<string, TextBlock> curatedLabels = new Dictionary<string, TextBlock>();
        readonly Dictionary<string, TextBlock> allLabels = new Dictionary<string, TextBlock>();
        WrapPanel allPanel;
        Border allContainer;
        bool allLoaded = false;
        volatile bool showAll = false;
        TextBlock status;
        Border statusDot;           // punto de estado en la barra inferior
        TextBlock titleTemp;        // lectura viva en la barra de título

        // Navegación por páginas (rail izquierdo)
        Grid pageHost;
        readonly Dictionary<string, ScrollViewer> pages = new Dictionary<string, ScrollViewer>();
        readonly Dictionary<string, NavItem> navItems = new Dictionary<string, NavItem>();
        class NavItem {
            public Border Box;
            public TextBlock Label;
            public List<Shape> Shapes = new List<Shape>();
        }
        StackPanel presetChips;     // vertical list, one row per saved preset
        TextBox presetNameBox;      // name field for saving the current config
        TextBlock presetPlaceholder; // faux placeholder for the name field
        string activePreset;        // name of the preset currently applied (null = none / custom)
        System.Windows.Forms.ToolStripMenuItem trayPresetsItem;  // tray "Presets" submenu
        volatile bool running = true;
        const double BAR_W = 505;   // ancho de la barra de RPM (columna de ventiladores)
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
            Width = 940; Height = 566;
            ResizeMode = ResizeMode.CanMinimize;   // layout fijo, estilo utilidad de escritorio
            WindowStyle = WindowStyle.None;        // barra de título propia (ver BuildTitleBar)
            // WindowChrome mantiene el comportamiento nativo (snap, sombra, taskbar) aunque
            // dibujemos nosotros la barra: CaptionHeight 0 => el arrastre lo hace DragMove.
            try {
                var chrome = new System.Windows.Shell.WindowChrome {
                    CaptionHeight = 0,
                    ResizeBorderThickness = new Thickness(0),
                    GlassFrameThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(0),
                    UseAeroCaptionButtons = false
                };
                System.Windows.Shell.WindowChrome.SetWindowChrome(this, chrome);
            } catch { }
            try { Icon = AppIconSource(); } catch { }
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

            var titleBar = BuildTitleBar();
            DockPanel.SetDock(titleBar, Dock.Top);
            root.Children.Add(titleBar);

            // Barra de estado con punto indicador (verde = OK, ámbar = solo lectura)
            var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(18, 6, 18, 6) };
            statusDot = new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(3.5), Background = SUB, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            status = new TextBlock { Text = "Starting…", FontSize = 11, Foreground = SUB, VerticalAlignment = VerticalAlignment.Center };
            statusRow.Children.Add(statusDot);
            statusRow.Children.Add(status);
            var statusBar = new Border { Background = BAR, BorderBrush = BORDER, BorderThickness = new Thickness(0, 1, 0, 0), Child = statusRow };
            DockPanel.SetDock(statusBar, Dock.Bottom);
            root.Children.Add(statusBar);

            // Rail de navegación a la izquierda + área de páginas a la derecha.
            var main = new Grid();
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(74) });
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.Children.Add(main);

            var nav = BuildNavRail();
            Grid.SetColumn(nav, 0);
            main.Children.Add(nav);

            pageHost = new Grid { Margin = new Thickness(0) };
            Grid.SetColumn(pageHost, 1);
            main.Children.Add(pageHost);

            // Página de ventiladores: controles a la izquierda, lectura viva a la derecha
            // (poder ver las temperaturas mientras ajustas es justo lo que uno necesita).
            var fansPage = NewPage("fans");
            var fansGrid = new Grid();
            fansGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fansGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(272) });
            var stack = new StackPanel();
            Grid.SetColumn(stack, 0); fansGrid.Children.Add(stack);
            var liveCol = new StackPanel();
            Grid.SetColumn(liveCol, 1); fansGrid.Children.Add(liveCol);
            fansPage.Children.Add(fansGrid);

            if (!Smc.IsInpOutDriverOpen())
                stack.Children.Add(Card(new TextBlock { Text = "Couldn't open the I/O driver (InpOut).\nRun the app as administrator.", Foreground = RED, TextWrapping = TextWrapping.Wrap }));

            // SALVAGUARDA: validar hardware Apple + coherencia del SMC antes de permitir escribir
            Smc.Validate();
            if (!Smc.WritesAllowed) {
                var warn = new StackPanel();
                warn.Children.Add(new TextBlock { Text = "⚠  Read-only mode", FontSize = 14, FontWeight = FontWeights.Bold, Foreground = WARN });
                warn.Children.Add(new TextBlock { Text = Smc.SafetyReason, Foreground = TXT, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
                stack.Children.Add(Card(warn));
            }

            BuildFans(stack);
            BuildHistoryCard(stack);
            BuildLiveTemps(liveCol);

            BuildTempsPane(NewPage("sensors"));
            BuildPresetsCard(NewPage("profiles"));
            BuildSettingsCard(NewPage("settings"));
            ShowPage("fans");

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
                Background = CARD, CornerRadius = new CornerRadius(12),
                BorderBrush = BORDER, BorderThickness = new Thickness(1),
                Padding = new Thickness(18, 16, 18, 16), Margin = new Thickness(6, 6, 6, 10),
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 10, ShadowDepth = 1, Opacity = 0.16 },
                Child = content
            };
        }

        // Rótulo de sección: mayúsculas pequeñas con tracking (jerarquía tipográfica)
        static TextBlock SectionLabel(string text, double topMargin) {
            return new TextBlock {
                Text = text.ToUpperInvariant(), FontSize = 10, FontWeight = FontWeights.SemiBold,
                Foreground = SUB, Opacity = 0.9, Margin = new Thickness(0, topMargin, 0, 6)
            };
        }

        // ---- Iconos vectoriales ---------------------------------------------------
        // Trazo (24x24). Devuelve el contenedor y acumula las formas para poder recolorear.
        static UIElement StrokeIcon(double size, string[] datas, List<Shape> bag) {
            var cv = new Canvas { Width = 24, Height = 24 };
            foreach (var d in datas) {
                var p = new Path {
                    Data = Geometry.Parse(d), Stroke = SUB, StrokeThickness = 1.9,
                    StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                    StrokeLineJoin = PenLineJoin.Round
                };
                cv.Children.Add(p);
                if (bag != null) bag.Add(p);
            }
            return new Viewbox { Width = size, Height = size, Child = cv, HorizontalAlignment = HorizontalAlignment.Center };
        }

        // Ventilador de 3 aspas (el mismo dibujo del icono de la app), relleno.
        const string BLADE_PATH = "M42.5,41 C25,24 38,4 54,10 C65,14.5 64,31 58.5,42.5 Z";
        static UIElement FanGlyph(double size, List<Shape> bag) {
            var cv = new Canvas { Width = 100, Height = 100 };
            for (int i = 0; i < 3; i++) {
                var p = new Path {
                    Data = Geometry.Parse(BLADE_PATH), Fill = SUB, Tag = "fill",
                    RenderTransform = new RotateTransform(i * 120, 50, 50)
                };
                cv.Children.Add(p);
                if (bag != null) bag.Add(p);
            }
            var hub = new Ellipse { Width = 21, Height = 21, Fill = SUB, Tag = "fill" };
            Canvas.SetLeft(hub, 39.5); Canvas.SetTop(hub, 39.5);
            cv.Children.Add(hub);
            if (bag != null) bag.Add(hub);
            return new Viewbox { Width = size, Height = size, Child = cv, HorizontalAlignment = HorizontalAlignment.Center };
        }

        static void Recolor(List<Shape> shapes, Brush b) {
            foreach (var s in shapes) {
                if ("fill".Equals(s.Tag)) s.Fill = b; else s.Stroke = b;
            }
        }

        // Icono de la app tomado del propio .exe (recurso win32), para ventana y bandeja.
        static ImageSource appIconSrc;
        static ImageSource AppIconSource() {
            if (appIconSrc != null) return appIconSrc;
            try {
                string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                using (var ic = System.Drawing.Icon.ExtractAssociatedIcon(exe))
                    appIconSrc = Imaging.CreateBitmapSourceFromHIcon(ic.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            } catch { }
            return appIconSrc;
        }

        // ---- Barra de título propia ----------------------------------------------
        UIElement BuildTitleBar() {
            var grid = new Grid { Height = 46, Background = BAR };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var ico = AppIconSource();
            if (ico != null) left.Children.Add(new Image { Source = ico, Width = 18, Height = 18, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 9, 0) });
            left.Children.Add(new TextBlock { Text = "RPMac", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = TXT, VerticalAlignment = VerticalAlignment.Center });
            left.Children.Add(new TextBlock { Text = "v" + VERSION, FontSize = 10.5, Foreground = SUB, Opacity = 0.8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 1, 0, 0) });
            titleTemp = new TextBlock { Text = "", FontSize = 11.5, Foreground = SUB, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 1, 0, 0) };
            left.Children.Add(titleTemp);
            grid.Children.Add(left);

            var btns = new StackPanel { Orientation = Orientation.Horizontal };
            btns.Children.Add(CaptionButton("M0,0 H10", false, delegate { WindowState = WindowState.Minimized; }));
            btns.Children.Add(CaptionButton("M0,0 L10,10 M10,0 L0,10", true, delegate { HideToTray(); }));
            Grid.SetColumn(btns, 1);
            grid.Children.Add(btns);

            // Arrastrar la ventana desde la barra (CaptionHeight = 0 en WindowChrome)
            grid.MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e) {
                if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } }
            };
            return grid;
        }

        Border CaptionButton(string geom, bool danger, Action onClick) {
            var p = new Path {
                Data = Geometry.Parse(geom), Stroke = SUB, StrokeThickness = 1.2,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            var bd = new Border { Width = 46, Height = 46, Background = Brushes.Transparent, Child = p, Cursor = Cursors.Arrow };
            bd.MouseEnter += delegate { bd.Background = danger ? RED : CHIP; p.Stroke = danger ? Brushes.White : TXT; };
            bd.MouseLeave += delegate { bd.Background = Brushes.Transparent; p.Stroke = SUB; };
            // Swallow the press: otherwise it bubbles to the title bar, which starts
            // DragMove() — that captures the mouse and the button never sees the release,
            // so the click silently does nothing.
            bd.MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e) { e.Handled = true; };
            bd.MouseLeftButtonUp += delegate { onClick(); };
            return bd;
        }

        // ---- Rail de navegación ---------------------------------------------------
        UIElement BuildNavRail() {
            var panel = new StackPanel { Background = BAR };
            var wrap = new Border { Background = BAR, BorderBrush = BORDER, BorderThickness = new Thickness(0, 0, 1, 0), Child = panel };

            panel.Children.Add(NavButton("fans", "Fans", null));
            panel.Children.Add(NavButton("sensors", "Sensors", new[] {
                "M14.5,13.6 V5.5 a2.5,2.5 0 0 0 -5,0 V13.6 a4.5,4.5 0 1 0 5,0 Z",
                "M12,9.5 V15.2"
            }));
            panel.Children.Add(NavButton("profiles", "Presets", new[] {
                "M7,3.5 h10 a1,1 0 0 1 1,1 V20.5 l-6,-4 -6,4 V4.5 a1,1 0 0 1 1,-1 Z"
            }));
            panel.Children.Add(NavButton("settings", "Settings", new[] {
                "M3.5,7 h9.5 M17.5,7 h3 M3.5,12 h3.5 M11.5,12 h9 M3.5,17 h11 M19,17 h1.5",
                "M13,7 a2.25,2.25 0 1 0 4.5,0 a2.25,2.25 0 1 0 -4.5,0",
                "M7,12 a2.25,2.25 0 1 0 4.5,0 a2.25,2.25 0 1 0 -4.5,0",
                "M14.5,17 a2.25,2.25 0 1 0 4.5,0 a2.25,2.25 0 1 0 -4.5,0"
            }));
            return wrap;
        }

        Border NavButton(string key, string label, string[] iconData) {
            var it = new NavItem();
            var col = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            col.Children.Add(iconData == null ? FanGlyph(21, it.Shapes) : StrokeIcon(21, iconData, it.Shapes));
            it.Label = new TextBlock { Text = label, FontSize = 9.5, Foreground = SUB, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 0) };
            col.Children.Add(it.Label);

            it.Box = new Border {
                Margin = new Thickness(8, 8, 8, 0), Padding = new Thickness(0, 10, 0, 9),
                CornerRadius = new CornerRadius(10), Background = Brushes.Transparent,
                Cursor = Cursors.Hand, Child = col
            };
            it.Box.MouseEnter += delegate { if (currentPage != key) it.Box.Background = CHIP; };
            it.Box.MouseLeave += delegate { if (currentPage != key) it.Box.Background = Brushes.Transparent; };
            it.Box.MouseLeftButtonUp += delegate { ShowPage(key); };
            navItems[key] = it;
            return it.Box;
        }

        string currentPage = "";

        StackPanel NewPage(string key) {
            var sp = new StackPanel { Margin = new Thickness(8, 10, 14, 12) };
            var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = sp, Visibility = Visibility.Collapsed };
            pages[key] = sv;
            pageHost.Children.Add(sv);
            return sp;
        }

        void ShowPage(string key) {
            currentPage = key;
            foreach (var kv in pages) kv.Value.Visibility = (kv.Key == key) ? Visibility.Visible : Visibility.Collapsed;
            foreach (var kv in navItems) {
                bool sel = (kv.Key == key);
                kv.Value.Box.Background = sel ? ACCENT : Brushes.Transparent;
                kv.Value.Label.Foreground = sel ? Brushes.White : SUB;
                Recolor(kv.Value.Shapes, sel ? Brushes.White : SUB);
            }
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

        // Un segmento del selector de modo. Se distingue por Tag = "seg": su fondo
        // inactivo es transparente (el contenedor ya pinta), no CHIP.
        Border SegButton(string text, MouseButtonEventHandler onClick) {
            var tb = new TextBlock {
                Text = text, Foreground = TXT, FontSize = 12.5, FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            var bd = new Border {
                Background = Brushes.Transparent, CornerRadius = new CornerRadius(7),
                Padding = new Thickness(0, 7, 0, 7), Width = 92, Cursor = Cursors.Hand,
                Tag = "seg", Child = tb
            };
            bd.MouseEnter += delegate { if (bd.Background == Brushes.Transparent) bd.Background = CHIP; };
            bd.MouseLeave += delegate { if (bd.Background == CHIP) bd.Background = Brushes.Transparent; };
            bd.MouseLeftButtonUp += onClick;
            return bd;
        }

        // Highlight a mode chip: colored background + white label when active, so the text
        // stays readable on the accent/red fill in the light themes too (not just dark).
        static void SetChipActive(Border chip, bool active, Brush activeBg) {
            if (chip == null) return;
            chip.Background = active ? activeBg : ("seg".Equals(chip.Tag) ? Brushes.Transparent : (Brush)CHIP);
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
            if (f.CurveApplyRow != null) f.CurveApplyRow.Visibility = cur ? Visibility.Visible : Visibility.Collapsed;
            f.Mode.Text = mode == "auto" ? "Mode: automatic"
                        : mode == "max" ? "Mode: maximum"
                        : mode == "manual" ? "Mode: manual"
                        : "Mode: curve";
        }

        // Linear ramp: rpm_min below t_min, rpm_max above t_max, interpolated between.
        // RPM que pide la curva a una temperatura dada: interpolación lineal entre los dos
        // puntos que la rodean, y plano fuera del rango (por debajo del primer punto y por
        // encima del último). 'p' es la instantánea plana [t0,r0,t1,r1,...].
        static double CurveRpm(double temp, double[] p) {
            if (p == null || p.Length < 4) return 0;
            int n = p.Length / 2;
            if (double.IsNaN(temp) || temp <= p[0]) return p[1];
            if (temp >= p[(n - 1) * 2]) return p[(n - 1) * 2 + 1];
            for (int i = 0; i < n - 1; i++) {
                double t0 = p[i * 2], r0 = p[i * 2 + 1];
                double t1 = p[(i + 1) * 2], r1 = p[(i + 1) * 2 + 1];
                if (temp >= t0 && temp <= t1)
                    return (t1 <= t0) ? r1 : r0 + (r1 - r0) * (temp - t0) / (t1 - t0);
            }
            return p[p.Length - 1];
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

        // ---- Editor gráfico de la curva -------------------------------------------
        // Dibuja la curva temp→RPM en un canvas con dos puntos arrastrables (min y max).
        // Los 4 sliders ocultos siguen guardando los valores (Apply/presets no cambian);
        // arrastrar un punto actualiza los sliders y cualquier cambio de slider re-dibuja.
        const double CV_W = 505, CV_H = 172;             // tamaño del canvas
        const double CVL = 36, CVR = 10, CVT = 12, CVB = 20; // padding: ejes izq/der/arriba/abajo
        const double CV_TMAX = 110;                      // rango del eje X en °C

        double CvX(double t) {
            if (t < 0) t = 0; if (t > CV_TMAX) t = CV_TMAX;
            return CVL + (t / CV_TMAX) * (CV_W - CVL - CVR);
        }
        double CvY(FanUi f, double r) {
            double f01 = (f.Max > f.Min) ? (r - f.Min) / (f.Max - f.Min) : 0;
            if (f01 < 0) f01 = 0; if (f01 > 1) f01 = 1;
            return (CV_H - CVB) - f01 * (CV_H - CVB - CVT);
        }

        static string ShortTemp(double c) {
            return Settings.Fahrenheit
                ? string.Format("{0:0}°F", c * 9.0 / 5.0 + 32.0)
                : string.Format("{0:0}°C", c);
        }

        UIElement BuildCurveCanvas(FanUi f) {
            var wrap = new StackPanel();

            f.CurveCv = new Canvas { Width = CV_W, Height = CV_H, Background = Brushes.Transparent, ClipToBounds = true };

            // Rejilla: líneas verticales cada 20°C + eje inferior. Comparten brushes del
            // tema (BORDER/SUB), así que siguen al tema automáticamente.
            var ticks = new List<TextBlock>();
            for (int t = 20; t <= 100; t += 20) {
                var gl = new Line { X1 = CvX(t), Y1 = CVT, X2 = CvX(t), Y2 = CV_H - CVB, Stroke = BORDER, StrokeThickness = 1 };
                f.CurveCv.Children.Add(gl);
                var lb = new TextBlock { Text = t + "°", FontSize = 9.5, Foreground = SUB };
                Canvas.SetLeft(lb, CvX(t) - 9); Canvas.SetTop(lb, CV_H - CVB + 3);
                f.CurveCv.Children.Add(lb);
                ticks.Add(lb);
            }
            f.CvTicks = ticks.ToArray();
            f.CurveCv.Children.Add(new Line { X1 = CVL, Y1 = CV_H - CVB, X2 = CV_W - CVR, Y2 = CV_H - CVB, Stroke = BORDER, StrokeThickness = 1 });

            // Etiquetas del eje Y (RPM min y max del ventilador)
            f.CvYMin = new TextBlock { Text = ((int)f.Min).ToString(), FontSize = 9.5, Foreground = SUB, Width = CVL - 6, TextAlignment = TextAlignment.Right };
            Canvas.SetLeft(f.CvYMin, 0); Canvas.SetTop(f.CvYMin, CV_H - CVB - 7);
            f.CurveCv.Children.Add(f.CvYMin);
            f.CvYMax = new TextBlock { Text = ((int)f.Max).ToString(), FontSize = 9.5, Foreground = SUB, Width = CVL - 6, TextAlignment = TextAlignment.Right };
            Canvas.SetLeft(f.CvYMax, 0); Canvas.SetTop(f.CvYMax, CVT - 7);
            f.CurveCv.Children.Add(f.CvYMax);

            // Área bajo la curva + la curva
            f.CurveFill = new Polygon { Fill = ACCENT, Opacity = 0.14 };
            f.CurveCv.Children.Add(f.CurveFill);
            f.CurveLine = new Polyline { Stroke = ACCENT, StrokeThickness = 2.5, StrokeLineJoin = PenLineJoin.Round };
            f.CurveCv.Children.Add(f.CurveLine);

            // Punto vivo: dónde está el ventilador AHORA sobre la curva (solo en modo curve)
            f.CurveLive = new Ellipse { Width = 9, Height = 9, Fill = RED, Visibility = Visibility.Collapsed, IsHitTestVisible = false };
            f.CurveCv.Children.Add(f.CurveLive);

            // Doble clic en el lienzo: añadir un punto donde se hizo clic.
            f.CurveCv.MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e) {
                if (e.ClickCount != 2) return;
                double t, r; CvInverse(f, e.GetPosition(f.CurveCv), out t, out r);
                // insertar en su sitio por temperatura y ajustarlo a los vecinos, para que
                // el punto nuevo nunca deje la curva con una bajada
                int i = 0;
                while (i < f.Pts.Count && f.Pts[i].T < t) i++;
                f.Pts.Insert(i, new CurvePt(t, r));
                ClampToNeighbours(f, i, ref t, ref r);
                f.Pts[i].T = Math.Round(t); f.Pts[i].R = Math.Round(r);
                f.SyncFlat(); RenderCurve(f); MarkCurveEdited(f);
                e.Handled = true;
            };

            var frame = new Border {
                Background = BAR, CornerRadius = new CornerRadius(10),
                Padding = new Thickness(0, 4, 0, 2), Margin = new Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = f.CurveCv
            };
            wrap.Children.Add(frame);

            f.CurveReadout = new TextBlock { FontSize = 12, Foreground = SUB, Margin = new Thickness(2, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
            wrap.Children.Add(f.CurveReadout);
            wrap.Children.Add(new TextBlock {
                Text = "Drag a point to move it · double-click the graph to add one · right-click a point to remove it",
                FontSize = 10.5, Foreground = SUB, Opacity = 0.7, Margin = new Thickness(2, 4, 0, 0), TextWrapping = TextWrapping.Wrap
            });

            RenderCurve(f);
            return wrap;
        }

        // Posición en el lienzo -> (temperatura, RPM). Inverso de CvX/CvY.
        void CvInverse(FanUi f, Point p, out double temp, out double rpm) {
            temp = (p.X - CVL) / (CV_W - CVL - CVR) * CV_TMAX;
            if (temp < 0) temp = 0; if (temp > CV_TMAX) temp = CV_TMAX;
            double f01 = ((CV_H - CVB) - p.Y) / (CV_H - CVB - CVT);
            if (f01 < 0) f01 = 0; if (f01 > 1) f01 = 1;
            rpm = f.Min + f01 * (f.Max - f.Min);
        }

        // Mantiene un punto dentro de lo que permiten sus vecinos: no puede cruzarlos por
        // temperatura (se deja un hueco mínimo) ni romper la regla de que a más calor,
        // más revoluciones. Sin esto se pueden dibujar curvas absurdas — un punto que
        // adelanta al de al lado deja la curva bajando cuando la máquina se calienta.
        const double MIN_T_GAP = 3;
        void ClampToNeighbours(FanUi f, int i, ref double t, ref double r) {
            double tLo = (i > 0) ? f.Pts[i - 1].T + MIN_T_GAP : 0;
            double tHi = (i < f.Pts.Count - 1) ? f.Pts[i + 1].T - MIN_T_GAP : CV_TMAX;
            if (tHi < tLo) tHi = tLo;
            if (t < tLo) t = tLo; if (t > tHi) t = tHi;

            double rLo = (i > 0) ? f.Pts[i - 1].R : f.Min;
            double rHi = (i < f.Pts.Count - 1) ? f.Pts[i + 1].R : f.Max;
            if (rHi < rLo) rHi = rLo;
            if (r < rLo) r = rLo; if (r > rHi) r = rHi;
        }

        // Un punto arrastrable. El índice se guarda en el Tag y se mantiene válido porque
        // el orden de la lista nunca cambia (los puntos no pueden cruzarse).
        Ellipse MakeThumb(FanUi f) {
            var el = new Ellipse {
                Width = 15, Height = 15,
                Fill = CARD, Stroke = ACCENT, StrokeThickness = 3,
                Cursor = Cursors.SizeAll,
                ToolTip = "Drag to move · right-click to remove"
            };
            el.MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e) {
                if (e.ClickCount == 2) { e.Handled = true; return; }   // no añadir encima de un punto
                f.CurveDrag = (int)el.Tag; el.CaptureMouse(); e.Handled = true;
            };
            el.MouseLeftButtonUp += delegate (object s, MouseButtonEventArgs e) {
                if (f.CurveDrag >= 0) { f.CurveDrag = -1; el.ReleaseMouseCapture(); MarkCurveEdited(f); }
            };
            el.MouseMove += delegate (object s, MouseEventArgs e) {
                if (f.CurveDrag < 0 || f.CurveDrag >= f.Pts.Count) return;
                if (!ReferenceEquals(f.Thumbs[f.CurveDrag], el)) return;
                double t, r; CvInverse(f, e.GetPosition(f.CurveCv), out t, out r);
                ClampToNeighbours(f, f.CurveDrag, ref t, ref r);
                var pt = f.Pts[f.CurveDrag];
                pt.T = Math.Round(t); pt.R = Math.Round(r);
                f.SyncFlat();
                RenderCurve(f);
            };
            el.MouseRightButtonUp += delegate (object s, MouseButtonEventArgs e) {
                int i = (int)el.Tag;
                if (f.Pts.Count <= 2) { status.Text = "A curve needs at least two points."; e.Handled = true; return; }
                if (i >= 0 && i < f.Pts.Count) {
                    f.Pts.RemoveAt(i); f.SyncFlat(); RenderCurve(f); MarkCurveEdited(f);
                }
                e.Handled = true;
            };
            return el;
        }

        // Si la curva ya está activa, aplicar los cambios en cuanto se sueltan (sin
        // tener que volver a pulsar "Apply curve"); si no, solo se guarda al aplicar.
        void MarkCurveEdited(FanUi f) {
            if (f.CurMode == "curve") ApplyCurveFromUi(f, true);
        }

        // Re-dibuja la curva y sincroniza el número de puntos arrastrables.
        void RenderCurve(FanUi f) {
            if (f.CurveCv == null) return;
            if (f.Pts.Count < 2) return;

            double xl = CVL, xr = CV_W - CVR, yb = CV_H - CVB;
            var line = new PointCollection();
            var fill = new PointCollection();
            fill.Add(new Point(xl, yb));
            line.Add(new Point(xl, CvY(f, f.Pts[0].R)));       // plano antes del primer punto
            fill.Add(new Point(xl, CvY(f, f.Pts[0].R)));
            foreach (var pt in f.Pts) {
                var q = new Point(CvX(pt.T), CvY(f, pt.R));
                line.Add(q); fill.Add(q);
            }
            var last = f.Pts[f.Pts.Count - 1];
            line.Add(new Point(xr, CvY(f, last.R)));           // plano después del último
            fill.Add(new Point(xr, CvY(f, last.R)));
            fill.Add(new Point(xr, yb));
            f.CurveLine.Points = line;
            f.CurveFill.Points = fill;

            // crear/eliminar anillos hasta igualar el número de puntos
            while (f.Thumbs.Count < f.Pts.Count) {
                var el = MakeThumb(f);
                f.Thumbs.Add(el);
                f.CurveCv.Children.Add(el);
            }
            while (f.Thumbs.Count > f.Pts.Count) {
                var el = f.Thumbs[f.Thumbs.Count - 1];
                f.CurveCv.Children.Remove(el);
                f.Thumbs.RemoveAt(f.Thumbs.Count - 1);
            }
            for (int i = 0; i < f.Pts.Count; i++) {
                var el = f.Thumbs[i];
                el.Tag = i;
                Canvas.SetLeft(el, CvX(f.Pts[i].T) - 7.5);
                Canvas.SetTop(el, CvY(f, f.Pts[i].R) - 7.5);
            }

            // Etiquetas de ticks en °C o °F según la preferencia
            if (f.CvTicks != null) {
                int i = 0;
                for (int t = 20; t <= 100 && i < f.CvTicks.Length; t += 20, i++)
                    f.CvTicks[i].Text = Settings.Fahrenheit ? ((int)Math.Round(t * 9.0 / 5.0 + 32)) + "°" : t + "°";
            }
            if (f.CvYMin != null) f.CvYMin.Text = ((int)f.Min).ToString();
            if (f.CvYMax != null) f.CvYMax.Text = ((int)f.Max).ToString();

            var sb = new StringBuilder();
            for (int i = 0; i < f.Pts.Count; i++) {
                if (i > 0) sb.Append("   ·   ");
                sb.Append(string.Format("{0} → {1:0}", ShortTemp(f.Pts[i].T), f.Pts[i].R));
            }
            f.CurveReadout.Text = sb.ToString() + " RPM";
        }

        bool lastGuardHit = false;

        // ---- Registro a CSV -------------------------------------------------------
        // Una fila por refresco: hora, RPM de cada ventilador y cada sensor curado.
        // Sirve para ver qué pasó durante una partida o un render largo.
        static string LogPath {
            get {
                return System.IO.Path.Combine(
                    System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RPMac"),
                    "history.csv");
            }
        }
        bool logHeaderWritten;
        void WriteLogRow(List<FanInfo> infos, Dictionary<string, double> curated) {
            if (!Settings.LogToFile) { logHeaderWritten = false; return; }
            try {
                var keys = new List<string>();
                foreach (var c in CURATED) if (curated.ContainsKey(c[0])) keys.Add(c[0]);

                var sb = new StringBuilder();
                if (!logHeaderWritten) {
                    if (!System.IO.File.Exists(LogPath) || new System.IO.FileInfo(LogPath).Length == 0) {
                        sb.Append("time");
                        for (int i = 0; i < infos.Count; i++) sb.Append(",fan").Append(i).Append("_rpm");
                        foreach (var k in keys) sb.Append(',').Append(k.Trim());
                        sb.Append("\r\n");
                    }
                    logHeaderWritten = true;
                }
                sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                foreach (var fi in infos) sb.Append(',').Append(double.IsNaN(fi.Actual) ? "" : ((int)fi.Actual).ToString());
                foreach (var k in keys) {
                    double v = curated[k];
                    sb.Append(',').Append(double.IsNaN(v) ? "" : v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
                }
                sb.Append("\r\n");
                System.IO.File.AppendAllText(LogPath, sb.ToString());
            } catch { }
        }

        // Suaviza el RPM que se manda al SMC. Sin esto el ventilador persigue cada
        // oscilación de un grado y se oye subir y bajar todo el rato:
        //  - ignora diferencias menores que SMOOTH_DEAD RPM,
        //  - sube rápido (la mitad del camino de golpe) para no quedarse corto si algo
        //    se calienta de verdad, y baja despacio (una cuarta parte) para que no se
        //    note el vaivén al enfriar.
        const double SMOOTH_DEAD = 25;
        static double Smooth(FanUi f, double want) {
            double prev = f.LastCurveRpm;
            if (double.IsNaN(prev)) return want;             // primera vez: sin suavizar
            double d = want - prev;
            if (Math.Abs(d) < SMOOTH_DEAD) return prev;      // zona muerta
            return prev + d * (d > 0 ? 0.5 : 0.25);
        }

        // Coloca el punto vivo de la curva (temp actual → RPM que pide la curva).
        void UpdateCurveLive(FanUi f) {
            if (f.CurveCv == null || f.CurveLive == null) return;
            float t = f.LastCurveTemp;
            if (f.CurMode != "curve" || float.IsNaN(t)) { f.CurveLive.Visibility = Visibility.Collapsed; return; }
            double rpm = CurveRpm(t, f.Flat);
            Canvas.SetLeft(f.CurveLive, CvX(t) - 4.5);
            Canvas.SetTop(f.CurveLive, CvY(f, rpm) - 4.5);
            f.CurveLive.Visibility = Visibility.Visible;
        }

        // Read the curve editor controls, validate, cache the values on the FanUi (so the
        // refresh loop can use them without touching UI), switch the fan to curve mode and
        // persist. The loop then drives the RPM each tick.
        void ApplyCurveFromUi(FanUi f) { ApplyCurveFromUi(f, false); }

        // 'quiet' = el usuario solo movió un punto de una curva que ya estaba activa:
        // se guarda y sigue, sin volver a anunciar el modo.
        void ApplyCurveFromUi(FanUi f, bool quiet) {
            if (f.Pts.Count < 2) { status.Text = "Curve: needs at least two points."; return; }
            string key = null;
            var item = f.CurveSensor.SelectedItem as ComboBoxItem;
            if (item != null) key = item.Tag as string;
            if (key == null) { status.Text = "Curve: pick a sensor first."; return; }
            f.CurveSensorKey = key;
            f.SyncFlat();
            if (!quiet) SetMode(f, "curve");
            Settings.SetFanCurve(f.Index, key, PointsToText(f));
            ClearActivePreset();
            status.Text = string.Format("Fan {0}: curve on · {1} · {2} points, {3:0}–{4:0} RPM",
                f.Index, (key == HIGHEST_SENSOR ? "highest temp" : key), f.Pts.Count,
                f.Pts[0].R, f.Pts[f.Pts.Count - 1].R);
        }

        // Copia la curva (y el sensor) de un ventilador a todos los demás. Los puntos se
        // reescalan al rango de RPM de cada ventilador, porque no todos comparten mínimo
        // y máximo: copiar 1900 RPM a un ventilador que tope en 1200 no tendría sentido.
        void CopyCurveToAll(FanUi src) {
            if (src.Pts.Count < 2) return;
            var item = src.CurveSensor.SelectedItem as ComboBoxItem;
            string key = (item == null) ? src.CurveSensorKey : item.Tag as string;
            if (key == null) return;
            int n = 0;
            foreach (var f in fans) {
                if (ReferenceEquals(f, src) || f.CurveSensor == null) continue;
                var pts = new List<CurvePt>();
                foreach (var p in src.Pts) {
                    double frac = (src.Max > src.Min) ? (p.R - src.Min) / (src.Max - src.Min) : 0;
                    if (frac < 0) frac = 0; if (frac > 1) frac = 1;
                    pts.Add(new CurvePt(p.T, Math.Round(f.Min + frac * (f.Max - f.Min))));
                }
                f.Pts = pts; f.SyncFlat();
                foreach (var obj in f.CurveSensor.Items) {
                    var it = obj as ComboBoxItem;
                    if (it != null && (it.Tag as string) == key) { f.CurveSensor.SelectedItem = it; break; }
                }
                RenderCurve(f);
                f.CurveSensorKey = key;
                SetMode(f, "curve");
                Settings.SetFanCurve(f.Index, key, PointsToText(f));
                n++;
            }
            ApplyCurveFromUi(src);
            ClearActivePreset();
            status.Text = string.Format("Curve copied to {0} other fan{1}.", n, n == 1 ? "" : "s");
        }

        // Curva -> texto para el config: "t0,r0;t1,r1;..."
        static string PointsToText(FanUi f) {
            var sb = new StringBuilder();
            for (int i = 0; i < f.Pts.Count; i++) {
                if (i > 0) sb.Append(';');
                sb.Append((int)f.Pts[i].T).Append(',').Append((int)f.Pts[i].R);
            }
            return sb.ToString();
        }

        // Texto -> curva. Devuelve null si no es válido (menos de 2 puntos).
        static List<CurvePt> PointsFromText(string s) {
            if (string.IsNullOrEmpty(s)) return null;
            var list = new List<CurvePt>();
            foreach (var part in s.Split(';')) {
                var kv = part.Split(',');
                if (kv.Length != 2) continue;
                double t, r;
                if (!double.TryParse(kv[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out t)) continue;
                if (!double.TryParse(kv[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out r)) continue;
                list.Add(new CurvePt(t, r));
            }
            if (list.Count < 2) return null;
            list.Sort(delegate (CurvePt a, CurvePt b) { return a.T.CompareTo(b.T); });
            // Sanear lo que venga del archivo: separar puntos pegados y quitar bajadas de
            // RPM. Una curva guardada por una versión anterior (o editada a mano) podía
            // tener un tramo descendente, que no tiene sentido para refrigerar.
            for (int i = 1; i < list.Count; i++) {
                if (list[i].T < list[i - 1].T + MIN_T_GAP) list[i].T = list[i - 1].T + MIN_T_GAP;
                if (list[i].R < list[i - 1].R) list[i].R = list[i - 1].R;
            }
            return list;
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

                // Cabecera: nombre del fan a la izquierda, RPM grande a la derecha (estilo escritorio)
                string fname = null;
                try { fname = Smc.FanName(fi.Index); } catch { }
                var head = new Grid { Margin = new Thickness(0, 0, 0, 2) };
                head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var headL = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
                headL.Children.Add(new TextBlock {
                    Text = fname == null ? ("FAN " + fi.Index) : ("FAN " + fi.Index + " · " + fname.ToUpperInvariant()),
                    FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = SUB
                });
                headL.Children.Add(new TextBlock {
                    Text = string.Format("{0:0}–{1:0} RPM range", fmn, double.IsNaN(fi.Max) ? 6000 : fi.Max),
                    FontSize = 11, Foreground = SUB, Opacity = 0.75, Margin = new Thickness(0, 2, 0, 4)
                });
                head.Children.Add(headL);
                var rpmRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
                f.Rpm = new TextBlock { Text = "—", FontSize = 34, FontWeight = FontWeights.Bold, Foreground = TXT };
                rpmRow.Children.Add(f.Rpm);
                rpmRow.Children.Add(new TextBlock { Text = "RPM", FontSize = 13, Foreground = SUB, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(6, 0, 0, 7) });
                Grid.SetColumn(rpmRow, 1);
                head.Children.Add(rpmRow);
                col.Children.Add(head);

                // barra visual de RPM, con una marca vertical en el RPM objetivo (target)
                var barGrid = new Grid { Width = BAR_W, Height = 14, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 8) };
                var track = new Border { Background = CHIP, CornerRadius = new CornerRadius(4), Height = 8, VerticalAlignment = VerticalAlignment.Center };
                f.BarFill = new Border { Background = ACCENT, CornerRadius = new CornerRadius(4), Height = 8, Width = 0, HorizontalAlignment = HorizontalAlignment.Left };
                track.Child = f.BarFill;
                barGrid.Children.Add(track);
                f.TargetTick = new Border { Width = 2, Background = SUB, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Stretch, Visibility = Visibility.Collapsed, ToolTip = "Target RPM" };
                barGrid.Children.Add(f.TargetTick);
                col.Children.Add(barGrid);

                f.Info = new TextBlock { Text = "", FontSize = 12, Foreground = SUB, Margin = new Thickness(0, 0, 0, 12) };
                col.Children.Add(f.Info);

                // Selector de modo como control segmentado (una sola pieza, no 4 botones sueltos)
                var seg = new StackPanel { Orientation = Orientation.Horizontal };
                f.Auto = SegButton("Auto", delegate { if (!Guard()) return; Smc.SetFanAuto(f.Index); SetMode(f, "auto"); Settings.SetFan(f.Index, "auto", 0); ClearActivePreset(); });
                f.MaxBtn = SegButton("Max", delegate { if (!Guard()) return; Smc.SetFanMax(f.Index); SetMode(f, "max"); Settings.SetFan(f.Index, "max", 0); ClearActivePreset(); });
                f.Manual = SegButton("Manual", delegate { if (!Guard()) return; SetMode(f, "manual"); });
                f.Auto.ToolTip = "Give the fan back to the Mac's own thermal control";
                f.MaxBtn.ToolTip = "Run this fan at full speed";
                f.Manual.ToolTip = "Hold a fixed RPM you choose";
                seg.Children.Add(f.Auto); seg.Children.Add(f.MaxBtn); seg.Children.Add(f.Manual);
                if (availSensors.Count > 0) {
                    f.CurveBtn = SegButton("Curve", delegate { if (!Guard()) return; SetMode(f, "curve"); });
                    f.CurveBtn.ToolTip = "Ramp the RPM automatically from a temperature sensor";
                    seg.Children.Add(f.CurveBtn);
                }
                col.Children.Add(new Border {
                    Background = BAR, BorderBrush = BORDER, BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10), Padding = new Thickness(3),
                    HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 14),
                    Child = seg
                });

                var manualRow = new StackPanel { Orientation = Orientation.Horizontal };
                double mn = double.IsNaN(fi.Min) ? 0 : fi.Min;
                double tg = double.IsNaN(fi.Target) ? mn : fi.Target;
                f.Slider = new Slider { Minimum = mn, Maximum = f.Max, Value = tg, Width = 390, IsEnabled = false, VerticalAlignment = VerticalAlignment.Center };
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

                    // Curva por defecto: silenciosa hasta 40 °C, a tope a 80 °C.
                    f.Pts.Add(new CurvePt(40, f.Min));
                    f.Pts.Add(new CurvePt(80, f.Max));
                    f.SyncFlat();
                    cv.Children.Add(BuildCurveCanvas(f));
                    f.CurveRow = cv;
                    col.Children.Add(cv);

                    var fc = f;
                    var applyRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
                    var curveApply = Chip("Apply curve", ACCENT, Brushes.White, delegate { if (!Guard()) return; ApplyCurveFromUi(fc); });
                    f.CurveApply = curveApply;
                    applyRow.Children.Add(curveApply);
                    // Con varios ventiladores, copiar esta curva a todos ahorra repetir el
                    // trabajo punto por punto en cada uno (Mac Pro con 4-5 ventiladores).
                    if (Smc.GetFans().Count > 1) {
                        var toAll = Chip("Copy to all fans", CHIP, TXT, delegate { if (!Guard()) return; CopyCurveToAll(fc); });
                        toAll.ToolTip = "Give every fan this same curve and sensor";
                        applyRow.Children.Add(toAll);
                    }
                    f.CurveApplyRow = applyRow;
                    col.Children.Add(applyRow);
                }

                if (!Smc.WritesAllowed) {
                    f.Auto.Opacity = 0.45; f.MaxBtn.Opacity = 0.45; f.Manual.Opacity = 0.45; apply.Opacity = 0.45;
                    if (f.CurveBtn != null) f.CurveBtn.Opacity = 0.45;
                    if (f.CurveApply != null) f.CurveApply.Opacity = 0.45;
                }

                // El modo ya se ve en el control segmentado; el TextBlock se conserva (SetMode
                // lo escribe) pero fuera del árbol visual para no repetir información.
                f.Mode = new TextBlock { Text = "", FontSize = 11, Foreground = SUB };

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

        // Nombre legible de un sensor curado (para la barra de estado)
        static string CuratedName(string key) {
            foreach (var c in CURATED) if (c[0] == key) return c[1];
            return key;
        }

        // Sección a la que pertenece un sensor curado (para los subtítulos de la tarjeta)
        static string TempGroup(string key) {
            if (key == "TCGC") return "GPU";                 // GPU vía PECI (empieza por TC)
            if (key.StartsWith("TC")) return "CPU";
            if (key.StartsWith("TG")) return "GPU";
            return "SYSTEM";
        }

        // Fila de sensor: nombre | clave SMC (monoespaciada, tenue) | valor. Se estira
        // al ancho de su tarjeta (columnas con proporción fija).
        UIElement TempRowKeyed(string name, string key) {
            var g = new Grid { Margin = new Thickness(0, 3.5, 0, 3.5) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });

            var nm = new TextBlock { Text = name, Foreground = SUB, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center };
            g.Children.Add(nm);
            var kk = new TextBlock { Text = key.Trim(), Foreground = SUB, Opacity = 0.5, FontSize = 10, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(kk, 1); g.Children.Add(kk);
            var val = new TextBlock { Text = "—", Foreground = TXT, TextAlignment = TextAlignment.Right, FontWeight = FontWeights.SemiBold, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(val, 2); g.Children.Add(val);
            curatedLabels[key] = val;
            return g;
        }

        // ---- Gráfica de historial (últimos minutos) -------------------------------
        // Muestra el sensor más caliente y el RPM del primer ventilador. El eje de
        // temperatura es fijo (20-100 °C) para que la línea no "salte" al reescalar.
        const int HIST_MAX = 150;                 // 150 muestras x 2 s = 5 minutos
        const double HIST_H = 142, HIST_TMIN = 20, HIST_TMAX = 100;
        readonly List<float[]> hist = new List<float[]>();   // [tempC, rpm]
        Canvas histCv; Polyline histTempLine, histRpmLine;
        TextBlock histLegendTemp, histLegendRpm;
        double histRpmMax = 1;

        void BuildHistoryCard(Panel parent) {
            var col = new StackPanel();
            col.Children.Add(SectionLabel("Last 5 minutes", 0));

            histCv = new Canvas { Width = CV_W, Height = HIST_H, ClipToBounds = true };
            for (int i = 1; i <= 3; i++) {   // rejilla horizontal
                double y = HIST_H * i / 4.0;
                histCv.Children.Add(new Line { X1 = 0, Y1 = y, X2 = CV_W, Y2 = y, Stroke = BORDER, StrokeThickness = 1, Opacity = 0.6 });
            }
            histRpmLine = new Polyline { Stroke = SUB, StrokeThickness = 1.6, Opacity = 0.8, StrokeLineJoin = PenLineJoin.Round };
            histTempLine = new Polyline { Stroke = ACCENT, StrokeThickness = 2, StrokeLineJoin = PenLineJoin.Round };
            histCv.Children.Add(histRpmLine);
            histCv.Children.Add(histTempLine);

            col.Children.Add(new Border {
                Background = BAR, CornerRadius = new CornerRadius(10), Padding = new Thickness(0, 6, 0, 6),
                HorizontalAlignment = HorizontalAlignment.Left, Child = histCv
            });

            var legend = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 8, 0, 0) };
            legend.Children.Add(new Border { Width = 9, Height = 3, CornerRadius = new CornerRadius(1.5), Background = ACCENT, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            histLegendTemp = new TextBlock { Text = "Hottest sensor", FontSize = 11.5, Foreground = SUB, VerticalAlignment = VerticalAlignment.Center };
            legend.Children.Add(histLegendTemp);
            legend.Children.Add(new Border { Width = 9, Height = 3, CornerRadius = new CornerRadius(1.5), Background = SUB, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 6, 0) });
            histLegendRpm = new TextBlock { Text = "Fan RPM", FontSize = 11.5, Foreground = SUB, VerticalAlignment = VerticalAlignment.Center };
            legend.Children.Add(histLegendRpm);
            col.Children.Add(legend);

            parent.Children.Add(Card(col));
        }

        void PushHistory(double temp, double rpm, double rpmMax) {
            if (histCv == null) return;
            if (rpmMax > 0) histRpmMax = rpmMax;
            hist.Add(new[] { (float)temp, (float)rpm });
            while (hist.Count > HIST_MAX) hist.RemoveAt(0);

            var pt = new PointCollection();
            var pr = new PointCollection();
            double step = CV_W / (double)(HIST_MAX - 1);
            for (int i = 0; i < hist.Count; i++) {
                // la muestra más nueva queda pegada al borde derecho
                double x = CV_W - (hist.Count - 1 - i) * step;
                float t = hist[i][0], r = hist[i][1];
                if (!float.IsNaN(t)) {
                    double f = (t - HIST_TMIN) / (HIST_TMAX - HIST_TMIN);
                    if (f < 0) f = 0; if (f > 1) f = 1;
                    pt.Add(new Point(x, HIST_H - f * HIST_H));
                }
                if (!float.IsNaN(r) && histRpmMax > 0) {
                    double f = r / histRpmMax;
                    if (f < 0) f = 0; if (f > 1) f = 1;
                    pr.Add(new Point(x, HIST_H - f * HIST_H));
                }
            }
            histTempLine.Points = pt;
            histRpmLine.Points = pr;
            if (!double.IsNaN(temp)) histLegendTemp.Text = "Hottest sensor  " + FormatTemp(temp);
            if (!double.IsNaN(rpm)) histLegendRpm.Text = "Fan RPM  " + ((int)rpm);
        }

        // Panel compacto de la página de ventiladores: nombre + valor, agrupado.
        // Usa su propio diccionario de etiquetas (summaryLabels) porque la página de
        // sensores ya registró las suyas en curatedLabels para las mismas claves.
        readonly Dictionary<string, TextBlock> summaryLabels = new Dictionary<string, TextBlock>();

        void BuildLiveTemps(Panel parent) {
            var col = new StackPanel();
            col.Children.Add(SectionLabel("Live temperatures", 0));

            string lastGroup = null; int shown = 0;
            foreach (var c in CURATED) {
                double v = Smc.ReadTemp(c[0]);
                if (double.IsNaN(v) || v < 5 || v > 120) continue;
                string g = TempGroup(c[0]);
                if (g != lastGroup) {
                    col.Children.Add(new TextBlock { Text = g, FontSize = 9.5, Foreground = SUB, Opacity = 0.65, Margin = new Thickness(0, shown == 0 ? 2 : 9, 0, 3) });
                    lastGroup = g;
                }
                var g2 = new Grid { Margin = new Thickness(0, 2.5, 0, 2.5) };
                g2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
                g2.Children.Add(new TextBlock { Text = c[1], Foreground = SUB, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
                var val = new TextBlock { Text = "—", Foreground = TXT, FontSize = 12, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(val, 1); g2.Children.Add(val);
                summaryLabels[c[0]] = val;
                col.Children.Add(g2);
                shown++;
            }
            if (shown == 0) col.Children.Add(new TextBlock { Text = "No sensors detected.", Foreground = SUB, FontSize = 12 });

            var card = Card(col);
            card.Margin = new Thickness(4, 6, 0, 10);
            card.VerticalAlignment = VerticalAlignment.Top;
            parent.Children.Add(card);
        }

        // Página de sensores: una tarjeta por grupo (CPU / GPU / SYSTEM) en columnas.
        void BuildTempsPane(Panel parent) {
            var groups = new[] { "CPU", "GPU", "SYSTEM" };
            var row = new Grid();
            for (int i = 0; i < groups.Length; i++)
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int total = 0;
            for (int i = 0; i < groups.Length; i++) {
                var col = new StackPanel();
                col.Children.Add(SectionLabel(groups[i], 0));
                int shown = 0;
                foreach (var c in CURATED) {
                    if (TempGroup(c[0]) != groups[i]) continue;
                    double v = Smc.ReadTemp(c[0]);
                    if (double.IsNaN(v) || v < 5 || v > 120) continue;
                    col.Children.Add(TempRowKeyed(c[1], c[0]));
                    shown++;
                }
                if (shown == 0) col.Children.Add(new TextBlock { Text = "None detected", Foreground = SUB, Opacity = 0.6, FontSize = 12 });
                total += shown;
                var card = Card(col);
                card.Margin = new Thickness(i == 0 ? 0 : 5, 0, i == groups.Length - 1 ? 0 : 5, 10);
                card.VerticalAlignment = VerticalAlignment.Top;
                Grid.SetColumn(card, i);
                row.Children.Add(card);
            }
            parent.Children.Add(row);
            if (total == 0) parent.Children.Add(Card(new TextBlock { Text = "No known sensors detected on this Mac.", Foreground = SUB, TextWrapping = TextWrapping.Wrap }));

            // Lista cruda (todas las claves T* que responden), bajo demanda
            var rawCol = new StackPanel();
            rawCol.Children.Add(SectionLabel("All sensors (raw)", 0));
            rawCol.Children.Add(new TextBlock { Text = "Every temperature key the SMC reports, including ones RPMac can't name.", Foreground = SUB, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });
            var toggle = Chip("Show all sensors (raw)", CHIP, TXT, delegate { ToggleAll(); });
            toggle.HorizontalAlignment = HorizontalAlignment.Left;
            rawCol.Children.Add(toggle);
            allPanel = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            allContainer = new Border { Child = allPanel, Visibility = Visibility.Collapsed };
            rawCol.Children.Add(allContainer);
            parent.Children.Add(Card(rawCol));
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
            var track = new Border { Width = 40, Height = 22, CornerRadius = new CornerRadius(11), Background = initial ? ACCENT : CHIP, BorderBrush = BORDER, BorderThickness = new Thickness(1), Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
            var knob = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(8), Background = Brushes.White, HorizontalAlignment = initial ? HorizontalAlignment.Right : HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 2, 0) };
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

            // ---- Smooth fan changes ----
            var rowSm = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 14, 0, 0) };
            var labelsSm = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            labelsSm.Children.Add(new TextBlock { Text = "Smooth fan changes", Foreground = TXT, FontSize = 13, FontWeight = FontWeights.SemiBold });
            labelsSm.Children.Add(new TextBlock { Text = "Ignore tiny temperature wobbles and ease the fan down slowly, so a curve doesn't make it audibly hunt up and down.", Foreground = SUB, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            var toggleSm = BuildToggle(Settings.Smoothing, delegate (bool on) {
                Settings.Smoothing = on; Settings.Save();
                foreach (var fu in fans) fu.LastCurveRpm = double.NaN;   // empezar limpio
                status.Text = on ? "Smoothing: on" : "Smoothing: off";
            });
            DockPanel.SetDock(toggleSm, Dock.Right);
            rowSm.Children.Add(toggleSm);
            rowSm.Children.Add(labelsSm);
            col.Children.Add(rowSm);

            // ---- Thermal safety limit ----
            var rowG = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 14, 0, 0) };
            var labelsG = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            labelsG.Children.Add(new TextBlock { Text = "Emergency cooling", Foreground = TXT, FontSize = 13, FontWeight = FontWeights.SemiBold });
            var guardSub = new TextBlock { Foreground = SUB, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
            labelsG.Children.Add(guardSub);
            var toggleG = BuildToggle(Settings.SafetyGuard, delegate (bool on) {
                Settings.SafetyGuard = on; Settings.Save();
                status.Text = on ? "Emergency cooling: on" : "Emergency cooling: off";
            });
            DockPanel.SetDock(toggleG, Dock.Right);
            rowG.Children.Add(toggleG);
            rowG.Children.Add(labelsG);
            col.Children.Add(rowG);

            // umbral de la guardia térmica
            var guardRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            var guardSlider = new Slider { Minimum = 60, Maximum = 105, Value = Settings.GuardTemp, Width = 260, VerticalAlignment = VerticalAlignment.Center };
            Action paintGuard = delegate {
                guardSub.Text = string.Format("If any sensor reaches {0}, every fan goes to maximum until it cools down — whatever mode it's in.",
                    ShortTemp(Settings.GuardTemp));
            };
            paintGuard();
            guardSlider.ValueChanged += delegate {
                Settings.GuardTemp = (int)guardSlider.Value;
                paintGuard();
            };
            guardSlider.PreviewMouseUp += delegate { Settings.Save(); };
            guardRow.Children.Add(new TextBlock { Text = "Trigger at", Foreground = SUB, FontSize = 12, Width = 70, VerticalAlignment = VerticalAlignment.Center });
            guardRow.Children.Add(guardSlider);
            col.Children.Add(guardRow);

            // ---- CSV log ----
            var rowLog = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 14, 0, 0) };
            var labelsLog = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            labelsLog.Children.Add(new TextBlock { Text = "Record to a CSV file", Foreground = TXT, FontSize = 13, FontWeight = FontWeights.SemiBold });
            labelsLog.Children.Add(new TextBlock { Text = "Append every reading to history.csv, so you can look back at what ran hot during a game or a long render.", Foreground = SUB, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            var toggleLog = BuildToggle(Settings.LogToFile, delegate (bool on) {
                Settings.LogToFile = on; Settings.Save();
                status.Text = on ? "Recording to " + LogPath : "Recording stopped";
            });
            DockPanel.SetDock(toggleLog, Dock.Right);
            rowLog.Children.Add(toggleLog);
            rowLog.Children.Add(labelsLog);
            col.Children.Add(rowLog);

            var openLog = Chip("Open the folder", CHIP, TXT, delegate {
                try {
                    string dir = System.IO.Path.GetDirectoryName(LogPath);
                    if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                    System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
                } catch (Exception ex) { status.Text = "Couldn't open the folder: " + ex.Message; }
            });
            openLog.Margin = new Thickness(0, 10, 0, 0);
            openLog.HorizontalAlignment = HorizontalAlignment.Left;
            col.Children.Add(openLog);

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
            // Icono real de la app (recurso win32 del propio .exe). Si por lo que sea no
            // se puede extraer, se cae al círculo dibujado de siempre.
            try {
                string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var appIco = System.Drawing.Icon.ExtractAssociatedIcon(exe);
                if (appIco != null) { staticIconHandle = IntPtr.Zero; return appIco; }
            } catch { }
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
                else if (mode == "curve" && s.Length >= 4 && f.CurveSensor != null) {
                    // Dos formatos: el nuevo lleva los puntos en s[3] ("t,r;t,r;..."), el
                    // viejo (<= v1.5.x) traía tmin/tmax/rmin/rmax en s[3..6]. Se aceptan
                    // ambos para no perder la configuración al actualizar.
                    var pts = PointsFromText(s[3]);
                    if (pts == null && s.Length >= 7) {
                        double tmin, tmax, rmin, rmax;
                        double.TryParse(s[3], out tmin); double.TryParse(s[4], out tmax);
                        double.TryParse(s[5], out rmin); double.TryParse(s[6], out rmax);
                        if (tmax > tmin) pts = new List<CurvePt> { new CurvePt(tmin, rmin), new CurvePt(tmax, rmax) };
                    }
                    if (pts == null) return;
                    f.CurveSensorKey = s[2];
                    f.Pts = pts; f.SyncFlat();
                    RenderCurve(f);
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
                case "curve":  return new[] { "curve", "0", f.CurveSensorKey ?? "", PointsToText(f) };
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
                presetChips.Children.Add(new TextBlock { Text = "No profiles yet. Set your fans up on the Fans page, then save the setup here.", Foreground = SUB, FontSize = 12, Margin = new Thickness(2, 2, 0, 10), FontStyle = FontStyles.Italic });
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
                        // Red de seguridad térmica: por encima del umbral, todo al máximo.
                        double hottest = HighestCurated(curated);
                        bool guardHit = Settings.SafetyGuard && !double.IsNaN(hottest) && hottest >= Settings.GuardTemp;
                        if (guardHit && Smc.WritesAllowed) {
                            foreach (var f in fans) Smc.SetFanMax(f.Index);
                        } else if (Smc.WritesAllowed) {
                            foreach (var f in fans) {
                                if (f.CurMode != "curve" || f.CurveSensorKey == null) {
                                    f.LastCurveTemp = float.NaN; f.LastCurveRpm = double.NaN; continue;
                                }
                                double t = (f.CurveSensorKey == HIGHEST_SENSOR)
                                    ? hottest
                                    : Smc.ReadTemp(f.CurveSensorKey);
                                f.LastCurveTemp = (float)t;   // para el punto vivo del editor gráfico
                                if (double.IsNaN(t)) continue;
                                double want = CurveRpm(t, f.Flat);
                                double send = Settings.Smoothing ? Smooth(f, want) : want;
                                f.LastCurveRpm = send;
                                Smc.SetFanRpm(f.Index, send);
                            }
                        }
                        if (guardHit != lastGuardHit) {
                            lastGuardHit = guardHit;
                            foreach (var f in fans) { f.LastCurveRpm = double.NaN; }
                        }
                        WriteLogRow(infos, curated);

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
                                // marca del RPM objetivo sobre la barra
                                if (f.TargetTick != null && !double.IsNaN(fi.Target) && f.Max > 0) {
                                    double tf = fi.Target / f.Max;
                                    if (tf < 0) tf = 0; if (tf > 1) tf = 1;
                                    f.TargetTick.Margin = new Thickness(BAR_W * tf - 1, 0, 0, 0);
                                    f.TargetTick.Visibility = Visibility.Visible;
                                }
                                // El rango min-max ya está en la cabecera de la tarjeta
                                f.Info.Text = string.Format("Target {0:0} RPM · {1}",
                                    fi.Target, fi.Forced ? "controlled by RPMac" : "controlled by the Mac");
                                UpdateCurveLive(f);
                            }
                            UpdateTemps(curated, curatedLabels);
                            UpdateTemps(curated, summaryLabels);
                            if (all != null) UpdateTemps(all, allLabels);
                            UpdateOverlay(infos, curated);
                            ApplyTrayMode(curated);
                            statusDot.Background = Smc.WritesAllowed ? GOOD : WARN;
                            double hot = double.NaN; string hotKey = null;
                            foreach (var kv in curated)
                                if (!double.IsNaN(kv.Value) && (hotKey == null || kv.Value > hot)) { hot = kv.Value; hotKey = kv.Key; }
                            status.Text = "Driver OK · "
                                + fans.Count + (fans.Count == 1 ? " fan" : " fans")
                                + " · " + curatedLabels.Count + " sensors"
                                + (hotKey != null ? " · hottest: " + CuratedName(hotKey) + " " + FormatTemp(hot) : "")
                                + " · updated " + DateTime.Now.ToString("HH:mm:ss");
                            PushHistory(hotKey == null ? double.NaN : hot,
                                        infos.Count > 0 ? infos[0].Actual : double.NaN,
                                        fans.Count > 0 ? fans[0].Max : 0);
                            // Lectura viva en la barra de título (visible en todas las páginas)
                            if (titleTemp != null && hotKey != null) {
                                string rpmPart = (infos.Count > 0 && !double.IsNaN(infos[0].Actual))
                                    ? "   ·   " + ((int)infos[0].Actual) + " RPM" : "";
                                titleTemp.Text = FormatTemp(hot) + rpmPart;
                                titleTemp.Foreground = TempBrush(hot);
                            }
                        });
                    } catch { }
                    Thread.Sleep(2000);
                }
            }) { IsBackground = true }.Start();
        }

        // Color del valor según el calor: normal → ámbar (>=65°C) → rojo (>=80°C)
        static Brush TempBrush(double c) {
            return c >= 80 ? (Brush)RED : (c >= 65 ? (Brush)WARN : (Brush)TXT);
        }

        void UpdateTemps(Dictionary<string, double> vals, Dictionary<string, TextBlock> labels) {
            foreach (var kv in vals) {
                TextBlock t;
                if (!double.IsNaN(kv.Value) && labels.TryGetValue(kv.Key, out t)) {
                    t.Tag = kv.Value;            // guardamos el valor crudo en °C para poder reformatear
                    t.Text = FormatTemp(kv.Value);
                    t.Foreground = TempBrush(kv.Value);
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
            foreach (var f in fans) if (f.CurveCv != null) RenderCurve(f);   // ticks/readout de la curva en °C/°F
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
        // Suaviza la curva: ignora cambios pequeños y baja despacio, para que el ventilador
        // no persiga cada oscilación de un grado (que se oye como un subir/bajar constante).
        public static bool Smoothing = true;
        // Red de seguridad: por encima de GuardTemp todos los ventiladores van al máximo,
        // sin importar el modo en el que estén.
        public static bool SafetyGuard = false;
        public static int GuardTemp = 90;
        // Guarda una fila por refresco en %APPDATA%\RPMac\history.csv
        public static bool LogToFile = false;

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
                    else if (s.Length >= 2 && s[0] == "smooth") Smoothing = (s[1] == "1");
                    else if (s.Length >= 2 && s[0] == "guard") SafetyGuard = (s[1] == "1");
                    else if (s.Length >= 2 && s[0] == "guardtemp") { int g; if (int.TryParse(s[1], out g) && g >= 60 && g <= 105) GuardTemp = g; }
                    else if (s.Length >= 2 && s[0] == "logcsv") LogToFile = (s[1] == "1");
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
                lines.Add("smooth|" + (Smoothing ? "1" : "0"));
                lines.Add("guard|" + (SafetyGuard ? "1" : "0"));
                lines.Add("guardtemp|" + GuardTemp);
                lines.Add("logcsv|" + (LogToFile ? "1" : "0"));
                foreach (var kv in Fans) lines.Add("fan|" + kv.Key + "|" + string.Join("|", kv.Value));
                foreach (var p in Presets)
                    foreach (var kv in p.Value)
                        lines.Add("preset|" + p.Key + "|" + kv.Key + "|" + string.Join("|", kv.Value));
                System.IO.File.WriteAllLines(FilePath, lines.ToArray());
            } catch { }
        }
        public static void SetFan(int idx, string mode, int rpm) { Fans[idx] = new string[] { mode, rpm.ToString() }; Save(); }
        public static void SetFanCurve(int idx, string sensor, string points) {
            Fans[idx] = new string[] { "curve", "0", sensor, points };
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
