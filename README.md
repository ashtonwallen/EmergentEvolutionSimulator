# Tideglass

A standalone Windows **3D** ecosystem you can leave running. Start with primitive, one-to-four-cell organisms in a volume of shallow water. Watch cells grow, populations compete, and inherited developmental recipes change. Bodies, movement, sensing, contact, and chemical fields have depth; this is not a tilted rendering of a flat simulation.

## Branching development

New organisms carry an inherited tree of tissue-growth instructions. Roots attach independently to body sections at mutable positions and angles. Every piece can bear further pieces; mutations can add a root, extend a chain, create a fork, remove a piece, or change a connection. There is no required bilateral symmetry, fixed limb count, or two-joint template. Odd attachment counts and asymmetric branching are supported on a single body section.

Each piece inherits length, width, flattening, taper, stiffness, muscle strength/rhythm/phase, grip, digestive investment, photosynthetic investment, and local responses to touch, nutrients, and light. Parent rhythm can influence a child's contraction. Local responses change temporary muscle activation, never the inherited genome. Flexible tissue sags when unsupported; thin long structures have less supporting strength. Motion, material, growth, sensing, and feeding specializations cost energy. Anatomy is built gradually and only when energy is available, with child tissue waiting for its supporting piece to develop.

The simulation and 3D renderer use the same branch poses. Independent tissue can press against the substrate, drag through water, absorb food, photosynthesize, and digest exposed living tissue. Photosynthetic tissue is tinted green, digestive tissue reddish, and stiff tissue muted amber; these visualize continuous investments and do not designate fixed organs or species.

A shared **128-node computation budget** covers body scaffold nodes plus branch pieces. This replaces the old 24-cell/eight-paired-site anatomy limits for new genomes. Finite numeric ranges and the computation budget remain implementation limits. This is an articulated, overdamped approximation, not unlimited geometry or a full soft-body/fluid solver. Interactions still use a coarse resource grid and nearby-organism discovery, so very long structures can exceed the interaction search radius.

The first normal launch preserves ecosystem.json.pre-branching. Existing adults keep the old growth recipe and appearance; their offspring inherit a translated recipe that can mutate freely under the new system. A new ecosystem starts with one to three small independently placed bumps. Mutations occur only in offspring, and developing tissue follows that inherited recipe.

The one-hour seed-731 validation survived through 21 generations with 399 organisms. Surviving recipes had zero to seven attachment sites, 78 organisms had branching recipes, and the largest recipe had 11 pieces. Tests also exercise 21 independent roots, multi-joint chains, forks, 600 rounds of mutation/recombination, unchanged adult genomes, gradual energy-limited development, touch responses, local photosynthesis/digestion, and deterministic save/resume. These checks demonstrate implemented capabilities, not a guarantee of lasting diversity or terrestrial animals.

## Tidal shoreline update

The habitat now slopes from deeper water onto exposed land, with a shallow depression and a raised bank. The tide rises and falls over six simulated minutes. Light cycles independently. **Shoreline** begins the cycle of coastal and underwater views; the automatic camera tour starts after a 24-second landscape view. Live field counts report near-bottom, exposed, and ground-contacting organisms.

Primitive founders carry tiny outgrowths. The branching recipe described above supplies tissue geometry and contact/fluid forces, with growth and movement paid for by energy. Moving tips push against the bottom through friction, support body weight, or drag through water. Gravity, buoyancy, inherited density, and hydration affect survival. Membrane waterproofing trades a maintenance cost for slower desiccation. There is no leg unlock or scheduled transformation.

Surface food grows in finite, patchy mats. Light, submersion, and moisture control renewal; bodies and contacting outgrowths consume it. The rendered tufts track that actual resource field. Suspended food remains available at a lower renewal rate. Neighbor shading limits free photosynthesis, and the previous direct elongation swimming bonus is removed. Tide exposure, day/night conditions, depletion, and competing feeding strategies can change which locations pay off. No population cap or diversity quota is imposed.

The observer records longer outgrowths, the first substantial appendage contact, and the first exposed-shore birth when they actually occur. These observations do not change fitness. Wander favors visible anatomy, ground contacts, feeding encounters, and recent offspring. The inspector includes appendages in its silhouette and reports hydration, stiffness, contact count, and surface-derived energy.

**Existing saves are preserved** as ecosystem.json.pre-shoreline before the first normal launch. Old populations keep their genomes and ancestry, receive primitive defaults for new traits, and continue under the new rules. Cells underneath newly introduced terrain are lifted above it. Choose **New** to watch a fresh population of primitive founders; the current world is saved as ecosystem.json.previous.

Older saved adults retain their original paired, two-segment outgrowths; newly generated genomes use independent branching tissue. It is not unrestricted CAD growth, learned neural control, or a full joint/rigid-body/fluid solver. Elaborate legs, terrestrial colonization, and persistent diversity are possible directions, not guaranteed outcomes. Low-detail skinning approximates the tissue envelope; contact uses the scaffold and appendage tips.

## Run

Open `dist/Tideglass.exe` after publishing. The self-contained build needs no separate .NET installation, browser, account, or network connection.

To run from source with the .NET 9 SDK:

```powershell
dotnet run --project Tideglass.csproj -c Release
```

## Watching

- Click a creature to inspect its body, parents, offspring, traits, and lifetime energy sources.
- **WASD / arrow keys** move relative to the camera; **Q/E** move down/up and **Shift** moves faster. Movement stops on focus loss.
- Drag to **orbit** the 3D camera. **Shift-drag** or middle-drag pans. Scroll zooms. **F** follows the selected organism. Dragging or keyboard movement releases follow mode.
- **Wander** / **G** moves the camera between actual living organisms, including recent offspring. It starts disabled; enable it explicitly with the button or G. Manual camera input releases wandering and following. Camera choices never affect evolution. Foreground organisms remain visible.
- **Space** pauses. Buttons select 1×, 5×, 20×, or 100×. Requested and achieved simulation speed are shown separately; high multipliers are limited by CPU capacity. Rendering remains independent. Simulation uses fixed 0.1-second steps at every speed.
- **Overview** / **Escape** shows the habitat. **H** hides field notes. **F11** toggles fullscreen.
- **O₂ map** shows a horizontal slice through the selected organism's water layer: oxygen-rich water in blue and depleted water in coral. Temperature, oxygen, dissolved carbon, tide phase, and light are shown at the selected creature, or the habitat center when nothing is selected. Gas and distance values are simulation units, not scientific measurements.
- The inspector shows a body silhouette, current activity, inherited proportions, and lifetime energy sources. Organisms have continuous textured skins with traveling contractions, rather than visible cell spheres. Green and brown suspended grains sample the actual algae/organic food field: their size and presence change as local food is consumed. Surface tufts visualize edible biofilm; the sand texture is decorative.

