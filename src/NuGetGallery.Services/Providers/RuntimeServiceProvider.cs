// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace NuGetGallery
{
    public sealed class RuntimeServiceProvider : IDisposable
    {
        private readonly CompositionContainer _compositionContainer;
        private bool _isDisposed;

        private RuntimeServiceProvider(CompositionContainer compositionContainer)
        {
            _compositionContainer = compositionContainer;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _compositionContainer.Dispose();

                _isDisposed = true;
            }
        }

        public static RuntimeServiceProvider Create(string baseDirectoryPath)
        {
            if (string.IsNullOrEmpty(baseDirectoryPath))
            {
                throw new ArgumentException(ServicesStrings.ParameterCannotBeNullOrEmpty, nameof(baseDirectoryPath));
            }

            var compositionContainer = CreateCompositionContainer(baseDirectoryPath);

            return new RuntimeServiceProvider(compositionContainer);
        }

        public void ComposeExportedValue<T>(string contractName, T exportedValue)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(RuntimeServiceProvider));
            }

            _compositionContainer.ComposeExportedValue(contractName, exportedValue);
        }

        public void ComposeExportedValue<T>(T exportedValue)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(RuntimeServiceProvider));
            }

            _compositionContainer.ComposeExportedValue(exportedValue);
        }

        public IEnumerable<T> GetExportedValues<T>()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(RuntimeServiceProvider));
            }

            try
            {
                return _compositionContainer.GetExportedValues<T>();
            }
            catch (Exception ex)
            {
                try
                {
                    var tracePrefix = $"Error in {nameof(RuntimeServiceProvider)}.{nameof(GetExportedValues)}<{typeof(T).Name}>().";

                    // We use Trace instead of the ILogger or TelemetryClient because they may be unavailable at this point.
                    Trace.TraceError("{0} Failed with {1}:{2}{3}", tracePrefix, ex.GetType().Name, Environment.NewLine, ex);
                    var currentEx = ex;
                    var loaderExceptionCount = 0;
                    while (currentEx is not null)
                    {
                        if (ex is ReflectionTypeLoadException rex)
                        {
                            foreach (var loaderException in rex.LoaderExceptions)
                            {
                                loaderExceptionCount++;
                                Trace.TraceError("{0} Loader exception {1}:{2}{3}", tracePrefix, loaderExceptionCount, Environment.NewLine, loaderException);
                            }
                        }

                        currentEx = currentEx.InnerException;
                    }
                }
                catch
                {
                    // best effort
                }

                throw;
            }
        }

        // Runtime loadable services are only allowed from subdirectories of the base directory path.
        private static CompositionContainer CreateCompositionContainer(string baseDirectoryPath)
        {
            if (!Directory.Exists(baseDirectoryPath))
            {
                return new CompositionContainer();
            }

            var subdirectoryPaths = Directory.GetDirectories(baseDirectoryPath);

            if (!subdirectoryPaths.Any())
            {
                return new CompositionContainer();
            }

            var catalog = new AggregateCatalog();

            foreach (var subdirectoryPath in subdirectoryPaths)
            {
                catalog.Catalogs.Add(new DirectoryCatalog(subdirectoryPath));
            }

            return new CompositionContainer(catalog);
        }
    }
}
