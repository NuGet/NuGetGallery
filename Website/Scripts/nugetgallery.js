// Global utility script for NuGetGallery
/// <reference path="jquery-1.6.2.js" />
(function (window, $, undefined) {
    $(function () {
        // Export an object with global config data
        var app = $(document.documentElement).data();
        window.app = app;

        // Get the service status
        $.get(app.root + 'api/v2/service-alert?cachebust=' + new Date().getTime())
         .done(function (data) {
             if (typeof data === 'string' && data.length > 0) {
                 $('#service-alert').html(data).slideDown('fast');
             }
             else {
                 $('#service-alert').slideUp('fast').html();
             }
         }); // If this fails, just silently show no status.
    });
})(window, jQuery);