# SW Experiment / Robust 294 migration — machine recovery note
schema=checkpoint.v1; updated=2026-10-27; source=verified GitHub branch+CI (NOT a claim of successful build)
repo=Bladefire5/SW-public-Yet-Another-Branch
branch=Experiment (case sensitive); only modify this branch; preserve Stance-Break and all other branches.
last_source_commit=2dabf089d2f150a62539b08bef1564b0dc1d7464 (pre-todo checkpoint; refresh HEAD before edits)
source_commit_subject="compat: adapt client map, color and GUI APIs; remove retired reanchor event"
RobustToolbox submodule SHA=3eeaf8253774ab59d705d453fe90ede75b84c607 (294.0.0)
verified SS14 upstream stable=space-wizards/space-station-14@107b6552856df306ba03f95334e0819b6b2be627; pinned same Robust SHA when checked.
.NET SDK=10.0.100 via global.json; package/project/CI updates landed.
baseline_for_before_after=67577b1854571b586e90274372fb8b5bb21d053; old Robust=c06ca39009ce993bed98c216a8affef5f566cb66 (267.1.0), SDK=9.0.100.
GOAL: make Experiment compile, run, pass unit/integration tests, and preserve Spellward gameplay while incorporating required stable upstream SS14 changes that make RobustToolbox 294 compatible. This is NOT an instruction to import unrelated stable SS14 gameplay/balance.
IMPORTANT: upstream RobustToolbox transform-interpolation overhaul PR #7067 was reverted by #7149 due to shuttle movement defects. DO NOT reintroduce it; target engine 294 already excludes it.

## State / completed
- Initial migration: commit b0af8baaa084: engine 294, .NET10, stable Content.props, Directory.Packages.props, global.json, project .csprojs, .gitmodules/submodule.
- 3d3c1fd870c8 removed obsolete OpenToolkit.GraphicsLibraryFramework from solution.
- 60d531222e70 Pow3r and bad cross-project imports.
- 6d14677dbab9 migrated 248 prototype-ID sites across 178 files (mechanical conversion, requires correctness regression checks); converter .github/scripts/migrate_engine_protoids.py, workflow .github/workflows/experiment-prototype-migration.yml.
- 6529f2edb7d6 removed IMapManager from initial shared files + .NET10 workflows.
- 49b7a959b879 other prototype validators / generic interfaces.
- 7b7edc43d2c1, 49954e1dad24, 69653b6f4cf4 fixed shared serializers, [DataDefinition]/[DataRecord]/partial/get-set, map interfaces.
- 650ff64c2e31 migrated IMapManager types throughout Content.Client and Content.Server via .github/scripts/migrate_engine_maps.py and .github/workflows/experiment-map-migration.yml (CI migration succeeded run 40539267171); IMPORTANT: type-only rewrite caused duplicate SharedMapSystem dependencies RA0032 and invalid old signatures; repair individually.
- 2a804a97529d removed old server-only serializer imports, restored missing package references incl Linguini.Bundle / ImageSharp, more prototype fixes.
- 2dabf089d2f1 adapted some client map/color/GUI APIs, removed retired ReAnchorEvent. VERIFY semantic parity for anchor events, especially shuttles, electricity, atmos, node containers, radiation.
- Shared project progressed through compilation; CI then exposed errors in Content.Server and one Content.Client guidebook file. NO full clean solution build yet, and no post-upgrade passing tests.

## Latest CI baseline (exact)
workflow=.github/workflows/experiment-engine-compat.yml
run=https://github.com/Bladefire5/SW-public-Yet-Another-Branch/actions/runs/40541483904
run_id=40541483904; job_id=122320740151; head_sha=2dabf089d2f150a62539b08bef1564b0dc1d7464
result=FAIL at "Build all projects" on 2026-10-27; logs contained ~213 emitted error lines (~107 unique locations; compilation prints errors multiple times). CI fetch latest via tools.mcp__GitHub__fetch_workflow_run_jobs / fetch_workflow_job_logs. Do not trust the numbers without a fresh run after fixes.
Existing pre-upgrade CI had a successful compile & unit tests, but integration failed on missing localization/sprite assets. Do not incorrectly attribute old asset failures to modernization.
REST head: GET https://api.github.com/repos/Bladefire5/SW-public-Yet-Another-Branch/branches/Experiment
REST CI: GET https://api.github.com/repos/Bladefire5/SW-public-Yet-Another-Branch/actions/runs?branch=Experiment&per_page=10

