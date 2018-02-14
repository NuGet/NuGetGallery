(function () {
    'use strict';

    $(function () {
        function addAntiForgeryToken(data) {
            var $field = $("#AntiForgeryForm input[name=__RequestVerificationToken]");
            data["__RequestVerificationToken"] = $field.val();
        }

        var configureSection = function (prefix) {
            var containerId = prefix + "-container";
            $("#cancel-" + prefix).click(function (e) {
                // Collapse the container.
                $("#" + containerId).collapse('hide');

                // Prevent navigation.
                e.preventDefault();

                // Reset the form.
                var formElement = $("#" + containerId + " form")[0];
                if (formElement) {
                    formElement.reset();
                }

                // Clear values.
                $("#" + containerId + " input[type='text']").val("");
                $("#" + containerId + " input[type='password']").val("");

                // Reset the validation state.
                if (formElement) {
                    window.nuget.resetFormValidation(formElement);
                }
            });
        }

        function OrganizationMemberViewModel(parent, member) {
            var self = this;

            this.OrganizationViewModel = parent;

            this.Username = member.Username;
            this.EmailAddress = member.EmailAddress;
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

            this.DeleteMember = function () {
                if (!window.nuget.confirmEvent("Are you sure you want to delete member '" + self.Username + "'?")) {
                    return;
                }

                // Build the request.
                var data = {
                    accountName: self.OrganizationViewModel.AccountName,
                    memberName: self.Username
                };
                addAntiForgeryToken(data);

                // Send the request.
                $.ajax({
                    url: self.OrganizationViewModel.DeleteMemberUrl,
                    type: 'POST',
                    dataType: 'json',
                    data: data,
                    success: function () {
                        parent.Error(null);
                        parent.Members.remove(self);
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
                    isAdmin: self.IsAdmin()
                };
                addAntiForgeryToken(data);

                // Send the request.
                $.ajax({
                    url: self.OrganizationViewModel.UpdateMemberUrl,
                    type: 'POST',
                    dataType: 'json',
                    data: data,
                    success: function () {
                        parent.Error(null);
                        parent.UpdateMemberCounts();
                    },
                    error: function (jqXHR, textStatus, errorThrown) {
                        var error = "Unknown error when trying to update member '" + self.Username + "'.";
                        if (jqXHR.responseText) {
                            error = jqXHR.responseJSON;
                        }
                        parent.Error(error);
                    }
                });
            };
        }

        function ManageOrganizationViewModel(initialData) {
            var self = this;

            this.AccountName = initialData.AccountName;
            this.AddMemberUrl = initialData.AddMemberUrl;
            this.UpdateMemberUrl = initialData.UpdateMemberUrl;
            this.DeleteMemberUrl = initialData.DeleteMemberUrl;
            this.ProfileUrlTemplate = initialData.ProfileUrlTemplate;

            this.NewMemberUsername = ko.observable();

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
            this.AddMember = function () {
                // Build the request.
                var data = {
                    accountName: self.AccountName,
                    memberName: self.NewMemberUsername(),
                    isAdmin: self.AddMemberRole() == self.RoleNames()[0]
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

        // Set up the section expanders.
        for (var i in sections) {
            configureSection(sections[i]);
        }

        // Set up the data binding.
        var manageOrganizationViewModel = new ManageOrganizationViewModel(initialData);
        ko.applyBindings(manageOrganizationViewModel, document.body);
    });

})();