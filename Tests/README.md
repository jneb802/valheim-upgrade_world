# Quadrant filter checks

These checks use the Valheim assemblies without starting the game. They verify quadrant boundaries, accepted names, invalid values, and multi-quadrant zone selection.

```sh
dotnet build Tests/UpgradeWorld.Tests.csproj
MONO_PATH="$HOME/.nuget/packages/jotunnlib/2.30.0/build" mono Tests/bin/Debug/net48/UpgradeWorld.Tests.exe
```

The feature still requires an in-game test on a disposable copy of the production world before use on production.