## Highest priority next changes (compiler-derived, as of run 40541483904)
P0: clean RA0032 duplicate map-system [Dependency] fields produced by auto replacing IMapManager -> SharedMapSystem. In each file keep one mapped field, rewrite usages to retained one, remove duplicate field:
  Content.Server/Anomaly/Effects/ProjectileAnomalySystem.cs
  Content.Server/Salvage/SalvageSystem.cs
  Content.Server/Radiation/Systems/RadiationSystem.cs
  Content.Server/Pointing/EntitySystems/PointingSystem.cs
  Content.Server/Pinpointer/NavMapSystem.cs
  Content.Server/Parallax/BiomeSystem.cs
  Content.Server/NPC/Systems/NPCCombatSystem.cs
  Content.Server/EntityEffects/EntityEffectSystem.cs
  Content.Server/Explosion/EntitySystems/ExplosionSystem.cs
  Content.Server/Fluids/EntitySystems/PuddleDebugDebugOverlaySystem.cs
  Content.Server/Shuttles/Systems/DockingSystem.cs
  Content.Server/Singularity/EntitySystems/EventHorizonSystem.cs
  Content.Server/GameTicking/GameTicker.cs
  Content.Server/Atmos/EntitySystems/AtmosphereSystem.cs
  Content.Server/Imperial/Medieval/Ships/Wave/WaveSystem.cs.
  Engine generator treats duplicate same-type dependencies as error. Important to review field names across partial types before removing.

P0: map/tile API changed from MapGridComponent extension methods to SharedMapSystem methods. Errors in:
  Content.Server/NodeContainer/Nodes/{PortPipeNode,PortablePipeNode,PipeNode,NodeHelpers,AdjacentNode}.cs
  Content.Server/Power/Nodes/{CableTerminalPortNode,CableTerminalNode,CableNode,CableDeviceNode}.cs
  Content.Server/Power/Generation/Teg/TegNodeGroup.cs
  Content.Server/Imperial/Medieval/Power/Nodes/{MyrmexPipeNode,MyrmexDeviceNode}.cs
  errors: MapGridComponent.TileIndicesFor missing; MapGridComponent.GetAnchoredEntities missing. Compare current stable upstream versions and adapt system calls, avoid stubbing out wiring.
  Additional method signature errors: Content.Server/Salvage/SalvageSystem.Magnet.cs:443 FindGridsIntersecting two-arg removal; Content.Server/Imperial/Medieval/Siege/SiegeSystem.cs:194,226 EntityCoordinates.FromMap now needs map entity and transform system; Content.Server/Fluids/EntitySystems/PuddleDebugDebugOverlaySystem.cs:82 and Construction/Conditions/ComponentInTile.cs:51 ToVector2i signature changed; GameTicking/GameTicker.RoundFlow.cs:745 SharedMapSystem.Restart removed; Gameplay Map lifecycle needs real upgrade, not no-op.

P0: serialization generator:
  Content.Server/Codewords/CodewordGeneratorPrototype.cs ID and Words get-only; CodewordFactionPrototype.cs ID and Generator get-only: RA0020 + generated CS0200, add private setters according to source generator.
  Content.Server/Imperial/ErtCall/ErtCallPresetPrototype.cs RA0017 (nonpartial DataDefinition) and RA0020 (get-only ID).
  Content.Server/Atmos/TileAtmosphere.cs RA0057: DataField on non-[DataDefinition]/[DataRecord] type, investigate lifetime/prototype semantics before adding marker.
  Content.Server/Worldgen/Prototypes/NoiseChannelPrototype.cs RA0057: NoiseChannelConfig DataFields, add proper [DataDefinition] partial as appropriate.
  Inspect first-party upstream implementations where possible.

