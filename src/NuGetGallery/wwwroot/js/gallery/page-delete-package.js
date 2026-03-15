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
});
