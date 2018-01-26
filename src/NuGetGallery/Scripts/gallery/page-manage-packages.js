(function () {
    'use strict';

    function showInitialPackagesData(dataSelector, packagesList) {
        var downloadsCount = 0;
        $.each(packagesList, function () { downloadsCount += this.TotalDownloadCount });
        $(dataSelector).text(formatPackagesData(packagesList.length, downloadsCount));
    }

    function formatPackagesData(packagesCount, downloadsCount) {
        return packagesCount.toLocaleString()
            + ' package' + (packagesCount == 1 ? '' : 's')
            + ' / '
            + downloadsCount.toLocaleString()
            + ' download' + (downloadsCount == 1 ? '' : 's');
    }

    $(function () {
        function PackageListItemViewModel(packagesListViewModel, packageItem) {
            var self = this;

            this.PackagesListViewModel = packagesListViewModel;
            this.Id = packageItem.Id;
            this.Owners = packageItem.Owners;
            this.DownloadCount = packageItem.TotalDownloadCount;
            this.LatestVersion = packageItem.LatestVersion;
            this.PackageIconUrl = packageItem.PackageIconUrl
                ? packageItem.PackageIconUrl
                : this.PackagesListViewModel.ManagePackagesViewModel.DefaultPackageIconUrl;
            this.PackageUrl = packageItem.PackageUrl;
            this.EditUrl = packageItem.EditUrl;
            this.ManageOwnersUrl = packageItem.ManageOwnersUrl;
            this.DeleteUrl = packageItem.DeleteUrl;
            this.CanEdit = packageItem.CanEdit;
            this.CanManageOwners = packageItem.CanManageOwners;
            this.CanDelete = packageItem.CanDelete;

            this.FormattedDownloadCount = ko.pureComputed(function () {
                return ko.unwrap(this.DownloadCount).toLocaleString();
            }, this);

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
            this.PackageIconUrlFallback = ko.pureComputed(function () {
                var url = this.PackagesListViewModel.ManagePackagesViewModel.PackageIconUrlFallback;
                return "this.src='" + url + "'; this.onerror = null;";
            }, this);
        }

        function PackagesListViewModel(managePackagesViewModel, type, packages) {
            var self = this;

            this.ManagePackagesViewModel = managePackagesViewModel;
            this.Type = type;
            this.Packages = $.map(packages, function (data) {
                return new PackageListItemViewModel(self, data);
            });
            this.VisiblePackagesCount = ko.observable();
            this.VisibleDownloadCount = ko.observable();
            this.VisiblePackagesHeading = ko.pureComputed(function () {
                return formatPackagesData(
                    ko.unwrap(self.VisiblePackagesCount()),
                    ko.unwrap(self.VisibleDownloadCount()));
            }, this);

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
            // More filter entries than 'All' and current user
            if (this.Owners.length > 2) {
                $("#ownerFilter").removeClass("hidden");
            }
            
            this.ListedPackages = new PackagesListViewModel(this, "published", initialData.ListedPackages);
            this.UnlistedPackages = new PackagesListViewModel(this, "unlisted", initialData.UnlistedPackages);
        }

        // Immediately load initial expander data
        showInitialPackagesData("#listed-data", initialData.ListedPackages);
        showInitialPackagesData("#unlisted-data", initialData.UnlistedPackages);

        // Set up the data binding.
        var managePackagesViewModel = new ManagePackagesViewModel(initialData);
        ko.applyBindings(managePackagesViewModel, document.body);
    });

})();