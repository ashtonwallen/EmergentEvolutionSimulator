using Tideglass;
using System.Diagnostics;

static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
var timer = Stopwatch.StartNew();
foreach (var genes in new[] { new Genome { Cells = 1 }, new Genome { Cells = 4 }, new Genome { Cells = 8, Elongation = 2.4, Flattening = .24, EdgeGrowth = .65 } })
{
    var mesh = BodySurface.Create(genes, genes.Cells, 22);
    Require(mesh.Vertices.Length > 50 && mesh.Triangles.Length > 150, "Empty tissue surface");
    Require(mesh.Vertices.All(p => double.IsFinite(p.Length)) && mesh.Normals.All(n=>n.Length > .9 && n.Length < 1.1), "Invalid tissue geometry");
    var edges = new Dictionary<(int,int),int>();
    var components = Enumerable.Range(0,mesh.Vertices.Length).ToArray();
    int Root(int a) { while (components[a] != a) { components[a] = components[components[a]]; a = components[a]; } return a; }
    var used = new HashSet<int>();
    for(int i=0;i<mesh.Triangles.Length;i+=3) for(int e=0;e<3;e++)
    {
        int a = mesh.Triangles[i+e], b = mesh.Triangles[i+(e+1)%3]; used.Add(a); used.Add(b); components[Root(a)] = Root(b);
        var edge = a < b ? (a,b) : (b,a); edges[edge] = edges.GetValueOrDefault(edge)+1;
    }
    Require(edges.Values.All(n=>n==2), "Tissue skin is not watertight");
    Require(used.Select(Root).Distinct().Count() == 1, "Tissue is disconnected into beads");
    Require(mesh.Vertices.Where((p,i)=>(mesh.DeformVertex(i,genes,1.1,.4)-mesh.DeformVertex(i,genes,1.8,.4)).Length > .01).Any(), "Contraction does not deform skin");
    Require(Enumerable.Range(0,mesh.Vertices.Length).All(i=>Math.Abs(Enumerable.Range(0,genes.Cells).Sum(n=>mesh.Weights[i*genes.Cells+n])-1) < 1e-9), "Skinning weights do not preserve a continuous blend");
}
Console.WriteLine("PASS: continuous connected watertight tissue meshes, normals, traveling deformation");
if (args.Length > 0 && args[0] == "--surface-check") return;
if (args.Length > 0 && args[0] == "--benchmark")
{
    var benchmark = World.Load(args.Length > 1 ? args[1] : World.SavePath);
    for (int i = 0; i < 10; i++) benchmark.Step(.1);
    long allocations = GC.GetAllocatedBytesForCurrentThread();
    var watch = Stopwatch.StartNew();
    for (int i = 0; i < 300; i++) benchmark.Step(.1);
    Console.WriteLine($"BENCHMARK population={benchmark.Organisms.Count} cells={benchmark.Organisms.Sum(o=>o.Body.Count)} ms/step={watch.Elapsed.TotalMilliseconds / 300:F3} allocated/step={(GC.GetAllocatedBytesForCurrentThread() - allocations) / 300:N0}");
    return;
}
if (args.Length > 0 && args[0] == "--speed-check")
{
    var speedRunner = new SimulationRunner(args.Length > 1 ? World.Load(args[1]) : World.Create(731)) { Speed = 0 };
    foreach (int requested in new[] { 1, 5, 20, 100, 1 })
    {
        speedRunner.Speed = requested;
        await Task.Delay(400);
        double started = speedRunner.Snapshot.Time;
        var elapsed = Stopwatch.StartNew();
        await Task.Delay(1600);
        double achieved = (speedRunner.Snapshot.Time - started) / elapsed.Elapsed.TotalSeconds;
        Console.WriteLine($"SPEED {requested}x requested: {achieved:F2}x measured");
        Require(speedRunner.Error == null, "Worker stopped during speed changes");
        if (requested == 1) Require(achieved >= .6 && achieved < 1.5, "Returning to 1x retained fast-forward backlog");
    }
    speedRunner.Speed = 0;
    await Task.Delay(200);
    double paused = speedRunner.Snapshot.Time;
    await Task.Delay(200);
    Require(speedRunner.Snapshot.Time == paused, "Fast-forward did not pause");
    await speedRunner.StopAsync();
    return;
}
World Lab(double light, double enzymes = 0)
{
    var w = new World { Habitat = new LabHabitat(light) };
    w.Organisms.Add(new Organism { Id = 1, Genes = new Genome { Cells = 1, Radius = 6, Contract = .02, Photosynthesis = .9, Enzymes = enzymes }, Energy = 20, Body = [new Cell { Position = new V(300, 300) }] });
    return w;
}
var lit = Lab(1); var dark = Lab(0);
for (int i = 0; i < 60; i++) { lit.Step(.1); dark.Step(.1); }
Require(lit.Organisms[0].LightEnergy > 0 && dark.Organisms[0].LightEnergy == 0 && lit.Organisms[0].Energy > dark.Organisms[0].Energy, "Light did not power photosynthesis");
Require(lit.Oxygen.Sum() > dark.Oxygen.Sum() && lit.Carbon.Sum() < dark.Carbon.Sum(), "Photosynthesis gas exchange failed");
var predator = Lab(0, 1);
predator.Organisms.Add(new Organism { Id = 2, Genes = new Genome { Cells = 1, Membrane = 0 }, Energy = 20, Body = [new Cell { Position = new V(311, 300) }] });
predator.Step(.1);
Require(predator.Organisms[0].TissueEnergy > 0 && predator.Organisms[1].Energy < 20, "Living tissue feeding failed");
Require(predator.Organisms[1].Irritation > 0 && predator.Organisms[1].ThreatDirection.Length > .9, "Tissue damage did not trigger withdrawal response");
var separated = Lab(0, 1);
separated.Organisms.Add(new Organism { Id = 2, Genes = new Genome { Cells = 1, Membrane = 0 }, Energy = 20, Body = [new Cell { Position = new V(311, 300, 150) }] });
separated.Step(.1);
Require(separated.Organisms[0].TissueEnergy == 0, "Organisms fed across separated depth layers");
var starving = Lab(0); starving.Organisms[0].Energy = .001; starving.Step(.1);
Require(starving.Organisms.Count == 0 && starving.Detritus.Count > 0, "Starvation/recycling failed");
Console.WriteLine("PASS: photosynthesis, carbon/oxygen exchange, tissue feeding, starvation/recycling");
// Shared geometry must produce contact propulsion and keep dry organisms out of the ground.
var coast = World.Create(912); coast.Organisms.Clear(); coast.Rocks.Clear();
double ground = coast.Ground(1700 * World.HabitatScale, 700 * World.HabitatScale);
var walker = new Organism { Age = 80, Heading = 0, Pitch = 0, Genes = new Genome { Cells = 1, Contract = .8, Rhythm = 1.2, Flattening = .5 }, Body = [new Cell { Position = new V(1700 * World.HabitatScale, 700 * World.HabitatScale, ground + 12) }] };
walker.Genes.Pattern[0].Outgrowth = .85; walker.Genes.Pattern[0].Stiffness = .8; walker.Genes.Pattern[0].Bend = .8;
double propulsion = 0; int contacts = 0;
for (int i = 0; i < 100; i++) { walker.Age += .05; var force = Appendages.Solve(coast, walker, .05); propulsion += force.X; contacts += walker.GroundContacts; Require(double.IsFinite(force.Length), "Non-finite appendage force"); }
Require(contacts > 0 && propulsion > 1, "Articulated contact did not produce forward reaction");
var bare = walker.Genes.Copy(); bare.Pattern[0].Outgrowth = 0; walker.Genes = bare;
Require(Appendages.Solve(coast, walker, .1).Length == 0, "Absent appendages produced a force");
Require(Shoreline.WaterLevel(90) > Shoreline.WaterLevel(270) + 60, "Tide is static");
walker.Id = 1; walker.Energy = 40; coast.Organisms.Add(walker);
for (int i = 0; i < 80; i++) coast.Step(.1);
Require(walker.Hydration < 1 && walker.ShoreEnergy > 0, "Drying or surface feeding failed");
Require(walker.Center.Z >= coast.Ground(walker.Center.X, walker.Center.Y), "Body fell through shore");
Console.WriteLine("PASS: articulated contact propulsion, absent-limb control, tide, desiccation, shore grazing, ground collision");
var dormant = new Organism { Genes = new Genome { Pattern = [new GrowthRule { Outgrowth = .04 }, new GrowthRule { Outgrowth = 1.2 }] }, Body = [new Cell()] };
Require(Appendages.Reach(dormant) == .04, "Unexpressed growth instruction counted as a visible appendage");
// A living organism keeps its inherited recipe; development only expresses that recipe.
var developing = World.Create(811);
var originalGenes = developing.Organisms.ToDictionary(o => o.Id, o => System.Text.Json.JsonSerializer.Serialize(o.Genes));
for (int i = 0; i < 1000; i++) developing.Step(.1);
Require(developing.Organisms.Where(o => originalGenes.ContainsKey(o.Id)).All(o => System.Text.Json.JsonSerializer.Serialize(o.Genes) == originalGenes[o.Id]), "Living organism mutated its genome");
var lateLimb = new Organism { Age = 100, Genes = new Genome(), Body = [new Cell { FormedAt = 100 }] };
lateLimb.Genes.Pattern[0].Outgrowth = .8;
Require(Appendages.Pose(lateLimb, 0, 1, 100).Radius == 0, "New section instantly received a mature limb");
Require(Appendages.Pose(lateLimb, 0, 1, 100.1).Radius < Appendages.Pose(lateLimb, 0, 1, 135).Radius * .01, "Appendage growth is discontinuous");
Require(lateLimb.CopyForView().Body[0].FormedAt == 100, "Snapshot lost developmental age");
var limbRoundTrip = System.Text.Json.JsonSerializer.Deserialize<Organism>(System.Text.Json.JsonSerializer.Serialize(lateLimb))!;
Require(Appendages.Pose(limbRoundTrip, 0, 1, 115) == Appendages.Pose(lateLimb, 0, 1, 115), "Save changed developmental anatomy");
Console.WriteLine("PASS: genomes unchanged during life, gradual section-local appendage growth, developmental save/snapshot fidelity");
var compact = World.Create(14); compact.FormatVersion = 3;
compact.Organisms.Clear();
compact.Organisms.Add(new Organism { Id = 7, Generation = 4, Energy = 50, Genes = new Genome { Cells = 2 }, Body = [new Cell { Position = new(500, 400, 120), FormedAt = 0 }, new Cell { Parent = 0, Position = new(512, 400, 120), FormedAt = 8 }] });
var compactCenter = compact.Organisms[0].Center;
var migrationPath = Path.Combine(Path.GetTempPath(), "tideglass-expand-" + Guid.NewGuid() + ".json");
compact.Save(migrationPath);
var enlarged = World.Load(migrationPath);
Require(enlarged.FormatVersion == 6 && enlarged.Organisms[0].Id == 7 && enlarged.Organisms[0].Energy == 50 && enlarged.Organisms[0].Generation == 4, "Expansion lost population state");
Require(Math.Abs(enlarged.Organisms[0].Center.X - compactCenter.X * World.HabitatScale) < 1e-8 && (enlarged.Organisms[0].Body[1].Position - enlarged.Organisms[0].Body[0].Position).Length == 12, "Expansion scaled anatomy or failed to spread centers");
Require(enlarged.Food.SequenceEqual(compact.Food) && enlarged.Biofilm.SequenceEqual(compact.Biofilm), "Expansion changed the food budget");
Require(enlarged.Organisms[0].Body[1].FormedAt == 8, "Expansion reset developmental age");
enlarged.Save(migrationPath);
var reloadedExpanded = World.Load(migrationPath);
Require(reloadedExpanded.Organisms[0].Center.X == enlarged.Organisms[0].Center.X, "Expansion repeated on reload");
File.Delete(migrationPath);
Require(enlarged.Tile(new V(World.Width - 1, World.Height - 1, World.Depth - 1)) == World.FieldCount - 1, "Expanded grid does not cover the full habitat");
Console.WriteLine("PASS: larger habitat migration, unchanged anatomy and food, idempotent reload, complete field coverage");
// Freeform topology: odd roots, branching, and long serial chains on a single body section.
var freeGenes = new Genome { Cells = 1, Buds = [
    new BudGene { AnchorAngle = 0, Length = 1, Muscle = 0 },
    new BudGene { AnchorAngle = 2, Length = 1, Muscle = 0 },
    new BudGene { AnchorAngle = 4, Length = 1, Muscle = 0 },
    new BudGene { Parent = 0, Turn = .5, Length = 1, Muscle = 0 },
    new BudGene { Parent = 0, Turn = -.5, Length = 1, Muscle = 0 },
    new BudGene { Parent = 3, Length = 1, Muscle = 0 },
    new BudGene { Parent = 5, Length = 1, Muscle = 0 }] };
