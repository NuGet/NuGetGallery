$(function () {
    'use strict';

    $('#SoftDelete').change(function (e) {
        if (!this.checked) {
            $('#DeleteEmptyPackageRegistrationContainer').show();
        } else {
            $('#DeleteEmptyPackageRegistrationContainer').hide();
        }
    });

    $('#delete-form').submit(function (e) {
        if (!confirm('Deleting this package will make it unavailable for download and package restore. Are you sure you want to continue with the delete?')) {
            e.preventDefault();
        }
    });

    $('#delete-symbols-form').submit(function (e) {
        if (!confirm('Deleting this symbols package will make it unavailable for download and remove all corresponding symbols from the symbol server. Are you sure you want to continue with the delete?')) {
            e.preventDefault();
        }
    });

    $('.page-delete-package #input-select-version').change(function () {
        // Make sure the forms now reference the selected version of the package.
        var version = $(this).val();
        $('#input-list-package').val(version);
        $('#input-delete-package').val(packageId + '|' + version);

        // Update the form to show the correct version.
        var state = versionListedState[version];
        // Update the listed checkbox to match the state of the package.
        var listed = state.Listed;
        $(".page-delete-package input#Listed")[0].checked = listed;
        // Update the text stating the download count on the delete form.
        var downloadCount = state.DownloadCount;
        $(".page-delete-package #downloadCount").text(downloadCount);
    });
});
