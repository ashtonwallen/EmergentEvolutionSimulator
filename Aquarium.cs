using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace Tideglass;

/// <summary>Retained, hardware-rendered 3D scene. Shared frozen meshes; transforms only per frame.</summary>
public sealed class Aquarium : Grid
{
    private readonly Viewport3D viewport = new() { ClipToBounds = true };
    private readonly PerspectiveCamera camera = new() { FieldOfView = 48, NearPlaneDistance = 1, FarPlaneDistance = 6500 };
    private readonly Model3DGroup scene = new(), life = new(), detail = new();
    private readonly Dictionary<int, CreatureVisual> creatures = [];
    private readonly Dictionary<Model3D, int> picking = [];
    private readonly Dictionary<int, Material> materials = [];
    private readonly MeshGeometry3D sphere = Sphere(20, 14), lowSphere = Sphere(8, 5), cylinder = Cylinder(10);
    private readonly Stopwatch watch = Stopwatch.StartNew();
    private World world;
    private HabitatVisual? habitatVisual;
    private double lastHabitatUpdate = -100;
    private int? selectedId;
    private Organism? deceasedSelection;
    private V target = new(World.Width / 2, World.Height / 2, 110);
    private double yaw = -.4, elevation = .65, snapshotAt, previousFrame;
    private Point mouseStart, lastMouse;
    private bool dragging, pan, moved;
    private MouseButton dragButton;
    private readonly HashSet<Key> navigationKeys = [];
    private V navigationVelocity, renderedTarget = new(World.Width / 2, World.Height / 2, 110);
    private double renderedYaw = -.4, renderedElevation = .65, renderedZoom = 1.25;
    private GeometryModel3D? oxygenPlane;
    private WriteableBitmap? oxygenTexture;
    private double lastWaterUpdate = -1;
    private double lastFoodUpdate = -1;
    private readonly GeometryModel3D algaeCloud = new(), detritusCloud = new();
    public World World { get => world; set { ResetScene(value); } }
    public Organism? Selected => selectedId.HasValue && creatures.TryGetValue(selectedId.Value, out var v) ? v.Organism : deceasedSelection;
    public bool Following { get; set; }
    public bool ShowWater { get; set; }
    public double Zoom { get; set; } = 1.25;
    public double LastRenderMilliseconds { get; private set; }
    public int VisibleCellCount => creatures.Values.Sum(v => v.Organism.Body.Count);
    public BodySurface? SelectedSurface => selectedId.HasValue && creatures.TryGetValue(selectedId.Value, out var v) ? v.Skin.Surface : null;
    public event Action? SelectedChanged;
    public event Action? ManualNavigation;

    private sealed class CreatureVisual
    {
        public Organism Organism = null!;
        public Model3DGroup Group = new();
        public OrganicVisual Skin = null!;
        public AppendageVisual Appendages = null!;
        public MatrixTransform3D Transform = new();
        public V From, To;
        public bool Visible = true;
        public int FogBand;
    }

    public Aquarium(World initial)
    {
        world = initial;
        Background = new LinearGradientBrush(Color.FromRgb(17, 50, 65), Color.FromRgb(3, 15, 24), 90);
        Children.Add(viewport); viewport.Camera = camera;
        viewport.Children.Add(new ModelVisual3D { Content = scene });
        RenderOptions.SetEdgeMode(viewport, EdgeMode.Aliased);
        ResetScene(initial);
        Focusable = true;
        MouseDown += (_, e) =>
        {
            if (e.ChangedButton != MouseButton.Left && e.ChangedButton != MouseButton.Right && e.ChangedButton != MouseButton.Middle) return;
            if (dragging) return;
            Focus(); ManualNavigation?.Invoke();
            dragging = true; moved = false; dragButton = e.ChangedButton;
            mouseStart = lastMouse = e.GetPosition(this);
            pan = dragButton == MouseButton.Middle || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            CaptureMouse(); e.Handled = true;
        };
        MouseMove += (_, e) =>
        {
            if (!dragging) return;
            Point p = e.GetPosition(this);
            if (!moved && (p - mouseStart).Length < 4) return;
            moved = true; Following = false;
            var d = p - lastMouse; lastMouse = p;
            if (pan)
            {
                double amount = 2 * (2050 / renderedZoom) * Math.Tan(camera.FieldOfView * Math.PI / 360) / Math.Max(1, ActualWidth);
                V right = new(Math.Cos(renderedYaw), -Math.Sin(renderedYaw));
                V up = new(-Math.Sin(renderedYaw) * Math.Sin(renderedElevation), -Math.Cos(renderedYaw) * Math.Sin(renderedElevation), Math.Cos(renderedElevation));
                target += (right * -d.X + up * d.Y) * amount;
                ClampTarget();
            }
            else { yaw -= d.X * .004; elevation = Math.Clamp(elevation + d.Y * .004, .06, 1.48); }
            e.Handled = true;
        };
        MouseUp += (_, e) =>
        {
            if (!dragging || e.ChangedButton != dragButton) return;
            bool click = !moved && dragButton == MouseButton.Left;
            dragging = false; ReleaseMouseCapture();
            if (click) Pick(e.GetPosition(viewport));
            e.Handled = true;
        };
        LostMouseCapture += (_, _) => { dragging = false; moved = false; };
        MouseWheel += (_, e) =>
        {
            ManualNavigation?.Invoke();
            Zoom = Math.Clamp(Zoom * Math.Exp(e.Delta / 120.0 * .14), .35, 18);
            e.Handled = true;
        };
    }

