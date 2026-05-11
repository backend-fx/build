#tool "dotnet:?package=GitVersion.Tool&version=6.2.0"

var target = Argument("target", "Publish");
var isRunningInCI = !string.IsNullOrEmpty(EnvironmentVariable("CI")) || !string.IsNullOrEmpty(EnvironmentVariable("GITHUB_ACTIONS"));
var configuration = Argument("configuration", isRunningInCI ? "Release" : "Debug");
var nugetApiKey = EnvironmentVariable("NUGET_APIKEY");

var artifactsDirectory = Directory("artifacts");
var solutionPath = Argument("solution", null as string);
var solutionFile = solutionPath != null 
    ? GetFiles(solutionPath).FirstOrDefault()
    : GetFiles("../*.slnx").Concat(GetFiles("../*.sln")).FirstOrDefault();
var version = "";

if (solutionFile == null)
{
    throw new Exception("No solution file found. Please specify the solution file path using the --solution argument.");
}

Task("Clean")
    .Does(() =>
    {
        DeleteDirectories(GetDirectories("**/bin"), new DeleteDirectorySettings { Recursive = true, Force = true });
        DeleteDirectories(GetDirectories("**/obj"), new DeleteDirectorySettings { Recursive = true, Force = true });
        EnsureDirectoryExists(artifactsDirectory);
        CleanDirectory(artifactsDirectory);
    });

Task("GetVersion")
    .Does(() =>
    {
        var gitVersion = GitVersion(new GitVersionSettings { NoFetch = true });
        version = gitVersion.SemVer;
        Information("Version: {0}", version);
    });

Task("Restore")
    .IsDependentOn("Clean")
    .Does(() =>
    {
        DotNetRestore(solutionFile.FullPath);
    });

Task("Compile")
    .IsDependentOn("Restore")
    .IsDependentOn("GetVersion")
    .Does(() =>
    {
        var gitVersion = GitVersion(new GitVersionSettings { NoFetch = true });
        DotNetBuild(solutionFile.FullPath, new DotNetBuildSettings
        {
            Configuration = configuration,
            NoRestore = true,
            MSBuildSettings = new DotNetMSBuildSettings()
                .WithProperty("AssemblyVersion", gitVersion.AssemblySemVer)
                .WithProperty("FileVersion", gitVersion.AssemblySemFileVer)
                .WithProperty("InformationalVersion", gitVersion.InformationalVersion)
        });
    });

Task("Test")
    .IsDependentOn("Compile")
    .ContinueOnError()
    .Does(() =>
    {
        DotNetTest(solutionFile.FullPath, new DotNetTestSettings
        {
            Configuration = configuration,
            NoRestore = true,
            NoBuild = true
        });
    });

Task("Pack")
    .IsDependentOn("Test")
    .Does(() =>
    {
        var gitVersion = GitVersion(new GitVersionSettings { NoFetch = true });
        var projectFiles = GetFiles("**/*.csproj")
            .Where(p => !p.GetFilename().ToString().EndsWith("Tests.csproj"));

        foreach (var projectFile in projectFiles)
        {
            var packVersion = gitVersion.MajorMinorPatch;
            if (gitVersion.CommitsSinceVersionSource > 0)
            {
                packVersion = $"{packVersion}-beta{gitVersion.CommitsSinceVersionSourcePadded}";
            }

            Information("Packing {0} as version {1}", projectFile, packVersion);
            DotNetPack(projectFile.FullPath, new DotNetPackSettings
            {
                Configuration = configuration,
                OutputDirectory = artifactsDirectory,
                VersionSuffix = null,
                NoBuild = true,
                NoRestore = true,
                MSBuildSettings = new DotNetMSBuildSettings()
                    .WithProperty("Version", packVersion)
            });
        }
    });

Task("Publish")
    .IsDependentOn("Pack")
    .WithCriteria(() => isRunningInCI && configuration == "Release")
    .Does(() =>
    {
        var packages = GetFiles(artifactsDirectory.ToString() + "/*.nupkg");
        foreach (var package in packages)
        {
            Information("Publishing {0}", package);
            DotNetNuGetPush(package.FullPath, new DotNetNuGetPushSettings
            {
                Source = "https://api.nuget.org/v3/index.json",
                ApiKey = nugetApiKey,
                SkipDuplicate = true
            });
        }
    });

RunTarget(target);