P1: compile type and methods, from exact CI:
  Store/Systems/StoreSystem.cs:128,149 currency dictionaries typed ProtoId<CurrencyPrototype> vs string.
  Imperial/ImperialStore/ImperialStoreSystem.cs:163 same for ImperialCurrencyPrototype.
  Imperial/Medieval/Trading/{TradingSystem.cs:210,TradingSystem.PublicListings.cs:166}: typed proto dictionaries vs expected string / EntProtoId.
  SurveillanceCamera/Systems/SurveillanceCameraSystem.cs:232 nullable ProtoId<DeviceFrequencyPrototype>.
  Sandbox/Commands/ColorNetworkCommand.cs:59-66 and Administration/Commands/SetAdminOOC.cs:31-43 Color.TryFromHex(string,out Color) now returns bool; migrate null-based old checks.
  Light/EntitySystems/PoweredLightSystem.cs:39 and Atmos/EntitySystems/FlammableSystem.cs:404 removed EntityUid.SpawnTimer extension; use corresponding timing system.
  Server/Kitchen/EntitySystems/MicrowaveSystem.cs:203 ingredient dictionary ProtoIds.
  Server/GuideGenerator/ReagentEntry.cs:76 dictionary string vs ProtoId<ReagentPrototype>.
  Server/Fax/FaxSystem.cs:592 EntProtoId.Length replaced with valid string equivalent after checking null/empty logic.
  Server/EntityEffects/EntityEffectSystem.cs:534 EntProtoId vs EntProtoId<EdgeSpreaderComponent>; :637 nonnullable ProtoId<EmotePrototype> compared/assigned null.
  Construction/FlatpackSystem.cs:93 nullable EntProtoId.
  Construction/ConstructionSystem.Graph.cs:370 TransformComponent.Anchored setter removed; use anchoring system.
  Construction/ConstructionSystem.Interactions.cs:385-389 changed TryUseTool signature, compare upstream.
  Chemistry/TileReactions/CleanTileReaction.cs:54 ambiguous SplitSolutionWithout overload.
  Chat/AutoEmoteComponent.cs:18 HashSet<string> to HashSet<ProtoId<AutoEmotePrototype>>.
  Server/GameTicking/Commands/SetGamePresetCommand.cs:22 int.InRange extension disappeared; ordinary range check.
  Speech/EntitySystems/AddAccentClothingSystem.cs:30 nullable ProtoId.
  Medieval/Ships/Wind/ServerWindSystem.cs:180,200 TryIndex overload changed for ProtoId<WeatherPrototype>?; unwrap nullables.
  Medieval/Chemistry/Randomization/ChemistryRandomizationSystem.cs:53 ContainerManagerComponent.TryGetContainer removed; use SharedContainerSystem.
  Client/Guidebook/Controls/GuideReagentReaction.xaml.cs:75 typed dictionary argument mismatch.
  IMPORTANT: these are incomplete first-error lists; rerun CI after every coherent batch. Treat all duplicate error output as one source location.

P1: validate retired ReAnchorEvent migration from last source commit 2dabf089d2f1. Old ReAnchorEvent removed in engine, but silently deleting handler may cause lost updates. Compare stable's AnchorStateChangedEvent behavior and adapt these systems: server Power/ExtensionCableSystem, Shuttles/DockingSystem, Atmos/AirtightSystem, NodeContainer/NodeContainerSystem, Singularity/ContainmentFieldGeneratorSystem, Radiation/RadiationSystem.Blockers, etc. Ships must be tested after modifications.

P2 (when compile clean): verify registration and generated serialization, all server/client startup, prototypes+YAML validation, integration/unit suites. Preserve custom projectile prediction, damage confirmation, predicted explosion visual logic+wall occlusion, parry/light attack, network ticks, custom boats/ships, alchemy. Resolve old baseline missing sprite/localization errors separately; no assert "passing" until CI proves it.
P2 compare current engine target with stable pin again if significant upstream movement, but DO NOT merge reverted movement overhaul.

