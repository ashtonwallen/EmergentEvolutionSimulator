using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace Tideglass;

// Owned by the simulation worker. Observations never draw from the simulation RNG.
public sealed class ResearchRecorder : IDisposable
{
    public static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tideglass", "Research");
    private readonly string directory;
    private readonly long segmentLimit;
    private readonly Func<long> availableSpace;
    private FileStream? recordingFile;
    private StreamWriter? writer;
    private double nextSample;
    private string? sampledRun;
    private readonly HashSet<int> recordedGenomes = [];
    private bool failed;
    private string? error;
    public string? Error => Volatile.Read(ref error);
    public ResearchRecorder(string? directory = null) : this(directory, 32L * 1024 * 1024) { }
    internal ResearchRecorder(string? directory, long segmentLimit, Func<long>? availableSpace = null)
    {
        this.directory = directory ?? DefaultDirectory;
        this.segmentLimit = segmentLimit > 0 ? segmentLimit : throw new ArgumentOutOfRangeException(nameof(segmentLimit));
        this.availableSpace = availableSpace ?? (() => new DriveInfo(Path.GetPathRoot(Path.GetFullPath(this.directory))!).AvailableFreeSpace);
    }
    private void Write(object value)
    {
        if (availableSpace() < 2L * 1024 * 1024 * 1024)
            throw new IOException("Data collection stopped to leave 2 GB free for saves and other applications. Free disk space, then turn collection off and on to retry.");
        // Stream large genomes directly, rather than allocating a population-sized string.
        writer!.Flush();
        JsonSerializer.Serialize(writer.BaseStream, value);
        writer.BaseStream.WriteByte((byte)'\n');
        writer.BaseStream.Flush();
    }
    public void Observe(World world, bool enabled)
    {
        if (!enabled) { End(world, "disabled"); failed = false; Volatile.Write(ref error, null); return; }
        if (failed) return;
        try
        {
            world.RunId ??= Guid.NewGuid().ToString("N");
            if (sampledRun != world.RunId) { sampledRun = world.RunId; nextSample = world.Time; }
            // Rotation may close the writer, but must never reset the sample clock.
            if (world.Time + 1e-8 < nextSample) return;
            if (writer == null)
            {
                Directory.CreateDirectory(directory);
                world.RunId ??= Guid.NewGuid().ToString("N");
                string id = Guid.NewGuid().ToString("N");
                recordingFile = new FileStream(Path.Combine(directory, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{id}.jsonl.gz"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                writer = new StreamWriter(new GZipStream(recordingFile, CompressionLevel.Fastest));
                Write(new { type = "start", schema = 2, physics = "tissue-feeding-v3", segment = id, world.RunId, world.Seed, world.RandomState, world.Time, world.MutationMode, world.PopulationCap, world.AutomaticRestarts, utc = DateTime.UtcNow, intervalSeconds = 30, feedingSince = world.FeedingSince ?? world.Time });
                recordedGenomes.Clear();
            }
            if (world.Time + 1e-8 < nextSample) return;
            recordedGenomes.IntersectWith(world.Organisms.Select(o => o.Id));
            Write(new { type = "sample", utc = DateTime.UtcNow, world.Time, world.Births, world.Deaths, world.Exchanges, world.RecordGeneration,
                population = world.Organisms.Count, world.PopulationCap, world.WaterLevel, world.Daylight, world.Feeding, world.FeedingSince,
                organisms = world.Organisms.Select(o => new { o.Id, o.ParentId, o.MateId, o.Generation, o.Age, o.Children, o.Energy, o.Capacity,
                    tissue = TissueEconomy.Volume(o), cells = o.Body.Count, branches = o.BudStates.Count(b => b.Growth > .01), o.Center, o.Velocity,
                    o.Feeding, o.Hydration, o.LightEnergy, o.GrazingEnergy, o.ShoreEnergy, o.TissueEnergy, o.LocomotionPower, o.GroundContacts, o.ContactSlip,
                    // Adults keep their inherited recipe. Record each observed individual once per segment.
                    genes = recordedGenomes.Add(o.Id) ? o.Genes : null }).ToArray() });
            nextSample = world.Time + 30;
            // Limit bytes on disk, not expanded JSON. This also avoids repeating
            // every surviving genome too frequently in crowded populations.
            if (recordingFile!.Position >= segmentLimit) End(world, "rotation");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { Fail(ex); }
    }
    private void Fail(Exception ex)
    {
        failed = true; Volatile.Write(ref error, ex.Message);
        try { writer?.Dispose(); } catch (IOException) { }
        writer = null;
        recordingFile = null;
    }
    public void End(World world, string reason)
    {
        if (writer == null) return;
        try { Write(new { type = "end", reason, world.RunId, world.Time, population = world.Organisms.Count, world.Births, world.Deaths, world.Feeding, world.FeedingSince, utc = DateTime.UtcNow }); writer.Dispose(); writer = null; recordingFile = null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Fail(ex); }
    }
    public void Dispose() { try { writer?.Dispose(); } catch (IOException ex) { Volatile.Write(ref error, ex.Message); } writer = null; recordingFile = null; }
}
