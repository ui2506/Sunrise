# Contributing

Thank you for your interest in contributing to this project! Please read the following guidelines before submitting a pull request.

## Development Setup

### 1. Configure Dependencies

If you have missing dependencies, you need to create the `SL_REFERENCES` environment variable pointing to:

```text
...\steamapps\common\SCP Secret Laboratory Dedicated Server\SCPSL_Data\Managed
```

Useful guides:
- [How to set an environment variable](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/How-to-set-an-environment-variable.html)
- [How to access game files through Steam](https://steamcommunity.com/sharedfiles/filedetails/?id=760447682)

### 2. Publicize Assemblies

Before building the project, use a publicizer to create publicized copies of the following assemblies from your Dedicated Server installation:

- `Assembly-CSharp.dll` → `Assembly-CSharp-Publicized.dll`
- `Mirror.dll` → `Mirror-Publicized.dll`

Place both publicized assemblies in the directory specified by `SL_REFERENCES`.

**Important:**
- Keep the original DLL files unchanged.
- Regenerate the publicized assemblies whenever the server assemblies are updated.

## Pull Requests

All pull requests **must target the `dev` branch**, not `master`.

Before submitting a pull request:
1. Make sure your changes build successfully.
2. Keep your changes focused and relevant to the project.
3. Provide a clear description of what your pull request changes.
4. Set `dev` as the base branch of your pull request.

We actively review pull requests, but we recommend discussing significant changes before starting work to avoid unnecessary effort or conflicts.

## Community

If you want to contribute, discuss changes, ask questions, or simply follow the project, feel free to join our [Discord server](https://discord.gg/3c8aYS6nAV).
