using Content.Shared.Maps;
using Content.Shared.Prying.Components;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Imperial.EngineCompatibility;

[TestFixture]
public sealed class ToolQualityMigrationTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ExperimentQualityTool
  components:
  - type: Tool
    qualities: [Prying, Screwing]
- type: entity
  id: ExperimentQualityMultitool
  components:
  - type: Tool
  - type: Prying
  - type: MultipleTool
    entries:
    - behavior: [Prying]
    - behavior: [Screwing]
- type: tile
  id: ExperimentQualityTile
  parent: FloorSteel
  deconstructTools: [Prying]
";

    [Test]
    public async Task QualitySequencesLoadAndKeepExistingCapabilityChecks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var tools = server.System<SharedToolSystem>();
            var pryingQuality = "Prying";
            var screwingQuality = "Screwing";
            var tool = server.EntMan.SpawnEntity("ExperimentQualityTool", map.GridCoords);
            Assert.That(tools.HasQuality(tool, pryingQuality), Is.True);
            Assert.That(tools.HasAllQualities(tool, new[] { pryingQuality, screwingQuality }), Is.True);
            Assert.That(tools.HasAllQualities(tool, new[] { pryingQuality, "Cutting" }), Is.False);
            Assert.That(tools.HasAllQualities(tool, Array.Empty<string>()), Is.True);

            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var tile = prototypes.Index<ContentTileDefinition>("ExperimentQualityTile");
            Assert.That(tile.CanCrowbar, Is.True);
            Assert.That(tile.DeconstructTools.Count, Is.EqualTo(1));

            // This real prototype failed to load before the tool-quality serializer migration.
            var bottle = prototypes.Index<EntityPrototype>("DrinkGildlagerBottleFull");
            var bottleTool = (ToolComponent) bottle.Components["Tool"].Component;
            ProtoId<ToolQualityPrototype> rollingQuality = "Rolling";
            Assert.That(bottleTool.Qualities, Does.Contain(rollingQuality));
            Assert.That(bottleTool.SpeedModifier, Is.EqualTo(0.75f));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CyclingMultitoolReplacesQualitiesAndPryingCapability()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var tools = server.System<SharedToolSystem>();
            var pryingQuality = "Prying";
            var screwingQuality = "Screwing";
            var tool = entities.SpawnEntity("ExperimentQualityMultitool", map.GridCoords);
            var prying = entities.GetComponent<PryingComponent>(tool);
            Assert.That(tools.HasQuality(tool, pryingQuality), Is.True);
            Assert.That(tools.HasQuality(tool, screwingQuality), Is.False);
            Assert.That(prying.Enabled, Is.True);

            Assert.That(tools.CycleMultipleTool(tool), Is.True);
            Assert.That(tools.HasQuality(tool, pryingQuality), Is.False);
            Assert.That(tools.HasQuality(tool, screwingQuality), Is.True);
            Assert.That(prying.Enabled, Is.False);

            Assert.That(tools.CycleMultipleTool(tool), Is.True);
            Assert.That(tools.HasQuality(tool, pryingQuality), Is.True);
            Assert.That(tools.HasQuality(tool, screwingQuality), Is.False);
            Assert.That(prying.Enabled, Is.True);
        });
        await pair.CleanReturnAsync();
    }
}
