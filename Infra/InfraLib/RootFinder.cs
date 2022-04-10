namespace InfraLib
{
    public static class RootFinder
    {
        public static string GetRepoRoot()
        {
            string? repoRoot = Path.GetDirectoryName(typeof(RootFinder).Assembly.Location);
            while (repoRoot is not null && !File.Exists(Path.Combine(repoRoot, "Pulumi.yaml")))
            {
                repoRoot = Path.GetDirectoryName(repoRoot);
            }
            if (repoRoot == null)
                throw new Exception("Could not find Pulumi.yaml");
            return repoRoot;
        }
    }
}
