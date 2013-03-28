﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using NuGetGallery.Data.Model;

namespace NuGetGallery
{
    [Bind(Include = "Message")]
    public class ContactOwnersViewModel
    {
        public string PackageId { get; set; }

        public IEnumerable<User> Owners { get; set; }

        [Required(ErrorMessage = "Please enter a message.")]
        [StringLength(4000)]
        public string Message { get; set; }
    }
}