## The rules

Organisms develop from a founding cell using an inherited, repeating list of growth instructions. Instructions control cell size, branching, growth angle and elevation, contraction phase, and regional photosynthetic/digestive investment. Mutations can change values, duplicate instructions, or remove them. Connected cells have deformable 3D spring connections; body arrangement changes drag and swimming efficiency. Organisms steer vertically and horizontally, with physical depth separation affecting interactions.

The cell network forms an internal growth scaffold. A blended implicit surface encloses it as one body, including connecting tissue. Additional inherited values control elongation, thickness, taper, and edge growth; they can produce compact, flattened, elongated, asymmetric, and lobed forms. Primitive founders have modest variations. New surfaces are built off-thread as organisms grow; nearby bodies receive smooth skin deformation and contraction waves, with simpler animation in the distance. There are no authored animal parts or scripted transitions to recognizable animals.

Proportions also affect forward drag, exposed photosynthetic area, and maintenance costs. Tissue damage produces a decaying withdrawal response whose effect depends on inherited sensing and contractility. Surface adhesion can sustain contact between organisms, including feeding interactions. These are local rules; they do not guarantee complex animals, predator/prey cycles, or a particular visual outcome.

Energy pays for maintenance, movement, growth, and reproduction. Organisms can absorb suspended algae, digest organic matter, photosynthesize, and digest exposed tissue on contact. Those are continuous investments with costs, rather than fixed diet classes. Photosynthetic organisms are edible too. Light, dissolved carbon, temperature, oxygen, membrane investment, and local resource depletion affect success. Metabolism consumes oxygen and returns carbon; photosynthesis does the reverse. Dissolved gases and nutrients occupy four depth layers, gases diffuse between layers, light weakens toward the floor, and surface water exchanges more strongly with the atmosphere. Remains return organic matter to the water.

Genetic responses to food, light, nearby relatives, and digestible tissue bias movement. Their interactions can produce aggregation or avoidance; no flock, hunter, or species is spawned by a script. Reproduction normally buds an offspring. Nearby compatible, sufficiently energized organisms may contribute recombined genes instead. Parent identities and a snapshot of the primary parent's traits are kept for comparison.

There are no complexity rewards, scheduled species, or guaranteed progression. Simpler organisms may win. Extinction leaves the world empty by default; the optional **Restart after extinction** setting starts a fresh ecosystem instead.

**Population cap** in the bottom controls pauses births at the chosen limit (default 1,000; 0 means unlimited). Click **Apply** or press Enter after editing. Lowering the cap preserves existing organisms and blocks births until natural deaths bring the population below it. The cap is saved and carried into new ecosystems and extinction restarts; caps below 100 also reduce the founder count. Research samples include the cap so capped runs can be identified during analysis.

## Persistence

Autosaves every 60 seconds and on normal close to `%LOCALAPPDATA%\Tideglass\ecosystem.json`. Relaunching resumes the same state, including the evolution random generator. Simulation continues while minimized, but does not simulate while the app is closed or the computer sleeps. **New** backs up the current ecosystem as `ecosystem.json.previous` before replacing it. Unreadable saves are preserved separately before a fresh world is created.

The first normal 3D launch preserves the previous save as `ecosystem.json.pre-3d`. Older 2D saves retain organism identities, genomes, generations, energy, and history; bodies are distributed through the water and existing nutrient totals are split between depth layers. Subsequent evolution follows the new 3D rules, so it will diverge from the old flat world.

The continuous-body update also preserves `ecosystem.json.pre-organic` before its first normal launch. Existing organisms receive neutral defaults for newly introduced proportion genes; descendants can inherit and mutate them. Existing ancestry and population state are kept.

## Scope and limitations

This is a playable prototype with stylized, overdamped 3D swimming and coarse chemical fields, not a fluid dynamics or historical Cambrian model. Shape influences motion, but locomotion uses an active-tissue approximation. New body scaffolds and branch tissue share the 128-node development budget; the growth program also has a finite instruction budget. Neighbor discovery uses body centers within a 65-unit range; tissue feeding then resolves cell-to-cell gaps. Very long bodies can still exceed that discovery range. Recombination is local gene exchange during budding, not an explicit gamete simulation. Plant-like forms share the organism system; there are no authored plant or animal species.

The continuous skin is a visual surface over the physical cell scaffold. Collision and digestion do not yet resolve the exact skin mesh, and skinning limits extreme deformation to keep bodies coherent. Visible food parcels and remains are physical items: tissue must touch them to capture energy. Coarse fields retain a small background absorption route for primitive organisms. Faraway bodies use lower mesh detail and simpler deformation to preserve responsiveness.

Water conditions are separated behind `IHabitat`; `Shallows` supplies the current wet conditions. Shoreline supplies the shared terrain and tide geometry used by physics and rendering; future habitats can replace or extend those rules. Viewing controls are separate from simulation; no environmental editing tools are included in this pass. Long-term diversity and performance remain tuning targets, not guaranteed outcomes.

## Development

