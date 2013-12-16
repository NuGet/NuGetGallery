using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Services.ServiceModel
{
    public class ServiceInstanceNameFacts
    {
        public class TheGetSetCurrentMethods
        {
            [Fact]
            public async Task ChildTasksCanSeeParentCurrentName()
            {
                // Arrange
                var name = new ServiceInstanceName(
                    new ServiceHostName(
                        new DatacenterName(
                            "test",
                            42),
                        "testhost"),
                    "testservice",
                    1);
                ServiceInstanceName.SetCurrent(name);

                // Act
                ServiceInstanceName[] names = new ServiceInstanceName[4];
                await Task.WhenAll(Enumerable.Range(0, 4).Select(i =>
                    Task.Factory.StartNew(() =>
                    {
                        names[i] = ServiceInstanceName.GetCurrent();
                    })).ToArray());

                // Assert
                Assert.Equal(names[0], names[1]);
                Assert.Equal(names[0], names[2]);
                Assert.Equal(names[0], names[3]);
            }

            [Fact]
            public async Task ChangesMadeInASubTaskAreNotSeenByParentTask()
            {
                // Arrange
                var name = new ServiceInstanceName(
                    new ServiceHostName(
                        new DatacenterName(
                            "test",
                            42),
                        "testhost"),
                    "testservice",
                    1);
                var bad = new ServiceInstanceName(
                    new ServiceHostName(
                        new DatacenterName(
                            "test",
                            42),
                        "testhost"),
                    "testservice",
                    1);
                ServiceInstanceName.SetCurrent(name);

                // Act
                await Task.Factory.StartNew(() => {
                    ServiceInstanceName.SetCurrent(bad);
                });

                // Assert
                Assert.Equal(name, ServiceInstanceName.GetCurrent());
            }
        }
    }
}