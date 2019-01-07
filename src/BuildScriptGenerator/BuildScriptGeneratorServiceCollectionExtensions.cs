﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Oryx.BuildScriptGenerator
{
    public static class BuildScriptGeneratorServiceCollectionExtensions
    {
        public static IServiceCollection AddBuildScriptGeneratorServices(this IServiceCollection services)
        {
            services
                .AddNodeScriptGeneratorServices()
                .AddPythonScriptGeneratorServices()
                .AddDotnetCoreScriptGeneratorServices();

            services.AddSingleton<IScriptGenerator, DefaultScriptGenerator>();
            services.AddSingleton<IEnvironment, DefaultEnvironment>();
            services.AddSingleton<ISourceRepoProvider, DefaultSourceRepoProvider>();
            services.AddSingleton<ITempDirectoryProvider, DefaulTempDirectoryProvider>();
            services.AddSingleton<IScriptExecutor, DefaultScriptExecutor>();
            services.AddSingleton<IEnvironmentSettingsProvider, DefaultEnvironmentSettingsProvider>();

            return services;
        }
    }
}