- `Branching.cs`: branching genetics, structural mutation, development, tissue properties, local reflexes, and contact/feeding physics.
- `Shoreline.cs`: shared terrain, tides, and preserved legacy outgrowth geometry/forces.
- `HabitatVisual.cs` / `AppendageVisual.cs`: terrain, waterline, resource tufts, and articulated tissue rendering.
- `Simulation.cs`: developmental genetics, physiology, reproduction, environment, persistence.
- `SimulationRunner.cs`: dedicated simulation worker, immutable view snapshots, command queue, background saving.
- `BodySurface.cs`: continuous implicit tissue surface, connected mesh generation, smooth skinning weights, contraction deformation.
- `OrganicVisual.cs`: asynchronous body mesh construction, detail levels, nearby skin deformation.
- `Aquarium.cs`: retained GPU-rendered WPF 3D scene, shared meshes/materials, interpolated movement, orbit camera, ray picking, off-screen culling, oxygen slice.
- `MainWindow.xaml` / `.cs`: native window, controls, rendering callbacks, inspector, save lifecycle.
- `SpecimenView.cs`: compact anatomy diagram in the inspector.

```powershell
dotnet run --project Tests/SimulationChecks.csproj -c Release
dotnet publish Tideglass.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist
```

The shoreline checks also exercise ground propulsion versus an absent-appendage control, tide range, drying, substrate grazing, ground collision, and deterministic hydration/biofilm continuation. The headless checks exercise connected watertight tissue meshes, finite normals, normalized skinning weights, traveling deformation, tissue-damage response, population endurance, reproduction, finite 3D chemical/body state, topology, extinction, deterministic save/resume, legacy migration, depth-separated feeding, worker snapshot immutability, pause, and asynchronous save/shutdown. `--smoke`, `--smoke-detail`, and `--smoke-small` produce app-rendered previews without modifying the user's saved ecosystem. `--smoke-perf` loads a copy of the saved population, requests 20×, and records rendering-callback intervals plus CPU update cost in `performance-3d.txt` before exiting; it never writes the ecosystem save.

Performance work removes simulation from the UI thread, caches centers and reuses 3D neighbor bins, reuses chemical buffers, builds meshes asynchronously, interpolates organism transforms, skips off-screen models, and samples environmental display data at 4 Hz. On the development machine's RTX 4080, the continuous-body crowded follow-view check with roughly 3,100 organisms / 4,300 scaffold cells averaged about 72 rendering callbacks per second (p95 interval 15.1 ms). Simulation in that dense world achieved around 4× while 20× was requested; high multipliers remain CPU-limited. Camera angle, population, window size, and requested simulation speed affect results. This is a local measurement, not a guaranteed frame rate.
\nThe shoreline tuning run at seed 149 survived two simulated hours (roughly 38 generations). The final contact-feeding rules passed a separate one-hour run at seed 731: 24 generations and 363 survivors, plus deterministic save/resume. These are endurance checks, not evidence that all seeds remain diverse or survive indefinitely.\n
Published shoreline build: the saved-population check with 3,461 organisms averaged 68 rendering callbacks/second (p95 interval 17.0 ms, CPU render update 1.82 ms). The crowded simulation achieved 1.97x while 20x was requested. The separate headless seed-731 one-hour test took 149 seconds including checks. These measurements describe different workloads; high speed multipliers remain CPU-limited. Distant articulated meshes update less often than the selected organism, but appendage visibility no longer changes with camera detail.

Appendage growth correction: each new scaffold section records its formation age. Its inherited appendages grow continuously over the next 35 simulated seconds, rather than borrowing the mature organism\'s age. Older saves retain their existing developmental state. Living genomes are unchanged by development; mutations are applied when offspring genomes are created. Checks cover genome stability during life and preservation of developmental age in saves and render snapshots.

Habitat expansion: the horizontal dimensions are now 2700 x 1680 (50% wider and longer; 2.25 times the ground area). Save format 4 spreads existing organism centers without scaling their anatomy, changing genes, or resetting developmental age. Rocks and remains retain their relative habitat locations. Field totals and grid counts stay unchanged; each resource tile covers a larger area, avoiding a matching increase in carrying capacity and field-update work. Overview and shoreline cameras account for the larger habitat. Migration is applied once and covered by save/reload tests.

The final attachment placement uses each body section\'s inherited proportions to place roots near its surface. A second seed (149) passed a 15-minute endurance/save-resume check after this correction. The evolved-population render check with about 604 organisms averaged 104 rendering callbacks/second (p95 14.2 ms), with 8.8x achieved simulation speed at a 20x request; this is a local workload measurement, not a guaranteed frame rate.


## Ecological pressures update

Tidal currents strengthen in a channel and weaken behind rocks and near the bottom. Broken rock ridges, depressions and a muddy patch offer different contact, shelter and locomotion conditions. Primitive core wriggling remains, at reduced strength. Branch strokes exert direction-dependent water drag, surface friction and support; adhesion can resist advection. These are inexpensive approximations, not a fluid or rigid-body solver.

Suspended nutrient fields assemble into finite green or brown food parcels, which drift, sink, decay and are consumed by physical contact. Branches sweep segments through this food. Rock mats draw their energy from nearby biofilm, and grazing requires surface contact. Remains sink and can be eaten directly. A small dissolved-food uptake route remains available to primitive bodies. Photosynthetic tissue competes for light and pays maintenance costs.

Predation injures body sections and branches. Inherited toughness reduces branch injury at a maintenance cost. Severe injury severs a branch and its descendants, leaving edible remains. Energy-funded repair restores the inherited anatomy gradually; repair and normal development do not mutate adult genomes. Injured tissue weakens, shrinks, and damaged branches become reddish. Inspector health, anchoring, local current and tissue signals expose these interactions.

Connected pieces exchange delayed local signals. Touch, food, light, mechanical load, parent coupling and child coupling affect contraction. Response weights, delay, toughness and repair investment mutate only in offspring. No anatomy is awarded a bonus for being a leg, fin or named body type, and useful complexity is not guaranteed.

Save format 6 preserves the existing population and introduces the habitat details once. The first launch backs up the previous save as ecosystem.json.pre-pressures. Existing speed controls remain; achieved speed depends on the population and hardware. The historical performance measurements above predate these additional mechanics.


