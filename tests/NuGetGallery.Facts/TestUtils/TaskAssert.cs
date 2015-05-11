// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery
{
    static class TaskAssert
    {
        /// <summary>
        /// Like XUnit Assert.Throws, but for actions which will throw an AggregateException that 
        /// wraps some TException (because they use Task Parallel or async paradigm).
        /// </summary>
        /// <typeparam name="TException">The type of AggregateException's (single) inner exception</typeparam>
        public static TException ThrowsAggregate<TException>(Action action) where TException : Exception
        {
            AggregateException exception = Assert.Throws<AggregateException>(() => action.Invoke());
            Assert.Equal(1, exception.InnerExceptions.Count);
            Assert.IsType<TException>(exception.InnerException);
            return (TException)(exception.InnerException);
        }

        public static TException ThrowsAsync<TException>(Func<Task> testCode) where TException : Exception
        {
            return Assert.Throws<TException>(() => testCode().GetAwaiter().GetResult());
        }
    }
}
