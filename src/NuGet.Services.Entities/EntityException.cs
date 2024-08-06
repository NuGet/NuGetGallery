// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace NuGet.Services.Entities
{
    [Serializable]
    public class EntityException
        : Exception
    {
        public EntityException(string message)
            : base(message)
        {
        }

        public EntityException(string message, params object[] args)
            : base(string.Format(CultureInfo.CurrentCulture, message, args))
        {
        }
    }
}