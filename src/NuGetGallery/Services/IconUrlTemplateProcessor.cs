// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class IconUrlTemplateProcessor : StringReplaceTemplateProcessor<Package>, IIconUrlTemplateProcessor
    {
        public IconUrlTemplateProcessor(IAppConfiguration configuration)
            : base(
                  GetIconUrlTemplate(configuration),
                  new Dictionary<string, Func<Package, string>>
                  {
                      { "{id-lower}", p => p.PackageRegistration.Id.ToLowerInvariant() },
                      { "{version-lower}", p => p.NormalizedVersion.ToLowerInvariant() },
                  })
        {
        }

        private static string GetIconUrlTemplate(IAppConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }
            return configuration.EmbeddedIconUrlTemplate;
        }
    }
}