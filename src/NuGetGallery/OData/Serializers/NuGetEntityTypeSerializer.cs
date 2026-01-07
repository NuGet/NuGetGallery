// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Data.OData;
using System.Collections.Generic;
using System.Web.Http.OData;
using System.Web.Http.OData.Formatter.Serialization;

namespace NuGetGallery.OData.Serializers
{
    public class NuGetEntityTypeSerializer
        : ODataEntityTypeSerializer
    {
        private readonly string _contentType;
        private readonly IReadOnlyCollection<IFeedPackageAnnotationStrategy> _annotationStrategies;

        public NuGetEntityTypeSerializer(ODataSerializerProvider serializerProvider)
            : base(serializerProvider)
        {
            _contentType = "application/zip";
            _annotationStrategies = new List<IFeedPackageAnnotationStrategy>
            {
                new V1FeedPackageAnnotationStrategy(_contentType),
                new V2FeedPackageAnnotationStrategy(_contentType)
            };
        }

        public override ODataEntry CreateEntry(SelectExpandNode selectExpandNode, EntityInstanceContext entityInstanceContext)
        {
            var entry = base.CreateEntry(selectExpandNode, entityInstanceContext);

            foreach (var annotationStrategy in _annotationStrategies)
            {
                if (annotationStrategy.CanAnnotate(entityInstanceContext.EntityInstance))
                {
                    annotationStrategy.Annotate(entityInstanceContext.Request, entry, entityInstanceContext.EntityInstance);
                }
            }
            
            return entry;
        }

        public string ContentType
        {
            get { return _contentType; }
        }
    }
}