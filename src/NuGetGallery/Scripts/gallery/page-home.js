$(function () {
    'use strict';

    var failHandler = function (jqXHR, textStatus, errorThrown) {
        viewModel.message(window.nuget.formatString(errorThrown));
    };

    function updateStat(observable, unparsedValue) {
        var parsedValue = parseInt(unparsedValue);
        if (!isNaN(parsedValue)) {
            observable(parsedValue);
        }
    }

    var viewModel = {
        packageDownloads: ko.observable(0),
        packageVersions: ko.observable(0),
        uniquePackages: ko.observable(0),

        modalTitle: ko.observable(''),
        message: ko.observable(''),
        feedbackText: ko.observable(''),
        showEnable2FADialog: ko.observable(false),

        sendFeedback: function () {
            viewModel.message("");

            var feedbackText = viewModel.feedbackText();
            if (!feedbackText) {
                viewModel.message("Please enter feedback.");
                return;
            }

            if (feedbackText.length > 1000) {
                viewModel.message("Please limit the feedback to 1000 characters.");
                return;
            }

            var obj = {
                feedback: feedbackText
            };

            window.nuget.addAjaxAntiForgeryToken(obj);

            $.ajax({
                url: feedbackUrl,
                dataType: 'json',
                type: 'POST',
                data: obj,
                success: function (data) {
                    if (data.success) {
                        emitAIMetric("Enable2FAModalProvidedFeedback");
                        viewModel.dismissModalOrGetFeedback(false);
                    } else {
                        viewModel.message(data.message);
                    }
                }
            })
            .fail(failHandler);
        },

        dismissModalOrGetFeedback: function (showFeedback) {
            if (showFeedback) {
                viewModel.resetViewModel();
                viewModel.setupFeedbackView();
            }
            else {
                emitAIMetric('Enable2FAModalDismissed');
                $("#popUp2FAModal").modal('hide');
            }
        },

        show2FAModal: function () {
            viewModel.resetViewModel();
            viewModel.setupEnable2FAView();
        },

        setupFeedbackView: function () {
            viewModel.modalTitle('We would like to hear your feedback!');
            viewModel.showEnable2FADialog(false);
        },

        setupEnable2FAView: function () {
            viewModel.modalTitle('Enable Two-factor authentication (2FA)');
            viewModel.showEnable2FADialog(true);
        },

        resetViewModel: function () {
            viewModel.message('');
            viewModel.modalTitle('');
            viewModel.feedbackText('');
            viewModel.setupEnable2FAView();
        }
    };

    viewModel.label = ko.computed(function () {
        return 'NuGet.org has ' +
            viewModel.packageDownloads() + ' package download' + (viewModel.packageDownloads() != 1 ? 's' : '') + ', ' +
            viewModel.packageVersions() + ' package version' + (viewModel.packageVersions() != 1 ? 's' : '') + ', and ' +
            viewModel.uniquePackages() + ' unique package' + (viewModel.uniquePackages() != 1 ? 's' : '') + '.';
    });

    function showModal() {
        $("#popUpModal").modal({
            show: true,
            focus: true
        });
    }

    function show2FAModal() {
        viewModel.setupEnable2FAView();
        emitAIMetric("Enable2FAModalShown");
        $("#popUp2FAModal").modal({
            show: true,
            focus: true
        });
    }

    function emitAIMetric(metricName) {
        window.nuget.sendAiMetric(metricName, 1, {});
    }

    function updateStats() {
        $.get('/stats/totals')
            .done(function (data) {
                updateStat(viewModel.packageDownloads, data['Downloads']);
                updateStat(viewModel.packageVersions, data['TotalPackages']);
                updateStat(viewModel.uniquePackages, data['UniquePackages']);
            })
            .fail(function () {
                // Fail silently.
            });
    }

    ko.bindingHandlers.animateNumber = {
        init: function (element, valueAccessor) {
            var value = ko.unwrap(valueAccessor());
            $(element).data('value', value);
            $(element).text(value.toLocaleString());
        },
        update: function (element, valueAccessor) {
            var oldValue = $(element).data('value');
            var newValue = ko.unwrap(valueAccessor());
            $(element).data('value', newValue);

            $({ value: oldValue }).animate({ value: newValue }, {
                duration: 250,
                easing: 'swing',
                step: function () {
                    $(element).text(Math.floor(this.value).toLocaleString());
                },
                done: function () {
                    $(element).text(newValue.toLocaleString());
                }
            });
        }
    };

    if (!window.nuget.supportsSvg()) {
        $('.circuit-board').hide();
    }

    ko.applyBindings(viewModel);
    updateStats();

    if (window.showModal) {
        showModal();
    }

    if (window.show2FAModal) {
        show2FAModal();
    }
});
