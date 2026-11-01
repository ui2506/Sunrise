Build with the .NET 10 SDK and .NET Framework 4.8 targeting pack.
Set the `SL_REFERENCES` environment variable to the dedicated server's `SCPSL_Data/Managed`
directory, then run `dotnet build Sunrise.sln -c Release`. LabAPI, Harmony and YamlDotNet restore through NuGet.
The publicized game/Mirror assemblies live in `_ProjectDependencies`; keep them consistent with
the server assemblies. Other DLL references use `SL_REFERENCES`.
Builds do not automatically copy files to a running server.

To add the variable use this guide: [How to set an environment variable](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/How-to-set-an-environment-variable.html).
To find the `SCP Secret Laboratory Dedicated Server` directory you can use steam: [How to access game files](https://steamcommunity.com/sharedfiles/filedetails/?id=760447682).

We will be actively reviewing pull requests, but it’s better to discuss changes before starting to work on them.
If you are willing to contribute, or just like the project, feel free to join the [Discord server](https://discord.gg/9nAaRVNCq3).
