// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Base class for exceptions throw by <see cref="IValidator.ValidateAsync(ValidationContext)"/>.
    /// </summary>
    public class ValidationException : Exception
    {
        public ValidationException()
            : base()
        {
        }

        public ValidationException(string message)
            : base(message)
        {
        }

        public ValidationException(string message, Exception e)
            : base(message, e)
        {
        }
    }
}
