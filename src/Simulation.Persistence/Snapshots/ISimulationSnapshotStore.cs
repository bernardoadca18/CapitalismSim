using System.Text.Json;

namespace CapitalismSim.Simulation.Persistence.Snapshots;

/// <summary>
/// Persists and retrieves <see cref="SimulationSnapshotDto"/> instances.
/// Abstracted so that <see cref="Simulation.Core"/> never needs to know about
/// filesystems, and so alternative storage strategies can be introduced without changing
/// callers.
/// </summary>
public interface ISimulationSnapshotStore
{
    void Save(string path, SimulationSnapshotDto snapshot);

    SimulationSnapshotDto Load(string path);
}

/// <summary>
/// A simple, human-readable JSON file store for simulation snapshots, using
/// <see cref="System.Text.Json"/>.
/// </summary>
/// <remarks>
/// This is an explicit, initial implementation choice for Phase 2. It
/// is not a promise that the game's eventual full save format will be
/// JSON — see docs/PERSISTENCE_STRATEGY.md for the long-term direction
/// (snapshots + deltas + indexed data, likely SQLite for some domains).
/// JSON was chosen here because it is trivially inspectable while
/// bootstrapping and debugging the simulation, and the snapshot is
/// relatively small.
/// </remarks>
public sealed class JsonFileSimulationSnapshotStore : ISimulationSnapshotStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public void Save(string path, SimulationSnapshotDto snapshot)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(snapshot);

        string json = JsonSerializer.Serialize(snapshot, SerializerOptions);

        // Atomic save: write to a temp file in the same directory, then rename
        // to the target. This ensures the destination file is either fully the
        // old content or fully the new content, never a partial write.
        string? directory = Path.GetDirectoryName(path);
        string tempPath = Path.Combine(
            directory ?? Path.GetTempPath(),
            Path.GetFileName(path) + $".tmp-{Guid.NewGuid():N}");

        try
        {
            File.WriteAllText(tempPath, json);

            // If the target already exists, replace it atomically (preserves
            // file permissions on Unix). If not, move the temp file into place.
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        finally
        {
            // Clean up the temp file if something went wrong before the rename.
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup; ignore failures here.
            }
        }
    }

    public SimulationSnapshotDto Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (!File.Exists(path))
        {
            throw new SnapshotFormatException($"Snapshot file not found: '{path}'.");
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            throw new SnapshotFormatException($"Could not read snapshot file '{path}'.", ex);
        }

        SimulationSnapshotDto? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<SimulationSnapshotDto>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new SnapshotFormatException($"Snapshot file '{path}' is not valid JSON or does not match the expected schema.", ex);
        }

        if (snapshot is null)
        {
            throw new SnapshotFormatException($"Snapshot file '{path}' deserialized to null.");
        }

        return snapshot;
    }
}