## High-risk custom files (do NOT bulk overwrite with upstream)
Content.Client/Projectiles/ProjectilePredictionSystem.cs
Content.Shared/Weapons/Ranged/Systems/SharedGunSystem.ProjectilePrediction.cs
Content.Shared/Projectiles/SharedProjectileSystem.Presentation.cs
Content.Client/Imperial/Medieval/Magic/MedievalMagicSystem.ProjectilePrediction.cs
Content.Client/Explosion/ExplosionSystem.cs
Content.Client/Explosion/ExplosionOverlaySystem.cs
Content.Server/Explosion/EntitySystems/ExplosionSystem.Processing.cs
Content.Client/Imperial/Medieval/Prediction/MedievalLagCompensationSystem.cs
Content.Shared/Imperial/Medieval/Prediction/MedievalSetLastRealTickEvent.cs
Content.Shared/Imperial/Medieval/Poise/LightAttack/LightAttackWindupSystem.cs
Content.Shared/Imperial/Medieval/Archery/MedievalBowChargeSystem.cs
Content.Shared/Imperial/Medieval/Poise/PoiseSystem.cs
Content.Server/Weapons/Melee/MeleeWeaponSystem.cs
Content.Server/Imperial/Medieval/MeleeResource/MedievalMeleeResourceSystem.cs

## Spellward behavior constraints
- 66ms input timing +400ms wind-up light attacks, weapon rate cooldown; stamina commitment and parry cancels all pending attacks; parry window 200ms default; poise 25 damage/stamina-through-protection/1.5s lockout.
- Unguided projectiles: client impact immediately, server damage authoritative, predicted fireball explosion visuals and wall occlusion; don't break reconciliation.
- Poise threshold baseline 50, armor and NPC stagger, explosion poise.
- Fireball beginner/middle/senior and fire magic tuned in user project; retain all YAML balance.
- Alchemy changes retained; do not merge new standard upstream potion chemistry unreviewed.
- No gameplay rebalance during engine migration.
- Windows user; prefers edited files/packages not shell commands for manual application.

## Testing checklist (already-existing integration suites)
Content.IntegrationTests/Tests/Projectiles/UnguidedProjectilePredictionTest.cs
Content.IntegrationTests/Tests/Projectiles/MagicProjectilePredictionTest.cs
Content.IntegrationTests/Tests/Projectiles/ProjectileExplosionPredictionTest.cs
Content.IntegrationTests/Tests/Projectiles/ProjectileExplosionWallsTest.cs
Content.IntegrationTests/Tests/Networking/SimplePredictReconcileTest.cs
Content.IntegrationTests/Tests/Networking/NetworkIdsMatchTest.cs
Content.IntegrationTests/Tests/Networking/ReconnectTest.cs
Content.IntegrationTests/Tests/Movement/MovementTest.cs
+ new manual/automated ship anchoring, ship movement, parry timing, high-latency remote player and guided projectile checks.
CI workflow currently restores, builds DebugOpt, runs Content.Tests then Content.IntegrationTests. GitHub Actions runner is the actual .NET10 build environment. This chat's terminal needn't compile. CI run logs often lengthy; parse ERROR lines, dedup by file+line+error. Fetch latest run by branch before acting.

## Execution protocol for future agent
1. Fetch Experiment HEAD and newest CI; check if migration workflow auto-committed since checkpoint. Avoid applying stale blobs.
2. Parse newest build log, group by unique path/line, fix highest-volume blocker families first.
3. Prefer stable upstream SS14 analog where safe; preserve Medieval diffs. Use GitHub connected actions for fetching & writing (create_blob/create_tree/create_commit/update_ref or update_file), only Experiment.
4. Commit small coherent batches and use latest GitHub Actions build results; do not claim compilation passes without CI green.
5. Risk note: global regex IMapManager -> SharedMapSystem made duplicate same-type injected dependencies and may leave behavioral API mismatches; verify semantics, not just compiler.
6. At any new interruption, append concise checkpoint to THIS file with latest HEAD SHA, latest CI run/job IDs, failures and corrected files. Keep history above for recovery.
7. No asynchronous promises: perform edits during active turn and report actual commits and CI state.

## Active progress log — 2026-10-27
- fff8348b577e6fb8564f31b994e11d836bc1be9c: repaired RA0032 duplicate SharedMapSystem [Dependency] injection in 6 systems: Docking, Salvage, Radiation, Pointing, Biome, NPCCombat. Previous commit did not modify this todo; recorded retroactively here.
- THIS COMMIT: repaired remaining 9 reported RA0032 systems: EntityEffect, PuddleDebugOverlay, Explosion, ProjectileAnomaly, NavMap, GameTicker, EventHorizon, Wave, Atmosphere. Kept the first [Dependency] and converted the secondary field into alias property so existing references still work; behavioral verification and CI still required.
- Baseline run 40545981126, job 122336601013, at 48d2c57: failed Build all projects; 106 unique compiler/analyzer diagnostics, including 15 RA0032. Previous fixes not yet confirmed by CI.
- Next: inspect new CI for removed RA0032 diagnostics; then upgrade MapGridComponent tile/anchored accessors, serializer DataDefinitions and obsolete APIs. No green build or unit/integration suite yet.
- PROCESS REQUIREMENT: every future code commit to Experiment MUST update todo.md atomically in the same commit, with changed files, CI state and next steps.

