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

The habitat now slopes from deeper water onto exposed land, with a shallow depression and a raised bank. The tide rises and falls over six simulated minutes. Light cycles independently. **Visit the shoreline** frames the water's edge; the automatic camera tour starts after a 24-second landscape view. Live field counts report near-bottom, exposed, and ground-contacting organisms.

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

There are no complexity rewards, scheduled species, or guaranteed progression. Simpler organisms may win. Extinction does not automatically reseed the habitat.

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

New tissue force calculations solve body motion against summed directional water drag and tangential ground resistance. There is no designated power stroke or preferred swimming direction. Unused tissue adds drag and maintenance; contact geometry, stiffness, surface orientation, and motion determine reaction forces. Muscle strength falls with tissue damage, low energy and mechanical load. Positive mechanical work costs energy. Basic core wriggling remains as the primitive locomotion approximation; this is still an overdamped model rather than a full fluid or articulated rigid-body solver.

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
