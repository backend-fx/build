using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Publish);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    readonly string NugetApiKey = Environment.GetEnvironmentVariable("NUGET_APIKEY");

    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;
    [GitVersion(NoFetch = true)] readonly GitVersion GitVersion;

    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    Target Clean => nuke => nuke
        .Before(Restore)
        .Executes(() =>
        {
            RootDirectory.GlobDirectories("**/bin", "**/obj").ForEach(dir => dir.DeleteDirectory());
            ArtifactsDirectory.CreateOrCleanDirectory();
        });

    Target Restore => nuke => nuke.Executes(() => DotNetRestore(s => s.SetProjectFile(Solution)));

    Target Compile => nuke => nuke
        .DependsOn(Clean)
        .DependsOn(Restore)
        .Executes(() =>
        {
            Log.Information("Version {Version}", GitVersion?.SemVer ?? "no version!");
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion)
                .EnableNoRestore());
        });

    Target Test => nuke => nuke
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        })
        .ProceedAfterFailure();

    Target Pack => nuke => nuke
        .DependsOn(Test)
        .Executes(() =>
        {
            var projectFiles = RootDirectory.GlobFiles("**/*.csproj").Where(p => !p.NameWithoutExtension.EndsWith("Tests"));
            foreach (var projectFile in projectFiles)
            {
                var version = GitVersion.MajorMinorPatch;
                if (int.TryParse(GitVersion.CommitsSinceVersionSource, out int commits) && commits > 0)
                {
                    version = $"{version}-beta{GitVersion.CommitsSinceVersionSourcePadded}";
                }
                
                Log.Information("Version: " + version);
                DotNetPack(s => s
                    .SetProject(projectFile)
                    .SetOutputDirectory(ArtifactsDirectory)
                    .SetVersion(version)
                    .SetVerbosity(DotNetVerbosity.minimal)
                    .SetConfiguration(Configuration));
            }
        });

    Target Publish => nuke => nuke
        .OnlyWhenDynamic(() => !IsLocalBuild && !Github.IsDependabotPullRequest() && Configuration.Equals(Configuration.Release))
        .DependsOn(Pack)
        .Executes(() =>
        {
            // ReSharper disable once StringLiteralTypo
            foreach (var nugetPackage in ArtifactsDirectory.GlobFiles("*.nupkg"))
            {
                DotNetNuGetPush(s =>
                {
                    s = s
                        .SetTargetPath(nugetPackage)
                        .EnableNoServiceEndpoint()
                        .EnableSkipDuplicate()
                        .SetSource("https://api.nuget.org/v3/index.json")
                        .SetApiKey(NugetApiKey);

                    return s;
                });
            }
        });
}
