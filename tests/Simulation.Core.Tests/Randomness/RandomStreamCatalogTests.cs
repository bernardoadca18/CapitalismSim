using CapitalismSim.Simulation.Core.Randomness;
using Xunit;

namespace CapitalismSim.Simulation.Core.Tests.Randomness;

public sealed class RandomStreamCatalogTests
{
    [Fact]
    public void SameDomain_DerivedTwice_ProducesSameSequence()
    {
        var catalog = new RandomStreamCatalog(rootSeed: 100UL);

        DeterministicRandom first = catalog.GetStream(new RandomDomain("A"));
        DeterministicRandom second = catalog.GetStream(new RandomDomain("A"));

        Assert.Equal(first.NextUInt64(), second.NextUInt64());
    }

    [Fact]
    public void DifferentDomains_ProduceIndependentSequences()
    {
        var catalog = new RandomStreamCatalog(rootSeed: 100UL);

        DeterministicRandom domainA = catalog.GetStream(new RandomDomain("A"));
        DeterministicRandom domainB = catalog.GetStream(new RandomDomain("B"));

        Assert.NotEqual(domainA.NextUInt64(), domainB.NextUInt64());
    }

    [Fact]
    public void DifferentEntityIdentities_ProduceIndependentSequences()
    {
        var catalog = new RandomStreamCatalog(rootSeed: 100UL);
        var domain = new RandomDomain("Population");

        DeterministicRandom entity1 = catalog.GetStream(domain, entityIdentity: 1, purpose: null);
        DeterministicRandom entity2 = catalog.GetStream(domain, entityIdentity: 2, purpose: null);

        Assert.NotEqual(entity1.NextUInt64(), entity2.NextUInt64());
    }

    [Fact]
    public void DifferentPurposes_ProduceIndependentSequences()
    {
        var catalog = new RandomStreamCatalog(rootSeed: 100UL);
        var domain = new RandomDomain("RealEstate");

        DeterministicRandom priceJitter = catalog.GetStream(domain, "PriceJitter");
        DeterministicRandom maintenanceEvent = catalog.GetStream(domain, "MaintenanceEvent");

        Assert.NotEqual(priceJitter.NextUInt64(), maintenanceEvent.NextUInt64());
    }

    [Fact]
    public void DifferentRootSeeds_ProduceIndependentSequencesForSameDomain()
    {
        var catalogA = new RandomStreamCatalog(rootSeed: 1UL);
        var catalogB = new RandomStreamCatalog(rootSeed: 2UL);
        var domain = new RandomDomain("Population");

        DeterministicRandom fromA = catalogA.GetStream(domain);
        DeterministicRandom fromB = catalogB.GetStream(domain);

        Assert.NotEqual(fromA.NextUInt64(), fromB.NextUInt64());
    }

    [Fact]
    public void AddingUnrelatedDomain_DoesNotAffectExistingDomainSequence()
    {
        // This is the exact scenario called out in the project brief:
        // adding a brand new domain must never perturb an existing one.
        var catalogBefore = new RandomStreamCatalog(rootSeed: 555UL);
        ulong valueBefore = catalogBefore.GetStream(new RandomDomain("Population")).NextUInt64();

        var catalogAfter = new RandomStreamCatalog(rootSeed: 555UL);
        _ = catalogAfter.GetStream(new RandomDomain("RealEstate")); // Simulates a new domain existing now.
        ulong valueAfter = catalogAfter.GetStream(new RandomDomain("Population")).NextUInt64();

        Assert.Equal(valueBefore, valueAfter);
    }
}