Pressure-update validation: Release build and publish succeeded. The focused mechanics checks cover physical food capture, localized branch severing, gradual energy-funded repair, unchanged adult genes, rock shelter, and snapshot independence. A seed-149 run survived 15 simulated minutes and passed deterministic save/resume. This is a smoke/stability check, not evidence that a particular complex anatomy will evolve.

The packaged crowded-save preview with about 984 organisms / 1,027 body cells averaged a 9.51 ms rendering-callback interval (p95 23.67 ms), with 1.05 ms CPU render-update cost. It achieved 1.55x simulation speed while 20x was requested. The existing population has roughly 9,263 inherited branch instructions; the richer ecology remains CPU-limited at that scale. Rendering callbacks are not a guarantee of presentation frame rate.


## CPU throughput and speed controls

Independent chemical-field rows, current-field rows, and large-population anatomy preparation run across up to eight CPU workers (bounded by available processors). Feeding transfers, damage, reproduction and random draws remain ordered. Terrain heights use a shared 5-unit lookup grid with bilinear interpolation; physics and rendering query the same surface. Local tissue signals are accumulated in one pass, and branch contact queries reject out-of-bounds points before detailed checks.

Fast-forward uses longer worker batches, avoids unnecessary sleeps when work is queued, and publishes visual snapshots at 10 Hz for 20x/100x requests. Physics remains at a fixed 0.1-second step. Changing the requested speed clears accumulated fast-forward debt, so returning to 1x does not continue catching up at the previous rate. The speed display highlights a throughput limit and the selected button is outlined.

On a fixed saved-population fixture (about 384 survivors after the benchmark), measured tick time fell from about 19 ms to 9 ms. A separate worker check measured 1.06x, 5.00x, 13.59x and 12.89x for 1x/5x/20x/100x requests, then 1.00x after returning to 1x. High targets still converge when the CPU is saturated; these results do not promise 100x or a particular throughput for larger populations. The RTX 4080 continues to render the WPF scene; simulation work uses the Ryzen CPU, with independent stages parallelized. A short survival run and deterministic save/resume passed after these changes.


## Gradual shore adaptation

Rain and tides now replenish a saved soil-moisture field. A terrain flood-fill identifies closed depressions; those retain finite puddles while excess water drains downhill. Soil and pools evaporate, rain wets exposed terrain, and nearby rocks reduce drying. Damp terrain darkens and retained water has a visible surface. Food growth on the substrate depends on moisture, light and available carbon.

Body and appendage contact can absorb finite soil/puddle water. Waterproofing slows both evaporation and uptake; food and prey also provide water. Evaporation scales with body surface relative to water storage, including exposed appendage costs. Sea immersion replenishes water, while puddle/soil absorption draws from the local reservoir. A heritable moisture-response weight starts neutral in older genomes and mutates in descendants; it responds to local moisture gradients, with no hard-coded return-to-water behavior.

Save format 7 adds moisture without replacing the population. The first launch preserves ecosystem.json.pre-moisture. These are opportunities and tradeoffs, not guarantees of permanent terrestrial species. Weather, hydrology, and body surface area are inexpensive approximations rather than a scientific water-cycle model.


## Local coordination and mechanical locomotion

New tissue force calculations solve body motion against summed directional water drag and tangential ground resistance. There is no designated power stroke or preferred swimming direction. Unused tissue adds drag and maintenance; contact geometry, stiffness, surface orientation, and motion determine reaction forces. Muscle strength falls with tissue damage, low energy and mechanical load. Positive mechanical work costs energy. Core surface traction remains a primitive locomotion approximation; this is still an overdamped model rather than a full fluid or articulated rigid-body solver.

Parent/child signals and optional signals between roots on the same body section affect local activation. Heritable contraction sensing, phase response, motor response, bend amplitude and bend phase can change timing and movement. New response weights start neutral and mutate only in offspring. Signal updates read the previous step together, avoiding an advantage from processing order. Phase offsets, measured load and slip are runtime state saved with the ecosystem. No preset gait, limb count, symmetry reward, complexity milestone or fitness score has been added.

The inspector reports movement power and contact slip. The empty introductory inspector is hidden, and UI copy is functional. The first updated launch preserves ecosystem.json.pre-coordination.

Additional CPU optimizations cache inherited attachment directions, reuse confirmed grazing contacts, use direct spatial-bin lookup, and skip water uptake and tissue-contact work when they cannot affect the result. Physics still advances in 0.1-second steps. A frozen saved-population benchmark produced byte-identical checkpoints after 310 steps before and after these changes; measured tick time decreased from about 10.5 ms to 7.6 ms. Separate 16-second app runs starting from the same save achieved 6.66x before and 10.66x after at a 20x request. These are local measurements: the faster app advances farther and therefore ends with a different population, and throughput varies with anatomy and population.

For repeatable headless timing, run `dotnet run --project Tests/SimulationChecks.csproj -c Release -- --benchmark <input-save> [output-checkpoint] [worker-count]`. It warms up for 10 steps, measures 300 steps, and optionally writes a checkpoint outside the timed section. Allocation reporting covers the calling thread only. Use a copy of the save and run benchmarks sequentially.


## Tissue memory and feeding mechanics

Tissues now carry a decaying short-term signal memory. Inherited weights can feed memory, local recurrence, hunger, slip and immersion into the existing parent/child/root signaling network. New response weights default to zero and evolve only through offspring; activity and memory change during life and persist in saves. This remains a small distributed controller, not an arbitrary neural topology or learned policy.

Attached mats respond to tangential scraping, tissue stiffness and taper. Suspended particles are retained according to adhesive surface, flattening and relative current speed. Dense remains resist digestive access; moving firm tissue and adhesion improve access. Tissue feeding combines enzymes with mechanical access and adhesion, while injury and food remain finite. These contact approximations create tradeoffs without named mouths, claws, gaits or complexity bonuses. Existing stiffness, muscle, sensitivity and toughness remain continuous inherited properties. New permeability changes branch water uptake and evaporation together, with a maintenance cost for specialization.

