// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Entities
{
    /// <summary>
    /// Specifies the type of the readme file used in the package
    /// </summary>
    public enum EmbeddedReadmeFileType
    {
        /// <summary>
        /// Indicates that the package has no embedded readme file.
        /// </summary>
        Absent = 0,

        /// <summary>
        /// Indicates that tne embedded readme file is markdown.
        /// </summary>
        Markdown = 1,
    }
}
