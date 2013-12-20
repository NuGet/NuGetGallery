using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Services.Work
{
    public class JobDescriptionFacts
    {
        public class TheNameProperty
        {
            [Fact]
            public void GivenAJobWithClassNameEndingJob_ItReturnsThePartBeforeTheWordJob()
            {
                Assert.Equal("ATest", JobDescription.Create(typeof(ATestJob)).Name);
            }

            [Fact]
            public void GivenAJobWithClassNameNotEndingJob_ItReturnsTheWholeTypeName()
            {
                Assert.Equal("ATestJerb", JobDescription.Create(typeof(ATestJerb)).Name);
            }

            [Fact]
            public void GivenAJobWithAttribute_ItReturnsTheNameFromTheAttribute()
            {
                Assert.Equal("ATestJob", JobDescription.Create(typeof(ATestJorb)).Name);
            }

            [Job("ATestJob")]
            public class ATestJorb : JobHandlerBase
            {
                public override System.Diagnostics.Tracing.EventSource GetEventSource()
                {
                    throw new NotImplementedException();
                }

                protected internal override Task<InvocationResult> Invoke()
                {
                    throw new NotImplementedException();
                }
            }

            public class ATestJerb : JobHandlerBase
            {
                public override System.Diagnostics.Tracing.EventSource GetEventSource()
                {
                    throw new NotImplementedException();
                }

                protected internal override Task<InvocationResult> Invoke()
                {
                    throw new NotImplementedException();
                }
            }

            public class ATestJob : JobHandlerBase
            {
                public override System.Diagnostics.Tracing.EventSource GetEventSource()
                {
                    throw new NotImplementedException();
                }

                protected internal override Task<InvocationResult> Invoke()
                {
                    throw new NotImplementedException();
                }
            }
        }
    }
}