var free = new Organism { Id = 1, Age = 100, Energy = 100, Genes = freeGenes, Body = [new Cell { Position = new(400, 400, 100) }], BudStates = freeGenes.Buds.Select(b => new BudState { Growth = 1, FormedAt = 0 }).ToList() };
var freePoses = new BudPose[freeGenes.Buds.Count]; Branching.Poses(free, free.Age, freePoses);
Require(freePoses.All(p => p.Active && double.IsFinite(p.Tip.Length)) && freeGenes.Buds.Count(b => b.Parent < 0) == 3, "Odd independent attachment sites failed");
Require(freePoses[3].Root.Equals(freePoses[0].Tip) && freePoses[4].Root.Equals(freePoses[0].Tip) && freePoses[6].Root.Equals(freePoses[5].Tip), "Branch or serial joint detached");
var many = freeGenes.Copy(); many.Buds = Enumerable.Range(0, 21).Select(i => new BudGene { AnchorAngle = i * Math.Tau / 21 }).ToList();
Require(Branching.Valid(many), "Old paired-limb count restriction remains");
var chain = freeGenes;
var mutationRandom = new Random(294);
bool sawFork = false, sawAdded = false, sawRemoved = false;
for (int i = 0; i < 600; i++)
{
    var child = chain.Offspring(mutationRandom, i % 3 == 0 ? freeGenes : null);
    Require(Branching.Valid(child), "Structural mutation produced invalid or over-budget topology");
    sawAdded |= child.Buds!.Count > chain.Buds!.Count; sawRemoved |= child.Buds.Count < chain.Buds.Count;
    sawFork |= child.Buds.Where(b => b.Parent >= 0).GroupBy(b => b.Parent).Any(g => g.Count() > 1);
    if (child.Buds.Count > 0)
    {
        string parentRecipe = System.Text.Json.JsonSerializer.Serialize(chain);
        child.Buds[0].Phase += .001;
        Require(System.Text.Json.JsonSerializer.Serialize(chain) == parentRecipe, "Offspring modified parent recipe");
    }
    chain = child;
}
Require(sawAdded && sawRemoved && sawFork, "Structural mutations did not explore add/remove/branch changes");
var oldAdult = new Organism { Genes = new Genome(), Body = [new Cell()] };
var oldAdultRecipe = System.Text.Json.JsonSerializer.Serialize(oldAdult.Genes);
var newChildGenes = oldAdult.Genes.Offspring(new Random(81), null);
Require(oldAdult.Genes.Buds == null && newChildGenes.Buds != null && oldAdultRecipe == System.Text.Json.JsonSerializer.Serialize(oldAdult.Genes), "Legacy adult was rewritten during offspring conversion");
var growingGraph = new Organism { Genes = freeGenes.Copy(), Age = 50, Energy = 1000, Body = [new Cell()] };
Branching.Develop(growingGraph, .1);
Require(growingGraph.BudStates.All(b => b.Growth == 0), "Branches appeared fully grown");
growingGraph.Age += .1; Branching.Develop(growingGraph, .1);
Require(growingGraph.BudStates[0].Growth > 0 && growingGraph.BudStates[0].Growth < .001 && growingGraph.BudStates[3].Growth == 0, "Growth is discontinuous or child grew before supporting tissue");
var touchWorld = new World { FormatVersion = 5 };
var touch = new Organism { Age = 100, Energy = 30, Genes = new Genome { Cells = 1, Buds = [new BudGene { Length = 2, Width = .3, Muscle = 0, Elevation = -1.2, TouchResponse = 1 }] }, Body = [new Cell { Position = new(400, 400, 4) }], BudStates = [new BudState { Growth = 1, FormedAt = 0 }] };
string touchRecipe = System.Text.Json.JsonSerializer.Serialize(touch.Genes);
Branching.Solve(touchWorld, touch, .1, null);
Require(touch.GroundContacts == 1 && touch.BudStates[0].Activation > 0 && System.Text.Json.JsonSerializer.Serialize(touch.Genes) == touchRecipe, "Local touch reflex changed anatomy or failed to respond");
var photoWorld = new World { FormatVersion = 5, Habitat = new LabHabitat(1) };
var leaf = new Organism { Age = 100, Energy = 20, Genes = new Genome { Cells = 1, Buds = [new BudGene { Length = 3, Width = .8, Flatness = .2, Muscle = 0, Photo = 1 }] }, Body = [new Cell { Position = new(400, 400, 100) }], BudStates = [new BudState { Growth = 1 }] };
double carbonBefore = photoWorld.Carbon.Sum();
Branching.Solve(photoWorld, leaf, .1, null);
Require(leaf.LightEnergy > 0 && photoWorld.Carbon.Sum() < carbonBefore, "Local photosynthetic tissue failed");
var mouth = leaf.CopyForView(); mouth.Genes = leaf.Genes.Copy(); mouth.Genes.Buds![0].Digestion = 1; mouth.Genes.Buds[0].Photo = 0; mouth.Energy = 20;
var mouthPoses = new BudPose[1]; Branching.Poses(mouth, mouth.Age, mouthPoses);
var prey = new Organism { Id = 2, Energy = 20, Genes = new Genome { Membrane = 0 }, Body = [new Cell { Position = mouthPoses[0].Tip }] };
Branching.Solve(photoWorld, mouth, .1, new[] { prey });
Require(mouth.TissueEnergy > 0 && prey.Energy < 20 && prey.Irritation > 0, "Local digestive tissue did not feed or trigger withdrawal");
var graphSave = Path.Combine(Path.GetTempPath(), "tideglass-graph-" + Guid.NewGuid() + ".json");
photoWorld.Organisms.Add(mouth); photoWorld.Save(graphSave); var restoredGraph = World.Load(graphSave); File.Delete(graphSave);
Require(System.Text.Json.JsonSerializer.Serialize(restoredGraph.Organisms[0].Genes) == System.Text.Json.JsonSerializer.Serialize(mouth.Genes) && restoredGraph.Organisms[0].BudStates[0].Activation == mouth.BudStates[0].Activation, "Branch graph/reflex save fidelity failed");
Console.WriteLine("PASS: independent odd roots, >16 roots, variable chains/forks, structural mutation/recombination, immutable adults, progressive growth, local reflex/photosynthesis/digestion, graph saves");
// Focused interaction checks for the pressure mechanics, without a parameter sweep.
var ecology = new World { Tidal = true, FormatVersion = 6, Time = 20, CurrentTime = 20 };
ecology.Clumps.Add(new FoodClump { Position = new V(300, 300, 120), Energy = 2, Radius = 3 });
ecology.PreparePhysicalFood(0);
Require(ecology.Capture(new V(200, 200, 120), new V(210, 200, 120), 2, 1, 0) == 0, "Food captured without contact");
double beforeParcel = ecology.Clumps[0].Energy;
Require(ecology.Capture(new V(290, 300, 120), new V(310, 300, 120), 2, .5, 0) > 0 && ecology.Clumps[0].Energy < beforeParcel, "Swept tissue missed physical food");
var victim = new Organism { Genes = new Genome { Cells = 1, Buds = [
    new BudGene { Length = 3, Muscle = 0, AnchorAngle = 0 },
    new BudGene { Parent = 0, Length = 2, Muscle = 0 }] },
    Age = 100, Energy = 44, Body = [new Cell { Position = new V(400, 400, 120) }],
    BudStates = [new BudState { Growth = 1, FormedAt = 0 }, new BudState { Growth = 1, FormedAt = 0 }] };
