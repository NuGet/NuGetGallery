// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Status.Table
{
    /// <summary>
    /// Categorizes a <see cref="MessageEntity"/> by the reason it was posted.
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// A message that was manually posted or edited.
        /// </summary>
        Manual = 0,

        /// <summary>
        /// A message that marks the start of an <see cref="Event"/>.
        /// </summary>
        Start = 1,

        /// <summary>
        /// A message that marks the end of an <see cref="Event"/>.
        /// </summary>
        End = 2
    }
}
