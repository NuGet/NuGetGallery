// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    /// <summary>
    /// Appends a name to the path of another <see cref="StorageFactory"/>.
    /// </summary>
    public class NamedStorageFactory : StorageFactory
    {
        private StorageFactory _innerStorageFactory;
        private string _name;

        public NamedStorageFactory(StorageFactory inner, string name)
        {
            _innerStorageFactory = inner;
            _name = name;
        }

        public override Storage Create(string name = null)
        {
            return _innerStorageFactory.Create($"{_name}/{name}");
        }
    }
}
