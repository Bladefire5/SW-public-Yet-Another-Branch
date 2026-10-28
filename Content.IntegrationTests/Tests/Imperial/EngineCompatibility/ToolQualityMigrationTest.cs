using Content.Shared.Maps;
using Content.Shared.Prying.Components;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Server.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.Components;
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
  id: ExperimentQualityArtifact
  components:
  - type: XenoArtifact
    isGenerationRequired: false
- type: entity
  id: ExperimentQualityMultitool
  components:
  - type: Tool
  - type: Prying
  - type: MultipleTool
    entries:
    - behavior: [Prying]
    - behavior: [Screwing]
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
            ProtoId<ToolQualityPrototype> pryingQuality = "Prying";
            ProtoId<ToolQualityPrototype> screwingQuality = "Screwing";
            ProtoId<ToolQualityPrototype> cuttingQuality = "Cutting";
            var tool = server.EntMan.SpawnEntity("ExperimentQualityTool", map.GridCoords);
            Assert.That(tools.HasQuality(tool, pryingQuality), Is.True);
            Assert.That(tools.HasAllQualities(tool, new[] { pryingQuality, screwingQuality }), Is.True);
            Assert.That(tools.HasAllQualities(tool, new[] { pryingQuality, cuttingQuality }), Is.False);
            Assert.That(tools.HasAllQualities(tool, Array.Empty<ProtoId<ToolQualityPrototype>>()), Is.True);

            var prototypes = server.ResolveDependency<IPrototypeManager>();
            // Tiles must already be registered before pooled test prototypes are reloaded.
            var tile = prototypes.Index<ContentTileDefinition>("FloorTechMaintDark");
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

    [TestCase("Welder", "Welding")]
    [TestCase("PowerDrill", "Screwing")]
    [TestCase("JawsOfLife", "Prying")]
    public async Task ExistingScalarQualityPrototypesLoadAndInitialize(string prototype, string quality)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var tool = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            Assert.That(server.System<SharedToolSystem>().HasQuality(tool, quality), Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ArtifactEffectAppliesAndCyclesTheMigratedToolRegistry()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var artifacts = server.System<XenoArtifactSystem>();
            var tools = server.System<SharedToolSystem>();
            var artifact = entities.SpawnEntity("ExperimentQualityArtifact", map.GridCoords);
            var artifactComponent = entities.GetComponent<XenoArtifactComponent>(artifact);
            var node = entities.SpawnEntity("XenoArtifactOmnitool", map.GridCoords);
            var nodeComponent = entities.GetComponent<XenoArtifactNodeComponent>(node);
            Assert.That(artifacts.AddNode((artifact, artifactComponent), (node, nodeComponent)), Is.True);
            Assert.That(tools.HasQuality(artifact, "Screwing"), Is.False);
            Assert.That(artifacts.ActivateNode((artifact, artifactComponent), (node, nodeComponent),
                null, null, map.GridCoords, consumeDurability: false), Is.True);
            Assert.That(tools.HasQuality(artifact, "Screwing"), Is.True);
            Assert.That(entities.GetComponent<ToolComponent>(artifact).SpeedModifier, Is.EqualTo(2));

            foreach (var quality in new[] { "Prying", "Anchoring", "Cutting", "Pulsing", "Screwing" })
            {
                Assert.That(tools.CycleMultipleTool(artifact), Is.True);
                Assert.That(tools.HasQuality(artifact, quality), Is.True, quality);
            }

            entities.DeleteEntity(artifact);
            Assert.That(entities.Deleted(node), Is.True);
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
