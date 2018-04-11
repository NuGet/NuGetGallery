﻿// Global utility script for NuGetGallery
/// <reference path="jquery-1.11.0.js" />

var addAjaxAntiForgeryToken = (function () {
    'use strict';

    // Shared function for adding an anti-forgery token defined by ViewHelpers.AjaxAntiForgeryToken to an ajax request
    return function (data) {
        var $field = $("#AntiForgeryForm input[name=__RequestVerificationToken]");
        data["__RequestVerificationToken"] = $field.val();
        return data;
    };
}());

(function (window, $, undefined) {
    'use strict';

    $(function () {
        // Export an object with global config data
        var app = $(document.documentElement).data();
        window.app = app;

        if (!app.root) {
            app.root = '';
        }

        attachPlugins();

        sniffClickonce();

        addOutboundTrackingEvent();
    });

	// Add validator that ensures provided value is NOT equal to a specified value.
    $.validator.addMethod('notequal', function (value, element, params) {
        return value !== params;
    });

    // Add unobtrusive adapters for mandatory checkboxes and notequal values
    $.validator.unobtrusive.adapters.addBool("mandatory", "required");
    $.validator.unobtrusive.adapters.addSingleVal('notequal', 'disallowed');

    // Source: https://developers.google.com/analytics/devguides/collection/analyticsjs/sending-hits
    function createFunctionWithTimeout(callback, opt_timeout) {
        var called = false;
        function fn() {
            if (!called) {
                called = true;
                callback();
            }
        }
        setTimeout(fn, opt_timeout || 1000);
        return fn;
    };

    function addOutboundTrackingEvent() {
        // Handle Google analytics tracking event on specific links.
        $.each($('a[data-track]'), function () {
            $(this).click(function (e) {
                var href = $(this).attr('href');
                var category = $(this).attr('data-track');
                if (ga && href && category) {
                    e.preventDefault();
                    ga('send', 'event', category, 'click', href, {
                        'transport': 'beacon',
                        'hitCallback': createFunctionWithTimeout(function () {
                            document.location = href;
                        })
                    });
                }
            });
        });
    }

    function padInt(i, size) {
        var s = i.toString();
        while (s.length < size) s = "0" + s;
        return s;
    }

    function hasMimeTypeSupport(desiredMime) {
        var mimes = window.navigator.mimeTypes,
            hasSupport = false;

        for (var i = 0; i < mimes.length; i++) {
            var mime = mimes[i];

            if (mime.type == desiredMime) {
                hasSupport = true;
            }
        }

        return hasSupport;
    };

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
            var expanded = $self.attr('aria-expanded') == 'true';
            var oldText = $self.text();

            $self.attr('aria-expanded', expanded ? 'false' : 'true');
            $self.text(toggletext);
            data.toggletext = oldText;

            $target.slideToggle('fast');
        });
        $('.s-confirm[data-confirm]').delegate('', 'click', function (evt) {
            if (!confirm($(this).data().confirm)) {
                evt.stopPropagation();
            }
        });
        if (!hasMimeTypeSupport("application/x-shockwave-flash")) {
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
            $(this).text(utc.getFullYear() + "-" + padInt(utc.getMonth() + 1, 2) + "-" + padInt(utc.getDate(), 2) + " " + hrs + ":" + padInt(utc.getMinutes(), 2) + " " + ampm + " (UTC)");
        });
        $('time.timeago').timeago();
    }

    function sniffClickonce() {
        var userAgent = window.navigator.userAgent.toUpperCase(),
            hasNativeDotNet = userAgent.indexOf('.NET CLR 3.5') >= 0;

        if (hasNativeDotNet) {
            $('body').removeClass('s-noclickonce');
        }
    }
})(window, jQuery);