string inherited = System.Text.Json.JsonSerializer.Serialize(victim.Genes);
ecology.CacheAnatomy(victim);
ecology.DamageTissue(victim, victim.PhysicalBuds[0].Tip, 100);
Require(ecology.LostBranches == 1 && victim.BudStates.All(b => b.Growth == 0), "Severing did not remove branch descendants");
Require(ecology.Detritus.Count == 2 && victim.Body[0].Integrity == 1, "Localized damage hit unrelated core tissue");
ecology.Time += 10; victim.Age += 10;
Branching.Solve(ecology, victim, .1, []);
Require(victim.BudStates[0].Integrity > 0 && victim.BudStates[0].Growth < .01 && victim.Energy < 44, "Repair skipped energy or gradual growth");
Require(inherited == System.Text.Json.JsonSerializer.Serialize(victim.Genes), "Injury/repair mutated adult genes");
var exposedCurrent = new World { Tidal = true, CurrentTime = 0 };
var shelteredCurrent = new World { Tidal = true, CurrentTime = 0 };
V currentPoint = new(315, 315, 100);
V direction = exposedCurrent.FlowAt(currentPoint); direction *= 1 / direction.Length;
shelteredCurrent.Rocks.Add(new Rock { Position = currentPoint - direction * 30, Radius = 25 });
Require(shelteredCurrent.FlowAt(currentPoint).Length < exposedCurrent.FlowAt(currentPoint).Length, "Rock did not shelter downstream flow");
var independent = ecology.Snapshot();
double originalParcelEnergy = ecology.Clumps[0].Energy;
independent.Clumps[0].Energy = 0;
Require(ecology.Clumps[0].Energy == originalParcelEnergy, "View mutated physical food");
Console.WriteLine("PASS: physical food contact, localized severing, gradual paid repair, immutable genes, rock shelter, independent food snapshots");
if (args.Length > 0 && args[0] == "--mechanics-only") return;

