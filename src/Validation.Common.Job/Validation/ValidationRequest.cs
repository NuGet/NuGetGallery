// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Validation
{
    // TODO: Finalize this class.
    // Tracked by: https://github.com/NuGet/Engineering/issues/3583
    public class ValidationRequest : IValidationRequest
    {
        public ValidationRequest(Guid validationStepId, Uri inputUrl)
        {
            ValidationStepId = validationStepId;
            InputUrl = inputUrl;
        }

        public Guid ValidationStepId { get; }

        public Uri InputUrl { get; }

        public T GetProperties<T>()
        {
            throw new NotImplementedException();
        }
    }
}
