using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class UserProfileModel
    {
        public UserProfileModel(User user, List<PackageViewModel> allPackages, int pageIndex, int pageSize, UrlHelper url)
        {
            User = user;
            Username = user.Username;
            EmailAddress = user.EmailAddress;
            UnconfirmedEmailAddress = user.UnconfirmedEmailAddress;
            AllPackages = allPackages;
            TotalPackages = allPackages.Count;
            PackagePage = pageIndex;
            PackagePageSize = pageSize;
            
            TotalPackageDownloadCount = AllPackages.Sum(p => p.TotalDownloadCount);

            PackagePageTotalCount = (TotalPackages + PackagePageSize - 1) / PackagePageSize;

            var pager = new PreviousNextPagerViewModel<PackageViewModel>(allPackages, pageIndex, PackagePageTotalCount,
                page => url.User(user, page));

            Pager = pager;
            PagedPackages = AllPackages.Skip(PackagePageSize * pageIndex)
                                       .Take(PackagePageSize).ToList();
        }

        public int PackagePageTotalCount { get; private set; }
        public User User { get; private set; }
        public string Username { get; private set; }
        public string EmailAddress { get; private set; }
        public string UnconfirmedEmailAddress { get; set; }
        public ICollection<PackageViewModel> AllPackages { get; private set; }
        public ICollection<PackageViewModel> PagedPackages { get; private set; }
        public int TotalPackageDownloadCount { get; private set; }
        public int TotalPackages { get; private set; }
        public int PackagePage { get; private set; }
        public int PackagePageSize { get; private set; }
        public IPreviousNextPager Pager { get; private set; }
        public bool ShowAllPackages { get; set; }
    }
}