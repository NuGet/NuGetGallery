// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Threading.Tasks;

namespace NuGetGallery.Backend.Monitoring
{
    public interface ICompletion<T>
    {
        Task Complete(T result);
    }

    public class Completion<T>(Func<T, Task> action) : ICompletion<T>
    {
        private Func<T, Task> _action = action;

        public Task Complete(T result) => _action(result);
    }
}
