// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Oryx.BuildScriptGenerator.Common;
using Microsoft.Oryx.BuildScriptGenerator.Exceptions;
using Microsoft.Oryx.BuildScriptGenerator.SourceRepo;
using Microsoft.Oryx.Common.Extensions;
using Microsoft.Oryx.Detector;
using Microsoft.Oryx.Detector.Php;

namespace Microsoft.Oryx.BuildScriptGenerator.Php
{
    /// <summary>
    /// PHP platform.
    /// </summary>
    internal class PhpPlatform : IProgrammingPlatform
    {
        private readonly PhpScriptGeneratorOptions _phpScriptGeneratorOptions;
        private readonly BuildScriptGeneratorOptions _commonOptions;
        private readonly IPhpVersionProvider _phpVersionProvider;
        private readonly IPhpComposerVersionProvider _phpComposerVersionProvider;
        private readonly ILogger<PhpPlatform> _logger;
        private readonly IPhpPlatformDetector _detector;
        private readonly PhpPlatformInstaller _phpInstaller;
        private readonly PhpComposerInstaller _phpComposerInstaller;

        /// <summary>
        /// Initializes a new instance of the <see cref="PhpPlatform"/> class.
        /// </summary>
        /// <param name="phpScriptGeneratorOptions">The options of phpScriptGenerator.</param>
        /// <param name="phpVersionProvider">The PHP version provider.</param>
        /// <param name="logger">The logger of PHP platform.</param>
        /// <param name="detector">The detector of PHP platform.</param>
        /// <param name="commonOptions">The <see cref="BuildScriptGeneratorOptions"/>.</param>
        /// <param name="phpComposerInstaller">The <see cref="PhpComposerInstaller"/>.</param>
        /// <param name="phpInstaller">The <see cref="PhpPlatformInstaller"/>.</param>
        /// <param name="phpComposerVersionProvider">The <see cref="IPhpComposerVersionProvider"/>.</param>
        public PhpPlatform(
            IOptions<PhpScriptGeneratorOptions> phpScriptGeneratorOptions,
            IOptions<BuildScriptGeneratorOptions> commonOptions,
            IPhpVersionProvider phpVersionProvider,
            IPhpComposerVersionProvider phpComposerVersionProvider,
            ILogger<PhpPlatform> logger,
            IPhpPlatformDetector detector,
            PhpPlatformInstaller phpInstaller,
            PhpComposerInstaller phpComposerInstaller)
        {
            _phpScriptGeneratorOptions = phpScriptGeneratorOptions.Value;
            _commonOptions = commonOptions.Value;
            _phpVersionProvider = phpVersionProvider;
            _phpComposerVersionProvider = phpComposerVersionProvider;
            _logger = logger;
            _detector = detector;
            _phpInstaller = phpInstaller;
            _phpComposerInstaller = phpComposerInstaller;
        }

        /// <summary>
        /// Gets the name of PHP platform which this generator will create builds for.
        /// </summary>
        public string Name => PhpConstants.PlatformName;

        /// <summary>
        /// Gets the list of versions that the script generator supports.
        /// </summary>
        public IEnumerable<string> SupportedVersions
        {
            get
            {
                var versionInfo = _phpVersionProvider.GetVersionInfo();
                return versionInfo.SupportedVersions;
            }
        }

        /// <summary>
        /// Detects the programming platform name and version required by the application in source directory.
        /// </summary>
        /// <param name="context">The repository context.</param>
        /// <returns>The results of language detector operations.</returns>
        public PlatformDetectorResult Detect(RepositoryContext context)
        {
            var detectionResult = _detector.Detect(new DetectorContext
            {
                SourceRepo = new Detector.LocalSourceRepo(context.SourceRepo.RootPath),
            });

            if (detectionResult == null)
            {
                return null;
            }

            ResolveVersions(context, detectionResult);
            return detectionResult;
        }

