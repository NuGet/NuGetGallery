// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public sealed class DisposableAction : IDisposable
    {
        public static readonly DisposableAction NoOp = new DisposableAction(() => { });

        private readonly Action _action;

        public DisposableAction(Action action)
        {
            _action = action;
        }

        public static IDisposable All(params IDisposable[] tokens)
        {
            return new DisposableAction(() =>
            {
                var exceptions = new List<Exception>();

                foreach (var token in tokens)
                {
                    try
                    {
                        token.Dispose();
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }

                if (exceptions.Any())
                {
                    throw new AggregateException(exceptions);
                }
            });
        }

        public static IDisposable All(IEnumerable<IDisposable> tokens)
        {
            return All(tokens.ToArray());
        }

        public void Dispose()
        {
            _action();
        }
    }
}