using System;

public static class Github
{
    public static bool IsDependabotPullRequest()
    {
        var githubActor = Environment.GetEnvironmentVariable("GITHUB_ACTOR");
        return githubActor != null && githubActor.Equals("dependabot[bot]", StringComparison.OrdinalIgnoreCase);
    }
}