Forty-nine additional rocks form broken pockets across deep water, shallows and shore, adding differently sized gaps, current shelter and damp refuges. Existing channels, tides and pools remain. Save format 8 adds the rocks once without replacing organisms or moisture fields; the app backs up the previous save as `ecosystem.json.pre-tissue-ecology`.

Backlog: ancestry comparison viewer (deferred at user request).

Validation: Release build/publish, focused mechanics checks, a five-minute seed-149 simulation and deterministic save/resume passed. Packaged fresh-world and copied-existing-population smoke runs exited successfully. The ancestry viewer remains deferred.


## Evolving wiring and repeated structures

Each tissue can inherit incoming weighted signal connections from any other tissue, including itself. Mutations add, remove, reconnect, or adjust these links and their blend of immediate versus remembered activity. All sources are sampled before destinations update, so feedback cannot exploit iteration order. Absent or severed tissues cannot transmit; active links cost energy according to strength and physical length. Existing populations and founders start with no extra wiring. The existing local reflex network remains available.

An offspring mutation can duplicate a whole branch and all descendants together, retaining their proportions, muscle timing, development, sensors and wiring. Internal links remap to the copied tissues; external inputs and outputs are preserved. The copied root changes attachment position or angle without imposing bilateral symmetry or a limb count. Later mutations can differentiate the copies. Removing a tissue drops its incident signal links and remaps surviving indices. Sexual reproduction inherits the tissue graph and its wiring together from one parent, alongside recombination of other traits.

The shared 128-node anatomy budget and a 512-link computation budget bound runtime; neither awards survival benefits. Copies still develop gradually and pay growth, tissue and signaling costs. No adult genome is edited, no gait or prey pursuit is scripted, and greater complexity is not guaranteed. Wiring serializes with the genome; existing runtime signal and memory fields continue to save normally. First launch preserves `ecosystem.json.pre-wiring`.

Validation: focused checks cover inhibitory and recurrent signaling, simultaneous source reads, severed-source gating, internal/external connection remapping, deep copies, deletion, size budgets, 600 offspring mutation/recombination rounds, and saved wiring. A five-minute seed-149 simulation and deterministic save/resume passed.


## Additional throughput improvements

Body-rock collisions use spatial candidates while preserving the original rock order, including re-querying when a collision pushes a body across grid buckets. Grazing reuses confirmed contact. A summed occupancy grid rejects empty carcass searches. The paired appendage poses reuse unchanged tissue dimensions, muscle strength and root coordinate frames; moving child frames are still recomputed. Tide/daylight values are computed once per ecosystem time, and unused food sensors skip their sampling work.

Neighborhood searches, temperature factors and pre-interaction anatomy preparation share one parallel stage. Feeding, damage, movement application and reproduction remain ordered: parallelizing these directly would change the simulated interactions. Worker snapshots omit simulation-only grids and neighbor lists, preallocate collections, and retain independent mutable tissue states. Physics still uses the same fixed 0.1-second step; no off-screen organisms are skipped.

The optional **Reduce display work** checkbox limits population snapshots to 2 Hz, environment snapshots to 1 Hz and viewport updates to at most 10 fps while speed is above 1x. Pause and 1x restore normal update rates. This trades visual smoothness for simulation throughput without changing physics or survival rules.

On one frozen saved-population fixture (626 survivors after 310 steps), three alternating baseline/optimized headless runs gave median tick times of 11.915 / 10.384 ms, about 15% more throughput. Checkpoints were byte-identical before/after and with one versus eight workers. Average allocated bytes per display snapshot fell from 1,721,307 to 1,528,971 (about 11%); tick allocation was essentially unchanged. Snapshot allocation measurements cover the calling thread.

Separate 16-second packaged-app runs from the same starting save measured final achieved speeds of 11.97x baseline, 11.69x optimized normal display, and 13.77x reduced display at a 20x request. These short live readings fluctuate and end with different populations because they advance different amounts of simulated time; they are not guarantees. Normal-display improvement was not established by this app check. Release build/publish, ordered collision comparisons, a five-minute survival run, deterministic save/resume, and both display-mode smoke checks passed.


## Running overnight without a display

**Restart after extinction** is available beside the display controls and in the display-off panel. When enabled, the simulation worker starts a fresh ecosystem with 100 primitive founders and a new seed after the last organism dies, including when the window is minimized or rendering is off. It also handles loading an already-extinct world. The checkbox is off by default, is saved with the ecosystem, and carries over into automatic and manual new worlds. Mutation mode is preserved. Pausing prevents automatic restarts until resumed.

Each automatic restart resets ecosystem time, ancestry, resources, and generation; the habitat panel and display-off status show the cumulative automatic restart count. A restart event records the previous world's duration and generation. The current world continues to autosave normally; automatic restarts do not accumulate backup files for extinct worlds. Manual **New** still keeps one `.previous` backup and resets the restart count. Returning to the viewport after a restart rebuilds the scene and clears the old organism selection.

Validation: `dotnet run --project Tests/SimulationChecks.csproj -c Release -- --restart-check` covers disabled/enabled restart, pauses, a last-organism death, repeated restarts, fresh seeds, saved settings, and no-display snapshot restoration.

**Run without display** hides the viewport, disconnects rendering updates and stops population/environment display snapshots. The simulation runs uncapped in fixed 0.1-second steps; the 1x/5x/20x/100x selection applies again after **Show simulation**. Small status updates continue once per second. Pause, manual save and autosave remain available. The optional **Keep computer awake** checkbox prevents idle system sleep while running in this mode, but allows the monitor to turn off. The sleep request is released on pause, display restoration, simulation failure or normal close. Launching with `--no-display` opens this mode directly.

Autosave uses an independent one-second maintenance timer, so its 60-second schedule works with the viewport hidden or the window minimized. Overlapping save requests share the pending save. Closing pauses the simulation, waits for any earlier save, and then saves the final live state before stopping. Worker command shutdown is synchronized so requests cannot be stranded during termination. None of these changes alter reproduction, genetics or ecology.

