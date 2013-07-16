using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    /// <summary>
    /// This class records the data corresponding to a particular 'edit' of a 'package version', 
    /// as created either by package upload or user doing a package edit operation on that version
    /// </summary>
    public class PackageMetadata
    {
        [Key]
        public int Key { get; set; }

        public Package Package { get; set; }
        public int PackageKey { get; set; }

        [StringLength(64)]
        public string EditName { get; set; }

        public DateTime Timestamp { get; set; } // Time this metadata was generated
        public bool IsCompleted { get; set; } // Flag so the worker knows if processing is required.
        public int TriedCount { get; set; } // Count so that the worker role can tell itself not to retry this edit forever.

        #region Package Data (will be partiallly filled until finished by worker role)
        public string Authors { get; set; }

        public string Copyright { get; set; }

        public string Description { get; set; }

        public string Hash { get; set; }

        public string HashAlgorithm { get; set; }

        public string IconUrl { get; set; }

        // Going to store the LicenseURL here, but disallow editing it in the UI in case we want to revisit the decision of should license URLs be editable
        public string LicenseUrl { get; set; }

        public long PackageFileSize { get; set; }

        public string ProjectUrl { get; set; }

        public string ReleaseNotes { get; set; }

        public string Summary { get; set; }

        public string Tags { get; set; }

        public string Title { get; set; }
        #endregion
    }
}