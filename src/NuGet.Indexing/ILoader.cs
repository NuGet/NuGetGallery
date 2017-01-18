// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using System.Threading.Tasks;

namespace NuGet.Indexing
{
    public interface ILoader
    {
        JsonReader GetReader(string name);

        /// <summary>
        /// Reloads the data for the reader.
        /// </summary>
        /// <param name="config">Configuration to use.</param>
        /// <returns>Returns true if the data has changed.</returns>
        bool Reload(IndexingConfiguration config);
    }
}
