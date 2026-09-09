# Compatibility checks

These tests use the actual Valheim managed assemblies without launching Unity or Valheim. They cover inventory versions 100–109, exact preservation of retained item bytes, truncated and unsupported input, coordinate bounds, location lookup tables, and alternate-biome placement rules.

Put the publicized 1.0.7 game assemblies and BepInEx references in the existing sibling `Libs` directory. Do not mix game versions. The tests also need the matching `UnityEngine.PhysicsModule.dll` from the game install.

```sh
dotnet build Tests/UpgradeWorld.Tests.csproj
mono Tests/bin/Debug/net48/UpgradeWorld.Tests.exe
```

The test project defaults to the standard macOS managed-assembly path. On another install, pass `-p:ValheimManagedPath=/path/to/Managed` to the build. If your publicized assemblies carry Jotunn build attributes, set `MONO_PATH` to the directory containing `JotunnBuildTask.dll`. Do not add the game's whole Managed directory to `MONO_PATH`; its system libraries are specific to Unity.

The mod build no longer copies a DLL into the shared `Libs` directory unless explicitly requested with `-p:CopyToLibs=true`.

## Pending in-game validation

The user requested code and build work without launching the game. No live compatibility claim is made from these tests.

Use disposable worlds and development characters for these checks:

- Load the plugin and verify all Harmony patches and commands register.
- Search and clean chests with current and legacy inventories. Retain valid items and custom item data. Leave malformed inventories unchanged.
- Clear a field on an already server-owned object in an otherwise unchanged chunk. Save, restart, and verify the field remains absent. Repeat for a portal.
- Clone, move, and swap portals. Check zone lookup, connections, and persistence after a restart.
- Remove and redistribute locations in one session. Check minimum/maximum group distances, alternate-biome restrictions, and base protection.
- Load a legacy world with automatic location generation disabled. Check that generation remains disabled and completion callbacks run.
- Change world-generation version. Check biome sectors and terrain. Test both saving and leaving without saving; the next load must rebuild biome data for the saved world version.
- Test with and without a compatible Location Placement Accelerator. Older API signatures fall back to the built-in allocator.
- Test synchronous and asynchronous saves and `save_disable`/`save_enable`. Inspect the full relevant log interval for new errors.

The optional save-telemetry branch is reviewed and updated separately from the main compatibility changes.
