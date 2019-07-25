// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;

namespace NuGetGallery
{
    internal sealed class RuntimeServiceProvider : IDisposable
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

        internal static RuntimeServiceProvider Create(string baseDirectoryPath)
        {
            if (string.IsNullOrEmpty(baseDirectoryPath))
            {
                throw new ArgumentException(ServicesStrings.ParameterCannotBeNullOrEmpty, nameof(baseDirectoryPath));
            }

            var compositionContainer = CreateCompositionContainer(baseDirectoryPath);

            return new RuntimeServiceProvider(compositionContainer);
        }

        internal IEnumerable<T> GetExportedValues<T>()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(RuntimeServiceProvider));
            }

            return _compositionContainer.GetExportedValues<T>();
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