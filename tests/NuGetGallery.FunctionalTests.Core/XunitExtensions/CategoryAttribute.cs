using System;
using Xunit.Sdk;

namespace NuGetGallery.FunctionalTests
{
    /// <summary>
    /// Apply this attribute to your test method to specify a category.
    /// </summary>
    [TraitDiscoverer("NuGetGallery.FunctionalTests.CategoryDiscoverer", "NuGetGallery.FunctionalTests.Core")]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class CategoryAttribute
        : Attribute, ITraitAttribute
    {
        public CategoryAttribute(string category)
        {
            Category = category;
        }

        public string Category { get; private set; }
    }
}