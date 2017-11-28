﻿$(function () {
    'use strict';

    window.nuget.configureExpander(
        "package-owners",
        "ChevronRight",
        "Current Owners",
        "ChevronDown",
        "Current Owners");

    window.nuget.configureExpander(
        "add-owner",
        "ChevronRight",
        "Add Owner",
        "ChevronDown",
        "Add Owner");

    var failHandler = function (jqXHR, textStatus, errorThrown) {
        viewModel.message('An unexpected error occurred! "' + errorThrown + '"');
    };

    var viewModel = {
        package: { id: packageId },
        isUserAnAdmin: isUserAnAdmin,
        owners: ko.observableArray([]),
        newOwnerUsername: ko.observable(''),
        newOwnerMessage: ko.observable(''),
        confirmation: ko.observable(''),
        policyMessage: ko.observable(''),

        message: ko.observable(''),

        IsAllowedToRemove: function (owner) {
            return true;
        },

        resetAddOwnerConfirmation: function () {
            viewModel.confirmation('');
            viewModel.policyMessage('');
        },

        cancelAddOwnerConfirmation: function () {
            viewModel.resetAddOwnerConfirmation();
            $("#newOwnerUserName").focus();
        },

        confirmAddOwner: function () {
            viewModel.message("");

            var newUsername = viewModel.newOwnerUsername();
            if (!newUsername) {
                viewModel.message("Please enter a valid user name.");
                return;
            }

            var existingOwner = ko.utils.arrayFirst(
                viewModel.owners(),
                function (owner) { return owner.name().toUpperCase() == newUsername.toUpperCase() });

            if (existingOwner) {
                viewModel.message("The user '" + newUsername + "' is already an owner or pending owner of this package.");
                return;
            }

            $.ajax({
                url: getAddPackageOwnerConfirmationUrl + '?id=' + viewModel.package.id + '&username=' + newUsername,
                cache: false,
                dataType: 'json',
                type: 'GET',
                success: function (data) {
                    if (data.success) {
                        viewModel.confirmation(data.confirmation);
                        viewModel.policyMessage(data.policyMessage);
                    }
                    else {
                        viewModel.message(data.message);
                    }
                }
            })
            .error(failHandler);
        },

        addOwner: function () {
            var newUsername = viewModel.newOwnerUsername();
            var message = viewModel.newOwnerMessage();

            var ownerInputModel =
                {
                    username: newUsername,
                    id: viewModel.package.id,
                    message: message
                };

            $.ajax({
                url: addPackageOwnerUrl,
                dataType: 'json',
                type: 'POST',
                data: window.nuget.addAjaxAntiForgeryToken(ownerInputModel),
                success: function (data) {
                    if (data.success) {
                        var newOwner = new Owner(data.name, data.profileUrl, data.imageUrl, /* pending */ true, data.current);
                        viewModel.owners.push(newOwner);

                        // reset the Username textbox
                        viewModel.newOwnerUsername('');
                        viewModel.newOwnerMessage('');

                        // when an operation succeeds, always clear the error message
                        viewModel.message('');
                        viewModel.resetAddOwnerConfirmation();
                    }
                    else {
                        viewModel.message(data.message);
                    }
                }
            })
            .error(failHandler);
        },

        removeOwner: function (item) {
            if (item.current) {
                if (!confirm("Are you sure you want to remove yourself from the owners?")) {
                    return;
                }
            }

            $.ajax({
                url: removePackageOwnerUrl,
                dataType: 'json',
                type: 'POST',
                data: window.nuget.addAjaxAntiForgeryToken({
                    id: viewModel.package.id,
                    username: item.name(),
                }),
                success: function (data) {
                    if (data.success) {
                        if (item.current) {
                            window.location.href = packageUrl;
                        }

                        viewModel.owners.remove(item);

                        // when an operation succeeds, always clear the error message
                        viewModel.message('');
                    } else {
                        viewModel.message(data.message);
                    }
                }
            })
            .error(failHandler);
        }
    };

    viewModel.IsAllowedToRemove = function (owner) {
        if (isUserAnAdmin.toLocaleLowerCase() === "True".toLocaleLowerCase()
            || owner.pending()) {
            return true;
        };

        if (this.owners().length < 2) {
            return false;
        }

        var approvedOwner = 0;
        var currentOwnerOwnsNamespace = false;
        var namespaceOwnerCount = 0;

        ko.utils.arrayForEach(this.owners(), function (owner) {
            if (owner.pending() === false) {
                approvedOwner++;
            }

            if (owner.current === true) {
                currentOwnerOwnsNamespace = owner.isNamespaceOwner();
            }

            if (owner.isNamespaceOwner() === true) {
                namespaceOwnerCount++;
            }
        });

        return approvedOwner >= 2
            && (!owner.isNamespaceOwner()
                || (currentOwnerOwnsNamespace
                    && namespaceOwnerCount >= 2));
    };

    ko.applyBindings(viewModel);

    // Load initial owners.
    $.ajax({
        url: getPackageOwnersUrl + '?id=' + viewModel.package.id,
        cache: false,
        dataType: 'json',
        type: 'GET',
        success: function (data) {
            viewModel.owners($.map(data, function (item) { return new Owner(item.name, item.profileUrl, item.imageUrl, item.pending, item.current, item.isNamespaceOwner); }));
        }
    })
    .error(failHandler);

    function Owner(name, profileUrl, imageUrl, pending, current, isNamespaceOwner) {
        this.name = ko.observable(name);
        this.profileUrl = ko.observable(profileUrl);
        this.imageUrl = ko.observable(imageUrl);
        this.pending = ko.observable(pending);
        this.current = current;
        this.isNamespaceOwner = ko.observable(isNamespaceOwner);
    }
});