    public bool SetNavigationKey(Key key, bool down)
    {
        if (key is not (Key.W or Key.A or Key.S or Key.D or Key.Up or Key.Left or Key.Down or Key.Right or Key.Q or Key.E)) return false;
        if (down)
        {
            if (navigationKeys.Add(key)) { Following = false; ManualNavigation?.Invoke(); }
        }
        else navigationKeys.Remove(key);
        return true;
    }
    public void CancelNavigation()
    {
        navigationKeys.Clear(); navigationVelocity = new();
        dragging = false; if (IsMouseCaptured) ReleaseMouseCapture();
    }
    private void ClampTarget() => target = new(Math.Clamp(target.X, -250, World.Width + 250), Math.Clamp(target.Y, -250, World.Height + 250), Math.Clamp(target.Z, 0, World.Depth + 500));
    private void UpdateNavigation(double dt)
    {
        bool Held(Key a, Key b) => navigationKeys.Contains(a) || navigationKeys.Contains(b);
        double x = (Held(Key.D, Key.Right) ? 1 : 0) - (Held(Key.A, Key.Left) ? 1 : 0);
        double y = (Held(Key.W, Key.Up) ? 1 : 0) - (Held(Key.S, Key.Down) ? 1 : 0);
        double z = (navigationKeys.Contains(Key.E) ? 1 : 0) - (navigationKeys.Contains(Key.Q) ? 1 : 0);
        V input = new(Math.Cos(renderedYaw) * x - Math.Sin(renderedYaw) * y, -Math.Sin(renderedYaw) * x - Math.Cos(renderedYaw) * y, z);
        if (input.Length > 1) input *= 1 / input.Length;
        double speed = Math.Clamp(437.5 / renderedZoom, 56.25, 1500) * (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 3 : 1);
        navigationVelocity += (input * speed - navigationVelocity) * (1 - Math.Exp(-dt * 16));
        if (navigationVelocity.Length > .01) { target += navigationVelocity * dt; ClampTarget(); }
    }

    public void Select(Organism? o) { selectedId = o?.Id; deceasedSelection = o; SelectedChanged?.Invoke(); }
    public void Overview() { Following = false; Zoom = 1 / World.HabitatScale; target = new(World.Width / 2, World.Height / 2, 100); elevation = .72; yaw = -.4; }
    public void Coast() { Following = false; Zoom = 2.5 / World.HabitatScale; target = new(1280 * World.HabitatScale, 570 * World.HabitatScale, 225); elevation = .47; yaw = -.85; }
    internal void RockPreview() { if (world.Rocks.Count == 0) return; Following = false; Zoom = 10; target = world.Rocks[6 % world.Rocks.Count].Position; elevation = .6; yaw = -.85; }
    public void Orbit(double horizontal, double vertical) { yaw += horizontal; elevation = Math.Clamp(elevation + vertical, -.12, 1.5); }
    private void Pick(Point p)
    {
        VisualTreeHelper.HitTest(viewport, null, result =>
        {
            if (result is RayMeshGeometry3DHitTestResult hit && picking.TryGetValue(hit.ModelHit, out int id) && creatures.TryGetValue(id, out var creature))
            { Select(creature.Organism); Following = false; return HitTestResultBehavior.Stop; }
            return HitTestResultBehavior.Continue;
        }, new PointHitTestParameters(p));
    }