Two runs per mode from the same saved starting population measured 12.96x/14.47x with reduced display and 18.39x/18.15x without display (100x requested for the former; uncapped for the latter). Each reading averages approximately twelve seconds after warmup. These are workload-dependent measurements, not a guaranteed overnight speed. Repeat with `--smoke-display-benchmark --smoke-reduced` or `--smoke-display-benchmark --smoke-no-display`; smoke runs use a copy of the saved world and never save over it.

Validation: build/publish, five-minute survival/save-resume, no-display advancement without snapshots, pause, saving live state, restoring snapshots, shutdown request completion, and packaged hide/restore checks passed. The general pass focused on unattended operation; ecology and evolutionary rules are unchanged.

## Mutation scaling and appendage contact

Offspring now use a shared, size-dependent parameter-mutation budget across whole-body traits, growth patterns, branch traits, and neural wiring. The expected parameter-edit budget grows as `4 + 0.5 * sqrt(1 + patternCount + branchCount + wireCount / 4)`. Probabilities are scaled against the estimated unscaled mutation count; this is an expected budget, not an exact number of edits. Small triangular parameter changes replace uniformly distributed changes. Per-offspring structural insertion, deletion, duplication, and rewiring opportunities remain. Sexual recombination remains unchanged and can still produce substantial differences. No symmetry, preferred body axis, part catalogue, or complexity reward was introduced.

Existing adult genomes are untouched. Existing saves default to scaled mutations in future offspring; saves now retain the selected mutation mode. The first normal updated launch backs up the previous world as `ecosystem.json.pre-evolution-mechanics`. `Legacy` is available for headless mutation comparisons; it restores the previous mutation probabilities/distribution, not the previous contact physics.

Branches contact rocks along both root-to-joint and joint-to-tip segments using exact segment/sphere proximity. Terrain contact samples each segment at intervals of at most five simulation units, bounded to 32 intervals per segment for extreme deformations. The deepest contact supplies support, grip, touch, scraping, and water uptake once per branch. Its actual position and velocity determine ground reaction, work, and torque. More samples do not multiply the feeding or force budget. Other organisms can contact and injure branch shafts, with full-segment bounds used for rejection checks.

This contact pass was extended by the tissue/force changes below. It still uses one representative ground contact per branch and a soft response rather than a complete nonpenetration solver.

## Repeatable evolution diagnostics

Run the matched comparison sequentially, using fresh worlds and the same seed for both mutation modes:

```powershell
dotnet run --project Tests/SimulationChecks.csproj -c Release -- --evolution-diagnostics 300 "$env:TEMP\tideglass-evolution-diagnostics.json" 149,731,2026
```

Arguments are simulated seconds (default 300), output JSON path (default the temporary directory), and comma-separated seeds (default `149,731,2026`). Both modes use the current physical rules and fixed 0.1-second steps with one simulation worker. The report is updated after each completed run. The command does not load or save over the live ecosystem.

The report includes fixed-parent mutation samples for 1, 10, and 64 branches; population, births, deaths, generations, developed anatomy, survivor offspring counts, and energy-source totals at 30-second intervals; and parent/offspring parameter and topology differences. Numerical comparisons align list indices, so structural edits and sexual recombination can inflate them; these are descriptive measures, not genetic homology or selection scores. Movement reports include path length, flow-relative path, and appendage work. Flow-relative path also includes settling and deformation; work now includes core surface motors and branch actuation, but excludes general metabolism. Cumulative movement/intake totals omit the final tick of organisms removed by death.

Each completed run also performs a paired 10-second assay on independent copies of one surviving specimen with mature root tissue. One copy keeps its anatomy; the other loses a root and all its descendants, including remapped wiring. Both are isolated from other organisms, retain identical environmental fields, and cannot reproduce during the assay. Removal does not release food. Results report survival, intake, energy change, movement, and appendage work. This measures a short-term effect in that setting; it does not establish lifetime fitness. Assays never affect the evolving population.

## Shared tissue accounting and force-driven motion

Save format 9 uses a normalized tissue ledger. A reference core is the default radius-6 ellipsoid. Core radius, elongation and flattening determine volume. Branch material is two tapered elliptical frusta, including cubic developmental growth. Every developmental piece owns its material; overlap and the blended skin do not remove that investment. Viable material scales with integrity. Construction and replacement cost 5 energy per volume unit; stored structural energy is 3.5. Reserve capacity is 44 per expressed volume, rather than rewarding unbuilt instructions. Branch and core maintenance use volume alongside surface/function costs. Newborn reserves pay for the initial core. Severed tissue and whole-body remains return the remaining structural investment, converted into the existing 1.7-energy food units. Damage destroys material; it does not generate food or reserves. Founders and anatomy migrated from older rules are initial conditions. This is an energy/material approximation, not a closed molecular mass-balance model.

Core locomotion sums local powered surface traction and torque against drag. Sensory input biases those forces; it no longer directly turns the organism or supplies a separate dry-land forward velocity. With contraction disabled, sensing cannot steer or propel the core. Symmetric internal spring corrections preserve the center of mass. Branch joints have finite-rate, load-limited motor responses instead of prescribed sinusoidal positions. Contact and self-contact deflect branches, including contact with nonattachment core sections. Energy pays for active work. Simple founders retain a coarse active-surface swimming mechanism without a prescribed limb arrangement.

Branch rendering and collision share their two-segment centerline, taper and elliptical cross-section orientation. Render-only contact projection was removed. Core contacts use oriented ellipsoid support; the blended skin, edge ripples and necks remain approximations. Organism discovery includes expressed body/branch extent, so long tissue can interact beyond the former fixed 65-unit center radius. Sensory and shading neighborhoods retain their original range. Self-contact is a bounded midpoint-based soft response, not an exact nonpenetration constraint solver. These changes do not guarantee adaptive shapes or prevent every overlap.

## Observation controls and passive data collection

The view button cycles Shoreline, Waterline, Overhead, Open water, Swimmers, Bottom dwellers, and displays the current preset name. Underwater creature views follow a suitable living organism; empty niches fall back to habitat views. All controls and run settings are always visible in the bottom bar and wrap at narrower window widths. The same restart, data collection and keep-awake controls remain available with the display off. Click the top-left statistics to collapse/restore them. **Fullscreen** or **F11** toggles fullscreen; **Esc** exits it and restores the previous window state. Sand uses subtle irregular variation and interpolated resource colors; biofilm placements no longer use a repeating grid mask.