## checkpoint.v2 / atomic commit b5222c8b346e
branch=Experiment
base_sha=b5222c8b346e3982e07ffcc44248a5e294868c3b
operation=compat_serializer_cleanup_01
edits=CodewordGeneratorPrototype.ID+Words setters;CodewordFactionPrototype.ID+Generator setters;ErtCallPresetPrototype partial+ID setter;NoiseChannelConfig DataDefinition partial;TileAtmosphere.LastShare runtime-only
ci_before=40546950388:failed(Build all projects;100 unique error locations);40547296304:in_progress(at checkpoint)
ci_after=UNVERIFIED;must fetch build for resulting commit
next=check generated serializer and RA0057, then map/tile APIs and obsolete signatures; no gameplay changes; no green build claimed
rule=EVERY Experiment code commit includes todo.md in SAME git tree/commit; may use machine-only format

```json
{"checkpoint":"v2","base":"37dc3e5464639aebbce45532e205db51e860c585","branch":"Experiment","subject":"compat: adapt color API, enum prototype IDs, typed reagent exclusions and command arg count","files":["Content.Server/Administration/Commands/SetAdminOOC.cs","Content.Server/Sandbox/Commands/ColorNetworkCommand.cs","Content.Server/GameTicking/Commands/SetGamePresetCommand.cs","Content.Server/Chat/AutoEmoteComponent.cs","Content.Server/Chemistry/TileReactions/CleanTileReaction.cs","Content.Server/SurveillanceCamera/Systems/SurveillanceCameraSystem.cs"],"ci":{"latest_completed":"40546950388:failed","predecessor":"40547296304:in_progress","newer":"40548224313:in_progress"},"verification":"not compiled after this commit; test in GitHub Actions","next":["inspect latest run errors","repair map/tile accessors","verify semantic parity for atmosphere/ships"],"invariant":"every Experiment code commit must include todo.md in same tree"}
```

checkpoint.v4={"base":"e5c4b0788878da13d8c0ccedfde7e6ffb322cd48","task":"map_api_node_container","files":["Content.Server/NodeContainer/Nodes/NodeHelpers.cs","Content.Server/NodeContainer/Nodes/PipeNode.cs","Content.Server/NodeContainer/Nodes/AdjacentNode.cs","Content.Server/NodeContainer/Nodes/PortPipeNode.cs","Content.Server/NodeContainer/Nodes/PortablePipeNode.cs"],"ci_before":"40548519208:failed(60 errors)","verification":"pending next CI","next":["migrate Power/Nodes, Teg, Myrmex tile APIs","other compile failures","test connectivity"],"invariant":"EVERY Experiment code commit includes todo.md atomically"}

checkpoint.v5={"base":"2940e1fdcc3fa35b43dac7ccaea1502e8f2a4679","task":"map_api_power_nodes","files":["Content.Server/Imperial/Medieval/Power/Nodes/MyrmexDeviceNode.cs","Content.Server/Imperial/Medieval/Power/Nodes/MyrmexPipeNode.cs","Content.Server/Power/Generation/Teg/TegNodeGroup.cs","Content.Server/Power/Nodes/CableDeviceNode.cs","Content.Server/Power/Nodes/CableNode.cs","Content.Server/Power/Nodes/CableTerminalNode.cs","Content.Server/Power/Nodes/CableTerminalPortNode.cs"],"verification":{"local":"diff --check; reviewed against pinned Robust SharedMapSystem.TileIndicesFor and stable cable implementation; connection filters unchanged","ci_before":"40550488923:failed(Build all projects); full logs tool unavailable (Transport closed)","ci_after":"pending"},"next":["inspect CI after commit","remaining compiler failures from checkpoint","runtime cable/TEG/Myrmex connectivity tests after clean build"],"invariant":"EVERY Experiment code commit includes todo.md atomically"}

