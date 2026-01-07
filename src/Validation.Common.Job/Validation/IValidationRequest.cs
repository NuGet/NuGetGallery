// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Validation
{
    /// <summary>
    /// The request to start or check a validation step.
    /// </summary>
    public interface IValidationRequest
    {
        /// <summary>
        /// The identifier for a single validation step execution.
        /// </summary>
        Guid ValidationStepId { get; }

        /// <summary>
        /// The URL to the package content. This URL should be accessible without special authentication headers.
        /// However, authentication information may be included in the URL (e.g. Azure Blob Storage SAS URL). This URL
        /// need not have a single value for a specific <see cref="ValidationStepId"/>.
        /// </summary>
        Uri InputUrl { get; }

        /// <summary>
        /// Deserializes the validation set's properties content as JSON.
        /// </summary>
        /// <seealso cref="PackageValidationSet.ValidationProperties"/>
        /// <typeparam name="T">The type to use to deserialize the properties.</typeparam>
        /// <returns>The validation set's properties.</returns>
        T GetProperties<T>();
    }
}