**New** opens a seed dialog accepting any signed 32-bit integer. Blank means random; Cancel leaves the current world running. Starting a run first backs up the current world. The current seed is displayed in the bottom bar, survives save/resume, and is included in recorded metadata. Older saves report an unknown seed rather than guessing from their current random state. Automatic extinction restarts continue deriving fresh seeds; the dialog controls manual new runs. Seeds reproduce starting conditions under the same simulation version, not outcomes from older physics versions.

**Collect data** is off by default, persists with the ecosystem, and works during ordinary viewing or display-off operation. It carries across new worlds and automatic restarts. Files are local JSON Lines in `%LOCALAPPDATA%\Tideglass\Research`; **Data folder** opens that location. Each recording segment has schema/physics identifiers, world/run identifiers, seed, random state and real timestamps. Observations are saved every 30 simulated seconds with population events, individual ancestry, anatomy counts, tissue volume, reserves, hydration, location, velocity, energy sources and movement/contact metrics. Full genomes are included when an individual is first observed in each segment; later records reference its run/individual ID. Individuals born and lost between samples may be absent; analysis must account for this.

New recordings use losslessly compressed `.jsonl.gz` files; decompress with any gzip reader to recover the same JSON Lines records. Existing `.jsonl` recordings remain untouched. Every record is flushed. Segments rotate after approximately 32 MiB of compressed data, which also avoids repeatedly writing every surviving genome after only a few samples in large populations. New runs/extinctions/toggle-off/shutdown receive explicit endings. Existing files are never deleted automatically. Recording failures are reported while simulation continues; toggling collection off/on retries. Collection does not use the simulation RNG or change selection. Tests compare recorded and unrecorded evolution for identical state, decode compressed records, and verify segment rotation and closure.

## Earlier research and validation