checkpoint.v6={"base":"7a242197571a405e2513a22ea36c4f99a0acbed8","task":"protoid_consumers_02","files":[".github/workflows/experiment-engine-compat.yml","Content.Client/Guidebook/Controls/GuideReagentReaction.xaml.cs","Content.Server/Construction/FlatpackSystem.cs","Content.Server/EntityEffects/EntityEffectSystem.cs","Content.Server/Fax/FaxSystem.cs","Content.Server/GuideGenerator/ReagentEntry.cs","Content.Server/Imperial/ImperialStore/ImperialStoreSystem.cs","Content.Server/Imperial/Medieval/Ships/Wind/ServerWindSystem.cs","Content.Server/Imperial/Medieval/Trading/TradingSystem.PublicListings.cs","Content.Server/Imperial/Medieval/Trading/TradingSystem.cs","Content.Server/Kitchen/EntitySystems/MicrowaveSystem.cs","Content.Server/Speech/EntitySystems/AddAccentClothingSystem.cs","Content.Server/Store/Systems/StoreSystem.cs"],"verification":{"local":"git diff --check; workflow YAML parsed, Experiment-only trigger asserted; inspected typed field declarations and string-facing consumers","ci_before":"40550488923:failed; 40551430890:in_progress(power-node changes)","ci_after":"pending; full console logs unavailable through connector before diagnostic-output reduction"},"behavior":"retain currency stack multiplication/consumption, chemistry data values, optional-prototype guards; no balance changes","next":["fetch new CI compiler diagnostics/artifact","timer/map/round-restart API migration","build and run tests; node/ship runtime regression checks"],"invariant":"EVERY Experiment code commit includes todo.md atomically"}

checkpoint.v7={"base":"a63c4c5337a0b8e51eef908589295e6fd52baa70","task":"map_lifecycle_construction_container_03","files":["Content.Server/Construction/Conditions/ComponentInTile.cs","Content.Server/Construction/ConstructionSystem.Graph.cs","Content.Server/Construction/ConstructionSystem.Interactions.cs","Content.Server/Fluids/EntitySystems/PuddleDebugDebugOverlaySystem.cs","Content.Server/GameTicking/GameTicker.RoundFlow.cs","Content.Server/Imperial/Medieval/Chemistry/Randomization/ChemistryRandomizationSystem.cs","Content.Server/Imperial/Medieval/Siege/SiegeSystem.cs","Content.Server/Salvage/SalvageSystem.Magnet.cs"],"verification":{"local":"git diff --check; signatures checked against pinned engine; restart compared with stable ServerGameTicker.RoundFlow.ResettingCleanup and MapComponent ComponentShutdown handling","ci_before":"40552096593:in_progress(Build all projects); 40551430890:in_progress(Build all projects)","ci_after":"pending"},"next":["retrieve compiler output from reduced-log CI","migrate ghost-light and fire-resist entity timers preserving pause/deletion behavior","anchor/reanchor parity review and runtime tests after clean compile"],"invariant":"EVERY Experiment code commit includes todo.md atomically"}

checkpoint.v8={"base":"83629fb67f46322f18fb698623e69c197aa715f3","task":"remaining_production_diagnostics_04","files":["Content.Client/Light/AmbientOcclusionOverlay.cs","Content.Server/Atmos/EntitySystems/FlammableSystem.cs","Content.Server/Fax/FaxSystem.cs","Content.Server/Imperial/Medieval/Ships/Wind/ServerWindSystem.cs","Content.Server/Light/EntitySystems/PoweredLightSystem.cs","Content.Server/NPC/Systems/NPCCombatSystem.cs","Content.Server/Parallax/BiomeSystem.cs","Content.Server/Pointing/EntitySystems/PointingSystem.cs","Content.Server/Radiation/Systems/RadiationSystem.cs","Content.Server/Salvage/SalvageSystem.cs","Content.Server/Shuttles/Systems/DockingSystem.cs","Content.Shared/Atmos/Components/FlammableComponent.cs","Content.Shared/Light/Components/PoweredLightComponent.cs"],"ci_baseline":{"run":40552096593,"job":122358266979,"sha":"a63c4c5337a0b8e51eef908589295e6fd52baa70","result":"failed(Build all projects);29 unique error diagnostics","artifact":12756570235},"verification":{"local":"git diff --check; all 29 baseline diagnostics addressed by map batch 83629fb6 plus this batch; no successful build claimed","ci_after":"pending"},"notes":["Reduced CI console output fixed connector log retrieval; fetch_workflow_job_logs now works","Earlier checkpoint incorrectly marked six RA0032 systems complete; actual duplicate fields verified and now replaced with aliases","Removed obsolete TryIndex logging bool argument in sea weather; fax branches both EntProtoId; AO uses grid.Comp.TileSize","Entity timers replaced with per-tick deadlines; AutoPausedField preserves pauses, component queries avoid deleted entities; original durations/effects retained"],"next":["fetch newest CI; expect dependent test/tool project errors once production projects compile","add/run timer pause/deletion regressions and existing power tests","review retired reanchor behavior; startup/prototype/unit/integration validation"],"invariant":"EVERY Experiment code commit includes todo.md atomically"}

