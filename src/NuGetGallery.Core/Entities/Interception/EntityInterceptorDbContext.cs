// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Objects;

namespace NuGetGallery
{
    public class EntityInterceptorDbContext
        : DbContext
    {
        
        public EntityInterceptorDbContext(string connectionString)
            : base(connectionString)
        {
            ObjectContext.ObjectMaterialized += ObjectContextOnObjectMaterialized;
        }

        private void ObjectContextOnObjectMaterialized(object sender, ObjectMaterializedEventArgs objectMaterializedEventArgs)
        {
            EntityInterception.InterceptObjectMaterialized(objectMaterializedEventArgs.Entity);
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