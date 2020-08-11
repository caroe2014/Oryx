﻿using System;
using System.IO;
using System.Net.Http;
using McMaster.Extensions.CommandLineUtils;

namespace AutoUpdater
{
    [Command("digest", Description = "")]
    public class GitHubRunnersCachedImagesCheckCommand
    {
        public int OnExecute(CommandLineApplication app, IConsole console)
        {
            Console.WriteLine("foo");
            var gitHubRunnersReadMeDigest = GetDigestFromGitHubRunnersReadMe();
            var oryxDockerfileDigest = GetDigestFromOryxGitHubRunnersDockerfile();
            if (!string.Equals(gitHubRunnersReadMeDigest, oryxDockerfileDigest, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(gitHubRunnersReadMeDigest))
                {
                    var newContentInDockerfile = $"FROM buildpack-deps:stretch@sha256:{gitHubRunnersReadMeDigest}";
                    var dockerFileLocation = "images/build/Dockerfiles/gitHubRunners.BuildPackDepsStretch.Dockerfile";
                    var newBranchName = "autoupdater/update.githubrunners.digest";
                    var tempDir = Path.GetTempPath();
                    var script = @$"
#!/bin/bash
set -ex
cd {tempDir}
git clone https://github.com/microsoft/oryx --depth 1
git checkout -b {newBranchName}
echo '{newContentInDockerfile}' > {dockerFileLocation}
git add {dockerFileLocation}
git commit -m 'Updated GitHub runners digest sha'
git push -u origin {newBranchName}
curl \
  -X POST \
  -H 'Accept: application/vnd.github.v3+json' \
  https://api.github.com/repos/microsoft/oryx/pulls \
  -d '{{""title"":""Updated GitHub runners digest"",""head"":""{{{newBranchName}}}"",""base"":""master""}}'
";

                    var scriptPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sh");
                    Console.WriteLine($"Script generated at {scriptPath}");
                    File.WriteAllText(scriptPath, script);

                    Console.WriteLine("Setting executable permission...");
                    var exitCode = ProcessHelper.TrySetExecutableMode(scriptPath);
                    if (exitCode != 0)
                    {
                        Console.WriteLine("Error setting executable permission on the script");
                        return exitCode;
                    }

                    Console.WriteLine("Running the script...");
                    exitCode = ProcessHelper.RunProcess(
                        scriptPath,
                        arguments: null,
                        workingDirectory: null,
                        // Preserve the output structure and use AppendLine as these handlers
                        // are called for each line that is written to the output.
                        standardOutputHandler: (sender, args) =>
                        {
                            Console.WriteLine(args.Data);
                        },
                        standardErrorHandler: (sender, args) =>
                        {
                            Console.Error.WriteLine(args.Data);
                        },
                        waitTimeForExit: TimeSpan.FromMinutes(15));

                    Console.WriteLine("Done.");
                    return exitCode;
                }
            }

            return 0;
        }

        private string GetDigestFromGitHubRunnersReadMe()
        {
            var gitHubRunnersReadMeFileUrl = "https://raw.githubusercontent.com/actions/virtual-environments/main/images/linux/Ubuntu2004-README.md";
            var httpClient = new HttpClient();
            var content = httpClient.GetStringAsync(gitHubRunnersReadMeFileUrl).Result;
            var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Contains("buildpack-deps:stretch", StringComparison.OrdinalIgnoreCase))
                {
                    var prefix = "sha256:";
                    var startIndex = line.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
                    // sha256 is 64 characters long
                    return line.Substring(startIndex + prefix.Length, 64);
                }
            }

            return null;
        }

        private string GetDigestFromOryxGitHubRunnersDockerfile()
        {
            var oryxGitHubRunnersDockerfileUrl = "https://raw.githubusercontent.com/microsoft/Oryx/master/images/build/Dockerfiles/gitHubRunners.BuildPackDepsStretch.Dockerfile";
            var httpClient = new HttpClient();
            var content = httpClient.GetStringAsync(oryxGitHubRunnersDockerfileUrl).Result;
            var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Contains("buildpack-deps:stretch", StringComparison.OrdinalIgnoreCase))
                {
                    var prefix = "sha256:";
                    var startIndex = line.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
                    // sha256 is 64 characters long
                    return line.Substring(startIndex + prefix.Length, 64);
                }
            }

            return null;
        }
    }
}
