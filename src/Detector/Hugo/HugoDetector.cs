﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Oryx.Detector.Exceptions;
using Nett;
using Newtonsoft.Json.Linq;
using YamlDotNet.RepresentationModel;

namespace Microsoft.Oryx.Detector.Hugo
{
    /// <summary>
    /// An implementation of <see cref="IPlatformDetector"/> which detects Hugo applications.
    /// </summary>
    public class HugoDetector : IHugoPlatformDetector
    {
        private readonly ILogger<HugoDetector> _logger;

        internal static readonly string[] HugoConfigurationKeys =
        {
            "archetypeDir",
            "baseURL",
            "contentDir",
            "languageCode",
            "layoutDir",
            "staticDir",
            "title",
            "theme",
        };

        /// <summary>
        /// Creates an instance of <see cref="HugoDetector"/>.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger{HugoDetector}"/>.</param>
        public HugoDetector(ILogger<HugoDetector> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public PlatformDetectorResult Detect(DetectorContext context)
        {
            var isHugoApp = IsHugoApp(context.SourceRepo, out string appDirectory);
            if (isHugoApp)
            {
                return new PlatformDetectorResult
                {
                    Platform = HugoConstants.PlatformName,
                    AppDirectory = appDirectory,
                };
            }

            return null;
        }

        private bool IsHugoApp(ISourceRepo sourceRepo, out string appDirectory)
        {
            // Hugo configuration variables:
            // https://gohugo.io/getting-started/configuration/#all-configuration-settings
            appDirectory = string.Empty;

            // Search for config.toml
            if (sourceRepo.FileExists(HugoConstants.TomlFileName)
                && IsHugoTomlFile(sourceRepo, HugoConstants.TomlFileName))
            {
                return true;
            }

            // Search for config.yml
            if (sourceRepo.FileExists(HugoConstants.YmlFileName)
                && IsHugoYamlFile(sourceRepo, HugoConstants.YmlFileName))
            {
                return true;
            }

            // Search for config.yaml
            if (sourceRepo.FileExists(HugoConstants.YamlFileName)
                && IsHugoYamlFile(sourceRepo, HugoConstants.YamlFileName))
            {
                return true;
            }

            // Search for config.json
            if (sourceRepo.FileExists(HugoConstants.JsonFileName)
                && IsHugoYamlFile(sourceRepo, HugoConstants.JsonFileName))
            {
                return true;
            }

            // NOTE: we do NOT disable looking up into the 'config' folder because it is a special folder
            // from perspective of Hugo where users can have configuration files.
            if (sourceRepo.DirExists(HugoConstants.ConfigFolderName))
            {
                // Search for config/**/*.toml
                var tomlFiles = sourceRepo.EnumerateFiles(
                    "*.toml",
                    searchSubDirectories: true,
                    subDirectoryToSearchUnder: HugoConstants.ConfigFolderName);
                foreach (var tomlFile in tomlFiles)
                {
                    if (IsHugoTomlFile(sourceRepo, tomlFile))
                    {
                        return true;
                    }
                }

                // Search for config/**/*.yaml and config/**/*.yml
                var yamlFiles = sourceRepo.EnumerateFiles(
                    "*.yaml",
                    searchSubDirectories: true,
                    subDirectoryToSearchUnder: HugoConstants.ConfigFolderName);
                foreach (var yamlFile in yamlFiles)
                {
                    if (IsHugoYamlFile(sourceRepo, yamlFile))
                    {
                        return true;
                    }
                }

                var ymlFiles = sourceRepo.EnumerateFiles(
                    "*.yml",
                    searchSubDirectories: true,
                    subDirectoryToSearchUnder: HugoConstants.ConfigFolderName);
                foreach (var ymlFile in ymlFiles)
                {
                    if (IsHugoYamlFile(sourceRepo, ymlFile))
                    {
                        return true;
                    }
                }

                // Search for config/**/*.json
                var jsonFiles = sourceRepo.EnumerateFiles(
                    "*.json",
                    searchSubDirectories: true,
                    subDirectoryToSearchUnder: HugoConstants.ConfigFolderName);
                foreach (var jsonFile in jsonFiles)
                {
                    if (IsHugoJsonFile(sourceRepo, jsonFile))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsHugoTomlFile(ISourceRepo sourceRepo, params string[] subPaths)
        {
            TomlTable tomlTable = null;

            var relativeFilePath = Path.Combine(subPaths);
            tomlTable = ParserHelper.ParseTomlFile(sourceRepo, relativeFilePath);
            if (tomlTable.Keys
                .Any(k => HugoConfigurationKeys.Contains(k, StringComparer.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }

        private bool IsHugoYamlFile(ISourceRepo sourceRepo, params string[] subPaths)
        {
            var relativeFilePath = Path.Combine(subPaths);

            YamlNode yamlNode = null;

            yamlNode = ParserHelper.ParseYamlFile(sourceRepo, relativeFilePath);
            
            var yamlMappingNode = yamlNode as YamlMappingNode;
            if (yamlMappingNode != null)
            {
                if (yamlMappingNode.Children.Keys
                    .Select(key => key.ToString())
                    .Any(key => HugoConfigurationKeys.Contains(key, StringComparer.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsHugoJsonFile(ISourceRepo sourceRepo, params string[] subPaths)
        {
            var relativeFilePath = Path.Combine(subPaths);

            JObject jObject = null;

            jObject = ParserHelper.ParseJsonFile(sourceRepo, relativeFilePath);
            
            if (jObject.Children()
                .Select(c => c.Path)
                .Any(c => HugoConfigurationKeys.Contains(c, StringComparer.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }
    }
}
