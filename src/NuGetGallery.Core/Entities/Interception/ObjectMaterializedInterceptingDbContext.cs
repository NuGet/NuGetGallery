// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;

namespace NuGetGallery
{
    public class ObjectMaterializedInterceptingDbContext
        : DbContext
    {
        public ObjectMaterializedInterceptingDbContext(string connectionString)
            : base(connectionString)
        {
            ObjectContext.ObjectMaterialized += ObjectContextOnObjectMaterialized;
        }

        public ObjectMaterializedInterceptingDbContext(DbConnection sqlConnection, bool contextOwnsConnection)
            : base(sqlConnection, contextOwnsConnection)
        {
            ObjectContext.ObjectMaterialized += ObjectContextOnObjectMaterialized;
        }

        private void ObjectContextOnObjectMaterialized(object sender, ObjectMaterializedEventArgs objectMaterializedEventArgs)
        {
            ObjectMaterializedInterception.InterceptObjectMaterialized(objectMaterializedEventArgs.Entity);
        }

        protected ObjectContext ObjectContext
        {
            get { return ((IObjectContextAdapter)this).ObjectContext; }
        }

        protected override void Dispose(bool disposing)
        {
            ObjectContext.ObjectMaterialized -= ObjectContextOnObjectMaterialized;
            base.Dispose(disposing);
        }
    }
}