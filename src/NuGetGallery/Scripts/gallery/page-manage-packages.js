(function () {
    'use strict';

    function formatPackagesData(packagesCount, downloadsCount) {
        if (packagesCount === null || downloadsCount === null) {
            return '';
        }

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

        function PackagesListViewModel(managePackagesViewModel, initialPackages, type, listed) {
            var self = this;

            this.ManagePackagesViewModel = managePackagesViewModel;
            this.Type = type;
            this.Listed = listed;

            this.CreatePackagePageIdentity = function (ownerFilter, page) {
                return {
                    ownerFilter: ownerFilter,
                    page: page
                };
            };

            this.DefaultPage = 0;
            this.PackagePageRange = 10;
            this.PackagePageNumber = ko.observable(self.DefaultPage);

            this.PackagePageIdentity = ko.pureComputed(function () {
                return self.CreatePackagePageIdentity(
                    self.ManagePackagesViewModel.OwnerFilter(),
                    self.PackagePageNumber());
            }, this);

            this.GetPackagePageIdentityCacheKey = function (identity) {
                return JSON.stringify(identity);
            };
            
            this.PackagesCache = {};
            this.PackagesCache[self.GetPackagePageIdentityCacheKey(self.PackagePageIdentity())] = ko.observable(initialPackages);
            this.IsCachingPage = {};
            this.GetPackagePage = function (identity, callback) {
                var packageCacheKey = self.GetPackagePageIdentityCacheKey(identity);
                var cachedPage = self.PackagesCache[packageCacheKey];
                if (cachedPage && cachedPage()) {
                    // The page has already been loaded.
                    callback && callback();
                    return;
                }
                
                var ownerFilter = identity.ownerFilter;
                var page = identity.page;
                var isCaching = self.IsCachingPage[packageCacheKey];
                if (isCaching) {
                    // This page is already being loaded.
                    // Don't try to load it again while it's already being loaded.
                    callback && callback();
                    return;
                }

                self.IsCachingPage[packageCacheKey] = true;
                var defaultPageValue = null;
                if (cachedPage) {
                    self.PackagesCache[packageCacheKey](defaultPageValue);
                } else {
                    self.PackagesCache[packageCacheKey] = ko.observable(defaultPageValue);
                }

                $.ajax({
                    url: getPagedPackagesUrl + '?page=' + page + '&listed=' + listed + (ownerFilter === allPackagesFilter ? '' : '&username=' + ownerFilter),
                    dataType: 'json',
                    success: function (data) {
                        self.PackagesCache[packageCacheKey](data);
                        self.IsCachingPage[packageCacheKey] = false;

                        callback && callback();
                    },
                    error: function () {
                        self.IsCachingPage[packageCacheKey] = false;

                        // Retry again in a couple seconds.
                        setTimeout(self.GetPackagePage.bind(identity, callback), 5000);
                    }
                });
            };

            this.GetCachedPackagePage = function (identity) {
                var packageCacheKey = self.GetPackagePageIdentityCacheKey(identity);
                self.GetPackagePage(identity);
                return self.PackagesCache[packageCacheKey]();
            };

            this.CachedCurrentPackagePage = ko.pureComputed(function () {
                var identity = self.PackagePageIdentity();
                return self.GetCachedPackagePage(identity);
            }, this);

            this.CachedDefaultPackagePage = ko.pureComputed(function () {
                var identity = self.CreatePackagePageIdentity(self.ManagePackagesViewModel.OwnerFilter(), self.DefaultPage);
                return self.GetCachedPackagePage(identity);
            }, this);
            
            this.CurrentPackagesPage = ko.pureComputed(function () {
                var cachedPackages = self.CachedCurrentPackagePage();
                if (cachedPackages) {
                    return $.map(cachedPackages.packages, function (item) {
                        return new PackageListItemViewModel(self, item);
                    });
                }
                
                return null; 
            }, this);

            this.PackagesCount = ko.pureComputed(function () {
                var cachedPackages = self.CachedCurrentPackagePage();
                if (!cachedPackages) {
                    // If the current page isn't cached, try to get the count from the default page.
                    cachedPackages = self.CachedDefaultPackagePage();
                }

                if (cachedPackages) {
                    return cachedPackages.totalCount;
                }
                
                return null;
            }, this);

            this.DownloadCount = ko.pureComputed(function () {
                var cachedPackages = self.CachedCurrentPackagePage();
                if (!cachedPackages) {
                    // If the current page isn't cached, try to get the count from the default page.
                    cachedPackages = self.CachedDefaultPackagePage();
                }

                if (cachedPackages) {
                    return cachedPackages.totalDownloadCount;
                }

                return null;
            }, this);

            this.SetPackagePage = function (page) {
                self.PackagePageNumber(page);
            };

            this.SetPackagePageFirst = function () {
                self.PackagePageNumber(0);
            };

            this.LastPackagePage = ko.pureComputed(function () {
                return self.PackagePagesCount() - 1;
            });
            this.SetPackagePageLast = function () {
                self.PackagePageNumber(self.LastPackagePage());
            };
            this.PackagesHeading = ko.pureComputed(function () {
                return formatPackagesData(
                    ko.unwrap(self.PackagesCount()),
                    ko.unwrap(self.DownloadCount()));
            }, this);

            this.PackagePagesCount = ko.pureComputed(function () {
                var packagesCount = self.PackagesCount();
                if (!packagesCount) {
                    // The first page always exists.
                    // If there are no packages that fit the criteria, the first page will exist, but it will be empty.
                    return 1;
                }

                return Math.ceil(packagesCount / pageSize);
            }, this);

            this.ManagePackagesViewModel.OwnerFilter.subscribe(function () {
                self.PackagePageNumber(self.DefaultPage);
                self.PreloadPagesForOwnerFilter();
            }, this);

            this.PackagePages = ko.pureComputed(function () {
                var pages = [];
                for (var i = 0; i < self.PackagePagesCount(); i++) {
                    pages.push(i);
                }

                return pages;
            }, this);

            this.PreloadPagesForOwnerFilter = function () {
                var ownerFilter = self.ManagePackagesViewModel.OwnerFilter();

                // Preload each page.
                var pages = self.PackagePages();
                var preloadPageByIndex = function (i) {
                    // Preload the pages in order, one at a time.
                    if (ownerFilter !== self.ManagePackagesViewModel.OwnerFilter()) {
                        // If the owner filter has changed, stop preloading pages.
                        return;
                    }
                    
                    if (i < pages.length) {
                        var identity = self.CreatePackagePageIdentity(ownerFilter, pages[i]);
                        self.GetPackagePage(identity, preloadPageByIndex.bind(self, i + 1));
                    }
                };

                preloadPageByIndex(0);
            };

            this.PreloadPagesForAllOwners = function () {
                var owners = self.ManagePackagesViewModel.Owners.slice(0);
                var preloadDefaultPageForOwnerByIndex = function (i) {
                    // Preload the pages in order, one owner at a time.
                    if (i < owners.length) {
                        var identity = self.CreatePackagePageIdentity(owners[i], self.DefaultPage);
                        self.GetPackagePage(identity, preloadDefaultPageForOwnerByIndex.bind(self, i + 1));
                    }
                };

                preloadDefaultPageForOwnerByIndex(0);
            };
        }

        function formatReservedNamespacesData(namespacesCount) {
            if (namespacesCount === null) {
                return '';
            }

            return namespacesCount.toLocaleString() + " namespace" + (namespacesCount === 1 ? '' : 's');
        }

        function ReservedNamespaceListItemViewModel(reservedNamespaceListViewModel, namespaceItem) {
            var self = this;

            this.ReservedNamespaceListViewModel = reservedNamespaceListViewModel;
            this.Pattern = namespaceItem.Pattern;
            this.SearchUrl = namespaceItem.SearchUrl;
            this.Owners = namespaceItem.Owners;
            this.IsPublic = namespaceItem.IsPublic;

            this.Visible = ko.pureComputed(function () {
                var ownerFilter = self.ReservedNamespaceListViewModel.ManagePackagesViewModel.OwnerFilter();
                var visible = ownerFilter === allPackagesFilter;
                if (!visible) {
                    for (var i in self.Owners) {
                        if (ownerFilter === self.Owners[i].Username) {
                            visible = true;
                            break;
                        }
                    }
                }

                return visible;
            }, this);
        }

        function ReservedNamespaceListViewModel(managePackagesViewModel, namespaces) {
            var self = this;

            this.ManagePackagesViewModel = managePackagesViewModel;
            this.Namespaces = $.map(namespaces, function (data) {
                return new ReservedNamespaceListItemViewModel(self, data);
            });
            this.VisibleNamespacesCount = ko.pureComputed(function () {
                var namespacesCount = 0;
                for (var i in self.Namespaces) {
                    if (self.Namespaces[i].Visible()) {
                        namespacesCount++;
                    }
                }

                return namespacesCount;
            }, this);
            this.VisibleNamespacesHeading = ko.pureComputed(function () {
                return formatReservedNamespacesData(ko.unwrap(self.VisibleNamespacesCount()));
            });
        }

        function formatOwnerRequestsData(requestsCount) {
            if (requestsCount === null) {
                return '';
            }

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

            this.Visible = ko.pureComputed(function () {ManagePackagesViewModel
                var ownerFilter = self.OwnerRequestsListViewModel.ManagePackagesViewModel.OwnerFilter();
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

                return visible;
            }, this);
            
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

            this.VisibleRequestsCount = ko.pureComputed(function () {
                var requestsCount = 0;
                for (var i in self.Requests) {
                    if (self.Requests[i].Visible()) {
                        requestsCount++;
                    }
                }

                return requestsCount;
            }, this);

            this.VisibleRequestsHeading = ko.pureComputed(function () {
                return formatOwnerRequestsData(ko.unwrap(self.VisibleRequestsCount()));
            }, this);
        }

        function ManagePackagesViewModel(initialData) {
            var self = this;

            this.Owners = initialData.Owners;
            this.OwnerFilter = ko.observable(this.Owners[0]);

            this.ListedPackages = new PackagesListViewModel(this, initialData.ListedPackages, "published", true);
            this.UnlistedPackages = new PackagesListViewModel(this, initialData.UnlistedPackages, "unlisted", false);
            this.ReservedNamespaces = new ReservedNamespaceListViewModel(this, initialData.ReservedNamespaces);
            this.RequestsReceived = new OwnerRequestsListViewModel(this, initialData.RequestsReceived, true, false);
            this.RequestsSent = new OwnerRequestsListViewModel(this, initialData.RequestsSent, false, true);
        }

        // Set up the data binding.
        var managePackagesViewModel = new ManagePackagesViewModel(initialData);
        ko.applyBindings(managePackagesViewModel, document.body);

        // Load the remaining pages for the current owner.
        managePackagesViewModel.ListedPackages.PreloadPagesForOwnerFilter();
        managePackagesViewModel.UnlistedPackages.PreloadPagesForOwnerFilter();

        // Load the default page for all owners
        managePackagesViewModel.ListedPackages.PreloadPagesForOwnerFilter();
        managePackagesViewModel.UnlistedPackages.PreloadPagesForOwnerFilter();
    });

})();