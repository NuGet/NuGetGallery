// Global utility script for NuGetGallery
/// <reference path="jquery-1.6.2.js" />
(function (window, $, undefined) {
    function checkServiceStatus() {
        $.get(app.root + 'api/v2/service-alert?cachebust=' + new Date().getTime())
            .done(function (data) {
                if (typeof data === 'string' && data.length > 0) {
                    $('#service-alert').html(data).slideDown('fast');
                }
                else {
                    $('#service-alert').slideUp('fast').html();
                }
            }) // If this fails, just silently show no status.
    }

    $(function () {
        // Export an object with global config data
        var app = $(document.documentElement).data();
        window.app = app;

        // Get the service status
        checkServiceStatus();
    });

	// Add validator that ensures provided value is NOT equal to a specified value.
    $.validator.addMethod('notequal', function (value, element, params) {
        return value !== params;
    });

    // Add unobtrusive adapters for mandatory checkboxes and notequal values
    $.validator.unobtrusive.adapters.addBool("mandatory", "required");
    $.validator.unobtrusive.adapters.addSingleVal('notequal', 'disallowed');
})(window, jQuery);
