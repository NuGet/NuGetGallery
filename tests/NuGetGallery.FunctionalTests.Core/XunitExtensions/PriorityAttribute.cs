using System;
using Xunit;

namespace NuGetGallery.FunctionalTests
{
    [TestCaseOrderer("NuGetGallery.FunctionalTests.TestPriorityOrderer", "NuGetGallery.FunctionalTests.Core")]
    [AttributeUsage(AttributeTargets.Method)]
    public class PriorityAttribute : Attribute
    {
        public PriorityAttribute(int priority)
        {
            Priority = priority;
        }

        public int Priority { get; private set; }
    }
}