        /// <inheritdoc/>
        public BuildScriptSnippet GenerateBashBuildScriptSnippet(
            BuildScriptGeneratorContext ctx,
            PlatformDetectorResult detectorResult)
        {
            var phpPlatformDetectorResult = detectorResult as PhpPlatformDetectorResult;
            if (phpPlatformDetectorResult == null)
            {
                throw new ArgumentException(
                    $"Expected '{nameof(detectorResult)}' argument to be of type " +
                    $"'{typeof(PhpPlatformDetectorResult)}' but got '{detectorResult.GetType()}'.");
            }

            var buildProperties = new Dictionary<string, string>();

            // Write the platform name and version to the manifest file
            buildProperties[ManifestFilePropertyKeys.PhpVersion] = phpPlatformDetectorResult.PlatformVersion;

            _logger.LogDebug("Selected PHP version: {phpVer}", phpPlatformDetectorResult.PlatformVersion);
            bool composerFileExists = false;

            if (ctx.SourceRepo.FileExists(PhpConstants.ComposerFileName))
            {
                composerFileExists = true;

                try
                {
                    dynamic composerFile = ctx.SourceRepo.ReadJsonObjectFromFile(PhpConstants.ComposerFileName);
                    if (composerFile?.require != null)
                    {
                        Newtonsoft.Json.Linq.JObject deps = composerFile?.require;
                        var depSpecs = deps.ToObject<IDictionary<string, string>>();
                        _logger.LogDependencies(
                            this.Name,
                            phpPlatformDetectorResult.PlatformVersion,
                            depSpecs.Select(kv => kv.Key + kv.Value));
                    }
                }
                catch (Exception exc)
                {
                    // Leave malformed composer.json files for Composer to handle.
                    // This prevents Oryx from erroring out when Composer itself might be able to tolerate the file.
                    _logger.LogWarning(exc, $"Exception caught while trying to deserialize {PhpConstants.ComposerFileName.Hash()}");
                }
            }

            var props = new PhpBashBuildSnippetProperties { ComposerFileExists = composerFileExists };
            string snippet = TemplateHelper.Render(TemplateHelper.TemplateResource.PhpBuildSnippet, props, _logger);
            return new BuildScriptSnippet { BashBuildScriptSnippet = snippet, BuildProperties = buildProperties };
        }

        /// <inheritdoc/>
        public bool IsEnabled(RepositoryContext ctx)
        {
            return _commonOptions.EnablePhpBuild;
        }

        /// <inheritdoc/>
        public bool IsEnabledForMultiPlatformBuild(RepositoryContext ctx)
        {
            return true;
        }

        /// <inheritdoc/>
        public bool IsCleanRepo(ISourceRepo repo)
        {
            return true;
        }

