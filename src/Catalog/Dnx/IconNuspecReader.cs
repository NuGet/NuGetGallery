// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet.Packaging;

namespace NuGet.Services.Metadata.Catalog.Dnx
{
    /// <summary>
    /// Temporary self-contained icon reading class until proper implementation is provided by the client library.
    /// </summary>
    public class IconNuspecReader : NuspecReader
    {
        private const string IconNodeName = "icon";

        public IconNuspecReader(Stream stream)
            : base(stream)
        {
        }

        public string IconPath => MetadataNode.Element(MetadataNode.Name.Namespace + IconNodeName)?.Value;
    }
}
