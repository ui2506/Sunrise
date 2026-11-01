If you have missing dependencies you need to add the `SL_REFERENCES` environment variable
pointing to `...\steamapps\common\SCP Secret Laboratory Dedicated Server\SCPSL_Data\Managed`.

Before building, use a publicizer to create publicized copies of `Assembly-CSharp.dll` and `Mirror.dll`
from your Dedicated Server installation. Place them in the directory pointed to by `SL_REFERENCES`
as `Assembly-CSharp-Publicized.dll` and `Mirror-Publicized.dll`. Keep the original DLLs unchanged
and regenerate the publicized copies whenever the server assemblies are updated.

To add the variable use this guide: [How to set an environment variable](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/How-to-set-an-environment-variable.html).
To find the `SCP Secret Laboratory Dedicated Server` directory you can use steam: [How to access game files](https://steamcommunity.com/sharedfiles/filedetails/?id=760447682).

We will be actively reviewing pull requests, but it’s better to discuss changes before starting to work on them.
If you are willing to contribute, or just like the project, feel free to join the [Discord server](https://discord.gg/9nAaRVNCq3).
