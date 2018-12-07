(function () {
    'use strict';

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
                : defaultPackageIconUrl;
            this.PackageUrl = packageItem.PackageUrl;
            this.EditUrl = packageItem.EditUrl;
            this.SetRequiredSignerUrl = packageItem.SetRequiredSignerUrl;
            this.ManageOwnersUrl = packageItem.ManageOwnersUrl;
            this.RequiredSignerMessage = packageItem.RequiredSignerMessage;
            this.AllSigners = packageItem.AllSigners;
            this.ShowRequiredSigner = packageItem.ShowRequiredSigner;
            this.ShowTextBox = packageItem.ShowTextBox;
            this.CanEditRequiredSigner = packageItem.CanEditRequiredSigner;
            this.DeleteUrl = packageItem.DeleteUrl;
            this.CanEdit = packageItem.CanEdit;
            this.CanManageOwners = packageItem.CanManageOwners;
            this.CanDelete = packageItem.CanDelete;

            this.FormattedDownloadCount = ko.pureComputed(function () {
                return ko.unwrap(this.DownloadCount).toLocaleString();
            }, this);

            var requiredSigner = null;

            if (packageItem.RequiredSigner) {
                if (this.ShowTextBox) {
                    requiredSigner = packageItem.RequiredSigner.OptionText;
                } else {
                    requiredSigner = packageItem.RequiredSigner.Username;
                }
            }

            this._requiredSigner = ko.observable(requiredSigner);

            this.RequiredSigner = ko.pureComputed({
                read: function () {
                    return self._requiredSigner();
                },
                write: function (newSignerUsername) {
                    var message = self.GetConfirmationMessage(packageItem, newSignerUsername);

                    if (confirm(message)) {
                        var url = packageItem.SetRequiredSignerUrl.replace("{username}", newSignerUsername);

                        $.ajax({
                            method: 'POST',
                            url: url,
                            cache: false,
                            data: window.nuget.addAjaxAntiForgeryToken({}),
                            complete: function (xhr, textStatus) {
                                switch (xhr.status) {
                                    case 200:
                                    case 409:
                                        break;

                                    default:
                                        break;
                                }
                            }
                        });

                        self._requiredSigner(newSignerUsername);
                    }
                }
            });

            this.Visible = ko.observable(true);

            this.UpdateVisibility = function (ownerFilter) {
                var visible = ownerFilter === allPackagesFilter;
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
                var url = packageIconUrlFallback;
                return "this.src='" + url + "'; this.onerror = null;";
            }, this);

            this.GetConfirmationMessage = function (packageItem, newSignerUsername) {
                var signerHasCertificate;
                var signerIsAny = !newSignerUsername;
                var message;

                for (var index in packageItem.AllSigners) {
                    var signer = packageItem.AllSigners[index];

                    if (signer.Username === newSignerUsername) {
                        signerHasCertificate = signer.HasCertificate;
                        break;
                    }
                }

                if (signerIsAny) {
                    var anySignerWithNoCertificate = false;
                    var anySignerWithCertificate = false;

                    for (var index in packageItem.AllSigners) {
                        var signer = packageItem.AllSigners[index];

                        if (signer.HasCertificate) {
                            anySignerWithCertificate = true;
                        } else {
                            anySignerWithNoCertificate = true;
                        }

                        if (!signer.Username) {
                            newSignerUsername = signer.OptionText;
                        }
                    }

                    message = window.nuget.formatString(strings_RequiredSigner_ThisAction, newSignerUsername) + "\n\n";

                    if (anySignerWithCertificate && anySignerWithNoCertificate) {
                        message += strings_RequiredSigner_AnyWithMixedResult;
                    } else if (anySignerWithCertificate) {
                        message += strings_RequiredSigner_AnyWithSignedResult;
                    } else {
                        message += strings_RequiredSigner_AnyWithUnsignedResult;
                    }
                } else {
                    message = window.nuget.formatString(strings_RequiredSigner_ThisAction, newSignerUsername) + "\n\n";

                    if (signerHasCertificate) {
                        message += window.nuget.formatString(strings_RequiredSigner_OwnerHasAtLeastOneCertificate, newSignerUsername);
                    } else {
                        message += window.nuget.formatString(strings_RequiredSigner_OwnerHasNoCertificate, newSignerUsername);
                    }
                }

                message += "\n\n" + strings_RequiredSigner_Confirm;

                return message;
            };

            this.OnRequiredSignerChange = function (packageItem, event) {
                // If the change was cancelled, we need to reset the selected value.
                event.currentTarget.value = self._requiredSigner();
            };
        }

        function PackagesListViewModel(managePackagesViewModel, type, listed) {
            var self = this;

            this.ManagePackagesViewModel = managePackagesViewModel;
            this.Type = type;
            this.Listed = listed;

            this.CurrentPackagesPage = ko.observable([]);

            this.SetPackagePage = function (page) {
                self.VisiblePackagePageNumber(page);
            };

            this.VisiblePackagesCount = ko.observable(0);
            this.VisibleDownloadCount = ko.observable(0);
            this.VisiblePackagesHeading = ko.pureComputed(function () {
                return formatPackagesData(
                    ko.unwrap(self.VisiblePackagesCount()),
                    ko.unwrap(self.VisibleDownloadCount()));
            }, this);

            this.VisiblePackagePagesCount = ko.pureComputed(function () {
                return Math.ceil(self.VisiblePackagesCount() / pageSize);
            }, this);

            this.VisiblePackagePageNumber = ko.observable(0);

            this.PackagePages = ko.pureComputed(function () {
                var pages = [];
                for (var i = 0; i < self.VisiblePackagePagesCount(); i++) {
                    pages.push(i);
                }

                return pages;
            }, this);

            this.VisiblePackagePageIdentity = ko.pureComputed(function () {
                return {
                    ownerFilter: self.ManagePackagesViewModel.OwnerFilter().Username,
                    page: self.VisiblePackagePageNumber()
                };
            }, this);
            this.VisiblePackagePageIdentity.subscribe(function (identity) {
                self.GetPackagePage(identity.ownerFilter, identity.page);
            }, this);

            this.GetPackagePage = function (ownerFilter, page) {
                $.ajax({
                    url: getPagedPackagesUrl + '?page=' + page + '&listed=' + listed + (ownerFilter === allPackagesFilter ? '' : '&username=' + ownerFilter),
                    dataType: 'json',
                    success: function (data) {
                        self.VisiblePackagesCount(data.totalCount);
                        self.VisibleDownloadCount(data.totalDownloadCount);

                        var packages = $.map(data.packages, function (item) {
                            return new PackageListItemViewModel(self, item);
                        });

                        self.CurrentPackagesPage(packages);
                    },
                    error: function (jqXhr, textStatus, errorThrown) {
                        // self.VisiblePackagePageNumber(page);
                    }
                });
            };

            this.GetPackagePage(allPackagesFilter, this.VisiblePackagePageNumber());

            this.ManagePackagesViewModel.OwnerFilter.subscribe(function () {
                self.VisiblePackagePageNumber(0);
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
                var visible = ownerFilter === allPackagesFilter;
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
                : defaultPackageIconUrl;
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
                var visible = ownerFilter === allPackagesFilter;
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
                var url = packageIconUrlFallback;
                return "this.src='" + url + "'; this.onerror = null;";
            }, this);
        }

        function OwnerRequestsListViewModel(managePackagesViewModel, requests, showReceived, showSent) {
            var self = this;

            this.ManagePackagesViewModel = managePackagesViewModel;
            this.Requests = $.map(requests, function (data) {
                return new OwnerRequestsItemViewModel(self, data, showReceived, showSent);
            });
            this.VisibleRequestsCount = ko.observable(0);
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

            this.OwnerFilter = ko.observable(this.Owners[0]);
            // More filter entries than 'All' and current user
            if (this.Owners.length > 2) {
                $("#ownerFilter").removeClass("hidden");
            }

            this.ListedPackages = new PackagesListViewModel(this, "published", true);
            this.UnlistedPackages = new PackagesListViewModel(this, "unlisted", false);
            this.ReservedNamespaces = new ReservedNamespaceListViewModel(this, initialData.ReservedNamespaces);
            this.RequestsReceived = new OwnerRequestsListViewModel(this, initialData.RequestsReceived, true, false);
            this.RequestsSent = new OwnerRequestsListViewModel(this, initialData.RequestsSent, false, true);
        }

        // Immediately load initial expander data
        showInitialReservedNamespaceData("#namespaces-data", initialData.ReservedNamespaces);
        showInitialOwnerRequestsData("#requests-received-data", initialData.RequestsReceived);
        showInitialOwnerRequestsData("#requests-sent-data", initialData.RequestsSent);

        // Set up the data binding.
        var managePackagesViewModel = new ManagePackagesViewModel(initialData);
        ko.applyBindings(managePackagesViewModel, document.body);
    });

})();