﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Microsoft.CSharp.RuntimeBinder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Oryx.Common.Extensions;
using Newtonsoft.Json;

namespace Microsoft.Oryx.Detector.Node
{
    /// <summary>
    /// An implementation of <see cref="IPlatformDetector"/> which detects NodeJS applications.
    /// </summary>
    public class NodeDetector : INodePlatformDetector
    {
        private readonly ILogger<NodeDetector> _logger;
        private readonly DetectorOptions _options;

        /// <summary>
        /// Creates an instance of <see cref="NodeDetector"/>.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger{NodeDetector}"/>.</param>
        /// <param name="options">The <see cref="DetectorOptions"/>.</param>
        public NodeDetector(ILogger<NodeDetector> logger, IOptions<DetectorOptions> options)
        {
            _logger = logger;
            _options = options.Value;
        }

        public PlatformDetectorResult Detect(DetectorContext context)
        {
            bool isNodeApp = false;
            bool hasLernaJsonFile = false;
            bool hasLageConfigJSFile = false;
            string appDirectory = string.Empty;
            var sourceRepo = context.SourceRepo;
            if (sourceRepo.FileExists(NodeConstants.PackageJsonFileName) ||
                sourceRepo.FileExists(NodeConstants.PackageLockJsonFileName) ||
                sourceRepo.FileExists(NodeConstants.YarnLockFileName))
            {
                isNodeApp = true;
            }
            else
            {
                _logger.LogDebug(
                    $"Could not find {NodeConstants.PackageJsonFileName}/{NodeConstants.PackageLockJsonFileName}" +
                    $"/{NodeConstants.YarnLockFileName} in repo");
            }

            if (sourceRepo.FileExists(NodeConstants.LernaJsonFileName))
            {
                hasLernaJsonFile = true;
            }
            if (sourceRepo.FileExists(NodeConstants.LageConfigJSFileName))
            {
                hasLageConfigJSFile = true;
            }

            if (!isNodeApp)
            {
                // Copying the logic currently running in Kudu:
                var mightBeNode = false;
                foreach (var typicalNodeFile in NodeConstants.TypicalNodeDetectionFiles)
                {
                    if (sourceRepo.FileExists(typicalNodeFile))
                    {
                        mightBeNode = true;
                        break;
                    }
                }

                if (mightBeNode)
                {
                    // Check if any of the known iis start pages exist
                    // If so, then it is not a node.js web site otherwise it is
                    foreach (var iisStartupFile in NodeConstants.IisStartupFiles)
                    {
                        if (sourceRepo.FileExists(iisStartupFile))
                        {
                            _logger.LogDebug(
                                "App in repo is not a Node.js app as it has the file {iisStartupFile}",
                                iisStartupFile.Hash());
                            return null;
                        }
                    }

                    isNodeApp = true;
                }
                else
                {
                    // No point in logging the actual file list, as it's constant
                    _logger.LogDebug("Could not find typical Node.js files in repo");
                }
            }

            if (!isNodeApp)
            {
                _logger.LogDebug("App in repo is not a NodeJS app");
                return null;
            }

            var version = GetVersion(context);
            IEnumerable<FrameworkInfo> detectedFrameworkInfos = null;
            if (!_options.DisableFrameworkDetection)
            {
                detectedFrameworkInfos = DetectFrameworkInfos(context);
            }

            return new NodePlatformDetectorResult
            {
                Platform = NodeConstants.PlatformName,
                PlatformVersion = version,
                AppDirectory = appDirectory,
                Frameworks = detectedFrameworkInfos,
                HasLernaJsonFile = hasLernaJsonFile,
                HasLageConfigJSFile = hasLageConfigJSFile,
            };
        }

        private string GetVersion(DetectorContext context)
        {
            var version = GetVersionFromPackageJson(context);
            if (version != null)
            {
                return version;
            }
            _logger.LogDebug("Could not get version from package Json.");
            return null;
        }

        private string GetVersionFromPackageJson(DetectorContext context)
        {
            var packageJson = GetPackageJsonObject(context.SourceRepo, _logger);
            return packageJson?.engines?.node?.Value as string;
        }

        private dynamic GetPackageJsonObject(ISourceRepo sourceRepo, ILogger logger)
        {
            dynamic packageJson = null;
            try
            {
                packageJson = ReadJsonObjectFromFile(sourceRepo, NodeConstants.PackageJsonFileName);
            }
            catch (Exception exc)
            {
                // Leave malformed package.json files for Node.js to handle.
                // This prevents Oryx from erroring out when Node.js itself might be able to tolerate the file.
                logger.LogWarning(
                    exc,
                    $"Exception caught while trying to deserialize {NodeConstants.PackageJsonFileName.Hash()}");
            }

            return packageJson;
        }

        private dynamic ReadJsonObjectFromFile(ISourceRepo sourceRepo, string fileName)
        {
            var jsonContent = sourceRepo.ReadFile(fileName);
            return JsonConvert.DeserializeObject(jsonContent);
        }

        private IEnumerable<FrameworkInfo> DetectFrameworkInfos(DetectorContext context)
        {
            var detectedFrameworkResult = new List<FrameworkInfo>();
            var packageJson = GetPackageJsonObject(context.SourceRepo, _logger);
            if (packageJson?.devDependencies != null)
            {
                var devDependencyObject = packageJson?.devDependencies;
                foreach (var keyWordToName in NodeConstants.DevDependencyFrameworkKeyWordToName)
                {
                    var keyword = keyWordToName.Key;
                    try
                    {
                        var frameworkInfo = new FrameworkInfo
                        {
                            Framework = keyWordToName.Value,
                            FrameworkVersion = devDependencyObject[keyword].Value as string
                        };
                        detectedFrameworkResult.Add(frameworkInfo);
                    } 
                    catch (RuntimeBinderException)
                    {
                    }
                }
            }

            if (packageJson?.dependencies != null)
            {
                var dependencyObject = packageJson?.dependencies;
                foreach (var keyWordToName in NodeConstants.DependencyFrameworkKeyWordToName)
                {
                    var keyword = keyWordToName.Key;
                    try
                    {
                        var frameworkInfo = new FrameworkInfo
                        {
                            Framework = keyWordToName.Value,
                            FrameworkVersion = dependencyObject[keyword].Value as string
                        };
                        detectedFrameworkResult.Add(frameworkInfo);
                    }
                    catch (RuntimeBinderException)
                    {
                    }
                }
            }

            if (context.SourceRepo.FileExists(NodeConstants.FlutterYamlFileName)) {
                var frameworkInfo = new FrameworkInfo
                {
                    Framework = NodeConstants.FlutterFrameworkeName,
                    FrameworkVersion = string.Empty
                };
                detectedFrameworkResult.Add(frameworkInfo);
            }

            return detectedFrameworkResult;
        }
    }
}