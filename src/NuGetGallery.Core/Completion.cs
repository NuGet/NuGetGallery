// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Backend.Monitoring
{
    public interface ICompletion<T>
    {
        Task Complete(T result);
    }

    public class Completion<T> : ICompletion<T>
    {
        private Func<T, Task> _action;

        public Completion(Func<T, Task> action)
        {
            _action = action;
        }

        public Task Complete(T result)
        {
            return _action(result);
        }
    }
}