var world = World.Create(args.Length > 0 ? int.Parse(args[0]) : 731);
Require(world.Organisms.Max(o=>o.Center.Z) - world.Organisms.Min(o=>o.Center.Z) > 100, "Founders are confined to a plane");
Require(world.Tile(new V(300,300,10)) != world.Tile(new V(300,300,200)), "Chemical fields ignore depth");
int steps = args.Length > 1 ? int.Parse(args[1]) : 24000;
for (int step = 0; step < steps; step++)
{
    world.Step(.1);
    if (step % 3000 == 0)
    {
        Console.WriteLine($"t={world.Time:F0} population={world.Organisms.Count} generation={world.RecordGeneration} births={world.Births} deaths={world.Deaths} exchanges={world.Exchanges} cells={world.Organisms.Sum(o=>o.Body.Count)} O2min={world.Oxygen.Min():F2} tissue={world.TissueMeals} outgrowth={(world.Organisms.Count > 0 ? world.Organisms.Max(o=>Appendages.Reach(o)) : 0):F2} contacts={world.Organisms.Sum(o=>o.GroundContacts)} shore={world.Organisms.Sum(o=>o.ShoreEnergy):F0}");
        Require(world.Food.All(f => double.IsFinite(f) && f >= 0), "Invalid food field");
        Require(world.Oxygen.All(f => double.IsFinite(f) && f >= 0 && f <= 2) && world.Carbon.All(f => double.IsFinite(f) && f >= 0 && f <= 3), "Invalid dissolved gases");
        Require(world.Organisms.All(o => double.IsFinite(o.Energy) && o.Body.All(c => double.IsFinite(c.Position.X) && double.IsFinite(c.Position.Y) && double.IsFinite(c.Position.Z) && c.Position.Z > -1 && c.Position.Z < World.Depth + 1)), "Invalid 3D body or energy");
        Require(world.Organisms.Select(o=>o.Id).Distinct().Count() == world.Organisms.Count, "Duplicate IDs");
        Require(world.Organisms.All(o=>o.Body.Select((c,i)=>c.Parent < i && c.Parent >= -1).All(v=>v)), "Invalid tissue topology");
    }
}
Require(world.Births > 0 && world.Deaths > 0 && world.RecordGeneration > 2, "Life cycle did not progress");
Require(world.Organisms.Count > 0, "Baseline ecosystem failed endurance check");
if (args.Length > 2) world.Save(args[2]);
var path = Path.Combine(Path.GetTempPath(), "tideglass-check-" + Guid.NewGuid() + ".json");
world.Save(path);
var loaded = World.Load(path);
Require(loaded.Time == world.Time && loaded.Organisms.Count == world.Organisms.Count && loaded.Food.SequenceEqual(world.Food), "Save roundtrip failed");
Require(loaded.Organisms.Zip(world.Organisms).All(p => p.First.Body.Zip(p.Second.Body).All(c => c.First.Position.X == c.Second.Position.X && c.First.Position.Y == c.Second.Position.Y && c.First.Position.Z == c.Second.Position.Z)), "Body positions changed on reload");
for (int i = 0; i < 50; i++) { loaded.Step(.1); world.Step(.1); }
Require(loaded.RandomState == world.RandomState && loaded.Organisms.Count == world.Organisms.Count && loaded.Food.SequenceEqual(world.Food) && loaded.Oxygen.SequenceEqual(world.Oxygen) && loaded.Biofilm.SequenceEqual(world.Biofilm) && loaded.Organisms.Zip(world.Organisms).All(pair=>pair.First.Hydration == pair.Second.Hydration && pair.First.Center.Z == pair.Second.Center.Z && System.Text.Json.JsonSerializer.Serialize(pair.First.BudStates) == System.Text.Json.JsonSerializer.Serialize(pair.Second.BudStates)), "Save did not preserve deterministic continuation");
File.Delete(path);
// Migration preserves ancestry and energy while opening the old plane into a water volume.
var legacy = World.Create(42); legacy.FormatVersion = 0;
legacy.Food = legacy.Food.Take(World.Cols * World.Rows).ToArray(); legacy.OrganicMatter = legacy.OrganicMatter.Take(World.Cols * World.Rows).ToArray();
legacy.Oxygen = legacy.Oxygen.Take(World.Cols * World.Rows).ToArray(); legacy.Carbon = legacy.Carbon.Take(World.Cols * World.Rows).ToArray();
foreach (var o in legacy.Organisms) foreach (var c in o.Body) c.Position = new V(c.Position.X,c.Position.Y);
legacy.Save(path); var migrated = World.Load(path); File.Delete(path);
Require(migrated.FormatVersion == 6 && migrated.Food.Length == World.FieldCount && migrated.Organisms.Zip(legacy.Organisms).All(p=>p.First.Id == p.Second.Id && p.First.Energy == p.Second.Energy && p.First.Generation == p.Second.Generation && p.First.Center.Z > 0), "Legacy migration lost population state");
var runner = new SimulationRunner(World.Create(77)) { Speed = 20 };
await Task.Delay(500);
var immutable = runner.Snapshot;
double savedTime = immutable.Time, savedX = immutable.Organisms[0].Body[0].Position.X;
await Task.Delay(300);
Require(immutable.Time == savedTime && immutable.Organisms[0].Body[0].Position.X == savedX && runner.Snapshot.Time > savedTime, "Worker mutated published snapshot");
runner.Speed = 0; await Task.Delay(150); double pausedTime = runner.Snapshot.Time; await Task.Delay(150);
Require(runner.Snapshot.Time == pausedTime, "Worker pause failed");
await runner.SaveAsync(path); await runner.StopAsync();
Require(World.Load(path).Time == pausedTime, "Worker save did not capture paused state"); File.Delete(path);
Console.WriteLine("PASS: depth-aware feeding and chemistry, 2D migration, immutable worker snapshots, pause, asynchronous save/shutdown");
var empty = new World(); empty.Step(.1); empty.Step(.1);
Require(empty.Events.Count(e=>e.Contains("extinction")) == 1, "Extinction notification repeats");
Console.WriteLine($"PASS: endurance, reproduction, topology, finite state, extinction, save/resume ({timer.Elapsed.TotalSeconds:F1}s)");

sealed class LabHabitat(double light) : IHabitat
{
    public V Flow(V p, double time) => new();
    public double Production(V p, double time) => 0;
    public double Temperature(V p, double time) => 23;
    public double Light(V p, double time) => light;
}