checkpoint.v9={"base":"b203f211145f02d76e7a4d5237904b9467945b18","task":"test_harness_map_migration_and_timer_regressions","files":["Content.IntegrationTests/Tests/Body/LegTest.cs","Content.IntegrationTests/Tests/Body/LungTest.cs","Content.IntegrationTests/Tests/Body/SaveLoadReparentTest.cs","Content.IntegrationTests/Tests/CargoTest.cs","Content.IntegrationTests/Tests/ContainerOcclusionTest.cs","Content.IntegrationTests/Tests/Damageable/DamageableTest.cs","Content.IntegrationTests/Tests/DeviceLinking/DeviceLinkingTest.cs","Content.IntegrationTests/Tests/DeviceNetwork/DeviceNetworkTest.cs","Content.IntegrationTests/Tests/Doors/AirlockTest.cs","Content.IntegrationTests/Tests/EntityTest.cs","Content.IntegrationTests/Tests/Fluids/FluidSpillTest.cs","Content.IntegrationTests/Tests/FollowerSystemTest.cs","Content.IntegrationTests/Tests/Interaction/Click/InteractionSystemTests.cs","Content.IntegrationTests/Tests/Interaction/InRangeUnobstructed.cs","Content.IntegrationTests/Tests/Interaction/InteractionTest.cs","Content.IntegrationTests/Tests/Networking/SimplePredictReconcileTest.cs","Content.IntegrationTests/Tests/PostMapInitTest.cs","Content.IntegrationTests/Tests/Power/PowerTest.cs","Content.IntegrationTests/Tests/SaveLoadMapTest.cs","Content.IntegrationTests/Tests/SaveLoadSaveTest.cs","Content.IntegrationTests/Tests/Shuttle/DockTest.cs","Content.IntegrationTests/Tests/ShuttleTest.cs","Content.IntegrationTests/Tests/VendingMachineRestockTest.cs","Content.MapRenderer/Painters/GridPainter.cs","Content.MapRenderer/Painters/MapPainter.cs","Content.IntegrationTests/Tests/Imperial/EngineCompatibility/EntityTimerMigrationTest.cs"],"verification":{"local":"git diff --check; no IMapManager or invalid IoC resolution of SharedMapSystem in integration tests/map renderer; new test prototype YAML parsed","ci_before":"40553078967:in_progress; 40552604331:in_progress at previous source commit","ci_after":"pending; tests not run yet"},"tests_added":["EntityTimerMigrationTest normal deadlines","pause/unpause retains remaining duration and ghost cooldown","deletion with pending timers does not affect another entity"],"notes":["23 integration files and 2 map renderer files resolve SharedMapSystem through entity systems instead of retired IMapManager service; map creation/lookup calls reviewed against pinned engine","SaveLoadReparentTest creates grid via CreateGridEntity"],"next":["fetch CI for latest HEAD; fix any newly exposed test/tool compiler errors","run focused new timer tests plus PowerTest and round restart tests","remaining full suite/startup/prototype and reanchor verification"],"invariant":"EVERY Experiment code commit includes todo.md atomically"}

