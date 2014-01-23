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
    public class DatacenterNameFacts
    {
        [Theory]
        [PropertyData("ValidDatacenterNames")]
        public void CorrectlyTryParses(string name, DatacenterName expected)
        {
            DatacenterName actual;
            Assert.True(DatacenterName.TryParse(name, out actual));
            Assert.Equal(expected, actual);
        }

        [Theory]
        [PropertyData("InvalidDatacenterNames")]
        public void CorrectlyFailsToParse(string name)
        {
            DatacenterName actual;
            Assert.False(DatacenterName.TryParse(name, out actual));
        }

        public static IEnumerable<object[]> ValidDatacenterNames
        {
            get
            {
                yield return new object[] { "qa-0", new DatacenterName("qa", 0) };
                yield return new object[] { "bloog_barg-124", new DatacenterName("bloog_barg", 124) };
            }
        }

        public static IEnumerable<object[]> InvalidDatacenterNames
        {
            get
            {
                yield return new object[] { "qa_DC0" };
                yield return new object[] { "qa DC 0" };
                yield return new object[] { "qa" };
                yield return new object[] { "qa_DCus" };
                yield return new object[] { "qa_DCus_flarg" };
            }
        }
    }
}
