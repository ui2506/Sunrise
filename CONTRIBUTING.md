Build with the .NET 10 SDK and .NET Framework 4.8 targeting pack. Restore `Sunrise/packages.config`
with `nuget restore Sunrise.sln`, then run `dotnet build Sunrise.sln -c Release`.
Game references live in `_ProjectDependencies`; keep them consistent with the dedicated server's
`SCPSL_Data/Managed` assemblies, including `Pooling.dll`, LabAPI and the publicized game/Mirror assemblies.
Builds do not automatically copy files to a running server.

Run `dotnet run --project Tests/VisibilityChecks.csproj` for visibility and module lifecycle checks.
These run the production event handler against controlled engine/API boundaries. Test on a LabAPI server
before deployment: room transitions and raycasts, SCP-049/096/939 visibility, flashlights and item sounds,
landing grace, item/ammo/candy pickups through walls, door buttons, SCP-3114 and cuffed item use,
backtracked shots, Tesla damage, round restart and plugin disable/re-enable.

We will be actively reviewing pull requests, but it’s better to discuss changes before starting to work on them.
If you are willing to contribute, or just like the project, feel free to join the [Discord server](https://discord.gg/9nAaRVNCq3).