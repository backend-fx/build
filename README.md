# Backend.Fx Build Template

This repository provides a reusable Cake build setup for Backend.Fx projects. It can be integrated as a git submodule and covers the common build lifecycle: clean, restore, compile, test, pack, and publish.

## Features

- **Clean:** Removes `bin`/`obj` folders and cleans the `artifacts` directory.
- **GetVersion:** Resolves and prints version information from GitVersion.
- **Restore:** Restores NuGet dependencies.
- **Compile:** Builds the solution with CI/versioning MSBuild properties.
- **Test:** Executes `dotnet test` and writes TRX reports to `artifacts/test-results`.
- **Pack:** Creates NuGet packages for all non-test projects.
- **Publish:** Pushes created packages to NuGet.org.

## Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download)

No global Cake installation is required. The wrapper script restores local tools and runs Cake.

## Usage

Run the default target (`Publish`) via the wrapper:

```bash
./build.sh
```

Run a specific target:

```bash
./build.sh --target=Pack
./build.sh --target=Test
```

Specify solution path/pattern explicitly if auto-discovery is not sufficient:

```bash
./build.sh --solution="../MyProject.sln"
```

## Parameters

- `target`: Cake target to run. Default: `Publish`.
- `configuration`: Build configuration. Default is `Debug` locally and `Release` in CI.
- `solution`: Optional solution file path/pattern. If omitted, the script searches for `../*.slnx` or `../*.sln`.

## Environment Variables

- `CI` or `GITHUB_ACTIONS`: Enables CI behavior.
- `NUGET_APIKEY`: Required for publishing packages.

## Publish Behavior

- `Publish` runs only when `CI`/`GITHUB_ACTIONS` is set and configuration is `Release`.
- Packages are pushed to `https://api.nuget.org/v3/index.json` with duplicate uploads skipped.

## License

This project is licensed under the MIT License. See [LICENSE.md](LICENSE.md).

## Acknowledgments

- [Cake](https://cakebuild.net/) - Cross-platform build automation for .NET.
- [GitVersion](https://gitversion.net/) - Semantic versioning from Git metadata.
