# Backend.Fx Build Template

This project uses the Nuke build automation system to manage the build, test, and publish workflows for a .NET solution. The build script is written in C# and leverages Nuke tasks to handle various aspects of the development lifecycle.

## Features

- **Clean:** Deletes build artifacts and directories.
- **Restore:** Restores NuGet packages for the solution.
- **Compile:** Builds the project with version information from Git.
- **Test:** Runs unit tests for the project.
- **Pack:** Creates NuGet packages for the project.
- **Publish:** Pushes NuGet packages to either NuGet.org (`main` branch) or myget.org.

## Usage

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) installed
- (Optional) [Nuke](https://nuke.build/) installed globally for command-line execution

### Build and Publish

1. Clone the repository.
2. Set the necessary environment variables for API keys and feed URLs.
3. Run the build script:

   ```bash
   ./build.sh
   ```

   or using Nuke:

   ```bash
   nuke Publish
   ```

## Configuration

- The build configuration (Debug/Release) is set via the `Configuration` parameter.
- API keys and feed URLs are configured through environment variables.

## License

This project is licensed under the [LICENSE NAME] - see the [LICENSE.md](LICENSE.md) file for details.

## Acknowledgments

- [Nuke](https://nuke.build/) - Build automation system for C#/.NET projects.
- [GitVersion](https://gitversion.net/) - Semantic versioning for Git.