        /// <inheritdoc/>
        public string GenerateBashRunTimeInstallationScript(RunTimeInstallationScriptGeneratorOptions options)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetDirectoriesToExcludeFromCopyToBuildOutputDir(BuildScriptGeneratorContext ctx)
        {
            return Array.Empty<string>();
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetDirectoriesToExcludeFromCopyToIntermediateDir(BuildScriptGeneratorContext ctx)
        {
            return Array.Empty<string>();
        }

        /// <inheritdoc/>
        public void ResolveVersions(RepositoryContext context, PlatformDetectorResult detectorResult)
        {
            var phpPlatformDetectorResult = detectorResult as PhpPlatformDetectorResult;
            if (phpPlatformDetectorResult == null)
            {
                throw new ArgumentException(
                    $"Expected '{nameof(detectorResult)}' argument to be of type " +
                    $"'{typeof(PhpPlatformDetectorResult)}' but got '{detectorResult.GetType()}'.");
            }

            ResolveVersionsUsingHierarchicalRules(phpPlatformDetectorResult);
        }

        /// <inheritdoc/>
        public string GetInstallerScriptSnippet(
            BuildScriptGeneratorContext context,
            PlatformDetectorResult detectorResult)
        {
            var phpPlatformDetectorResult = detectorResult as PhpPlatformDetectorResult;
            if (phpPlatformDetectorResult == null)
            {
                throw new ArgumentException(
                    $"Expected '{nameof(detectorResult)}' argument to be of type " +
                    $"'{typeof(PhpPlatformDetectorResult)}' but got '{detectorResult.GetType()}'.");
            }

            if (_commonOptions.EnableDynamicInstall)
            {
                _logger.LogDebug("Dynamic install is enabled.");

                var scriptBuilder = new StringBuilder();

                InstallPhp(phpPlatformDetectorResult.PlatformVersion, scriptBuilder);

                InstallPhpComposer(phpPlatformDetectorResult.PhpComposerVersion, scriptBuilder);

                if (scriptBuilder.Length == 0)
                {
                    return null;
                }

                return scriptBuilder.ToString();
            }
            else
            {
                _logger.LogDebug("Dynamic install not enabled.");
                return null;
            }
        }

        /// <inheritdoc/>
        public IDictionary<string, string> GetToolsToBeSetInPath(
            RepositoryContext context,
            PlatformDetectorResult detectorResult)
        {
            var phpPlatformDetectorResult = detectorResult as PhpPlatformDetectorResult;
            if (phpPlatformDetectorResult == null)
            {
                throw new ArgumentException(
                    $"Expected '{nameof(detectorResult)}' argument to be of type " +
                    $"'{typeof(PhpPlatformDetectorResult)}' but got '{detectorResult.GetType()}'.");
            }

            var tools = new Dictionary<string, string>();
            tools[PhpConstants.PlatformName] = phpPlatformDetectorResult.PlatformVersion;
            tools["composer"] = phpPlatformDetectorResult.PhpComposerVersion;
            return tools;
        }

        private void InstallPhp(string phpVersion, StringBuilder scriptBuilder)
        {
            if (_phpInstaller.IsVersionAlreadyInstalled(phpVersion))
            {
                _logger.LogDebug(
                   "PHP version {version} is already installed. So skipping installing it again.",
                   phpVersion);
            }
            else
            {
                _logger.LogDebug(
                    "PHP version {version} is not installed. " +
                    "So generating an installation script snippet for it.",
                    phpVersion);

                var script = _phpInstaller.GetInstallerScriptSnippet(phpVersion);
                scriptBuilder.AppendLine(script);
            }
        }

        private void InstallPhpComposer(string phpComposerVersion, StringBuilder scriptBuilder)
        {
            // Install PHP Composer
            if (string.IsNullOrEmpty(phpComposerVersion))
            {
                phpComposerVersion = PhpVersions.ComposerVersion;
            }

            if (_phpComposerInstaller.IsVersionAlreadyInstalled(phpComposerVersion))
            {
                _logger.LogDebug(
                   "PHP Composer version {version} is already installed. So skipping installing it again.",
                   phpComposerVersion);
            }
            else
            {
                _logger.LogDebug(
                    "PHP Composer version {version} is not installed. " +
                    "So generating an installation script snippet for it.",
                    phpComposerVersion);

                var script = _phpComposerInstaller.GetInstallerScriptSnippet(phpComposerVersion);
                scriptBuilder.AppendLine(script);
            }
        }

        private void ResolveVersionsUsingHierarchicalRules(PhpPlatformDetectorResult detectorResult)
        {
            var phpVersion = resolvePhpVersion(detectorResult.PlatformVersion);
            phpVersion = GetMaxSatisfyingPhpVersionAndVerify(phpVersion);

            var phpComposerVersion = resolvePhpComposerVersion(detectorResult.PhpComposerVersion);
            phpComposerVersion = GetMaxSatisfyingPhpComposerVersionAndVerify(phpComposerVersion);

            detectorResult.PlatformVersion = phpVersion;
            detectorResult.PhpComposerVersion = phpComposerVersion;

            string resolvePhpVersion(string detectedVersion)
            {
                // Explicitly specified version by user wins over detected version
                if (!string.IsNullOrEmpty(_phpScriptGeneratorOptions.PhpVersion))
                {
                    return _phpScriptGeneratorOptions.PhpVersion;
                }

                // If a version was detected, then use it.
                if (detectedVersion != null)
                {
                    return detectedVersion;
                }

                // Fallback to default version
                var versionInfo = _phpVersionProvider.GetVersionInfo();
                return versionInfo.DefaultVersion;
            }

            string resolvePhpComposerVersion(string detectedVersion)
            {
                // Explicitly specified version by user wins over detected version
                if (!string.IsNullOrEmpty(_phpScriptGeneratorOptions.PhpComposerVersion))
                {
                    return _phpScriptGeneratorOptions.PhpComposerVersion;
                }

                // If a version was detected, then use it.
                if (detectedVersion != null)
                {
                    return detectedVersion;
                }

                // Fallback to default version
                return PhpVersions.ComposerVersion;
            }
        }

        private string GetMaxSatisfyingPhpVersionAndVerify(string version)
        {
            var supportedVersions = SupportedVersions;
            var nonPreviewRuntimeVersions = supportedVersions.Where(v => !v.Any(c => char.IsLetter(c)));
            var maxSatisfyingVersion = SemanticVersionResolver.GetMaxSatisfyingVersion(
                version,
                nonPreviewRuntimeVersions);

            // Check if a preview version is available
            if (string.IsNullOrEmpty(maxSatisfyingVersion))
            {
                // Preview versions: '7.4.0RC4', '7.4.0beta2', etc
                var previewRuntimeVersions = supportedVersions
                    .Where(v => v.Any(c => char.IsLetter(c)))
                    .Where(v => v.StartsWith(version))
                    .OrderByDescending(v => v);
                if (previewRuntimeVersions.Any())
                {
                    maxSatisfyingVersion = previewRuntimeVersions.First();
                }
            }

            if (string.IsNullOrEmpty(maxSatisfyingVersion))
            {
                var exc = new UnsupportedVersionException(
                    PhpConstants.PlatformName,
                    version,
                    supportedVersions);
                _logger.LogError(
                    exc,
                    $"Exception caught, the version '{version}' is not supported for the PHP platform.");
                throw exc;
            }

            return maxSatisfyingVersion;
        }

        public string GetMaxSatisfyingPhpComposerVersionAndVerify(string version)
        {
            var versionInfo = _phpComposerVersionProvider.GetVersionInfo();
            var maxSatisfyingVersion = SemanticVersionResolver.GetMaxSatisfyingVersion(
                version,
                versionInfo.SupportedVersions);

            if (string.IsNullOrEmpty(maxSatisfyingVersion))
            {
                var exception = new UnsupportedVersionException(
                    PhpConstants.PhpComposerName,
                    version,
                    versionInfo.SupportedVersions);
                _logger.LogError(
                    exception,
                    $"Exception caught, the version '{version}' is not supported for the Node platform.");
                throw exception;
            }

            return maxSatisfyingVersion;
        }
    }
}