using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.ComponentModel;

namespace Tideglass;

public partial class MainWindow : Window
{
    private World world;
    private readonly Aquarium view;
    private readonly SpecimenView specimen;
    private readonly SimulationRunner runner;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private double previous, uiTime, saveTime, lastViewportUpdate;
    private int speed = 1;
    private bool paused, hidden, screenshotTaken, saveAllowed = true;
    private readonly bool smoke = Environment.GetCommandLineArgs().Any(a => a.StartsWith("--smoke"));
    private readonly bool smokeDetail = Environment.GetCommandLineArgs().Contains("--smoke-detail");
    private bool detailSelected;
    private bool closingComplete, closingStarted, changingWorld;
    private readonly bool perf = Environment.GetCommandLineArgs().Any(a => a == "--smoke-perf" || a == "--smoke-reduced" || a == "--smoke-display-benchmark");
    private readonly bool smallSmoke = Environment.GetCommandLineArgs().Contains("--smoke-small");
    private readonly List<double> frameTimes = [];
    private readonly List<double> renderCosts = [];
    private bool wandering;
    private double lastWander = -100;
    private readonly DispatcherTimer maintenanceTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly bool noDisplaySmoke = Environment.GetCommandLineArgs().Contains("--smoke-no-display");
    private double noDisplaySmokeStarted;
    private bool noDisplaySmokeRestored;
    private readonly bool displayBenchmark = Environment.GetCommandLineArgs().Contains("--smoke-display-benchmark");
    private bool displayBenchmarkStarted, displayBenchmarkFinished;
    private double displayBenchmarkAt, displayBenchmarkTime;
    private Task? saveOperation;
    private bool wakeRequested, wakeFailed;
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);
    public MainWindow()
    {
        InitializeComponent();
        string? error = null;
        try
        {
            if (!smoke && File.Exists(World.SavePath) && !File.Exists(World.SavePath + ".pre-3d")) File.Copy(World.SavePath, World.SavePath + ".pre-3d");
            if (!smoke && File.Exists(World.SavePath) && !File.Exists(World.SavePath + ".pre-shoreline")) File.Copy(World.SavePath, World.SavePath + ".pre-shoreline");
            if (!smoke && File.Exists(World.SavePath) && !File.Exists(World.SavePath + ".pre-coordination")) File.Copy(World.SavePath, World.SavePath + ".pre-coordination");
            if (!smoke && File.Exists(World.SavePath) && !File.Exists(World.SavePath + ".pre-tissue-ecology")) File.Copy(World.SavePath, World.SavePath + ".pre-tissue-ecology");
            if (!smoke && File.Exists(World.SavePath) && !File.Exists(World.SavePath + ".pre-wiring")) File.Copy(World.SavePath, World.SavePath + ".pre-wiring");
            if (!smoke && File.Exists(World.SavePath) && !File.Exists(World.SavePath + ".pre-moisture")) File.Copy(World.SavePath, World.SavePath + ".pre-moisture");
            if (!smoke && File.Exists(World.SavePath) && !File.Exists(World.SavePath + ".pre-pressures")) File.Copy(World.SavePath, World.SavePath + ".pre-pressures");
            if (!smoke && File.Exists(World.SavePath) && !File.Exists(World.SavePath + ".pre-branching")) File.Copy(World.SavePath, World.SavePath + ".pre-branching");
            world = (!smoke || perf) && File.Exists(World.SavePath) ? World.Load() : World.Create();
        }
        catch (Exception ex)
        {
            world = World.Create(); error = "Save could not be opened: " + ex.Message;
            try { File.Copy(World.SavePath, World.SavePath + ".unreadable-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")); }
            catch { saveAllowed = false; error += "\nAutomatic saving is disabled to preserve that file."; }
        }
        if (Environment.GetCommandLineArgs().Contains("--smoke-evolved")) world = World.Load(File.Exists("branching-validation.json") ? "branching-validation.json" : "tidal-validation.json");
        if (Environment.GetCommandLineArgs().Contains("--smoke-appendages"))
        {
            world.Organisms.Clear(); world.Rocks.Clear();
            var g = new Genome { Cells = 4, Radius = 8, Elongation = 1.4, Flattening = .6, Contract = .65, Hue = .13, Turn = 0 };
            g.Pattern[0].Outgrowth = .95; g.Pattern[0].Stiffness = .8; g.Pattern[0].Bend = .85;
            var o = new Organism { Id = 1, Genes = g, Age = 80, Energy = 85, Heading = .3 };
            for (int n = 0; n < 4; n++) o.Body.Add(new Cell { Parent = n - 1, Position = new(1330 * World.HabitatScale + n * 12, 700 * World.HabitatScale + n * 3.6, world.Ground(1348 * World.HabitatScale, 705 * World.HabitatScale) + 16) });
            world.Organisms.Add(o);
        }
        if (Environment.GetCommandLineArgs().Contains("--smoke-branching"))
        {
            world.Organisms.Clear(); world.Rocks.Clear();
            var g = new Genome { Cells = 1, Radius = 9, Elongation = 1.25, Flattening = .7, Contract = .6, Hue = .64, Buds = [] };
            for (int n = 0; n < 3; n++)
            {
                int root = g.Buds.Count;
                g.Buds.Add(new BudGene { AnchorAngle = n * 2.05 + .3, Length = 1.5, Width = .3, Elevation = -.1, Stiffness = .8, Muscle = .5, Phase = n * 1.8, TouchResponse = -.5 });
                g.Buds.Add(new BudGene { Parent = root, Length = 1.5, Width = .22, Elevation = -.75, Turn = .2, Stiffness = .75, Muscle = .6, Phase = n * 1.8 + .6 });
                g.Buds.Add(new BudGene { Parent = root + 1, Length = 1, Width = n == 2 ? .85 : .17, Flatness = n == 2 ? .18 : .85, Photo = n == 2 ? .85 : .05, Elevation = .3, Turn = -.4, Muscle = .4 });
                if (n == 0) g.Buds.Add(new BudGene { Parent = root, Length = 1.6, Width = .35, Turn = -1, Elevation = .4, Digestion = .8, Muscle = .6, Phase = 2 });
            }
            var o = new Organism { Id = 1, Age = 100, Energy = 80, Genes = g, Heading = .4, Body = [new Cell { Position = new(2400, 1050, world.Ground(2400, 1050) + 18) }], BudStates = g.Buds.Select(b => new BudState { Growth = 1, FormedAt = 0 }).ToList() };
            world.Organisms.Add(o);
        }
        runner = new SimulationRunner(world);
        if (Environment.GetCommandLineArgs().Contains("--smoke-reduced")) ReducedDisplayBox.IsChecked = true;
        world = runner.Snapshot;
        view = new Aquarium(world);
        specimen = new SpecimenView(view); SpecimenHost.Child = specimen;
        ViewportHost.Child = view;
        view.SelectedChanged += UpdateInspector;
        view.ManualNavigation += () => { StopWandering(); view.Following = false; };
        CompositionTarget.Rendering += Frame;
        maintenanceTimer.Tick += Maintenance;
        maintenanceTimer.Start();
        Closing += OnClosing;
        PreviewKeyDown += OnKey;
        PreviewKeyUp += (_, e) => { if (view.SetNavigationKey(e.Key, false)) e.Handled = true; };
        Deactivated += (_, _) => view.CancelNavigation();
        SizeChanged += (_, _) => { SaveLabel.Visibility = ActualWidth < 1240 ? Visibility.Collapsed : Visibility.Visible; SpeedLabel.Visibility = ActualWidth < 1080 ? Visibility.Collapsed : Visibility.Visible; };
        if (error != null) { SaveLabel.Text = "Previous save unavailable"; Loaded += (_, _) => MessageBox.Show(this, error, "Tideglass"); }
        if (perf) { speed = displayBenchmark ? 100 : 20; runner.Speed = speed; }
        if (!smoke) view.Coast();
        if (noDisplaySmoke || Environment.GetCommandLineArgs().Contains("--no-display")) Loaded += (_, _) =>
        {
            noDisplaySmokeStarted = runner.Status.Time;
            NoDisplayClick(this, new());
        };
        if (smallSmoke) { Width = 960; Height = 640; }
        if (Environment.GetCommandLineArgs().Contains("--smoke-coast")) view.Coast();
        if (Environment.GetCommandLineArgs().Contains("--smoke-rocks")) view.RockPreview();
        if (Environment.GetCommandLineArgs().Contains("--smoke-evolved"))
        { view.Select(world.Organisms.OrderByDescending(o => o.Body.Count * Appendages.Reach(o)).FirstOrDefault()); view.Following = true; view.Zoom = 10; }
        if (Environment.GetCommandLineArgs().Contains("--smoke-appendages") || Environment.GetCommandLineArgs().Contains("--smoke-branching"))
        { view.Select(world.Organisms[0]); view.Following = true; view.Zoom = 11; }
    }
    private void Frame(object? sender, EventArgs args)
    {
        double now = clock.Elapsed.TotalSeconds;
        if (now == previous || changingWorld || runner.NoDisplay) return;
        bool reduced = runner.ReducedDisplay && !paused && speed > 1;
        if (reduced && now - lastViewportUpdate < .1) return;
        lastViewportUpdate = now;
        double interval = now - previous; previous = now;
        var next = runner.Snapshot;
        if (!ReferenceEquals(world, next))
        {
            world = next; view.AcceptSnapshot(world);
        }
        view.RenderFrame();
        if (now > 2) { frameTimes.Add(interval * 1000); renderCosts.Add(view.LastRenderMilliseconds); }
        if (!perf && frameTimes.Count > 600) { frameTimes.RemoveRange(0, 300); renderCosts.RemoveRange(0, 300); }
        if (wandering && (now - lastWander > 24 || (view.Selected != null && !world.Organisms.Contains(view.Selected))))
        {
            lastWander = now;
            var candidates = world.Organisms.Where(o => o != view.Selected).OrderByDescending(o => o.Body.Count + o.GroundContacts * 2 + o.BudStates.Count(b => b.Growth > .1) * .5 + Appendages.Reach(o) * 7 + (world.Time - o.LastAttack < 2 ? 4 : 0) + (world.Time - o.BirthTime < 25 ? 3 : 0)).Take(60).ToArray();
            if (candidates.Length > 0)
            {
                view.Select(candidates[Random.Shared.Next(candidates.Length)]); view.Following = true; view.Zoom = 6;
            }
        }
        if ((smokeDetail || smallSmoke || perf) && now > 1 && !detailSelected)
        {
            detailSelected = true;
            view.Select(world.Organisms.Where(o => o.Body.Count >= 3).OrderBy(o => (o.Center - new V(World.Width / 2, World.Height / 2)).Length).FirstOrDefault());
            FollowClick(this, new()); view.Zoom = perf ? 4.2 : 7;
            double before = world.Time;
            PauseClick(this, new());
            if (!paused || world.Time != before) throw new InvalidOperationException("Pause failed");
            PauseClick(this, new());
            WaterClick(this, new()); WaterClick(this, new());
            view.Orbit(.25, -.1);
        }
        if (now - uiTime > .4)
        {
            uiTime = now;
            specimen.InvalidateVisual();
            Population.Text = world.Organisms.Count.ToString("N0"); Generation.Text = world.RecordGeneration.ToString();
            Elapsed.Text = $"{(int)(world.Time / 3600):00}:{(int)(world.Time / 60) % 60:00}:{(int)world.Time % 60:00}";
            EventText.Text = world.Events.FirstOrDefault() ?? "";
            HabitatLabel.Text = $"{world.Births:N0} births · {world.Deaths:N0} deaths · {world.Exchanges:N0} genetic exchanges";
            int exposed = world.Organisms.Count(o => world.Immersion(o.Center, o.Genes.Radius) < .3);
            int bottom = world.Organisms.Count(o => o.Center.Z - world.Ground(o.Center.X, o.Center.Y) < o.Genes.Radius * 3);
            HabitatLabel.Text += $"\n{bottom:N0} near the bottom / {exposed:N0} exposed / {world.Organisms.Count(o => o.GroundContacts > 0):N0} gripping";
            var waterAt = view.Selected?.Center ?? new V(World.Width / 2, World.Height / 2, World.Depth / 2);
            WaterLabel.Text = $"{world.Habitat.Temperature(waterAt, world.Time):F1} °C · O₂ {world.Oxygen[world.Tile(waterAt)] * 100:F0}% · carbon {world.Carbon[world.Tile(waterAt)]:F2}\nTide {world.WaterLevel:F0} / {(Math.Cos(world.Time * Math.Tau / 360) > 0 ? "rising" : "falling")} / light {Shoreline.Daylight(world.Time):P0}" + (view.ShowWater ? " · O₂ slice on" : "");
            WaterLabel.Text += $"\nCurrent {world.FlowAt(waterAt).Length:F1} / {world.Clumps.Count:N0} food parcels";
            WaterLabel.Text += $" / soil {world.SoilMoisture[world.SurfaceTile(waterAt)]:P0}" + (world.Rain > .05 ? " / rain" : "");
            ExtinctionPanel.Visibility = world.Organisms.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            bool limited = !paused && speed > 1 && runner.AchievedSpeed > 0 && runner.AchievedSpeed < speed * .85;
            SpeedLabel.Text = paused ? "Paused" : $"{speed}\u00d7 / {runner.AchievedSpeed:F1}\u00d7 actual" + (limited ? " - limited" : "");
            SpeedLabel.Foreground = limited ? Brushes.Khaki : new SolidColorBrush(Color.FromRgb(136, 166, 154));
            SpeedLabel.ToolTip = limited ? "The simulation is using its available CPU throughput. Higher targets cannot exceed that limit. Physics still advances in fixed steps." : "Requested speed / measured ecosystem seconds per real second.";
            if (runner.Error != null) { SpeedLabel.Text = "Simulation stopped"; SpeedLabel.ToolTip = runner.Error; }
            double average = frameTimes.Count > 0 ? frameTimes.TakeLast(60).Average() : 16.7;
            RenderLabel.Text = $"3D · {Math.Min(999, 1000 / average):F0} fps";
            UpdateInspector();
        }
        if (smoke && !noDisplaySmoke && now > (perf ? 16 : 5) && !screenshotTaken)
        {
            screenshotTaken = true;
            var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(this);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(perf ? "preview-3d-perf.png" : smallSmoke ? "preview-3d-small.png" : smokeDetail ? "preview-3d-detail.png" : "preview-3d.png")) encoder.Save(file);
            if (perf)
            {
                frameTimes.Sort(); renderCosts.Sort();
                File.WriteAllText("performance-3d.txt", $"Hardware tier: {RenderCapability.Tier >> 16}\nPopulation: {world.Organisms.Count}\nCells: {view.VisibleCellCount}\nMean rendering-callback interval: {frameTimes.Average():F2} ms\nP95 rendering-callback interval: {frameTimes[(int)(frameTimes.Count * .95)]:F2} ms\nMean CPU render update: {renderCosts.Average():F2} ms\nRequested simulation: {speed}x\nAchieved simulation: {runner.AchievedSpeed:F2}x\n");
            }
            Close();
        }
    }
    private void UpdateInspector()
    {
        var o = view.Selected;
        Inspector.Visibility = o == null || hidden ? Visibility.Collapsed : Visibility.Visible;
        if (o == null) return;
        SpecimenHost.Visibility = Visibility.Visible;
        bool alive = world.Organisms.Contains(o);
        CreatureTitle.Text = $"Organism {o.Id:0000}";
        CreatureInfo.Text = $"Generation {o.Generation} · {(alive ? "living" : "dead")}\nAge {o.Age / 60:F1} min · {o.Children} offspring\n" + (o.ParentId == 0 ? "Founder · no ancestors" : $"Parent #{o.ParentId}" + (o.MateId > 0 ? $" + #{o.MateId}" : " · asexual birth"));
        string Change(double n, double? p) => p.HasValue && Math.Abs(n - p.Value) > .005 ? $"  ({n - p.Value:+0.00;-0.00} inherited Δ)" : "";
        var g = o.Genes; var old = o.ParentGenes;
        Traits.Text = $"ENERGY   {Math.Max(0, o.Energy):F0} / {o.Capacity:F0}\nCELLS   {o.Body.Count} / {g.Cells}" + Change(g.Cells, old?.Cells) + $"\nCELL SIZE   {g.Radius:F1}" + Change(g.Radius, old?.Radius) + $"\nCONTRACTION   {g.Contract:F2}" + Change(g.Contract, old?.Contract) + $"\nSENSING   {g.Sense:F2}" + Change(g.Sense, old?.Sense) + $"\nBRANCHING   {g.Branch:F2}" + Change(g.Branch, old?.Branch);
        Traits.Text += $"\nENZYMES   {g.Enzymes:F2}" + Change(g.Enzymes, old?.Enzymes);
        Traits.Text += $"\nLENGTH / THICKNESS   {g.Elongation:F2} / {g.Flattening:F2}\nTAPER / EDGE GROWTH   {g.Taper:F2} / {g.EdgeGrowth:F2}";
        Traits.Text += $"\nOUTGROWTH   {Appendages.Reach(o):F2}\nSTIFFNESS   {(g.Buds?.Count > 0 ? g.Buds.Average(b => b.Stiffness) : g.Pattern.Average(r => r.Stiffness)):F2}\nHYDRATION   {o.Hydration:P0}\nGROUND CONTACTS   {o.GroundContacts}";
        if (g.Buds != null)
        {
            int roots = g.Buds.Count(b => b.Parent < 0), forks = g.Buds.Where(b => b.Parent >= 0).GroupBy(b => b.Parent).Count(group => group.Count() > 1);
            Traits.Text += $"\nATTACHMENT SITES   {roots}\nTISSUE PIECES   {o.BudStates.Count(b => b.Growth > .05)} / {g.Buds.Count}\nBRANCH POINTS   {forks}\nSIGNAL CONNECTIONS   {g.Buds.Sum(b => b.Wires.Count)}";
            if (o.BudStates.Count > 0) Traits.Text += $"\nLOCAL RESPONSE   {o.BudStates.Average(b => Math.Abs(b.Activation)):F2}";
        }
        Traits.Text += $"\nTISSUE HEALTH   {o.Body.Average(c => c.Integrity):P0}\nANCHORING   {o.Anchoring:F2}\nLOCAL CURRENT   {world.FlowAt(o.Center).Length:F1}";
        if (o.BudStates.Count > 0) Traits.Text += $"\nBRANCH HEALTH   {o.BudStates.Average(b => b.Integrity):P0}\nTISSUE SIGNAL   {o.BudStates.Average(b => Math.Abs(b.Signal)):F2}";
        Traits.Text += $"\nWATERPROOFING   {g.Waterproofing:P0}\nMOISTURE RESPONSE   {g.MoistureSense:F2}\nLOCAL SOIL WATER   {world.SoilMoisture[world.SurfaceTile(o.Center)]:P0}";
        Traits.Text += $"\nMOVEMENT POWER   {o.LocomotionPower:F3}\nCONTACT SLIP   {o.ContactSlip:F2}";
        string activity = !alive ? "Decomposed" : o.Irritation > .15 ? "Withdrawing from tissue damage" : world.Time - o.LastContact < 1 ? "Adhering on contact" : world.Time - o.LastAttack < .7 ? "Tissue-feeding contact" : world.Time - o.LastMeal < .5 ? "Feeding" : o.GroundContacts > 0 ? "Pushing against the substrate" : world.Immersion(o.Center, g.Radius) < .3 ? "Exposed by the tide" : "Moving through water";
        CreatureInfo.Text += "\n" + activity;
        double total = Math.Max(.001, o.LightEnergy + o.GrazingEnergy + o.TissueEnergy + o.ShoreEnergy);
        Traits.Text += $"\nGROWTH PATTERNS   {g.Pattern.Count}\n\nENERGY SOURCES / LIFETIME\nLight {o.LightEnergy / total:P0} · grazing {o.GrazingEnergy / total:P0}\nLiving tissue {o.TissueEnergy / total:P0}\nSurface grazing {o.ShoreEnergy / total:P0}";
        FollowButton.Content = !alive ? "Find another creature" : view.Following ? "Release camera" : "Follow this organism";
        DescendantButton.Visibility = world.Organisms.Any(c => c.ParentId == o.Id || c.MateId == o.Id) ? Visibility.Visible : Visibility.Collapsed;
    }
    private void FollowClick(object sender, RoutedEventArgs e)
    {
        StopWandering();
        if (view.Selected == null || !world.Organisms.Contains(view.Selected)) { view.Select(world.Organisms.OrderByDescending(o => o.Generation).FirstOrDefault()); view.Following = false; }
        view.Following = !view.Following;
        if (view.Following) view.Zoom = Math.Max(view.Zoom, 6);
        UpdateInspector();
    }
    private void DescendantClick(object sender, RoutedEventArgs e)
    {
        var child = world.Organisms.Where(o => o.ParentId == view.Selected?.Id || o.MateId == view.Selected?.Id).OrderByDescending(o => o.BirthTime).FirstOrDefault();
        if (child != null) { view.Select(child); view.Following = true; }
    }
    private void NoDisplayClick(object sender, RoutedEventArgs e)
    {
        StopWandering(); view.CancelNavigation();
        runner.NoDisplay = true;
        UpdateWakeRequest();
        CompositionTarget.Rendering -= Frame;
        SimulationView.Visibility = Visibility.Collapsed;
        NoDisplayPanel.Visibility = Visibility.Visible;
        WanderButton.IsEnabled = WaterButton.IsEnabled = OverviewButton.IsEnabled = false;
        NoDisplayStatus.Text = paused ? "Paused" : "Running at maximum throughput";
        RenderLabel.Text = "Display off";
    }
    private void ShowDisplayClick(object sender, RoutedEventArgs e)
    {
        runner.NoDisplay = false;
        UpdateWakeRequest();
        NoDisplayPanel.Visibility = Visibility.Collapsed;
        SimulationView.Visibility = Visibility.Visible;
        WanderButton.IsEnabled = WaterButton.IsEnabled = OverviewButton.IsEnabled = true;
        previous = clock.Elapsed.TotalSeconds; lastViewportUpdate = 0;
        CompositionTarget.Rendering -= Frame;
        CompositionTarget.Rendering += Frame;
    }
    private async void Maintenance(object? sender, EventArgs e)
    {
        if (closingStarted || changingWorld) return;
        double now = clock.Elapsed.TotalSeconds;
        // This timer also runs when the viewport is hidden or the window is minimized.
        if (!smoke && now - saveTime > 60) { saveTime = now; _ = Save(); }
        if (runner.NoDisplay)
        {
            if (runner.Error != null) UpdateWakeRequest();
            var status = runner.Status;
            Population.Text = status.Population.ToString("N0"); Generation.Text = status.Generation.ToString();
            Elapsed.Text = $"{(int)(status.Time / 3600):00}:{(int)(status.Time / 60) % 60:00}:{(int)status.Time % 60:00}";
            SpeedLabel.Text = paused ? "Paused" : $"Max / {status.Speed:F1}x actual";
            SpeedLabel.Foreground = new SolidColorBrush(Color.FromRgb(136, 166, 154));
            SpeedLabel.ToolTip = "Display is off; simulation runs without a speed cap. Pause and autosave remain available.";
            NoDisplayStatus.Text = runner.Error != null ? "Simulation stopped: " + runner.Error
                : paused ? "Paused" : status.Population == 0 ? "Population extinct. Environment continues running." : "Running at maximum throughput";
            if (wakeFailed) NoDisplayStatus.Text += " / Could not prevent sleep";
        }
        if (displayBenchmark && now > 2 && !displayBenchmarkStarted)
        {
            displayBenchmarkStarted = true;
            displayBenchmarkTime = await runner.ReadTimeAsync(); displayBenchmarkAt = clock.Elapsed.TotalSeconds;
        }
        if (displayBenchmark && now > 14 && !displayBenchmarkFinished)
        {
            displayBenchmarkFinished = true;
            double end = await runner.ReadTimeAsync();
            File.WriteAllText("display-benchmark.txt", $"Mode={(runner.NoDisplay ? "off" : "reduced")} average={(end - displayBenchmarkTime) / (clock.Elapsed.TotalSeconds - displayBenchmarkAt):F2}x");
            if (runner.NoDisplay) ShowDisplayClick(this, new());
            return;
        }
        if (displayBenchmark && displayBenchmarkFinished && now > 15) { Close(); return; }
        if (!displayBenchmark && noDisplaySmoke && now > 5 && !noDisplaySmokeRestored)
        {
            if (runner.Status.Time <= noDisplaySmokeStarted || runner.Error != null) throw new InvalidOperationException("No-display simulation failed");
            File.WriteAllText("no-display-check.txt", $"Advanced {runner.Status.Time - noDisplaySmokeStarted:F1} seconds; {runner.Status.Speed:F1}x actual; restoring display.");
            var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(this);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create("preview-no-display.png")) encoder.Save(file);
            ShowDisplayClick(this, new()); noDisplaySmokeRestored = true;
        }
        if (noDisplaySmoke && noDisplaySmokeRestored && now > 8)
        {
            if (runner.Snapshot.Time <= noDisplaySmokeStarted) throw new InvalidOperationException("Display snapshot did not resume");
            Close();
        }
    }

    private void KeepAwakeChanged(object sender, RoutedEventArgs e) { if (runner != null) UpdateWakeRequest(); }
    private void UpdateWakeRequest()
    {
        bool wanted = !smoke && !closingStarted && runner.Error == null && runner.NoDisplay && !paused && KeepAwakeBox.IsChecked == true;
        if (wanted == wakeRequested) return;
        // Keep the system running; the monitor may still turn off normally.
        wakeFailed = SetThreadExecutionState(wanted ? 0x80000001u : 0x80000000u) == 0;
        wakeRequested = wanted;
    }

    private void ReducedDisplayChanged(object sender, RoutedEventArgs e)
    {
        if (runner != null) runner.ReducedDisplay = ReducedDisplayBox.IsChecked == true;
    }
    private void PauseClick(object sender, RoutedEventArgs e) { paused = !paused; runner.Speed = paused ? 0 : speed; PauseButton.Content = paused ? "▶  Resume" : "Ⅱ  Pause"; UpdateWakeRequest(); }
    private void SpeedClick(object sender, RoutedEventArgs e)
    {
        var chosen = (Button)sender;
        speed = int.Parse((string)chosen.Tag); runner.Speed = paused ? 0 : speed;
        if (chosen.Parent is StackPanel buttons)
            foreach (var button in buttons.Children.OfType<Button>().Where(b => b.Tag != null))
            {
                button.FontWeight = button == chosen ? FontWeights.Bold : FontWeights.Normal;
                button.BorderBrush = button == chosen ? Brushes.LightSeaGreen : new SolidColorBrush(Color.FromRgb(49, 78, 77));
            }
    }
    private void CoastClick(object sender, RoutedEventArgs e) { StopWandering(); view.Coast(); }
    private void OverviewClick(object sender, RoutedEventArgs e) { StopWandering(); view.Overview(); }
    private void StopWandering() { wandering = false; WanderButton.Content = "Wander"; }
    private void WanderClick(object sender, RoutedEventArgs e) { wandering = !wandering; WanderButton.Content = wandering ? "Wandering" : "Wander"; lastWander = -100; if (!wandering) view.Following = false; }
    private void WaterClick(object sender, RoutedEventArgs e) { view.ShowWater = !view.ShowWater; WaterButton.Content = view.ShowWater ? "O₂ map on" : "O₂ map"; }
    private async void SaveClick(object sender, RoutedEventArgs e) => await Save();
    private Task Save()
    {
        if (saveOperation is { IsCompleted: false }) return saveOperation;
        return saveOperation = SaveCore();
    }
    private async Task SaveCore()
    {
        if (!saveAllowed) { SaveLabel.Text = "Save disabled · unreadable file preserved"; return; }
        try { await runner.SaveAsync(); SaveLabel.Text = "Saved " + DateTime.Now.ToString("HH:mm"); }
        catch (Exception ex) { SaveLabel.Text = "Save failed"; SaveLabel.ToolTip = ex.Message; }
    }
    private async void NewClick(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Start a new ecosystem? Your current world will be kept as a backup.", "New ecosystem", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        changingWorld = true;
        try { await runner.NewAsync(Random.Shared.Next()); }
        catch (Exception ex) { MessageBox.Show(this, "Could not back up the current world: " + ex.Message); changingWorld = false; return; }
        world = runner.Snapshot; view.World = world; view.Select(null); view.Overview(); changingWorld = false;
        SpecimenHost.Visibility = Visibility.Collapsed;
        CreatureTitle.Text = "Organism"; CreatureInfo.Text = ""; Traits.Text = ""; Inspector.Visibility = Visibility.Collapsed;
        FollowButton.Content = "Find a creature"; DescendantButton.Visibility = Visibility.Collapsed; await Save();
    }
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (closingComplete) return;
        e.Cancel = true; if (closingStarted) return;
        closingStarted = true; IsEnabled = false; maintenanceTimer.Stop(); CompositionTarget.Rendering -= Frame;
        runner.Speed = 0; UpdateWakeRequest();
        if (!smoke)
        {
            if (saveOperation is { IsCompleted: false }) await saveOperation;
            await SaveCore();
        }
        await runner.StopAsync(); closingComplete = true; Close();
    }
    private void OnKey(object sender, KeyEventArgs e)
    {
        if (runner.NoDisplay && e.Key != Key.Space) return;
        if (view.SetNavigationKey(e.Key, true)) { e.Handled = true; return; }
        if (e.IsRepeat) return;
        if (e.Key == Key.Space) { PauseClick(this, new()); e.Handled = true; }
        if (e.Key == Key.F) FollowClick(this, new());
        if (e.Key == Key.Escape) OverviewClick(this, new());
        if (e.Key == Key.G) WanderClick(this, new());
        if (e.Key == Key.H) { hidden = !hidden; Inspector.Visibility = hidden ? Visibility.Collapsed : Visibility.Visible; }
        if (e.Key == Key.F11) { WindowStyle = WindowStyle == WindowStyle.None ? WindowStyle.SingleBorderWindow : WindowStyle.None; WindowState = WindowState.Maximized; }
    }
}
