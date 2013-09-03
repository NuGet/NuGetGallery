using System.Collections.Generic;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class MigrationListViewModel
    {
        public ICollection<MigrationViewModel> Applied { get; set; }
        public ICollection<MigrationViewModel> Pending { get; set; }
        public ICollection<MigrationViewModel> Available { get; set; }
    }
}