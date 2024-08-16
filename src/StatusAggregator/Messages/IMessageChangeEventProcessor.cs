// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Messages
{
    public interface IMessageChangeEventProcessor
    {
        /// <summary>
        /// Processes <paramref name="change"/>.
        /// </summary>
        /// <param name="eventEntity">The <see cref="EventEntity"/> associated with the change.</param>
        /// <param name="rootComponent">The <see cref="IComponent"/> associated with this iteration.</param>
        /// <param name="existingStartMessageContext">
        /// The <see cref="ExistingStartMessageContext"/> associated with this iteration.
        /// <c>null</c> if there is no existing start message without a corresponding end message.
        /// </param>
        /// <returns>
        /// A <see cref="ExistingStartMessageContext"/> that reflects the change made by processing <paramref name="change"/>.
        /// <c>null</c> if there is no existing start message without a corresponding end message.
        /// </returns>
        Task<ExistingStartMessageContext> ProcessAsync(
            MessageChangeEvent change, 
            EventEntity eventEntity, 
            IComponent rootComponent, 
            ExistingStartMessageContext existingStartMessageContext);
    }
}