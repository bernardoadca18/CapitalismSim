using CapitalismSim.Simulation.Core.Engine;
using CapitalismSim.Simulation.Core.Time;
using CapitalismSim.Simulation.Persistence.Snapshots;
using Xunit;

namespace CapitalismSim.Simulation.Persistence.Tests.Snapshots;

public sealed class KernelSnapshotRoundTripTests
{
    [Fact]
    public void CaptureThenApply_RestoresClockAndEntityAllocatorExactly()
    {
        var source = new SimulationEngine(new SimulationEngineOptions { WorldSeed = 42UL });
        source.AdvanceTo(new SimulationInstant(12345));
        source.EntityIds.Allocate();
        source.EntityIds.Allocate();

        KernelSnapshotDto snapshot = KernelSnapshotWriter.Capture(source);

        var target = new SimulationEngine(new SimulationEngineOptions { WorldSeed = 42UL });
        KernelSnapshotReader.Apply(target, snapshot);

        Assert.Equal(source.Clock.Current, target.Clock.Current);
        Assert.Equal(source.EntityIds.NextValue, target.EntityIds.NextValue);
        Assert.Equal(source.Events.NextSequenceNumber, target.Events.NextSequenceNumber);
    }

    [Fact]
    public void Apply_MismatchedWorldSeed_Throws()
    {
        var source = new SimulationEngine(new SimulationEngineOptions { WorldSeed = 1UL });
        KernelSnapshotDto snapshot = KernelSnapshotWriter.Capture(source);

        var target = new SimulationEngine(new SimulationEngineOptions { WorldSeed = 2UL });

        Assert.Throws<SnapshotFormatException>(() => KernelSnapshotReader.Apply(target, snapshot));
    }

