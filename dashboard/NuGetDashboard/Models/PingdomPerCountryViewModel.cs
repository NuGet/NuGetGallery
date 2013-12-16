using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetDashboard.Models
{
    public class PingdomPerCountryViewModel
    {
        public PingdomPerCountryViewModel(string name, string status, int lastResponseTime)
            {
                this.Name = name;
                this.Status = status;
                this.LastResponseTime = lastResponseTime;                
            }

            public string Name;          
            public int LastResponseTime;
            public string Status;
            
    }
}