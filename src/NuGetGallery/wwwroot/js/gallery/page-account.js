(function () {
    'use strict';

    $(function () {
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

        // Set up the change password form.
        var $disablePasswordLogin = $("#ChangePassword_DisablePasswordLogin[type=checkbox]");
        function setPasswordLoginReadonly() {
            var disablePasswordLogin = $disablePasswordLogin[0];
            if (!disablePasswordLogin) {
                return;
            }

            var disabled = disablePasswordLogin.checked;
            $("#ChangePassword_OldPassword").prop('disabled', disabled);
            $("#ChangePassword_NewPassword").prop('disabled', disabled);
            $("#ChangePassword_VerifyPassword").prop('disabled', disabled);
        }
        $("#show-change-password-container").click(setPasswordLoginReadonly);
        $disablePasswordLogin.change(setPasswordLoginReadonly);
        setPasswordLoginReadonly();

        // Set up the section expanders.
        for (var i in sections) {
        	configureSection(sections[i]);
        }
    });
})();