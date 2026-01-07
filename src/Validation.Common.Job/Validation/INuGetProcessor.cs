// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation
{
    /// <summary>
    /// A marker interface to state that this validator mutates packages. The only purpose of this interface is to
    /// allow the caller of the validators (e.g. orchestrator) to verify in advance that a validator (i.e. processor)
    /// that mutates packages does not run in parallel with any other validator.
    /// </summary>
    public interface INuGetProcessor : INuGetValidator
    {
    }
}