    [Fact]
    public void Apply_UnknownFormatVersion_Throws()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions { WorldSeed = 1UL });
        var snapshot = new KernelSnapshotDto
        {
            FormatVersion = 999,
            WorldSeed = 1UL,
            CurrentInstantSeconds = 0,
            NextEntityId = 1,
            NextEventSequenceNumber = 1,
        };

        Assert.Throws<SnapshotFormatException>(() => KernelSnapshotReader.Apply(engine, snapshot));
    }

    [Fact]
    public void Apply_InvalidNextEntityId_Throws()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions { WorldSeed = 1UL });
        var snapshot = new KernelSnapshotDto
        {
            FormatVersion = KernelSnapshotWriter.CurrentFormatVersion,
            WorldSeed = 1UL,
            CurrentInstantSeconds = 0,
            NextEntityId = 0,
            NextEventSequenceNumber = 1,
        };

        Assert.Throws<SnapshotFormatException>(() => KernelSnapshotReader.Apply(engine, snapshot));
    }

    [Fact]
    public void Apply_InvalidNextEventSequenceNumber_Throws()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions { WorldSeed = 1UL });
        var snapshot = new KernelSnapshotDto
        {
            FormatVersion = KernelSnapshotWriter.CurrentFormatVersion,
            WorldSeed = 1UL,
            CurrentInstantSeconds = 0,
            NextEntityId = 1,
            NextEventSequenceNumber = 0,
        };

        Assert.Throws<SnapshotFormatException>(() => KernelSnapshotReader.Apply(engine, snapshot));
    }

    /// <summary>
    /// P1.7: Negative instant is invalid - time cannot be negative.
    /// </summary>
    [Fact]
    public void Apply_NegativeCurrentInstant_Throws()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions { WorldSeed = 1UL });
        var snapshot = new KernelSnapshotDto
        {
            FormatVersion = KernelSnapshotWriter.CurrentFormatVersion,
            WorldSeed = 1UL,
            CurrentInstantSeconds = -1,
            NextEntityId = 1,
            NextEventSequenceNumber = 1,
        };

        var ex = Assert.Throws<SnapshotFormatException>(() => KernelSnapshotReader.Apply(engine, snapshot));
        Assert.Contains("CurrentInstantSeconds", ex.Message);
        Assert.Contains("negative", ex.Message);
    }

    /// <summary>
    /// P1.7: Probe system with even increment is invalid for PCG32.
    /// </summary>
    [Fact]
    public void Apply_ProbeSystemWithEvenIncrement_Throws()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions { WorldSeed = 1UL });
        var snapshot = new KernelSnapshotDto
        {
            FormatVersion = KernelSnapshotWriter.CurrentFormatVersion,
            WorldSeed = 1UL,
            CurrentInstantSeconds = 0,
            NextEntityId = 1,
            NextEventSequenceNumber = 1,
            ProbeSystems = new Dictionary<string, ProbeSystemSnapshotDto>
            {
                ["probe"] = new ProbeSystemSnapshotDto
                {
                    RandomState = 12345UL,
                    RandomIncrement = 100UL, // Even - invalid for PCG32
                    Accumulator = 0UL,
                },
            },
        };

        var ex = Assert.Throws<SnapshotFormatException>(() => KernelSnapshotReader.Apply(engine, snapshot));
        Assert.Contains("even RandomIncrement", ex.Message);
    }

    /// <summary>
    /// P1.7: Probe system with null/empty system id is invalid.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Apply_ProbeSystemWithEmptyId_Throws(string id)
    {
        var engine = new SimulationEngine(new SimulationEngineOptions { WorldSeed = 1UL });
        var snapshot = new KernelSnapshotDto
        {
            FormatVersion = KernelSnapshotWriter.CurrentFormatVersion,
            WorldSeed = 1UL,
            CurrentInstantSeconds = 0,
            NextEntityId = 1,
            NextEventSequenceNumber = 1,
            ProbeSystems = new Dictionary<string, ProbeSystemSnapshotDto>
            {
                [id] = new ProbeSystemSnapshotDto
                {
                    RandomState = 12345UL,
                    RandomIncrement = 1UL,
                    Accumulator = 0UL,
                },
            },
        };

        var ex = Assert.Throws<SnapshotFormatException>(() => KernelSnapshotReader.Apply(engine, snapshot));
        Assert.Contains("null or empty system id", ex.Message);
    }

    /// <summary>
    /// P1.7: Probe system with null snapshot is invalid.
    /// </summary>
    [Fact]
    public void Apply_ProbeSystemWithNullSnapshot_Throws()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions { WorldSeed = 1UL });
        var snapshot = new KernelSnapshotDto
        {
            FormatVersion = KernelSnapshotWriter.CurrentFormatVersion,
            WorldSeed = 1UL,
            CurrentInstantSeconds = 0,
            NextEntityId = 1,
            NextEventSequenceNumber = 1,
            ProbeSystems = new Dictionary<string, ProbeSystemSnapshotDto>
            {
                ["probe"] = null!,
            },
        };

        var ex = Assert.Throws<SnapshotFormatException>(() => KernelSnapshotReader.Apply(engine, snapshot));
        Assert.Contains("null probe system snapshot", ex.Message);
    }

    [Fact]
    public void JsonFileStore_SaveThenLoad_RoundTripsExactly()
    {
        string path = Path.Combine(Path.GetTempPath(), $"snapshot-{Guid.NewGuid():N}.json");
        try
        {
            var engine = new SimulationEngine(new SimulationEngineOptions { WorldSeed = 77UL });
            engine.AdvanceTo(new SimulationInstant(4321));
            engine.EntityIds.Allocate();

            KernelSnapshotDto original = KernelSnapshotWriter.Capture(engine);

            var store = new JsonFileKernelSnapshotStore();
            store.Save(path, original);
            KernelSnapshotDto loaded = store.Load(path);

            Assert.Equal(original.FormatVersion, loaded.FormatVersion);
            Assert.Equal(original.WorldSeed, loaded.WorldSeed);
            Assert.Equal(original.CurrentInstantSeconds, loaded.CurrentInstantSeconds);
            Assert.Equal(original.NextEntityId, loaded.NextEntityId);
            Assert.Equal(original.NextEventSequenceNumber, loaded.NextEventSequenceNumber);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void JsonFileStore_Load_MissingFile_Throws()
    {
        var store = new JsonFileKernelSnapshotStore();
        string missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json");

        Assert.Throws<SnapshotFormatException>(() => store.Load(missingPath));
    }

    [Fact]
    public void JsonFileStore_Load_MalformedJson_Throws()
    {
        string path = Path.Combine(Path.GetTempPath(), $"malformed-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{ this is not valid json");
            var store = new JsonFileKernelSnapshotStore();

            Assert.Throws<SnapshotFormatException>(() => store.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
