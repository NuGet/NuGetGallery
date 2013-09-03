using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetOperations.FunctionalTests.Helpers;
using NuGetGallery.Operations;


namespace NuGetOperations.FunctionalTests
{
    /// <summary>
    /// Tests the aggregate stats task.
    /// </summary>
    [TestClass]
    public class AggregateStatsTaskTest
    {
        [TestMethod]
        [Description("Does a download and invokes the aggregatestatstask and validates if the total download count has increased")]
        public void AggregateStatsTest()
        {
           int PrevCount =  DataBaseHelper.GetTotalDownCount();
           string packageId = DateTime.Now.Ticks.ToString();
           PackageHelper.UploadNewPackage(packageId);
           PackageHelper.DownloadPackage(packageId);
           TaskInvocationHelper.InvokeAggregateStatsTask();
           int currentCount = DataBaseHelper.GetTotalDownCount();
           //Instead of increasing by 1, we need to get the count of new rows in package statistics.
           Assert.IsTrue((currentCount == PrevCount + 1), "The total download count did not increase by one after executing the aggregate stats task");
        }
    }
}