    private void ResetScene(World next)
    {
        world = next; scene.Children.Clear(); life.Children.Clear(); detail.Children.Clear(); creatures.Clear(); picking.Clear();
        scene.Children.Add(new AmbientLight(Color.FromRgb(100, 128, 133)));
        scene.Children.Add(new DirectionalLight(Color.FromRgb(235, 246, 209), new Vector3D(-.5, -1, -.3)));
        scene.Children.Add(new DirectionalLight(Color.FromRgb(64, 110, 151), new Vector3D(.8, -.15, 1)));
        habitatVisual = new HabitatVisual(world); scene.Children.Add(habitatVisual.Model);
        var stone = HabitatVisual.StoneMaterial();
        foreach (var rock in world.Rocks)
        {
            var eroded = sphere.Clone();
            var points = new Point3DCollection();
            for (int y = 0; y <= 14; y++) for (int x = 0; x <= 20; x++)
                eroded.TextureCoordinates.Add(new Point(x / 20.0, y / 14.0));
            foreach (var p in sphere.Positions)
            {
                double erosion = HabitatVisual.RockRadius(rock, new(p.X, p.Z, p.Y));
                points.Add(new Point3D(p.X * erosion, p.Y * erosion, p.Z * erosion));
            }
            eroded.Positions = points; eroded.Normals.Clear(); eroded.Freeze();
            var r = new GeometryModel3D(eroded, stone);
            r.Transform = new MatrixTransform3D(ScaleAt(rock.Position, rock.Radius, rock.Radius, rock.Radius)); r.Freeze(); scene.Children.Add(r);
        }
        algaeCloud.Material = Solid(Color.FromRgb(104, 145, 86), 0); detritusCloud.Material = Solid(Color.FromRgb(138, 112, 79), 0);
        scene.Children.Add(algaeCloud); scene.Children.Add(detritusCloud); lastFoodUpdate = -1;
        oxygenTexture = new WriteableBitmap(World.Cols, World.Rows, 96, 96, PixelFormats.Bgra32, null);
        var overlayBrush = new ImageBrush(oxygenTexture) { Opacity = .48 };
        oxygenPlane = new GeometryModel3D(Plane(World.Depth / 2), new DiffuseMaterial(overlayBrush));
        scene.Children.Add(life); scene.Children.Add(detail); scene.Children.Add(habitatVisual.Water); AcceptSnapshot(next);
    }
    private void AddLine(V a, V b, double radius, Material material)
    {
        var model = new GeometryModel3D(cylinder, material) { Transform = new MatrixTransform3D(LinkMatrix(a, b, radius)) }; model.Freeze(); scene.Children.Add(model);
    }

    public void AcceptSnapshot(World next)
    {
        double now = watch.Elapsed.TotalSeconds;
        double blend = Math.Clamp((now - snapshotAt) / .05, 0, 1);
        world = next;
        var alive = new HashSet<int>();
        foreach (var o in next.Organisms)
        {
            alive.Add(o.Id);
            if (!creatures.TryGetValue(o.Id, out var visual))
            {
                visual = new CreatureVisual { From = o.Center, To = o.Center }; visual.Group.Transform = visual.Transform;
                visual.Skin = new OrganicVisual(o, Membrane(o.Genes)); visual.Group.Children.Add(visual.Skin.Model); picking[visual.Skin.Model] = o.Id;
                visual.Appendages = new AppendageVisual(Membrane(o.Genes)); visual.Group.Children.Add(visual.Appendages.Model); picking[visual.Appendages.Model] = o.Id;
                creatures.Add(o.Id, visual); life.Children.Add(visual.Group);
            }
            visual.Organism = o;
            visual.From += (visual.To - visual.From) * blend; visual.To = o.Center;
        }
        foreach (int id in creatures.Keys.Where(id => !alive.Contains(id)).ToArray())
        {
            var v = creatures[id]; if (id == selectedId) deceasedSelection = v.Organism;
            foreach (Model3D m in v.Group.Children) picking.Remove(m);
            life.Children.Remove(v.Group); creatures.Remove(id);
        }
        snapshotAt = now;
        if (ShowWater && oxygenPlane != null && !scene.Children.Contains(oxygenPlane)) scene.Children.Insert(3, oxygenPlane);
        if (!ShowWater && oxygenPlane != null) scene.Children.Remove(oxygenPlane);
        if (ShowWater && now - lastWaterUpdate > .25 && oxygenTexture != null)
        {
            lastWaterUpdate = now;
            int layer = Math.Clamp((int)((Selected?.Center.Z ?? World.Depth / 2) / (World.Depth / World.Layers)), 0, World.Layers - 1);
            byte[] pixels = new byte[World.Cols * World.Rows * 4];
            for (int i = 0; i < World.Cols * World.Rows; i++)
            {
                double oxygen = Math.Clamp(next.Oxygen[layer * World.Cols * World.Rows + i], 0, 1);
                pixels[i * 4] = (byte)(90 + 120 * oxygen); pixels[i * 4 + 1] = (byte)(90 + 90 * oxygen); pixels[i * 4 + 2] = (byte)(215 * (1 - oxygen)); pixels[i * 4 + 3] = 255;
            }
            oxygenTexture.WritePixels(new Int32Rect(0, 0, World.Cols, World.Rows), pixels, World.Cols * 4, 0);
            oxygenPlane!.Transform = new TranslateTransform3D(0, (layer + .5) * World.Depth / World.Layers - World.Depth / 2, 0);
        }
        if (now - lastHabitatUpdate > .75) { lastHabitatUpdate = now; habitatVisual?.Update(next); }
        if (now - lastFoodUpdate > .4) { lastFoodUpdate = now; UpdateFood(); }
    }

