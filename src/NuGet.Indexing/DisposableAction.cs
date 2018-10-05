// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Indexing
{
    /// <summary>
    /// Source:
    /// https://github.com/NuGet/NuGet2/blob/c3d1027a51b31fd0c41e9abbe90810cf1c924c9f/src/Core/Utility/DisposableAction.cs
    /// </summary>
    public sealed class DisposableAction : IDisposable
    {
        public static readonly DisposableAction NoOp = new DisposableAction(() => { });

        private Action _action;

        public DisposableAction(Action action)
        {
            _action = action;
        }

        public static IDisposable All(params IDisposable[] tokens)
        {
            return new DisposableAction(() =>
            {
                foreach (var token in tokens)
                {
                    token.Dispose();
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
