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
        const stagingValidationModal = $('#staging-validation-modal');
        const stagingValidationModalTitle = $('#staging-validation-modal-title');
        const stagingValidationModalContent = $('.staging-validation-modal-content');

        stagingValidationModal.on('shown.bs.modal', function () {
            stagingValidationModalTitle.focus();
        });

        stagingValidationModal.on('hide.bs.modal', function () {
            stagingValidationModal.addClass('staging-validation-modal-closing');
        });

        stagingValidationModal.on('hidden.bs.modal', function () {
            stagingValidationModal.removeClass('staging-validation-modal-closing');
            $('html').removeClass('staging-validation-modal-open');
        });

        function StagedPackageViewModel(packageItem) {
            const self = this;

            this.Id = packageItem.Id;
            this.Version = packageItem.Version;
            this.Owner = packageItem.Owner;
            this.OwnerUrl = packageItem.OwnerUrl;
            this.Status = packageItem.Status;
            this.StatusClass = packageItem.StatusClass;
            this.HasValidationIssues = packageItem.HasValidationIssues;
            this.ValidationIssuesId = packageItem.ValidationIssuesId;
            this.CanManage = packageItem.CanManage;
            this.CanPromote = packageItem.CanPromote;
            this.ListedInputId = packageItem.ListedInputId;
            this.DownloadUrl = packageItem.DownloadUrl;
            this.ReplaceUrl = packageItem.ReplaceUrl;
            this.UpdateListedUrl = packageItem.UpdateListedUrl;
            this.PromoteUrl = packageItem.PromoteUrl;
            this.DeleteUrl = packageItem.DeleteUrl;

            this.Listed = ko.observable(packageItem.Listed);
            this.IsBusy = ko.observable(packageItem.Status === 'Promoting');
            this.IsSavingListed = ko.observable(false);
            this.ListedStatus = ko.observable('');
            this.IsDisabled = ko.pureComputed(function () {
                return self.IsBusy() || self.IsSavingListed();
            });

            this.UpdateListed = function (model, event) {
                const input = $(event.currentTarget);
                const form = input.closest('form');
                const previousValue = !self.Listed();

                self.IsSavingListed(true);
                self.ListedStatus('');

                $.ajax({
                    method: 'POST',
                    url: form.attr('action'),
                    cache: false,
                    data: form.serialize()
                })
                    .fail(function () {
                        self.Listed(previousValue);
                        self.ListedStatus('Not saved');
                    })
                    .always(function () {
                        self.IsSavingListed(false);
                    });
            };

            this.FollowLink = function (model, event) {
                if (self.IsBusy()) {
                    event.preventDefault();
                    return false;
                }

                return true;
            };

            this.ChooseReplacement = function (model, event) {
                event.preventDefault();
                if (!self.IsBusy()) {
                    $(event.currentTarget)
                        .siblings('.staging-replace-form')
                        .find('.staging-replace-input')
                        .trigger('click');
                }
            };

            this.Replace = function (model, event) {
                const input = event.currentTarget;
                if (input.files.length > 0) {
                    self.IsBusy(true);
                    input.form.submit();
                }
            };

            this.Promote = function (model, event) {
                event.preventDefault();
                const trigger = $(event.currentTarget);
                const message = 'Promote staged package ' + self.Id + ' ' + self.Version + '?';
                if (!self.IsBusy() && window.nuget.confirmEvent(message)) {
                    self.IsBusy(true);
                    trigger.siblings('.staging-promote-form')[0].submit();
                }
            };

            this.Delete = function (model, event) {
                event.preventDefault();
                const trigger = $(event.currentTarget);
                const message = 'Delete staged package ' + self.Id + ' ' + self.Version + '?';
                if (!self.IsBusy() && window.nuget.confirmEvent(message)) {
                    self.IsBusy(true);
                    trigger.siblings('.staging-delete-form')[0].submit();
                }
            };

            this.ShowValidationIssues = function () {
                const validationIssues = $('#' + self.ValidationIssuesId).html();

                $('html').addClass('staging-validation-modal-open');
                stagingValidationModalTitle.text('Validation errors for ' + self.Id);
                stagingValidationModalContent.html(validationIssues);
                return true;
            };
        }

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
            this.ManageUrl = packageItem.ManageUrl;
            this.SetRequiredSignerUrl = packageItem.SetRequiredSignerUrl;
            this.RequiredSignerMessage = packageItem.RequiredSignerMessage;
            this.AllSigners = packageItem.AllSigners;
            this.ShowRequiredSigner = packageItem.ShowRequiredSigner;
            this.ShowTextBox = packageItem.ShowTextBox;
            this.CanEditRequiredSigner = packageItem.CanEditRequiredSigner;
            this.IsVulnerable = packageItem.IsVulnerable;
            this.CanEdit = packageItem.CanEdit;
            this.CanManageOwners = packageItem.CanManageOwners;
            this.CanDelete = packageItem.CanDelete;
            this.VersionSortOrder = packageItem.VersionSortOrder;

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

            this.GetConfirmationMessage = function (packageItem, newSignerUsername) {
                var signerHasCertificate;
                var signerIsAny = !newSignerUsername;
                var message;

                var index, signer;
                for (index in packageItem.AllSigners) {
                    signer = packageItem.AllSigners[index];

                    if (signer.Username === newSignerUsername) {
                        signerHasCertificate = signer.HasCertificate;
                        break;
                    }
                }

                if (signerIsAny) {
                    var anySignerWithNoCertificate = false;
                    var anySignerWithCertificate = false;

                    for (index in packageItem.AllSigners) {
                        signer = packageItem.AllSigners[index];

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
            this.ManagePackageOwnershipUrl = ownerRequestItem.ManagePackageOwnershipUrl;
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
            this.StagedPackages = $.map(initialData.StagedPackages, function (packageItem) {
                return new StagedPackageViewModel(packageItem);
            });
            this.ReservedNamespaces = new ReservedNamespaceListViewModel(this, initialData.ReservedNamespaces);
            this.RequestsReceived = new OwnerRequestsListViewModel(this, initialData.RequestsReceived, true, false);
            this.RequestsSent = new OwnerRequestsListViewModel(this, initialData.RequestsSent, false, true);
        }

        function setupColumnSorting() {
            $('.sortable').click(function () {

                var table = $(this).parents('table').eq(0);
                var rows = table.find('tr:gt(0)').toArray().sort(comparer($(this).index()));
                this.asc = !this.asc;
                if (!this.asc) {
                    rows = rows.reverse();
                }
                for (var i = 0; i < rows.length; i++) {
                    table.append(rows[i]);
                }

                table.find('.sortable').each(function () {
                    var currentText = $(this).text();
                    var newText = currentText.replace(' ▲', '').replace(' ▼', '');
                    $(this).text(newText);
                });

                var columnText = $(this).text();
                $(this).text(columnText + " " + (this.asc ? "▼" : "▲"));

            })
            function comparer(index) {
                return function (a, b) {
                    var valA = getCellValue(a, index), valB = getCellValue(b, index);
                    return $.isNumeric(valA) && $.isNumeric(valB) ? valB - valA : valA.toString().localeCompare(valB);
                }
            }
            function getCellValue(row, index) {
                var td = $(row).children('td').eq(index);

                // check for the data-sortby attribute, if found, use that data to sort by
                var sortby = td.data('sortby');

                if (typeof sortby !== 'undefined') {
                    return sortby;
                }

                return td.text();
            }
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

        setupColumnSorting();

        $(".package-warning-icon").each(window.nuget.setPopovers);
    });

})();