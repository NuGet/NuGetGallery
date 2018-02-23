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

        getEmailAddress: function () {
            viewModel.message("");

            var username = viewModel.usernameForAssistance();
            if (!username) {
                viewModel.message(strings_InvalidUsername);
                return;
            }

            $.ajax({
                url: signinAssistanceUrl,
                dataType: 'json',
                type: 'POST',
                data: window.nuget.addAjaxAntiForgeryToken({
                    username: username
                }),
                success: function (data) {
                    if (data.success) {
                        formattedEmailAddress(data.EmailAddress);
                    } else {
                        viewModel.message(data.errorMessage);
                    }
                }
            })
            .error(failHandler);
        }
    };

    ko.applyBindings(viewModel);
});
