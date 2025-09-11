$(function () {
    'use strict';

    // Constants following the established pattern
    var ConfirmRemoveMessage = "Are you sure you want to remove this sponsorship link?";
    var AddErrorMessage = "An error occurred while adding the sponsorship URL. Please try again.";
    var RemoveErrorMessage = "An error occurred while removing the sponsorship URL. Please try again.";
    var EmptyUrlMessage = "Please enter a URL.";
    var RemoveSuccessMessage = "Sponsorship link removed successfully.";
    var AddSuccessMessage = "URL added successfully.";
    var RemovingMessage = "Removing sponsorship link...";

    // Set up sponsorship links section following the established viewModel pattern
    var viewModel = {
        message: ko.observable(''),
        successMessage: ko.observable(''),
        newSponsorshipUrl: ko.observable(''),
        confirmation: ko.observable(false),
        sponsorshipLinks: ko.observableArray([]),

        addSponsorshipLink: function (data, event) {
            // Prevent form submission
            if (event && event.preventDefault) {
                event.preventDefault();
            }
            
            var url = viewModel.newSponsorshipUrl().trim();

            // Clear previous error messages
            viewModel.message('');

            // Basic empty check - server handles all validation including domain, format, etc.
            if (!url) {
                viewModel.message(EmptyUrlMessage);
                return false;
            }

            // Submit to server for validation and processing
            var packageId = $('input[name="id"]').val() || window.nuget.packageId;
            
            $.ajax({
                url: window.nuget.urls.addSponsorshipUrl,
                type: 'POST',
                dataType: 'json',
                data: window.nuget.addAjaxAntiForgeryToken({
                    id: packageId,
                    sponsorshipUrl: url
                }),
                success: function (data) {
                    if (data.success) {
                        // Use the validated URL returned from server
                        var urlToAdd = data.validatedUrl || url;
                        
                        var newSponsorshipLink = new SponsorshipLink({
                            Url: urlToAdd,
                            IsDomainAccepted: data.isDomainAccepted || false
                        });
                        viewModel.sponsorshipLinks.push(newSponsorshipLink);

                        // Clear form and show success message
                        viewModel.newSponsorshipUrl('');
                        viewModel.message('');
                        viewModel.successMessage(data.message || AddSuccessMessage);
                        
                        // Clear success message after 3 seconds
                        setTimeout(function() {
                            viewModel.successMessage('');
                        }, 3000);
                    } else {
                        viewModel.message(data.message || AddErrorMessage);
                    }
                },
                error: function () {
                    viewModel.message(AddErrorMessage);
                }
            })
            .fail(failHandler);

            return false; // Prevent form submission
        },

        removeSponsorshipLink: function (sponsorshipLink) {
            var url = sponsorshipLink.url();
            
            // Show confirmation dialog using the established pattern
            if (!window.nuget.confirmEvent(ConfirmRemoveMessage + '\n\n' + url)) {
                return;
            }
            
            viewModel.message(RemovingMessage);

            var packageId = $('input[name="id"]').val() || window.nuget.packageId;
            
            $.ajax({
                url: window.nuget.urls.removeSponsorshipUrl,
                type: 'POST',
                dataType: 'json',
                data: window.nuget.addAjaxAntiForgeryToken({
                    id: packageId,
                    sponsorshipUrl: url
                }),
                success: function (data) {
                    if (data.success) {
                        viewModel.sponsorshipLinks.remove(sponsorshipLink);
                        
                        // When an operation succeeds, always clear the error message
                        viewModel.message('');
                        viewModel.successMessage(data.message || RemoveSuccessMessage);
                        
                        // Clear success message after 3 seconds
                        setTimeout(function() {
                            viewModel.successMessage('');
                        }, 3000);
                    } else {
                        viewModel.message(data.message || RemoveErrorMessage);
                    }
                },
                error: function () {
                    viewModel.message(RemoveErrorMessage);
                }
            })
            .fail(failHandler);
        }
    };

    // Failure handler following the established pattern (defined after viewModel)
    var failHandler = function (jqXHR, textStatus, errorThrown) {
        viewModel.message(window.nuget.formatString(errorThrown));
    };

    // Clear alerts when textbox is emptied
    viewModel.newSponsorshipUrl.subscribe(function(newValue) {
        // Clear alerts when textbox becomes empty
        if (!newValue || newValue.trim() === '') {
            viewModel.message('');
            viewModel.successMessage('');
        }
    });

    // Load initial sponsorship links from server data first
    if (window.nuget && window.nuget.initialSponsorshipLinks) {
        for (var i = 0; i < window.nuget.initialSponsorshipLinks.length; i++) {
            var entry = window.nuget.initialSponsorshipLinks[i];
            var sponsorshipLink = new SponsorshipLink({
                Url: entry.Url,
                IsDomainAccepted: entry.IsDomainAccepted
            });
            viewModel.sponsorshipLinks.push(sponsorshipLink);
        }
    }

    // Apply knockout bindings to sponsorship section after data is loaded
    var sponsorshipContainer = document.querySelector('.page-manage-sponsorship-links');
    if (sponsorshipContainer) {
        ko.applyBindings(viewModel, sponsorshipContainer);
    } else {
        // Fallback - try to apply to document
        ko.applyBindings(viewModel);
    }

    function SponsorshipLink(data) {
        this.url = ko.observable(data.Url);
        this.isDomainAccepted = ko.observable(data.IsDomainAccepted);
    }
});
