// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Entities
{
    /// <summary>
    /// Specifies the type of the license file used in the package
    /// </summary>
    public enum EmbeddedLicenseFileType
    {
        /// <summary>
        /// Indicates that package has no license file embedded.
        /// </summary>
        Absent = 0,

        /// <summary>
        /// Indicates that embedded license file is plain text.
        /// </summary>
        PlainText = 1,

        /// <summary>
        /// Indicates that embedded license file is markdown.
        /// </summary>
        Markdown = 2,
    }
}
