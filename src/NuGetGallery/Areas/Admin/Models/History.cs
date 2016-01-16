namespace NuGetGallery.Areas.Admin.Models
{ 
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("History")]
    public partial class History : IEntity
    {
        [Key]
        public int Key { get; set; }

        public int IssueKey { get; set; }

        public int EditedBy { get; set; }

        public DateTime? EntryDate { get; set; }

        public string Comments { get; set; }

        [StringLength(200)]
        public string IssueStatus { get; set; }

        [StringLength(100)]
        public string AssignedTo { get; set; }
    }
}