// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Entities
{
    /// <summary>
    /// The common entity type to be shared by <see cref="Package"/> and <see cref="SymbolPackage"/>
    /// This allows us the generic type instantiation in dependency injection for the commonly required code.
    /// </summary>
    public interface IPackageEntity : IEntity
    {
        string Id { get; }

        string Version { get; }
    }
}