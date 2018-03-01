$(function () {
    'use strict';

    var _gravatarTimeout = 0;
    var _gravatarDelay = 500;
    var _gravatarIsUpdating = false;

    var _emailBox = $("#" + emailBoxGroupId + " > input");
    var _gravatar = $("#" + gravatarId);
    var _template = "https://secure.gravatar.com/avatar/{email}?s=512&r=g&d=retro";

    // When the email in the form changes, update the Gravatar displayed as the logo.
    // If the user is continuing to type, wait until they are done before updating the Gravatar.
    _emailBox.on("keyup", function (e) {
        if ((e.keyCode >= 46 && e.keyCode <= 90)        // delete, 0-9, a-z
            || (e.keyCode >= 96 && e.keyCode <= 111)    // numpad
            || (e.keyCode >= 186)                       // punctuation
            || (e.keyCode === 8))                        // backspace
        {
            clearTimeout(_gravatarTimeout);
            _gravatarTimeout = setTimeout(UpdateGravatar, _gravatarDelay);
        }
    });

    // When the user switches focus from the email textbox, update the Gravatar displayed as the logo.
    // This is because the user can change the email without typing (for example, by pasting contents from somewhere else).
    _emailBox.on("blur", function (e) {
        if (!_gravatarTimeout) {
            UpdateGravatar()
        }
    });

    function UpdateGravatar() {
        _gravatarTimeout = 0;
        var src = defaultImage;

        var email = _emailBox.val();
        if (email.match(emailValidationRegex)) {
            src = _template.replace("{email}", MD5(email));
        }

        _gravatar.attr("src", src);
    }
});