Research basis: [Sims, mutation scaling and body/control inheritance](https://www.karlsims.com/papers/siggraph94.pdf), [Framsticks, genotype/phenotype separation](https://www.framsticks.com/a/al_genotype.html), and [EvoGym, physical material and control co-design](https://evolutiongym.github.io/). These motivate experiments; they do not guarantee that more complex organisms will evolve in this ecosystem.

Validation on 2026-10-11: focused mutation/contact checks and the 40-simulated-minute seed-731 endurance run passed, including deterministic save/resume, migrations, worker snapshots, and no-display lifecycle checks. A 300-second seed-731 run remained at generation two, below the endurance check's required third generation; the full run reached generation twelve by 2,100 seconds and completed successfully. Six 300-second comparisons (three seeds, two modes) all survived. At 300 seconds, legacy/scaled populations were 59/66 for seed 149, 65/63 for 731, and 76/72 for 2026. These mixed short-run results do not establish better long-term adaptation or diversity.

Across 400 offspring from a fixed ten-branch parent, mean index-aligned scalar differences fell from 84.03 with legacy mutations to 7.93 with scaled mutations. For 64 branches the comparison was 479.52 versus 13.71; structural changes still occurred. Three alternating timing runs from the same copied saved-population fixture measured median tick times of 11.069 ms before and 11.518 ms after (about 4% more time per tick). The populations diverged to 308/303 organisms during the benchmark; this is a workload-specific estimate, not an isolated contact-kernel measurement or performance guarantee.

Release build/publish and packaged fresh-anatomy/copied-existing-population smoke runs passed. The copied-population app check ended with 324 organisms, a 10.52 ms mean rendering-callback interval, and 6.95x achieved speed at a 20x request. These smoke runs did not save over the live ecosystem.

Validation on 2026-10-12: release build and the 40-simulated-minute seed-731 endurance check passed (generation 11 by 2,100 seconds). New tests cover volume scaling, paid growth, expressed storage, one-time recycling, powerless-cortex control, powered steering, tapered/flat contact, long-body neighbor discovery, noninterfering recording, disk errors, and recording across extinction. Existing mechanics, migration, deterministic save/resume, worker and no-display lifecycle checks pass. UI smoke checks exercise all six view labels, statistics collapse, fullscreen restoration, and the 960-pixel layout.

Richer physics costs more for heavily branched populations. A copied mature-world benchmark (one worker, 300 steps after warmup) measured roughly 31 ms/tick after removing repeated tissue calculations, versus 13 ms/tick for the earlier baseline. The worlds evolved to different populations (457 versus 308), so these are workload measurements, not a pure kernel comparison or a promise about display-off speed. Fresh seed-731 endurance completed in about 98 wall seconds including the rest of the checks.

Additional fresh seeds 149 and 2026 passed ten-simulated-minute survival, reproduction and save/resume checks. Recording stores each observed genome once per segment to avoid repeatedly writing unchanged adult recipes.

## Crowded-population performance

The October 12 overnight pass bounds neighbor searches to the actual query area, keeps candidate geometry together in spatial buckets, rejects distant tissue/rocks before oriented contact calculations, and reuses shape frames and branch contacts within each solve. Tissue capacity is reused only while that organism's tissue volume is unchanged; a neighbor's capacity is still read after damage. Physics, mutation, interaction order, and the fixed 0.1-second step are unchanged.

A copied live save started with 2,851 organisms and ended with 2,773 after 10 warmup plus 300 measured steps. One-worker tick time decreased from 51.94 to 39.14 ms; eight-worker time decreased from 41.56 to 31.36 ms (about 33% more throughput). The original and optimized final saves were byte-for-byte identical, including across worker counts. The packaged app measured 2.45x with display disabled and passive recording enabled on this save. These are workload-specific measurements; population density, anatomy, and background work affect overnight speed.

`--mechanics-only` also checks 8,000 randomized core contacts and 1,200 branch/terrain/rock contacts against unpruned reference calculations, plus ordered neighbor discovery across bin boundaries, long appendages, and clamped world edges. Mechanics, recording, restart, and migration checks passed. The packaged display-off benchmark passed and restored its display before closing. The live run was resumed from its unchanged save using `--no-display`.

The same pass added fast gzip compression to passive recording after observing raw segments approaching 32 MiB in about 40 wall seconds. A packaged recording shrank from 31,889,618 to 8,416,484 bytes (74% smaller) and decoded successfully with a normal closing record. Rotation now uses compressed bytes, further reducing repeated genome writes. Recording tests cover compressed round trips, rotation, extinction, disk failure, and unchanged simulation outcomes. Existing recordings are retained.

## Resource budgets and large-world recovery

The October 12 follow-up corrects a small-body loophole: core food/light uptake now scales with physical area, branch exchange uses reference-cell area rather than dividing by the organism's own radius squared, and reserve capacity has only a numerical minimum of 0.001. Growth keeps at most 15% of capacity in reserve, capped at 8 energy, so small organisms can develop tissue. Previously a fixed 8-energy reserve blocked development in almost all organisms in the overnight population. Mating contributions are limited to actual donor reserves, with a transfer loss, instead of crediting a fixed amount to a newborn regardless of what the mate could afford.

All photosynthetic tissue shares a finite incoming-light budget per vertical habitat column, as well as local carbon. Organic decomposition consumes local oxygen and releases carbon. Population pressure therefore comes from resource competition and waste; there is no population cap, forced cull, or reward for a preferred shape. These rules change evolutionary outcomes relative to earlier versions. New recordings identify the physics as `resource-budgets-v2`.

Saving and loading stream JSON directly to/from files. Manual New still backs up the current world before replacement, without allocating a population-sized JSON string. Recording rotation preserves the 30-simulated-second sampling clock, including when an individual observation exceeds the segment limit or the simulation is paused. Collection stops with a visible error when free disk space falls below 2 GiB; existing recordings remain intact. Turning collection off and on retries after space is available.

Above 600 organisms, the viewport batches distant body and branch geometry by color and limits detailed skins to 192 visible organisms plus the selected specimen. Selection restores full detail. Display snapshots slow gradually to at most a half-second interval at high populations, and paused worlds stop producing duplicate snapshots. All organisms continue to receive full physics updates.

Validation: mechanics, resource accounting, recording rotation/disk errors, extinction restart, migration and deterministic save/resume checks passed. A real 602 MB save containing 6,396 organisms completed streamed backup and New in 11.8 seconds. A packaged, paused 8,000-organism rendering fixture used 192 detailed bodies and 12 batches, with a mean rendering-callback interval of 18.51 ms and 3.73 ms mean CPU render work. This measures rendering, not simulation throughput.

The seed-731 40-minute endurance check passed. Independent one-hour runs ended at 377 organisms for seed 149 and 484 for seed 2026, averaging 4.29 and 2.62 developed branches respectively; both declined from higher populations in their final five minutes. These checks establish working growth and population responses, not long-term ecological stability or guaranteed morphological diversity. Repeat with `dotnet run --project Tests/SimulationChecks.csproj -c Release -- --resource-survey 3600 149,2026`.

`Tests/overnight_analysis.py [recording-directory] [save-path]` summarizes archived observations without loading the entire population's genomes at once; it requires Python and `ijson`.

## Tissue feeding and scavenging

Core surfaces and digestive appendage tips consume contacted material. A bite removes a bounded volume from a particular core section or branch, takes the corresponding fraction of the victim's stored reserves, and returns 60–80% of that energy according to digestive investment. Processing costs energy. Consumed volume is subtracted from integrity before severing or death produces remains, so carcasses cannot return that material again. Hungry organisms select one victim per digestive surface per tick; additional neighbors cannot multiply its throughput. There is no predator class, kill reward, or required predator population.

Membrane protection and branch toughness continuously resist digestion instead of providing a binary immunity threshold. Digestive investment, mechanical access and exposed wounds affect the rate. Sustained physical contact gradually improves handling; losing contact resets that benefit. Adhesion supplies bounded contact friction, reacts on both bodies, and costs energy. Relative movement can overcome it. This is an overdamped contact approximation, not a rigid jaw or tether simulation.

The same digestive investment earns energy from physically contacted carcasses, with conversion losses and processing costs. Local carcass gradients and tissue/wound cues feed existing heritable food responses; the existing signed tissue-attraction trait responds to nearby tissue without requiring enzymes to exceed armor first. These cues can be ignored or opposed by the evolved controller. They do not prescribe pursuit or create locomotion forces independently of the existing motors.

Recording schema 2, physics `tissue-feeding-v3`, adds cumulative `Feeding` counters to world observations and individual records. Fields are `PreyNearbySeconds`, `Encounters`, `ContactSeconds`, `Bites`, `TissueVolume`, `TissueEnergy`, `ScavengedEnergy`, `DigestionCost`, `GripCost`, and `LocomotionEnergy`. Energy gains report accepted intake before the separately recorded processing/gripping costs. Movement cost covers all locomotion, not specifically pursuit. Nearby-prey time counts organism-seconds within the sensory range; contact time counts digestive-surface-seconds, so multiple appendages can accumulate time simultaneously. Encounters are new feeding contacts, and bites are successful processing updates, not unique prey or kills. World totals survive deaths between samples and are also written in segment endings. They reset with a new world; individual totals start at birth (or at this upgrade for existing adults).

`FeedingSince` identifies the start of these counters when an older world is resumed. Contact duration, counters and their start time survive saves, with independent copies in display snapshots. Existing recordings retain their original schema. The existing **Collect data** toggle controls disk collection; no new analysis UI or comparative predation experiment was added.

Validation covers tissue/reserve energy conservation, low-reserve prey, armor/wounds/pressure, one-surface throughput, contact duration and separation, paid grip and escape, severing without duplicate carcass energy, core and appendage integration, scavenging, local sensing, independent snapshots, saved counters and recorded fields. The 40-minute seed-731 endurance check and deterministic continuation passed. These checks validate mechanics and stability, not the emergence of sustained hunting or predator/prey coexistence.
