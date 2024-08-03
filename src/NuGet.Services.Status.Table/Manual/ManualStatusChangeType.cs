// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Status.Table.Manual
{
    public enum ManualStatusChangeType
    {
        /// <summary>
        /// <see cref="AddStatusEventManualChangeEntity"/>
        /// </summary>
        AddStatusEvent = 0,

        /// <summary>
        /// <see cref="EditStatusEventManualChangeEntity"/>
        /// </summary>
        EditStatusEvent = 1,

        /// <summary>
        /// <see cref="DeleteStatusEventManualChangeEntity"/>
        /// </summary>
        DeleteStatusEvent = 2,

        /// <summary>
        /// <see cref="AddStatusMessageManualChangeEntity"/>
        /// </summary>
        AddStatusMessage = 3,
        
        /// <summary>
        /// <see cref="EditStatusMessageManualChangeEntity"/>
        /// </summary>
        EditStatusMessage = 4,

        /// <summary>
        /// <see cref="DeleteStatusMessageManualChangeEntity"/>
        /// </summary>
        DeleteStatusMessage = 5,
    }
}
