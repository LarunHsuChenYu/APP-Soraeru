using Shouldly;

namespace Soraeru.Curator.Tests.Deployment;

public sealed class CuratorContainerStartupTests
{
    [Fact]
    public void Entrypoint_guards_chown_and_drops_root_privileges()
    {
        var script = File.ReadAllText(RepoFile("src", "Soraeru.Curator", "curator-entrypoint.sh"));

        script.ShouldContain("keys_path=\"${DataProtection__KeysPath:-}\"");
        script.ShouldContain("if [ -n \"$keys_path\" ]; then");
        script.ShouldContain("case \"$keys_path\" in");
        script.ShouldContain("if [ \"$keys_path\" = \"/\" ]; then");
        script.ShouldContain("resolved_keys_path=\"$(readlink -f -- \"$keys_path\")\"");
        script.ShouldContain("if [ -z \"$resolved_keys_path\" ] || [ \"$resolved_keys_path\" = \"/\" ]; then");
        script.ShouldContain("chown app:app -- \"$resolved_keys_path\"");
        script.ShouldContain("exec gosu app \"$@\"");

        script.IndexOf("if [ -n \"$keys_path\" ]; then", StringComparison.Ordinal)
            .ShouldBeLessThan(script.IndexOf("chown app:app", StringComparison.Ordinal));
        script.IndexOf("if [ -z \"$resolved_keys_path\" ] || [ \"$resolved_keys_path\" = \"/\" ]; then", StringComparison.Ordinal)
            .ShouldBeLessThan(script.IndexOf("chown app:app", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("src/Soraeru.Curator/Dockerfile")]
    [InlineData("Dockerfile.curator")]
    public void Curator_images_install_gosu_and_use_the_privilege_dropping_entrypoint(string relativePath)
    {
        var dockerfile = File.ReadAllText(RepoFile(relativePath.Split('/')));

        dockerfile.ShouldContain("apt-get install -y --no-install-recommends gosu");
        dockerfile.ShouldContain("sed -i 's/\\r$//'");
        dockerfile.ShouldContain("ENTRYPOINT [\"/usr/local/bin/curator-entrypoint.sh\"]");
        dockerfile.ShouldContain("CMD [\"dotnet\", \"Soraeru.Curator.dll\"]");
        dockerfile.ShouldNotContain("USER $APP_UID");
    }

    private static string RepoFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Soraeru.slnx")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull("Could not locate the repository root from the test output directory.");
        return Path.Combine([directory.FullName, .. segments]);
    }
}
