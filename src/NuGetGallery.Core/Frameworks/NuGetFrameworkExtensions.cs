// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;
using System.Text;

namespace NuGetGallery.Frameworks
{
    public static class NuGetFrameworkExtensions
    {
        public static string GetBadgeVersion(this NuGetFramework framework)
        {
            if (framework.Version == FrameworkConstants.EmptyVersion)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append(framework.Version.Major);
            builder.Append('.');
            builder.Append(framework.Version.Minor);

            if (framework.Version.Build != 0)
            {
                builder.Append('.');
                builder.Append(framework.Version.Build);

                if (framework.Version.Revision != 0)
                {
                    builder.Append('.');
                    builder.Append(framework.Version.Revision);
                }
            }
            else if (framework.Version.Revision != 0)
            {
                builder.Append(".0.");
                builder.Append(framework.Version.Revision);
            }

            return builder.ToString();
        }
    }
}
