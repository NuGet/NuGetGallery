$(function () {
    'use strict';

    // Essential message constants only
    const ConfirmRemoveMessage = "Are you sure you want to remove this sponsorship link?";
    const AddErrorMessage = "An error occurred while adding the sponsorship URL. Please try again.";
    const RemoveErrorMessage = "An error occurred while removing the sponsorship URL. Please try again.";
    const EmptyUrlMessage = "Please enter a URL.";
    const AddSuccessMessage = "Sponsorship URL added successfully.";
    const RemoveSuccessMessage = "Sponsorship URL removed successfully.";

    // Set up sponsorship links section
    var viewModel = {
        message: ko.observable(''),
        successMessage: ko.observable(''),
        newSponsorshipUrl: ko.observable(''),
        sponsorshipLinks: ko.observableArray([]),

        addSponsorshipLink: function (data, event) {
            if (event && event.preventDefault) {
                event.preventDefault();
            }
            
            var url = viewModel.newSponsorshipUrl().trim();

            // Clear previous messages
            viewModel.message('');

            // Basic validation - server handles all complex validation
            if (!url) {
                viewModel.message(EmptyUrlMessage);
                return false;
            }

            // Submit to server
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
                    // Log debug information to browser console
                    if (data.debug && data.debug.length > 0) {
                        console.log("=== SPONSORSHIP URL DEBUG INFO ===");
                        data.debug.forEach(function(message) {
                            if (message.indexOf("[ERROR]") >= 0) {
                                console.error(message);
                            } else {
                                console.log(message);
                            }
                        });
                        console.log("=== END DEBUG INFO ===");
                    }
                    
                    if (data.success) {
                        var urlToAdd = data.validatedUrl || url;
                        
                        var newSponsorshipLink = new SponsorshipLink({
                            Url: urlToAdd,
                            IsDomainAccepted: data.isDomainAccepted || false
                        });
                        viewModel.sponsorshipLinks.push(newSponsorshipLink);
                        viewModel.newSponsorshipUrl('');
                        viewModel.message('');
                        viewModel.successMessage(AddSuccessMessage);
                        
                        // Clear success message after 3 seconds
                        setTimeout(function() {
                            viewModel.successMessage('');
                        }, 3000);
                    } else {
                        viewModel.message(data.message || AddErrorMessage);
                    }
                },
                error: function (xhr, status, error) {
                    console.error("=== AJAX ERROR ===");
                    console.error("Status:", status);
                    console.error("Error:", error);
                    console.error("Response:", xhr.responseText);
                    console.error("=== END AJAX ERROR ===");
                    viewModel.message(AddErrorMessage);
                }
            });

            return false;
        },

        removeSponsorshipLink: function (sponsorshipLink) {
            var url = sponsorshipLink.url();
            
            if (!window.nuget.confirmEvent(ConfirmRemoveMessage + '\n\n' + url)) {
                return;
            }

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
                        viewModel.message('');
                        viewModel.successMessage(RemoveSuccessMessage);
                        
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
            });
        }
    };

    // Clear messages when user starts typing
    viewModel.newSponsorshipUrl.subscribe(function(newValue) {
        if (!newValue || newValue.trim() === '') {
            viewModel.message('');
            viewModel.successMessage('');
        }
    });

    // Load initial sponsorship links
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

    // Apply knockout bindings
    var sponsorshipContainer = document.querySelector('.page-manage-sponsorship-links');
    if (sponsorshipContainer) {
        ko.applyBindings(viewModel, sponsorshipContainer);
    }

    function SponsorshipLink(data) {
        this.url = ko.observable(data.Url);
        this.isDomainAccepted = ko.observable(data.IsDomainAccepted);
    }
});