    private void UpdateFood()
    {
        var green = new MeshGeometry3D(); var brown = new MeshGeometry3D();
        V focus = Selected?.Center ?? target;
        double reach = Math.Clamp(1000 / Zoom, 180, 850);
        void Parcel(V position, double radius, MeshGeometry3D mesh)
        {
            if ((position - focus).LengthSquared > reach * reach) return;
            var p = Point(position); double r = radius;
            int n = mesh.Positions.Count;
            mesh.Positions.Add(p + new Vector3D(r, 0, 0)); mesh.Positions.Add(p + new Vector3D(-r, 0, 0));
            mesh.Positions.Add(p + new Vector3D(0, r, 0)); mesh.Positions.Add(p + new Vector3D(0, -r, 0));
            mesh.Positions.Add(p + new Vector3D(0, 0, r)); mesh.Positions.Add(p + new Vector3D(0, 0, -r));
            foreach (int vertex in new[] { 0, 2, 4, 4, 2, 1, 1, 2, 5, 5, 2, 0, 4, 3, 0, 1, 3, 4, 5, 3, 1, 0, 3, 5 }) mesh.TriangleIndices.Add(n + vertex);
        }
        foreach (var c in world.Clumps) if (c.Energy > .005) Parcel(c.Position, c.Radius * Math.Min(1, Math.Sqrt(c.Energy)), c.Organic ? brown : green);
        foreach (var d in world.Detritus) if (d.Energy > 0) Parcel(d.Position, d.Radius * Math.Min(1, Math.Sqrt(d.Energy / 4)), brown);
        green.Freeze(); brown.Freeze(); algaeCloud.Geometry = green; detritusCloud.Geometry = brown;
    }

