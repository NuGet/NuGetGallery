(function () {
    'use strict';
    
    $(function () {
        function PackageListItemViewModel(packagesListViewModel, packageItem) {
            var self = this;

            this.PackagesListViewModel = packagesListViewModel;
            this.Id = packageItem.Id;
            this.Owners = packageItem.Owners;
            this.DownloadCount = packageItem.TotalDownloadCount;
            this.LatestVersion = packageItem.LatestVersion;
            this.PackageIconUrl = (packageItem.PackageIconUrl)
                ? packageItem.PackageIconUrl
                : this.PackagesListViewModel.ManagePackagesViewModel.DefaultPackageIconUrl;
            this.PackageIconUrlFallback = this.PackagesListViewModel.ManagePackagesViewModel.PackageIconUrlFallback;
            this.PackageUrl = packageItem.PackageUrl;
            this.EditUrl = packageItem.EditUrl;
            this.ManageOwnersUrl = packageItem.ManageOwnersUrl;
            this.DeleteUrl = packageItem.DeleteUrl;
            this.CanEdit = packageItem.CanEdit;
            this.CanManageOwners = packageItem.CanManageOwners;
            this.CanDelete = packageItem.CanDelete;

            this.Visible = ko.observable(true);

            this.UpdateVisibility = function (ownerFilter) {
                var visible = ownerFilter === "All packages";
                if (!visible) {
                    for (var i in self.Owners) {
                        if (ownerFilter === self.Owners[i].Username) {
                            visible = true;
                            break;
                        }
                    }
                }
                this.Visible(visible);
            };
        }

        function PackagesListViewModel(managePackagesViewModel, type, packages) {
            var self = this;

            this.ManagePackagesViewModel = managePackagesViewModel;
            this.Type = type;
            this.Packages = $.map(packages, function (data) {
                return new PackageListItemViewModel(self, data)
            });
            this.VisiblePackagesCount = ko.observable();
            this.VisibleDownloadCount = ko.observable();

            this.ManagePackagesViewModel.OwnerFilter.subscribe(function (newOwner) {
                var packagesCount = 0;
                var downloadCount = 0;
                for (var i in self.Packages) {
                    self.Packages[i].UpdateVisibility(newOwner.Username);
                    if (self.Packages[i].Visible()) {
                        packagesCount++;
                        downloadCount += self.Packages[i].DownloadCount;
                    }
                }
                this.VisiblePackagesCount(packagesCount);
                this.VisibleDownloadCount(downloadCount);
            }, this);
        }

        function ManagePackagesViewModel(initialData) {
            var self = this;

            this.Owners = initialData.Owners;
            this.DefaultPackageIconUrl = initialData.DefaultPackageIconUrl;
            this.PackageIconUrlFallback = initialData.PackageIconUrlFallback;

            this.OwnerFilter = ko.observable();
            
            this.ListedPackages = new PackagesListViewModel(this, "published", initialData.ListedPackages);
            this.UnlistedPackages = new PackagesListViewModel(this, "unlisted", initialData.UnlistedPackages);
        }

        // Set up the data binding.
        var managePackagesViewModel = new ManagePackagesViewModel(initialData);
        ko.applyBindings(managePackagesViewModel, document.body);

        // Configure the expander headings.
        window.nuget.configureExpanderHeading("listed-container");
        window.nuget.configureExpanderHeading("unlisted-container");
        window.nuget.configureExpanderHeading("namespaces-container");
        window.nuget.configureExpanderHeading("requests-incoming-container");
        window.nuget.configureExpanderHeading("requests-outgoing-container");
    });

})();