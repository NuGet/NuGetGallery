// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGet.Services.Validation.Orchestrator
{
    public interface IMessageService<T> where T : class, IEntity
    {
        Task SendPublishedMessageAsync(T entity);
        Task SendValidationFailedMessageAsync(T entity, PackageValidationSet validationSet);
        Task SendValidationTakingTooLongMessageAsync(T entity);
    }
}
