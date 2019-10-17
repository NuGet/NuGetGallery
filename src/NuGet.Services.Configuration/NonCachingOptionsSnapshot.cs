// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace NuGet.Services.Configuration
{
    /// <summary>
    /// <see cref="IOptionsSnapshot{TOptions}"/> implementation that does not use default implementation's 
    /// cache for the <typeparamref name="TOptions"/> objects and always instantiates and binds a new one.
    /// </summary>
    /// <typeparam name="TOptions">The actual data object</typeparam>
    /// <example>
    /// To use, add the following line before services.AddOptions() call:
    /// services.Add(ServiceDescriptor.Scoped(typeof(IOptionsSnapshot<>), typeof(NonCachingOptionsSnapshot<>)));
    /// </example>
    public class NonCachingOptionsSnapshot<TOptions> : IOptionsSnapshot<TOptions>
        where TOptions : class, new()
    {
        private readonly TOptions _value;

        public NonCachingOptionsSnapshot(IEnumerable<IConfigureOptions<TOptions>> setups)
        {
            setups = setups ?? throw new ArgumentNullException(nameof(setups));

            _value = new TOptions();
            foreach (var setup in setups)
            {
                setup.Configure(_value);
            }
        }

        public TOptions Value => _value;

        public TOptions Get(string name)
        {
            throw new NotImplementedException();
        }
    }
}
