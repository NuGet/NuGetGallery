(function () {
    'use strict';

    function showInitialPackagesData(dataSelector, packagesList) {
        var downloadsCount = 0;
        $.each(packagesList, function () { downloadsCount += this.TotalDownloadCount; });
        $(dataSelector).text(formatPackagesData(packagesList.length, downloadsCount));
    }

    function formatPackagesData(packagesCount, downloadsCount) {
        return packagesCount.toLocaleString()
            + ' package' + (packagesCount === 1 ? '' : 's')
            + ' / '
            + downloadsCount.toLocaleString()
            + ' download' + (downloadsCount === 1 ? '' : 's');
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

        function showInitialReservedNamespaceData(dataSelector, namespacesList) {
            $(dataSelector).text(formatReservedNamespacesData(namespacesList.length));
        }

        function formatReservedNamespacesData(namespacesCount) {
            return namespacesCount.toLocaleString() + " namespace" + (namespacesCount === 1 ? '' : 's');
        }

        function ReservedNamespaceListItemViewModel(reservedNamespaceListViewModel, namespaceItem) {
            var self = this;

            this.ReservedNamespaceListViewModel = reservedNamespaceListViewModel;
            this.Pattern = namespaceItem.Pattern;
            this.SearchUrl = namespaceItem.SearchUrl;
            this.Owners = namespaceItem.Owners;
            this.IsPublic = namespaceItem.IsPublic;

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

        function ReservedNamespaceListViewModel(managePackagesViewModel, namespaces) {
            var self = this;

            this.ManagePackagesViewModel = managePackagesViewModel;
            this.Namespaces = $.map(namespaces, function (data) {
                return new ReservedNamespaceListItemViewModel(self, data);
            });
            this.VisibleNamespacesCount = ko.observable();
            this.VisibleNamespacesHeading = ko.pureComputed(function () {
                return formatReservedNamespacesData(ko.unwrap(self.VisibleNamespacesCount()));
            });

            this.ManagePackagesViewModel.OwnerFilter.subscribe(function (newOwner) {
                var namespacesCount = 0;
                for (var i in self.Namespaces) {
                    self.Namespaces[i].UpdateVisibility(newOwner.Username);
                    if (self.Namespaces[i].Visible()) {
                        namespacesCount++;
                    }
                }
                this.VisibleNamespacesCount(namespacesCount);
            }, this);
        }

        function showInitialOwnerRequestsData(dataSelector, requestsList) {
            $(dataSelector).text(formatOwnerRequestsData(requestsList.length));
        }

        function formatOwnerRequestsData(requestsCount) {
            return requestsCount.toLocaleString() + " request" + (requestsCount === 1 ? '' : 's');
        }

        function OwnerRequestsItemViewModel(ownerRequestsListViewModel, ownerRequestItem, showReceived, showSent) {
            var self = this;

            this.OwnerRequestsListViewModel = ownerRequestsListViewModel;
            this.Id = ownerRequestItem.Id;
            this.Requesting = ownerRequestItem.Requesting;
            this.New = ownerRequestItem.New;
            this.Owners = ownerRequestItem.Owners;
            this.PackageIconUrl = ownerRequestItem.PackageIconUrl
                ? ownerRequestItem.PackageIconUrl
                : this.OwnerRequestsListViewModel.ManagePackagesViewModel.DefaultPackageIconUrl;
            this.PackageUrl = ownerRequestItem.PackageUrl;
            this.CanAccept = ownerRequestItem.CanAccept;
            this.CanCancel = ownerRequestItem.CanCancel;
            this.ConfirmUrl = ownerRequestItem.ConfirmUrl;
            this.RejectUrl = ownerRequestItem.RejectUrl;
            this.CancelUrl = ownerRequestItem.CancelUrl;
            this.ShowReceived = showReceived;
            this.ShowSent = showSent;

            this.Visible = ko.observable(true);

            this.UpdateVisibility = function (ownerFilter) {
                var visible = ownerFilter === "All packages";
                if (!visible) {
                    if (self.ShowReceived && ownerFilter === self.New.Username) {
                        visible = true;
                    }

                    if (self.ShowSent) {
                        for (var i in self.Owners) {
                            if (ownerFilter === self.Owners[i].Username) {
                                visible = true;
                                break;
                            }
                        }
                    }
                }
                this.Visible(visible);
            };
            this.PackageIconUrlFallback = ko.pureComputed(function () {
                var url = this.OwnerRequestsListViewModel.ManagePackagesViewModel.PackageIconUrlFallback;
                return "this.src='" + url + "'; this.onerror = null;";
            }, this);
        }

        function OwnerRequestsListViewModel(managePackagesViewModel, requests, showReceived, showSent) {
            var self = this;

            this.ManagePackagesViewModel = managePackagesViewModel;
            this.Requests = $.map(requests, function (data) {
                return new OwnerRequestsItemViewModel(self, data, showReceived, showSent);
            });
            this.VisibleRequestsCount = ko.observable();
            this.VisibleRequestsHeading = ko.pureComputed(function () {
                return formatOwnerRequestsData(ko.unwrap(self.VisibleRequestsCount()));
            }, this);

            this.ManagePackagesViewModel.OwnerFilter.subscribe(function (newOwner) {
                var requestsCount = 0;
                for (var i in self.Requests) {
                    self.Requests[i].UpdateVisibility(newOwner.Username);
                    if (self.Requests[i].Visible()) {
                        requestsCount++;
                    }
                }
                this.VisibleRequestsCount(requestsCount);
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
            this.ReservedNamespaces = new ReservedNamespaceListViewModel(this, initialData.ReservedNamespaces);
            this.RequestsReceived = new OwnerRequestsListViewModel(this, initialData.RequestsReceived, true, false);
            this.RequestsSent = new OwnerRequestsListViewModel(this, initialData.RequestsSent, false, true);
        }

        // Immediately load initial expander data
        showInitialPackagesData("#listed-data", initialData.ListedPackages);
        showInitialPackagesData("#unlisted-data", initialData.UnlistedPackages);
        showInitialReservedNamespaceData("#namespaces-data", initialData.ReservedNamespaces);
        showInitialOwnerRequestsData("#requests-received-data", initialData.RequestsReceived);
        showInitialOwnerRequestsData("#requests-sent-data", initialData.RequestsSent);

        // Set up the data binding.
        var managePackagesViewModel = new ManagePackagesViewModel(initialData);
        ko.applyBindings(managePackagesViewModel, document.body);
    });

})();