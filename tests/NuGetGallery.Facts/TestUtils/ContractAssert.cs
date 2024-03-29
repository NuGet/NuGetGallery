﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery
{
    public static class ContractAssert
    {
        public static void Throws<TException>(Action act, string message) where TException : Exception
        {
            var ex = Assert.Throws<TException>(() => act());
            Assert.Equal(message, ex.Message);
        }

        public static void ThrowsArgNull(Action act, string paramName)
        {
            var argNullEx = Assert.Throws<ArgumentNullException>(() => act());
            Assert.Equal(paramName, argNullEx.ParamName);
        }

        public static async Task ThrowsArgNullAsync(Func<Task> act, string paramName)
        {
            var argNullEx = await Assert.ThrowsAsync<ArgumentNullException>(async () => await act());
            Assert.Equal(paramName, argNullEx.ParamName);
        }

        public static void ThrowsArgNullOrEmpty(Action<string> act, string paramName)
        {
            var message = String.Format(Strings.ParameterCannotBeNullOrEmpty, paramName);
            ContractAssert.ThrowsArgException(() => act(null), paramName, message);
            ContractAssert.ThrowsArgException(() => act(String.Empty), paramName, message);
        }

        public static void ThrowsArgException(Action act, string paramName, string message)
        {
            var argEx = Assert.Throws<ArgumentException>(() => act());
            Assert.Equal(paramName, argEx.ParamName);
            Assert.StartsWith(
                message,
                argEx.Message);
        }

        public static async Task ThrowsArgExceptionAsync(Func<Task> act, string paramName, string message = "")
        {
            var argEx = await Assert.ThrowsAsync<ArgumentException>(async () => await act());
            Assert.Equal(paramName, argEx.ParamName);
            Assert.StartsWith(
                message,
                argEx.Message);
        }
    }
}
