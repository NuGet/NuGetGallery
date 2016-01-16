namespace NuGetGallery.Areas.Admin.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class Issue : IEntity
    {
        [Key]
        public int Key { get; set; }

        [StringLength(50)]
        public string CreatedBy { get; set; }

        [DataType(DataType.Date)]
        public DateTime? CreatedDate { get; set; }

        [StringLength(1000)]
        public string IssueTitle { get; set; }

        public int? AssignedTo { get; set; }

        public int? IssueStatus { get; set; }

        public string Details { get; set; }

        public string Comments { get; set; }

        [DataType(DataType.Url)]
        public string SiteRoot { get; set; }

        [StringLength(300)]
        public string PackageID { get; set; }

        [StringLength(300)]
        public string PackageVersion { get; set; }

        [StringLength(100)]
        [DataType(DataType.EmailAddress)]
        public string OwnerEmail { get; set; }

        [StringLength(100)]
        public string Reason { get; set; }
    }
}