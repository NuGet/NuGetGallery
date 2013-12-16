using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NuGetDashboard.Utilities;

namespace NuGetDashboard.Models
{
    /// <summary>
    /// This class represents report per month for pingdom.
    /// </summary>
    public class PingdomMonthlyReportViewModel
    {
        public PingdomMonthlyReportViewModel(string name, double totalUpTime, int downTime, int outages, int avgResponseTime, string month)
        {
            this.Name = name;
            this.AvgResponseTime = avgResponseTime;
           // this.totalUpTime = Math.Round((totalUpTime / (DateTimeUtility.GetSecondsForDays(DateTimeUtility.GetDaysInMonth(month)))) * 100, 2);
            this.totalUpTime = totalUpTime;
            this.Outages = outages;
            TimeSpan t = TimeSpan.FromSeconds(downTime);

            DownTime = string.Format("{0:D2}h:{1:D2}m:{2:D2}s",
                            t.Hours,
                            t.Minutes,
                            t.Seconds
                            );
        }

        public string Name;
        public int Outages;
        public string DownTime;
        public int AvgResponseTime;
        public double totalUpTime;

    }
}