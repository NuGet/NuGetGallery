// Global utility script for NuGetGallery
/// <reference path="jquery-1.6.4.js" />
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

        if (!app.root) {
            app.root = '';
        }

        // Get the service status
        checkServiceStatus();

        attachPlugins();

        attachTypeahead();
    });

	// Add validator that ensures provided value is NOT equal to a specified value.
    $.validator.addMethod('notequal', function (value, element, params) {
        return value !== params;
    });

    // Add unobtrusive adapters for mandatory checkboxes and notequal values
    $.validator.unobtrusive.adapters.addBool("mandatory", "required");
    $.validator.unobtrusive.adapters.addSingleVal('notequal', 'disallowed');

    function padInt(i, size) {
        var s = i.toString();
        while (s.length < size) s = "0" + s;
        return s;
    }

    // Attach script plugins
    function attachPlugins() {
        $('.s-toggle[data-show][data-hide]').delegate('', 'click', function (evt) {
            evt.preventDefault();
            var $hide = $($(this).data().hide);
            var $show = $($(this).data().show);
            $hide.fadeOut('fast', function () {
                $show.fadeIn('fast');
            });
        });
        $('.s-expand[data-target]').delegate('', 'click', function (evt) {
            evt.preventDefault();
            var $self = $(this);
            var data = $self.data();
            var $target = $(data.target);
            var toggletext = data.toggletext || $self.text();

            $target.slideToggle('fast', function () {
                var oldText = $self.text();
                $self.text(toggletext);
                data.toggletext = oldText;
            });
        });
        $('.s-confirm[data-confirm]').delegate('', 'click', function (evt) {
            if (!confirm($(this).data().confirm)) {
                evt.preventDefault();
            }
        });
        if(!navigator.mimeTypes["application/x-shockwave-flash"]) {
            $('.s-reqflash').remove();
        }
        $('.s-localtime[data-utc]').each(function () {
            var utc = new Date($(this).data().utc);
            var hrs = utc.getHours();
            var ampm = "AM";
            if (hrs >= 12) {
                if (hrs > 12) {
                    hrs = hrs - 12;
                }
                ampm = "PM";
            }
            $(this).text(utc.getFullYear() + "-" + padInt(utc.getMonth(), 2) + "-" + padInt(utc.getDate(), 2) + " " + hrs + ":" + padInt(utc.getMinutes(), 2) + " " + ampm + " Local Time");
        });
    }

    function attachTypeahead() {
        var engine = new Bloodhound({
            name: 'packages',
            remote: app.root + 'api/v2/typeahead?q=%QUERY',
            datumTokenizer: function (d) {
                return Bloodhound.tokenizers.whitespace(d.val);
            },
            queryTokenizer: Bloodhound.tokenizers.whitespace
        });
        engine.initialize()
            .done(function () {
                $('#searchBoxInput').typeahead({
                    minLength: 3,
                    highlight: true
                }, {
                    source: engine.ttAdapter(),
                    displayKey: 'Id',
                    templates: {
                        suggestion: function (context) {
                            return "<div class=\"typeahead-package-id\">" + context.Id + "</div>" +
                                "<div class=\"typeahead-package-summary\">" + (context.Title || context.Summary) + "</div>";
                        },
                        empty: "<div class=\"typeahead-empty\">No results</div>"
                    }
                });
            });
    }
})(window, jQuery);
