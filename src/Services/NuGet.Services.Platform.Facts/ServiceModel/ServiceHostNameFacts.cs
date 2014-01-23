using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.ServiceModel;
using Xunit;
using Xunit.Extensions;

namespace NuGet.Services.ServiceModel
{
    public class ServiceHostNameFacts
    {
        [Theory]
        [PropertyData("ValidServiceNames")]
        public void CorrectlyTryParses(string name, ServiceHostName expected)
        {
            ServiceHostName actual;
            Assert.True(ServiceHostName.TryParse(name, out actual));
            Assert.Equal(expected, actual);
        }

        [Theory]
        [PropertyData("InvalidServiceNames")]
        public void CorrectlyFailsToParse(string name)
        {
            ServiceHostName actual;
            Assert.False(ServiceHostName.TryParse(name, out actual));
        }

        public static IEnumerable<object[]> ValidServiceNames
        {
            get
            {
                yield return new object[] { "qa-0-flarg_IN0", new ServiceHostName(new DatacenterName("qa", 0), "flarg", 0) };
                yield return new object[] { "qa-0-flarg_IN1", new ServiceHostName(new DatacenterName("qa", 0), "flarg", 1) };
                yield return new object[] { "qa-0-flarg_IN2", new ServiceHostName(new DatacenterName("qa", 0), "flarg", 2) };
                yield return new object[] { "qa-0-flarg_IN3", new ServiceHostName(new DatacenterName("qa", 0), "flarg", 3) };
                
            }
        }

        public static IEnumerable<object[]> InvalidServiceNames
        {
            get
            {
                yield return new object[] { "qa_DC0_flarg" };
                yield return new object[] { "qa DC 0" };
                yield return new object[] { "qa" };
                yield return new object[] { "qa_DC0" };
                yield return new object[] { "qa_useast_flarg" };
                yield return new object[] { "qa_DCus_flarg" };
                yield return new object[] { "qa_DCus_flarg" };
            }
        }
    }
}
