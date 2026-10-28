using Content.Shared.Imperial.Medieval.Administration.Ships;
using Robust.Shared.Configuration;

namespace Content.IntegrationTests.Tests.Imperial.EngineCompatibility;

[TestFixture]
public sealed class SeaWindMigrationTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ExperimentDisabledSea
  components:
  - type: Sea
    Disabled: true
- type: entity
  id: ExperimentActiveSea
  components:
  - type: Sea
";

    [TestCase(null)]
    [TestCase(true)]
    [TestCase(false)]
    public async Task WindConfigurationOnlyRandomizesWithAnActiveSea(bool? disabledSea)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var config = server.ResolveDependency<IConfigurationManager>();
        await server.WaitPost(() =>
        {
            if (disabledSea.HasValue)
                server.EntMan.SpawnEntity(disabledSea.Value ? "ExperimentDisabledSea" : "ExperimentActiveSea", map.GridCoords);

            // Make both timers due on every tick, with a guaranteed storm increase if they incorrectly run.
            config.SetCVar(ShipsCCVars.WindChangeTime, 0f);
            config.SetCVar(ShipsCCVars.StormChangeTime, 0f);
            config.SetCVar(ShipsCCVars.StormIncreaseChance, 1f);
            config.SetCVar(ShipsCCVars.StormDecreaseChance, 0f);
            config.SetCVar(ShipsCCVars.StormLevel, 7f);
            config.SetCVar(ShipsCCVars.WindPower, 17f);
            config.SetCVar(ShipsCCVars.WindRotation, 123f);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            if (disabledSea == false)
            {
                Assert.That(config.GetCVar(ShipsCCVars.StormLevel), Is.EqualTo(8f), "An active sea must still advance its storm simulation.");
                Assert.That(config.GetCVar(ShipsCCVars.WindPower), Is.InRange(7f, 8f));
                return;
            }

            Assert.That(config.GetCVar(ShipsCCVars.StormLevel), Is.EqualTo(7f));
            Assert.That(config.GetCVar(ShipsCCVars.WindPower), Is.EqualTo(17f));
            Assert.That(config.GetCVar(ShipsCCVars.WindRotation), Is.EqualTo(123f));
        });
        await pair.CleanReturnAsync();
    }
}
