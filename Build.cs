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

    readonly string MygetApiKey = Environment.GetEnvironmentVariable("MYGET_APIKEY");

    readonly string MygetFeedUrl = 
        Environment.GetEnvironmentVariable("MYGET_FEED_URL") ?? "https://www.myget.org/F/marcwittke/api/v3/index.json";

    readonly string NugetApiKey = Environment.GetEnvironmentVariable("NUGET_APIKEY");

    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;
    [GitVersion(Framework = "net6.0", NoFetch = true)] readonly GitVersion GitVersion;

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
            var csprojs = RootDirectory.GlobFiles("**/*.csproj").Where(p => !p.NameWithoutExtension.EndsWith("Tests"));
            foreach (var csproj in csprojs)
            {
                DotNetPack(s => s
                    .SetProject(csproj)
                    .SetOutputDirectory(ArtifactsDirectory)
                    .SetVersion(GitVersion.NuGetVersion)
                    .SetVerbosity(DotNetVerbosity.minimal)
                    .SetConfiguration(Configuration));
            }
        });

    Target Publish => nuke => nuke
        .OnlyWhenDynamic(() => !IsLocalBuild && !Github.IsDependabotPullRequest() && Configuration.Equals(Configuration.Release))
        .DependsOn(Pack)
        .Executes(() =>
        {
            bool pushToNuget = GitRepository.Branch == "main";

            foreach (var nupkg in ArtifactsDirectory.GlobFiles("*.nupkg"))
            {
                DotNetNuGetPush(s =>
                {
                    s = s
                        .SetTargetPath(nupkg)
                        .EnableNoServiceEndpoint()
                        .EnableSkipDuplicate();

                    if (pushToNuget)
                    {
                        s = s.SetSource("https://api.nuget.org/v3/index.json")
                            .SetApiKey(NugetApiKey);
                    }
                    else
                    {
                        s = s.SetSource(MygetFeedUrl)
                            .SetApiKey(MygetApiKey);
                    }

                    return s;
                });
            }
        });
}