checkpoint.v10={"base":"17b67c558beb9872faaec0022242fb25450003e1","task":"docking_reanchor_semantics","files":["Content.Server/Shuttles/Systems/DockingSystem.cs","Content.IntegrationTests/Tests/Imperial/EngineCompatibility/DockingMigrationTest.cs"],"verification":{"local":"git diff --check; OnDockingMove ported from stable upstream 107b6552 DockingSystem; existing engine MoveEvent signature verified","ci":"40553078967 at b203f211 cleared C# diagnostics and failed in client XAML (12 XAML/MSBuild errors across 5 UI files); 40553660186 still running","tests":"new moved-anchored-port regression not run yet"},"anchor_review":{"node_container":"same AnchorStateChangedEvent and rotation MoveEvent handling as stable","extension_cable":"same anchor connect/disconnect dispatch as stable","radiation_blockers":"same anchor add/remove dispatch as stable","docking":"missing stable MoveEvent replacement for removed ReAnchorEvent restored"},"next":["fix NanoTaskItemControl, FactionMenuEntry, LatheMenu, QueuedRecipeControl, NewsArticleCard XAML style syntax","rerun full build to expose remaining dependent project errors","run timers/docking/power/restart regression tests; no passing build claimed"],"invariant":"EVERY Experiment code commit includes todo.md atomically"}

checkpoint.v11={"base":"e22fbcdee2bbe9aec0034d970c7a31a9b530bc93","task":"xaml_style_class_migration","files":["Content.Client/CartridgeLoader/Cartridges/NanoTaskItemControl.xaml","Content.Client/Imperial/Medieval/Factions/UI/Elements/FactionMenuEntry.xaml","Content.Client/Lathe/UI/LatheMenu.xaml","Content.Client/Lathe/UI/QueuedRecipeControl.xaml","Content.Client/MassMedia/Ui/NewsArticleCard.xaml"],"verification":{"local":"git diff --check; all five XML files parsed; named controls preserved; style-class lists/order compared to prior XAML","reference":"stable upstream NanoTaskItemControl uses StyleClasses attribute; pinned OptionButton.OptionStyleClasses is read-only ICollection<string>, populated by collection property element","ci_baseline":"40553078967 at b203f211: production C# cleared; failed XAML with 12 error lines across 5 files","ci_after":"pending; newest successful full build/test run does not yet exist"},"next":["fetch resulting CI run; inspect XAML and dependent test/tool project diagnostics","run focused entity timer/docking/power/restart regressions when build clean","full unit/integration/startup/prototype validation; baseline missing assets remain separate"],"invariant":"EVERY Experiment code commit includes todo.md atomically"}

checkpoint.v12={"base":"b4b958ab7132aff8983f6f4270927640dca47a02","task":"review_test_compile_and_suite_execution","files":[".github/workflows/experiment-engine-compat.yml","Content.IntegrationTests/Tests/Chemistry/TryAllReactionsTest.cs","Content.IntegrationTests/Tests/GravityGridTest.cs","Content.IntegrationTests/Tests/MaterialArbitrageTest.cs","Content.IntegrationTests/Tests/Projectiles/ProjectileExplosionWallsTest.cs","Content.IntegrationTests/Tests/PrototypeSaveTest.cs","Content.IntegrationTests/Tests/Toolshed/AdminTest.cs","Content.IntegrationTests/Tests/Toolshed/LocTest.cs"],"ci_baseline":{"run":40554475627,"job":122366714003,"result":"failed build:9 integration-test compiler diagnostics;unit+integration skipped;production C# and XAML compiled"},"fixes":["test Toolshed removed engine fixture reference (stable upstream parity)","serialization context provider receives serialization manager","reagent/material dictionary test consumers use string ID keys","missed integration-instance MapMan properties migrated incl projectile walls"],"workflow":["both unfiltered test suites execute after successful build even if other suite fails","TRX results retained as experiment-test-results artifact"],"verification":"git diff --check;workflow YAML+Experiment-only trigger parsed;no obsolete IMapManager/.MapMan/engine Toolshed fixture refs in test/tool projects;new CI pending","coverage_review_pending":["TEG/Myrmex directional node connectivity absent","currency stack ingestion and rejection absent","round restart test lacks explicit map-registry/recreation assertions"],"next":["add missing behavior regressions and audit existing suites","fetch new CI and repair remaining blockers without skipping tests"],"invariant":"EVERY Experiment code commit includes todo.md atomically"}
