using System;
using System.Web.Mvc;

namespace NuGetGallery
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public sealed class HintAttribute : Attribute, IMetadataAware
    {
        readonly object _typeId = new Object();

        public HintAttribute(string hint)
        {
            Hint = hint;
        }

        public string Hint { get; private set; }

        public void OnMetadataCreated(ModelMetadata metadata)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException("metadata");
            }
            metadata.AdditionalValues["Hint"] = Hint;
        }

        public override object TypeId
        {
            get
            {
                return _typeId;
            }
        }
    }
}