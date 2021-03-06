// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.ProjectModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;

namespace Microsoft.Extensions.ProjectModel
{
    internal class DotNetProjectContext : IProjectContext
    {
        private readonly ProjectContext _projectContext;
        private readonly OutputPaths _paths;
        private readonly Lazy<JObject> _rawProject;
        private readonly CommonCompilerOptions _compilerOptions;

        public DotNetProjectContext(ProjectContext projectContext, string configuration, string outputPath)
        {
            if (projectContext == null)
            {
                throw new ArgumentNullException(nameof(projectContext));
            }

            if (string.IsNullOrEmpty(configuration))
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            _rawProject = new Lazy<JObject>(() =>
                {
                    using (var stream = new FileStream(projectContext.ProjectFile.ProjectFilePath, FileMode.Open, FileAccess.Read))
                    using (var streamReader = new StreamReader(stream))
                    using (var jsonReader = new JsonTextReader(streamReader))
                    {
                        return JObject.Load(jsonReader);
                    }
                });

            Configuration = configuration;
            _projectContext = projectContext;

            _paths = projectContext.GetOutputPaths(configuration, buidBasePath: null, outputPath: outputPath);
            _compilerOptions = _projectContext.ProjectFile.GetCompilerOptions(TargetFramework, Configuration);

            // Workaround https://github.com/dotnet/cli/issues/3164
            IsClassLibrary = !(_compilerOptions.EmitEntryPoint
                    ?? projectContext.ProjectFile.GetCompilerOptions(null, configuration).EmitEntryPoint.GetValueOrDefault());
        }

        public bool IsClassLibrary { get; }

        public NuGetFramework TargetFramework => _projectContext.TargetFramework;
        public string Config => _paths.RuntimeFiles.Config;
        public string DepsJson => _paths.RuntimeFiles.DepsJson;
        public string RuntimeConfigJson => _paths.RuntimeFiles.RuntimeConfigJson;
        public string PackagesDirectory => _projectContext.PackagesDirectory;

        public string AssemblyFullPath =>
            !IsClassLibrary && (_projectContext.IsPortable || TargetFramework.IsDesktop())
                ? _paths.RuntimeFiles.Executable
                : _paths.RuntimeFiles.Assembly;

        public string Configuration { get; }
        public string ProjectFullPath => _projectContext.ProjectFile.ProjectFilePath;
        public string ProjectName => _projectContext.ProjectFile.Name;
        // TODO read from xproj if available
        public string RootNamespace => _projectContext.ProjectFile.Name;
        public string TargetDirectory => _paths.RuntimeOutputPath;
        public string Platform => _compilerOptions.Platform;

        public IEnumerable<string> CompilationItems
            => _compilerOptions.CompileInclude.ResolveFiles();

        public IEnumerable<string> EmbededItems
            => _compilerOptions.EmbedInclude.ResolveFiles();

        /// <summary>
        /// Returns string values of top-level keys in the project.json file
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public string FindProperty(string propertyName) => FindProperty<string>(propertyName);

        public TProperty FindProperty<TProperty>(string propertyName)
        {
            foreach (var item in _rawProject.Value)
            {
                if (item.Key.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return item.Value.Value<TProperty>();
                }
            }

            return default(TProperty);
        }
    }
}