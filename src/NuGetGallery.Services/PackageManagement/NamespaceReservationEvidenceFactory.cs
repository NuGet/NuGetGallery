// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;

namespace NuGetGallery
{
    public sealed class NamespaceReservationEvidenceFactory : INamespaceReservationEvidenceFactory
    {
        private readonly EntitiesContext _context;
        private readonly SemaphoreSlim _connectionGate = new SemaphoreSlim(1, 1);

        public NamespaceReservationEvidenceFactory(EntitiesContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public INamespaceReservationEvidenceSession Create(int submitterKey, int[] ownerKeys, string namespaceValue)
        {
            // Borrow the scoped context's connection. Never construct another context, trigger
            // EF initialization, change its connection string, or dispose the borrowed connection.
            return new NamespaceReservationSqlEvidenceSession(
                () => _context.Database.Connection,
                () => _context.Database.CurrentTransaction?.UnderlyingTransaction,
                _connectionGate,
                submitterKey,
                ownerKeys,
                namespaceValue,
                DateTime.UtcNow);
        }
    }
}