#tool "dotnet:?package=GitVersion.Tool&version=6.2.0"

var target = Argument<string>("target", "Publish");
var isRunningInCI = !string.IsNullOrEmpty(EnvironmentVariable("CI")) || !string.IsNullOrEmpty(EnvironmentVariable("GITHUB_ACTIONS"));
var configuration = Argument<string>("configuration", isRunningInCI ? "Release" : "Debug");
var nugetApiKey = EnvironmentVariable("NUGET_APIKEY");

var artifactsDirectory = new DirectoryPath("../artifacts");
var testResultsDirectory = artifactsDirectory.Combine("test-results");
var solutionPath = Argument<string>("solution", null);
var solutionFile = (solutionPath != null
    ? GetFiles(solutionPath).FirstOrDefault()
    : GetFiles("../*.slnx").Concat(GetFiles("../*.sln")).FirstOrDefault()) ?? throw new Exception("No solution file found. Please specify the solution file path using the --solution argument.");
var solutionDirectory = MakeAbsolute(solutionFile.GetDirectory());
var gitVersionInfo = new Lazy<GitVersionInfo>(() =>
{
    var resolvedVersion = GitVersion(new GitVersionSettings
    {
        NoFetch = true,
        WorkingDirectory = solutionDirectory
    });
    return new GitVersionInfo(
        resolvedVersion.SemVer,
        resolvedVersion.AssemblySemVer,
        resolvedVersion.AssemblySemFileVer,
        resolvedVersion.InformationalVersion,
        resolvedVersion.MajorMinorPatch,
        resolvedVersion.CommitsSinceVersionSource ?? 0,
        resolvedVersion.CommitsSinceVersionSourcePadded);
});

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
        Information("Version: {0}", gitVersionInfo.Value.SemVer);
    });

Task("Restore")
    .IsDependentOn("Clean")
    .Does(() =>
    {
        DotNetRestore(solutionFile.FullPath);
    });

Task("Compile")
    .IsDependentOn("Restore")
    .Does(() =>
    {
        DotNetBuild(solutionFile.FullPath, new DotNetBuildSettings
        {
            Configuration = configuration,
            NoRestore = true,
            MSBuildSettings = new DotNetMSBuildSettings()
                .WithProperty("ContinuousIntegrationBuild", isRunningInCI ? "true" : "false")
                .WithProperty("AssemblyVersion", gitVersionInfo.Value.AssemblySemVer)
                .WithProperty("FileVersion", gitVersionInfo.Value.AssemblySemFileVer)
                .WithProperty("InformationalVersion", gitVersionInfo.Value.InformationalVersion)
        });
    });

Task("Test")
    .IsDependentOn("Compile")
    .ContinueOnError()
    .Does(() =>
    {
        EnsureDirectoryExists(testResultsDirectory);
        GetFiles(solutionDirectory.FullPath + "/**/*Tests.csproj")
            .AsParallel()
            .ForAll(testProjectFile =>
            {
                var logFileName = $"{testProjectFile.GetFilenameWithoutExtension()}.trx";
                Information("Testing {0} -> {1}", testProjectFile, testResultsDirectory.CombineWithFilePath(logFileName));

                DotNetTest(testProjectFile.FullPath, new DotNetTestSettings
                {
                    Configuration = configuration,
                    NoRestore = true,
                    NoBuild = true,
                    ResultsDirectory = testResultsDirectory,
                    Loggers = new[] { $"trx;LogFileName={logFileName}" }
                });
            });
    });

Task("Pack")
    .IsDependentOn("Test")
    .Does(() =>
    {
        var projectFiles = GetFiles(solutionDirectory.FullPath + "/**/*.csproj")
            .Where(p => !p.GetFilename().ToString().EndsWith("Tests.csproj"));

        foreach (var projectFile in projectFiles)
        {
            var packVersion = gitVersionInfo.Value.MajorMinorPatch;

            if (gitVersionInfo.Value.CommitsSinceVersionSource > 0)
            {
                packVersion = $"{packVersion}-beta{gitVersionInfo.Value.CommitsSinceVersionSourcePadded}";
            }

            Information("Packing {0} as version {1}", projectFile, packVersion);
            DotNetPack(projectFile.FullPath, new DotNetPackSettings
            {
                Configuration = configuration,
                OutputDirectory = artifactsDirectory,
                VersionSuffix = null,
                NoBuild = true,
                NoRestore = true,
                MSBuildSettings = new DotNetMSBuildSettings().WithProperty("Version", packVersion)
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

record struct GitVersionInfo(
    string SemVer,
    string AssemblySemVer, 
    string AssemblySemFileVer, 
    string InformationalVersion, 
    string MajorMinorPatch, 
    int CommitsSinceVersionSource, 
    string CommitsSinceVersionSourcePadded);