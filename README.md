# Tideglass

A 3D evolution simulator for Windows that you can leave running. Simple
one-to-four-cell organisms live along a tidal shoreline, grow branching bodies
from an inherited recipe, compete for food and light, and mutate as they
reproduce.

Nothing is scripted. There are no species, body templates, complexity rewards
or population caps by default. Simple organisms may win, and the world can go
extinct.

## What's simulated

- **Bodies:** inherited growth recipes build branching tissue with its own
  size, stiffness, muscle rhythm, grip, digestion and photosynthesis. Growth
  costs energy and happens gradually.
- **Movement:** tissue pushes against water and ground. Gaits aren't preset;
  they come from evolved local signaling between tissues.
- **Environment:** tides, day/night, currents, rocks, puddles and soil
  moisture, plus oxygen, carbon and food fields at several depths.
- **Feeding:** grazing, filter feeding, photosynthesis, scavenging and
  digesting other organisms on contact.
- **Reproduction:** budding with mutation, or recombination with a nearby mate.

It's an approximation, not a fluid or rigid-body solver.

## Run

From source (.NET 9 SDK):

```powershell
dotnet run --project Tideglass.csproj -c Release
```

The publish command under Development builds a self-contained
`dist/Tideglass.exe` that needs no .NET install, account or network.

## Controls

| Input | Action |
| --- | --- |
| Click | Inspect an organism |
| Drag / Shift-drag / scroll | Orbit / pan / zoom |
| WASD, Q/E | Move the camera |
| F | Follow the selected organism |
| G | Wander between organisms |
| Space | Pause |
| F11 / Esc | Fullscreen / exit |

Speed buttons run at 1x to 100x, limited by CPU. **Run without display** keeps
simulating with rendering off, and **Restart after extinction** starts a new
world automatically, for overnight runs.

## Saves and data

The world autosaves every 60 seconds to `%LOCALAPPDATA%\Tideglass\ecosystem.json`
and resumes on launch. **New** backs up the current world first and accepts a
seed. **Collect data** writes compressed JSON Lines observations to
`%LOCALAPPDATA%\Tideglass\Research`.

## Development

```powershell
dotnet run --project Tests/SimulationChecks.csproj -c Release
dotnet publish Tideglass.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist
```

The checks cover mechanics, save/resume determinism, migrations and endurance
runs.

Inspired by [Karl Sims' evolved creatures](https://www.karlsims.com/papers/siggraph94.pdf),
[Framsticks](https://www.framsticks.com/) and [EvoGym](https://evolutiongym.github.io/).

MIT License.
