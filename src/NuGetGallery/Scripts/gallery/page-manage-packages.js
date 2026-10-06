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
            this.Status = namespaceItem.Status;
            this.RejectionReason = namespaceItem.RejectionReason;

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
            this.Namespaces = ko.observableArray($.map(namespaces, function (data) {
                return new ReservedNamespaceListItemViewModel(self, data);
            }));
            this.VisibleNamespacesCount = ko.observable(namespaces.length);
            this.VisibleNamespacesHeading = ko.pureComputed(function () {
                return formatReservedNamespacesData(ko.unwrap(self.VisibleNamespacesCount()));
            });

            this.UpdateVisibility = function (newOwner) {
                var namespacesCount = 0;
                var rows = self.Namespaces();
                for (var i = 0; i < rows.length; i++) {
                    rows[i].UpdateVisibility(newOwner ? newOwner.Username : 'All packages');
                    if (rows[i].Visible()) {
                        namespacesCount++;
                    }
                }
                self.VisibleNamespacesCount(namespacesCount);
            };

            this.Replace = function (items) {
                self.Namespaces($.map(items, function (data) {
                    return new ReservedNamespaceListItemViewModel(self, data);
                }));
                self.UpdateVisibility(managePackagesViewModel.OwnerFilter());
            };

            this.ManagePackagesViewModel.OwnerFilter.subscribe(this.UpdateVisibility);
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

        // The POST still awaits assessment on the server. Only the authenticated read
        // feed supplies table rows: never invent a persisted Pending or Reserved state.
        var namespaceForm = $('#namespace-request-form');
        var namespaceFeedback = $('#namespace-request-feedback');
        var namespaceSubmitting = false;
        var namespaceRefreshing = false;
        var namespaceRefreshAgain = false;
        var namespaceRefreshTimer;
        var namespaceRefreshBudget = 40;
        var submittedNamespacePattern;
        var namespaceRowsBeforeSubmission = 0;

        function showNamespaceFeedback(message, kind) {
            namespaceFeedback.find('[data-feedback-kind]').addClass('hidden');
            namespaceFeedback.find('[data-feedback-message]').text('');
            namespaceFeedback.find('[data-feedback-kind="' + kind + '"]').removeClass('hidden')
                .find('[data-feedback-message]').text(message);
            namespaceFeedback.removeClass('hidden');
        }

        function countSubmittedNamespaceRows(items) {
            return items.filter(function (row) {
                return row.Pattern.toLowerCase() === submittedNamespacePattern;
            }).length;
        }

        function scheduleNamespaceRefresh() {
            window.clearTimeout(namespaceRefreshTimer);
            var hasUnreservedRequests = managePackagesViewModel.ReservedNamespaces.Namespaces().some(function (row) {
                return row.Status === 'Pending' || row.Status === 'Approved';
            });
            if (namespaceRefreshBudget > 0 && (namespaceSubmitting || hasUnreservedRequests)) {
                namespaceRefreshTimer = window.setTimeout(refreshNamespaces, namespaceSubmitting ? 1500 : 15000);
            }
        }

        function refreshNamespaces() {
            window.clearTimeout(namespaceRefreshTimer);
            if (!initialData.NamespaceReservationStatusesUrl) {
                return;
            }
            if (namespaceRefreshing) {
                namespaceRefreshAgain = true;
                return;
            }
            if (document.hidden || !$('#namespaces-container').is(':visible')) {
                scheduleNamespaceRefresh();
                return;
            }
            namespaceRefreshing = true;
            namespaceRefreshBudget--;
            $.ajax({
                url: initialData.NamespaceReservationStatusesUrl,
                dataType: 'json',
                cache: false,
                timeout: 10000
            }).done(function (items) {
                if ($.isArray(items)) {
                    managePackagesViewModel.ReservedNamespaces.Replace(items);
                    // Confirm submission while assessment is still running only after
                    // a new matching row appears in the authenticated, saved-state feed.
                    // An existing request with the same name is not a new submission.
                    if (namespaceSubmitting && countSubmittedNamespaceRows(items) > namespaceRowsBeforeSubmission) {
                        showNamespaceFeedback(namespaceFeedback.attr('data-submitted-message'), 'success');
                    }
                }
            }).always(function () {
                namespaceRefreshing = false;
                if (namespaceRefreshAgain) {
                    namespaceRefreshAgain = false;
                    refreshNamespaces();
                } else {
                    scheduleNamespaceRefresh();
                }
            });
        }

        $('#namespaces-container').on('shown.bs.collapse', function () {
            namespaceRefreshBudget = 40;
            refreshNamespaces();
        });

        namespaceForm.on('submit', function (event) {
            event.preventDefault();
            if (namespaceSubmitting || ($.fn.valid && !namespaceForm.valid())) {
                return;
            }

            namespaceSubmitting = true;
            namespaceRefreshBudget = 120;
            submittedNamespacePattern = $.trim($('#namespace-request-namespace').val()).toLowerCase();
            namespaceRowsBeforeSubmission = countSubmittedNamespaceRows(managePackagesViewModel.ReservedNamespaces.Namespaces());
            var button = namespaceForm.find('button[type=submit]');
            var summary = namespaceForm.find('[data-valmsg-summary=true]');
            // MVC can omit an empty, global-errors-only validation summary. Create a
            // safe target so account/read-only/save errors are still visible via AJAX.
            if (!summary.length) {
                summary = $('<div>').attr({ 'data-valmsg-summary': 'true', role: 'alert' })
                    .addClass('text-danger').append($('<ul>')).prependTo(namespaceForm);
            }
            button.prop('disabled', true);
            summary.removeClass('validation-summary-errors').addClass('validation-summary-valid').find('ul').empty();
            namespaceForm.find('[data-valmsg-for]').empty().removeClass('field-validation-error').addClass('field-validation-valid');
            namespaceFeedback.addClass('hidden').find('[data-feedback-message]').text('');
            refreshNamespaces();

            $.ajax({
                url: namespaceForm.attr('action'),
                type: 'POST',
                data: namespaceForm.serialize(),
                dataType: 'json'
            }).done(function (result) {
                if (result.Success) {
                    showNamespaceFeedback(result.IsWarning ? result.Message : namespaceFeedback.attr('data-submitted-message'),
                        result.IsWarning ? 'warning' : 'success');
                } else {
                    showNamespaceFeedback('Please review the request errors.', 'danger');
                    summary.removeClass('validation-summary-valid').addClass('validation-summary-errors');
                    $.each(result.Errors || [], function (_, error) {
                        $.each(error.Messages, function (_, message) {
                            $('<li>').text(message).appendTo(summary.find('ul'));
                        });
                        namespaceForm.find('[data-valmsg-for]').filter(function () {
                            return $(this).attr('data-valmsg-for') === error.Key;
                        }).removeClass('field-validation-valid').addClass('field-validation-error').text(error.Messages.join(' '));
                    });
                }
            }).fail(function () {
                showNamespaceFeedback('We could not confirm the result of your submission. Check the table or refresh the page before retrying; your request may already have been saved.', 'warning');
            }).always(function () {
                namespaceSubmitting = false;
                button.prop('disabled', false);
                refreshNamespaces();
            });
        });

        scheduleNamespaceRefresh();

        setupColumnSorting();

        $(".package-warning-icon").each(window.nuget.setPopovers);
    });

})();