    public void RenderFrame()
    {
        var cost = Stopwatch.StartNew();
        double now = watch.Elapsed.TotalSeconds, elapsed = Math.Min(.1, now - previousFrame); previousFrame = now;
        double blend = Math.Clamp((now - snapshotAt) / .05, 0, 1);
        if (Following && Selected != null) target += (Selected.Center - target) * (1 - Math.Exp(-elapsed * 5));
        UpdateNavigation(elapsed);
        double smoothing = 1 - Math.Exp(-elapsed * 18);
        renderedTarget += (target - renderedTarget) * smoothing;
        renderedYaw += (yaw - renderedYaw) * smoothing;
        renderedElevation += (elevation - renderedElevation) * smoothing;
        renderedZoom = Math.Exp(Math.Log(renderedZoom) + (Math.Log(Zoom) - Math.Log(renderedZoom)) * smoothing);
        double distance = 2050 / renderedZoom;
        var focus = Point(renderedTarget);
        var eye = focus + new Vector3D(Math.Sin(renderedYaw) * Math.Cos(renderedElevation), Math.Sin(renderedElevation), Math.Cos(renderedYaw) * Math.Cos(renderedElevation)) * distance;
        camera.Position = eye; camera.LookDirection = focus - eye; camera.UpDirection = new Vector3D(0, 1, 0);
        var forward = camera.LookDirection; forward.Normalize();
        var right = Vector3D.CrossProduct(forward, camera.UpDirection); right.Normalize();
        var up = Vector3D.CrossProduct(right, forward);
        double tanH = Math.Tan(camera.FieldOfView * Math.PI / 360), tanV = tanH * Math.Max(1, ActualHeight) / Math.Max(1, ActualWidth);
        if (selectedId.HasValue && creatures.TryGetValue(selectedId.Value, out var chosen)) chosen.Skin.Prepare(chosen.Organism, true);
        int meshRequests = 0, animated = 0, limbRequests = 0;
        foreach (var visual in creatures.Values)
        {
            var o = visual.Organism;
            V position = visual.From + (visual.To - visual.From) * blend;
            var direction = Point(position) - eye;
            double along = Vector3D.DotProduct(direction, forward);
            double radius = Math.Max(visual.Skin.Extent, o.Genes.Radius * (2 + o.Body.Count) * o.Genes.Elongation) + Branching.Extent(o);
            bool visible = along + radius > 1 && Math.Abs(Vector3D.DotProduct(direction, right)) < along * tanH + radius * Math.Sqrt(1 + tanH * tanH) && Math.Abs(Vector3D.DotProduct(direction, up)) < along * tanV + radius * Math.Sqrt(1 + tanV * tanV);
            if (visible != visual.Visible)
            {
                visual.Visible = visible;
                if (visible) life.Children.Add(visual.Group); else life.Children.Remove(visual.Group);
            }
            if (!visible) continue;
            bool selected = o.Id == selectedId;
            int fogBand = selected ? 0 : Math.Clamp((int)((along - distance * .75) / 240), 0, 4);
            if (fogBand != visual.FogBand) { visual.FogBand = fogBand; visual.Skin.Model.Material = Membrane(o.Genes, fogBand); }
            if (selected || (visual.Skin.NeedsWork(o, false) && meshRequests++ < 4)) visual.Skin.Prepare(o, selected);
            bool movingSkin = selected || (direction.LengthSquared < 450 * 450 && animated++ < 3);
            visual.Skin.Animate(o, now, movingSkin);
            if (selected || (visual.Appendages.Due(now, movingSkin) && limbRequests++ < 18))
                visual.Appendages.Update(world, o, now, movingSkin, visual.Skin.Surface);
            var at = Point(position);
            V f = V.Direction(o.Heading, o.Pitch), s = new(-Math.Sin(o.Heading), Math.Cos(o.Heading), 0), u = new(-Math.Cos(o.Heading) * Math.Sin(o.Pitch), -Math.Sin(o.Heading) * Math.Sin(o.Pitch), Math.Cos(o.Pitch));
            double pulse = movingSkin ? 1 : 1 + Math.Sin(o.Age * o.Genes.Rhythm * 2 + o.Phase) * o.Genes.Contract * .05;
            visual.Transform.Matrix = new Matrix3D(f.X / pulse, f.Z / pulse, f.Y / pulse, 0, u.X * pulse, u.Z * pulse, u.Y * pulse, 0, s.X, s.Z, s.Y, 0, at.X, at.Y, at.Z, 1);
        }
        LastRenderMilliseconds = cost.Elapsed.TotalMilliseconds;
    }
    private Material Membrane(Genome genes, int fogBand = 0)
    {
        int key = (int)(genes.Hue * 48) + 48 * (int)(genes.Photosynthesis * 4) + fogBand * 256; if (materials.TryGetValue(key, out var result)) return result;
        double a = genes.Hue * Math.Tau;
        double r = 169 + 62 * Math.Cos(a), g = 143 + 48 * Math.Cos(a - 2), b = 137 + 57 * Math.Cos(a + 2);
        double chlorophyll = genes.Photosynthesis * .4;
        r = r * (1 - chlorophyll) + 95 * chlorophyll; g = g * (1 - chlorophyll) + 163 * chlorophyll;
        double fog = fogBand * .17;
        r = r * (1 - fog) + 15 * fog; g = g * (1 - fog) + 49 * fog; b = b * (1 - fog) + 61 * fog;
        const int width = 128, height = 64; var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            double u = (double)x / width, v = (double)y / height;
            double fold = Math.Pow(.5 + .5 * Math.Sin(u * 70 + Math.Sin(v * 12) * 1.8), 9);
            double vein = Math.Pow(.5 + .5 * Math.Cos(v * 30 + Math.Sin(u * 9) * 2), 22);
            double fleck = Math.Sin(x * 17.2 + y * 41.7) * Math.Sin(x * 8.1 - y * 31.3);
            double shade = .8 + .17 * Math.Sin(v * Math.PI) - fold * .075 - vein * .08 + fleck * .025;
            int i = (y * width + x) * 4;
            pixels[i] = (byte)Math.Clamp(b * shade, 0, 255); pixels[i + 1] = (byte)Math.Clamp(g * shade, 0, 255); pixels[i + 2] = (byte)Math.Clamp(r * shade, 0, 255); pixels[i + 3] = 255;
        }
        var texture = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4); texture.Freeze();
        var skin = new ImageBrush(texture); skin.Freeze();
        var material = new MaterialGroup(); material.Children.Add(new DiffuseMaterial(skin));
        material.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb(75, 190, 202, 176)), 18));
        material.Freeze(); result = material;
        materials[key] = result; return result;
    }
    private static Material Solid(Color color, double shine)
    {
        var group = new MaterialGroup(); group.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
        if (shine > 0) group.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromRgb(170, 192, 180)), shine));
        group.Freeze(); return group;
    }
    internal static Point3D Point(V v) => new(v.X - World.Width / 2, v.Z, v.Y - World.Height / 2);
    private static Matrix3D ScaleAt(V p, double x, double y, double z) { var q = Point(p); return new Matrix3D(x, 0, 0, 0, 0, y, 0, 0, 0, 0, z, 0, q.X, q.Y, q.Z, 1); }
    private static Matrix3D LinkMatrix(V a, V b, double radius)
    {
        var from = Point(a); var d = Point(b) - from;
        if (d.LengthSquared < .00001) d = new Vector3D(0, .001, 0);
        var axis = d; axis.Normalize();
        var side = Vector3D.CrossProduct(axis, Math.Abs(axis.Y) > .95 ? new Vector3D(1, 0, 0) : new Vector3D(0, 1, 0)); side.Normalize();
        var up = Vector3D.CrossProduct(axis, side); side *= radius; up *= radius;
        return new Matrix3D(side.X, side.Y, side.Z, 0, up.X, up.Y, up.Z, 0, d.X, d.Y, d.Z, 0, from.X, from.Y, from.Z, 1);
    }
    private static MeshGeometry3D Sphere(int slices, int stacks)
    {
        var mesh = new MeshGeometry3D();
        for (int y = 0; y <= stacks; y++) for (int x = 0; x <= slices; x++)
        {
            double v = y * Math.PI / stacks, u = x * Math.Tau / slices;
            var n = new Vector3D(Math.Sin(v) * Math.Cos(u), Math.Cos(v), Math.Sin(v) * Math.Sin(u));
            mesh.Positions.Add(new Point3D(n.X, n.Y, n.Z)); mesh.Normals.Add(n);
        }
        for (int y = 0; y < stacks; y++) for (int x = 0; x < slices; x++)
        {
            int a = y * (slices + 1) + x, b = a + slices + 1;
            foreach (int i in new[] { a, a + 1, b, a + 1, b + 1, b }) mesh.TriangleIndices.Add(i);
        }
        mesh.Freeze(); return mesh;
    }
    private static MeshGeometry3D Cylinder(int sides)
    {
        var mesh = new MeshGeometry3D();
        for (int i = 0; i <= sides; i++)
        {
            double a = i * Math.Tau / sides;
            for (int z = 0; z <= 1; z++) { mesh.Positions.Add(new Point3D(Math.Cos(a), Math.Sin(a), z)); mesh.Normals.Add(new Vector3D(Math.Cos(a), Math.Sin(a), 0)); }
        }
        for (int i = 0; i < sides; i++) foreach (int n in new[] { i * 2, i * 2 + 2, i * 2 + 1, i * 2 + 1, i * 2 + 2, i * 2 + 3 }) mesh.TriangleIndices.Add(n);
        mesh.Freeze(); return mesh;
    }
    private static MeshGeometry3D Plane(double altitude)
    {
        var mesh = new MeshGeometry3D
        {
            Positions = new Point3DCollection { Point(new(0, 0, altitude)), Point(new(0, World.Height, altitude)), Point(new(World.Width, World.Height, altitude)), Point(new(World.Width, 0, altitude)) },
            TextureCoordinates = new PointCollection { new(0, 0), new(0, 1), new(1, 1), new(1, 0) }, TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 }
        }; mesh.Freeze(); return mesh;
    }
}
