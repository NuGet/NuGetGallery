$(function () {
    'use strict';

    var failHandler = function (jqXHR, textStatus, errorThrown) {
        viewModel.message(window.nuget.formatString(errorThrown));
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
            if (isUserAnAdmin.toLocaleLowerCase() === "True".toLocaleLowerCase()
                || owner.pending()) {
                return true;
            }

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

                if (owner.grantsCurrentUserAccess) {
                    currentOwnerOwnsNamespace = currentOwnerOwnsNamespace || owner.isNamespaceOwner();
                }

                if (owner.isNamespaceOwner() === true) {
                    namespaceOwnerCount++;
                }
            });

            return approvedOwner >= 2
                && (!owner.isNamespaceOwner()
                    || (currentOwnerOwnsNamespace
                        && namespaceOwnerCount >= 2));
        },
        
        IsOnlyUserGrantingAccessToCurrentUser: function (item) {
            if (!item.grantsCurrentUserAccess) {
                // If the user we are trying to remove is not granting the current user access, they cannot be the only user granting access to the current user.
                return false;
            }

            if (isUserAnAdmin.toLocaleLowerCase() === "True".toLocaleLowerCase()) {
                // If user is an admin, removing any user will not remove their ability to manage package owners.
                return false;
            }

            var numUsersGrantingCurrentUserAccess = 0;

            ko.utils.arrayForEach(this.owners(), function (owner) {
                if (owner.grantsCurrentUserAccess) {
                    numUsersGrantingCurrentUserAccess++;
                }
            });

            return numUsersGrantingCurrentUserAccess < 2;
        },

        resetAddOwnerConfirmation: function () {
            viewModel.confirmation('');
            viewModel.policyMessage('');
        },

        cancelAddOwnerConfirmation: function () {
            viewModel.resetAddOwnerConfirmation();
            $("#newOwnerUserName").focus();
        },

        addOwner: function () {
            var newUsername = viewModel.newOwnerUsername();
            if (!newUsername) {
                viewModel.message(strings_InvalidUsername);
                return;
            }

            var existingOwner = ko.utils.arrayFirst(
                viewModel.owners(),
                function (owner) { return owner.name().toUpperCase() === newUsername.toUpperCase(); });

            if (existingOwner) {
                viewModel.message(window.nuget.formatString(strings_AlreadyPending, newUsername));
                return;
            }

            var message = viewModel.newOwnerMessage();

            var ownerInputModel =
                {
                    username: newUsername,
                    id: viewModel["package"].id,
                    message: message
                };

            $.ajax({
                url: addPackageOwnerUrl,
                dataType: 'json',
                type: 'POST',
                data: window.nuget.addAjaxAntiForgeryToken(ownerInputModel),
                success: function (data) {
                    if (data.success) {
                        var newOwner = new Owner(data.model);
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
            .fail(failHandler);
        },

        removeOwner: function (item) {
            var isOnlyUserGrantingAccessToCurrentUser = viewModel.IsOnlyUserGrantingAccessToCurrentUser(item);
            var isOnlyUserGrantingAccessToCurrentUserMessage = isOnlyUserGrantingAccessToCurrentUser ? strings_RemovingOwnership : "";

            if (item.isCurrentUserAdminOfOrganization) {
                if (!confirm(strings_RemovingOrganization + " " + isOnlyUserGrantingAccessToCurrentUserMessage)) {
                    return;
                }
            } else if (item.grantsCurrentUserAccess) {
                if (!confirm(strings_RemovingSelf + " " + isOnlyUserGrantingAccessToCurrentUserMessage)) {
                    return;
                }
            }

            $.ajax({
                url: removePackageOwnerUrl,
                dataType: 'json',
                type: 'POST',
                data: window.nuget.addAjaxAntiForgeryToken({
                    id: viewModel["package"].id,
                    username: item.name(),
                }),
                success: function (data) {
                    if (data.success) {
                        if (isOnlyUserGrantingAccessToCurrentUser) {
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
            .fail(failHandler);
        }
    };

    ko.applyBindings(viewModel, $(".page-manage-owners")[0]);

    // Load initial owners.
    $.ajax({
        url: getPackageOwnersUrl + '?id=' + viewModel["package"].id,
        cache: false,
        dataType: 'json',
        type: 'GET',
        success: function (data) {
            viewModel.owners($.map(data, function (item) { return new Owner(item); }));
        }
    })
    .fail(failHandler);

    function Owner(data) {
        this.name = ko.observable(data.Name);
        this.profileUrl = ko.observable(data.ProfileUrl);
        this.imageUrl = ko.observable(data.ImageUrl);
        this.pending = ko.observable(data.Pending);
        this.grantsCurrentUserAccess = data.GrantsCurrentUserAccess;
        this.isCurrentUserAdminOfOrganization = data.IsCurrentUserAdminOfOrganization;
        this.isNamespaceOwner = ko.observable(data.IsNamespaceOwner);
    }
});
