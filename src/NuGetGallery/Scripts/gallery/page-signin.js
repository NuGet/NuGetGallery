$(function () {
    'use strict';
    $('#signin-assistance').click(function () {
        $('#signinAssistanceModal').modal({
            show: true
        });
    });

    var failHandler = function (jqXHR, textStatus, errorThrown) {
        viewModel.message(window.nuget.formatString(errorThrown));
    };

    var viewModel = {
        message: ko.observable(''),
        usernameForAssistance: ko.observable(''),
        formattedEmailAddress: ko.observable(''),
        inputEmailAddress: ko.observable(''),
        getUsername: ko.observable(true),
        getEmail: ko.observable(false),
        emailNotificationSent: ko.observable(false),

        getEmailAddress: function () {
            viewModel.message("");

            var username = viewModel.usernameForAssistance();
            if (!username) {
                viewModel.message("Please enter a valid username");
                return;
            }

            var obj = {
                username: username
            };

            window.nuget.addAjaxAntiForgeryToken(obj);

            $.ajax({
                url: signinAssistanceUrl,
                dataType: 'json',
                type: 'POST',
                data: obj,
                success: function (data) {
                    if (data.success) {
                        viewModel.formattedEmailAddress(data.EmailAddress);
                        viewModel.getUsername(false);
                        viewModel.getEmail(true);
                    } else {
                        viewModel.message(data.message);
                    }
                }
            })
            .fail(failHandler);
        },

        sendEmailNotification: function () {
            viewModel.message("");

            var username = viewModel.usernameForAssistance();
            var inputEmailAddress = viewModel.inputEmailAddress();
            if (!inputEmailAddress) {
                viewModel.message("Please enter a valid email address");
                return;
            }

            var obj = {
                username: username,
                providedEmailAddress: inputEmailAddress
            };

            window.nuget.addAjaxAntiForgeryToken(obj);

            $.ajax({
                url: signinAssistanceUrl,
                dataType: 'json',
                type: 'POST',
                data: obj,
                success: function (data) {
                    if (data.success) {
                        viewModel.getUsername(false);
                        viewModel.getEmail(false);
                        viewModel.emailNotificationSent(true);
                    } else {
                        viewModel.message(data.message);
                    }
                }
            })
            .fail(failHandler);
        },

        resetViewModel: function () {
            viewModel.message('');
            viewModel.usernameForAssistance('');
            viewModel.formattedEmailAddress('');
            viewModel.inputEmailAddress('');
            viewModel.getUsername(true);
            viewModel.getEmail(false);
            viewModel.emailNotificationSent(false);
        }
    };

    ko.applyBindings(viewModel);
});
