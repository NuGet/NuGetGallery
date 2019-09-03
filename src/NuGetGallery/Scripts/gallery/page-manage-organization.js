(function () {
    'use strict';

    $(function () {
        function addAntiForgeryToken(data) {
            var $field = $("#AntiForgeryForm input[name=__RequestVerificationToken]");
            data["__RequestVerificationToken"] = $field.val();
        }

        function OrganizationMemberViewModel(parent, member) {
            var self = this;

            this.OrganizationViewModel = parent;

            this.Username = member.Username;
            this.IsAdmin = ko.observable(member.IsAdmin);
            this.SelectedRole = ko.pureComputed({
                read: function () {
                    return self.IsAdmin()
                        ? self.OrganizationViewModel.RoleNames()[0]
                        : self.OrganizationViewModel.RoleNames()[1]
                },
                write: function (value) {
                    this.IsAdmin(value == self.OrganizationViewModel.RoleNames()[0])
                },
                owner: this
            });
            this.IsCurrentUser = member.IsCurrentUser;
            this.ProfileUrl = parent.ProfileUrlTemplate.replace('{username}', this.Username);
            this.GravatarUrl = member.GravatarUrl;
            this.Pending = member.Pending;
            this.Expired = member.Expired;

            this.DeleteMember = function () {
                if (self.IsCurrentUser) {
                    if (!window.nuget.confirmEvent("Are you sure you want to leave this organization? You will no longer be able to manage it or its packages if you do.")) {
                        return;
                    }
                } else {
                    if (!window.nuget.confirmEvent("Are you sure you want to delete member '" + self.Username + "'?")) {
                        return;
                    }
                }

                // Build the request.
                var data = {
                    accountName: self.OrganizationViewModel.AccountName,
                    memberName: self.Username
                };
                addAntiForgeryToken(data);

                // Send the request.
                $.ajax({
                    url: self.Pending ? self.OrganizationViewModel.CancelMemberRequestUrl : self.OrganizationViewModel.DeleteMemberUrl,
                    type: 'POST',
                    dataType: 'json',
                    data: data,
                    success: function () {
                        parent.Error(null);
                        parent.Members.remove(self);
                        if (self.IsCurrentUser) {
                            document.location.href = "/";
                        }
                    },
                    error: function (jqXHR, textStatus, errorThrown) {
                        var error = "Unknown error when trying to delete member '" + self.Username + "'.";
                        if (jqXHR.responseText) {
                            error = jqXHR.responseJSON;
                        }
                        parent.Error(error);
                    }
                });
            };

            this.ToggleIsAdmin = function () {
                // Build the request.
                var data = {
                    accountName: self.OrganizationViewModel.AccountName,
                    memberName: self.Username,
                    isAdmin: self.IsAdmin(),
                };
                addAntiForgeryToken(data);

                // Send the request.
                $.ajax({
                    url: self.Pending ? self.OrganizationViewModel.AddMemberUrl : self.OrganizationViewModel.UpdateMemberUrl,
                    type: 'POST',
                    dataType: 'json',
                    data: data,
                    success: function (data) {
                        parent.Error(null);
                        parent.UpdateMemberCounts();
                        if (self.IsCurrentUser) {
                            window.location.reload();
                        }
                    },
                    error: function (jqXHR, textStatus, errorThrown) {
                        var error = "Unknown error when trying to update member '" + self.Username + "'.";
                        if (jqXHR.responseText) {
                            error = jqXHR.responseJSON;
                        }
                        parent.Error(error);
                        self.IsAdmin(!self.IsAdmin())
                    }
                });
            };
        }

        function ManageOrganizationViewModel(initialData) {
            var self = this;

            this.AccountName = initialData.AccountName;
            this.AddMemberUrl = initialData.AddMemberUrl;
            this.CancelMemberRequestUrl = initialData.CancelMemberRequestUrl;
            this.UpdateMemberUrl = initialData.UpdateMemberUrl;
            this.DeleteMemberUrl = initialData.DeleteMemberUrl;
            this.ProfileUrlTemplate = initialData.ProfileUrlTemplate;

            this.NewMemberUsername = ko.observable();
            this.NewMemberRoleDescription = ko.pureComputed(function () {
                if (self.AddMemberIsAdmin()) {
                    return "An administrator can manage the organization's memberships and its packages.";
                } else {
                    return "A collaborator can manage the organization's packages but cannot manage the organization's memberships.";
                }
            });

            this.AdminCount = ko.observable();
            this.CollaboratorCount = ko.observable();
            this.MembersLabel = ko.pureComputed(function () {
                if (!(self.AdminCount() && self.CollaboratorCount())) {
                    self.UpdateMemberCounts();
                }
                return self.AdminCount()
                    + " Admin" + (self.AdminCount() == 1 ? "" : "s")
                    + " | "
                    + self.CollaboratorCount()
                    + " Collaborator" + (self.CollaboratorCount() == 1 ? "" : "s")
            });

            this.Error = ko.observable();

            var members = $.map(initialData.Members, function (data) {
                return new OrganizationMemberViewModel(self, data);
            });
            this.Members = ko.observableArray(members);

            this.UpdateMemberCounts = function () {
                var admins = 0;
                var collaborators = 0;
                self.Members().forEach(function (data) {
                    if (data.IsAdmin()) {
                        admins += 1;
                    }
                    else {
                        collaborators += 1;
                    }
                });
                self.AdminCount(admins);
                self.CollaboratorCount(collaborators);
            };
            this.Members.subscribe(function () {
                self.UpdateMemberCounts();
            });

            this.RoleNames = ko.observableArray(["Administrator", "Collaborator"]);

            this.AddMemberRole = ko.observable(this.RoleNames()[1]);
            this.AddMemberIsAdmin = ko.pureComputed(function () {
                return self.AddMemberRole() == self.RoleNames()[0];
            });
            this.AddMember = function () {
                if (!self.NewMemberUsername()) {
                    self.Error("You must specify a user to add as a member.");
                    return;
                }

                // Check if the member already exists.
                var memberExists = false;
                self.Members().forEach(function (member) {
                    if (member.Username.toLocaleLowerCase() === self.NewMemberUsername().toLocaleLowerCase()) {
                        memberExists = true;
                    }
                });

                if (memberExists) {
                    var error = "'" + self.NewMemberUsername() + "' is already a member or pending member.";
                    self.Error(error);
                    return;
                }

                // Build the request.
                var data = {
                    accountName: self.AccountName,
                    memberName: self.NewMemberUsername(),
                    isAdmin: self.AddMemberIsAdmin()
                };
                addAntiForgeryToken(data);

                // Send the request.
                $.ajax({
                    url: self.AddMemberUrl,
                    type: 'POST',
                    dataType: 'json',
                    data: data,
                    success: function (data) {
                        self.Error(null);

                        // Remove any duplicates of this user in the UI.
                        // This can happen if a user makes multiple add requests before a response is received.
                        self.Members.remove(function (member) {
                            return member.Username.toLocaleLowerCase() === data.Username.toLocaleLowerCase();
                        });

                        self.Members.push(new OrganizationMemberViewModel(self, data));
                        self.NewMemberUsername(null);
                    },
                    error: function (jqXHR, textStatus, errorThrown) {
                        var error = "Unknown error when trying to add a new member.";
                        if (jqXHR.responseText) {
                            error = jqXHR.responseJSON;
                        }
                        self.Error(error);
                    }
                });
            };
        }

        // Set up the data binding.
        var manageOrganizationViewModel = new ManageOrganizationViewModel(initialData);
        var manageOrganizationMembersContainer = $('#manage-organization-members-container');
        ko.applyBindings(manageOrganizationViewModel, manageOrganizationMembersContainer[0]);

        // Set up the Add Member textbox to submit upon pressing enter.
        var newMemberTextbox = $("#new-member-textbox");
        newMemberTextbox.keydown(function (event) {
            if (event.which == 13 && newMemberTextbox.val()) {
                manageOrganizationViewModel.AddMember();
            }
        });
    });

})();