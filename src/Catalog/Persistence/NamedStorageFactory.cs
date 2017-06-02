// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    /// <summary>
    /// Appends a name to the path of another <see cref="IStorageFactory"/>.
    /// </summary>
    public class NamedStorageFactory : IStorageFactory
    {
        private IStorageFactory _innerStorageFactory;
        private string _name;

        public NamedStorageFactory(IStorageFactory inner, string name)
        {
            _innerStorageFactory = inner;
            _name = name;
        }

        public Uri BaseAddress => new Uri(_innerStorageFactory.BaseAddress, _name);

        public Storage Create(string name = null)
        {
            return _innerStorageFactory.Create($"{_name}/{name}");
        }
    }
}
