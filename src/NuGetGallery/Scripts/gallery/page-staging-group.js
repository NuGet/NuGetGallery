// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

(function () {
    'use strict';

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

    function StagedPackageViewModel(packageItem, searchQuery) {
        const self = this;

        this.Id = packageItem.Id;
        this.Version = packageItem.Version;
        this.Status = packageItem.Status;
        this.StatusClass = packageItem.StatusClass;
        this.UploadedDate = packageItem.UploadedDate;
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
        this.SearchText = `${packageItem.Id} ${packageItem.Version}`.toLowerCase();

        this.Listed = ko.observable(packageItem.Listed);
        this.IsBusy = ko.observable(packageItem.Status === 'Promoting');
        this.IsSavingListed = ko.observable(false);
        this.ListedStatus = ko.observable('');
        this.IsDisabled = ko.pureComputed(function () {
            return self.IsBusy() || self.IsSavingListed();
        });
        this.Visible = ko.pureComputed(function () {
            const query = searchQuery().trim().toLowerCase();
            return query.length === 0 || self.SearchText.indexOf(query) !== -1;
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
            const message = `Promote staged package ${self.Id} ${self.Version}?`;
            if (!self.IsBusy() && window.nuget.confirmEvent(message)) {
                self.IsBusy(true);
                trigger.siblings('.staging-promote-form')[0].submit();
            }
        };

        this.Delete = function (model, event) {
            event.preventDefault();
            const trigger = $(event.currentTarget);
            const message = `Delete staged package ${self.Id} ${self.Version}?`;
            if (!self.IsBusy() && window.nuget.confirmEvent(message)) {
                self.IsBusy(true);
                trigger.siblings('.staging-delete-form')[0].submit();
            }
        };

        this.ShowValidationIssues = function () {
            const validationIssues = $(`#${self.ValidationIssuesId}`).html();

            $('html').addClass('staging-validation-modal-open');
            stagingValidationModalTitle.text(`Validation errors for ${self.Id}`);
            stagingValidationModalContent.html(validationIssues);
            return true;
        };
    }

    function StagingGroupViewModel(data) {
        const self = this;

        this.SearchQuery = ko.observable('');
        this.Packages = data.Packages.map(function (packageItem) {
            return new StagedPackageViewModel(packageItem, self.SearchQuery);
        });
        this.VisiblePackageCount = ko.pureComputed(function () {
            return self.Packages.filter(function (packageItem) {
                return packageItem.Visible();
            }).length;
        });
        this.VisiblePackageSummary = ko.pureComputed(function () {
            const visibleCount = self.VisiblePackageCount();
            const totalCount = self.Packages.length;
            const totalText = `${totalCount} staged package${totalCount === 1 ? '' : 's'}`;
            if (self.SearchQuery().trim().length === 0) {
                return totalText;
            }

            return `Showing ${visibleCount} of ${totalText}`;
        });
        this.Search = function () {
            return false;
        };
    }

    const page = document.getElementById('staging-group');
    if (page) {
        ko.applyBindings(new StagingGroupViewModel(window.stagingGroupData), page);
    }
})();
