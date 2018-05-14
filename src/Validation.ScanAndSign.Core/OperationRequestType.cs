// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.Validation.ScanAndSign
{
    /// <summary>
    /// Specifies the type of the request sent to the validator job
    /// </summary>
    public enum OperationRequestType
    {
        Scan,
        Sign
    }
}
