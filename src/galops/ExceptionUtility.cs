// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Reflection;

namespace NuGet
{
    public static class ExceptionUtility
    {
        public static Exception Unwrap(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            if (exception.InnerException == null)
            {
                return exception;
            }

            // Always return the inner exception from a target invocation exception
            if (exception is AggregateException ||
                exception is TargetInvocationException)
            {
                return exception.GetBaseException();
            }

            return exception;
        }
    }
}
