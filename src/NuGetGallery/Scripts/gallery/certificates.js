var CertificatesManagement = (function () {
    'use strict';

    var _addCertificateUrl;
    var _getCertificatesUrl;
    var _model;

    return new function () {
        this.init = function (addCertificateUrl, getCertificatesUrl) {
            _addCertificateUrl = addCertificateUrl;
            _getCertificatesUrl = getCertificatesUrl;

            window.nuget.configureFileInputButton("register-new");

            $('#input-select-file').on('change', function () {
                clearErrors();

                var filePath = $('#input-select-file').val();

                if (filePath) {
                    var uploadForm = $('#uploadCertificateForm')[0];
                    var formData = new FormData(uploadForm);

                    uploadForm.reset();

                    addCertificateAsync(formData);
                }
            });

            listCertificatesAsync();
        }

        function listCertificatesAsync() {
            $.ajax({
                method: 'GET',
                url: _getCertificatesUrl,
                dataType: 'json',
                cache: false,
                contentType: false,
                processData: false,
                success: function (response) {
                    applyModel(response);
                },
                error: onError.bind(this)
            });
        }

        function deleteCertificateAsync(model) {
            clearErrors();

            if (model.CanDelete) {
                $.ajax({
                    method: 'DELETE',
                    url: model.DeleteUrl,
                    cache: false,
                    data: window.nuget.addAjaxAntiForgeryToken({}),
                    dataType: 'json',
                    success: function (response) {
                        listCertificatesAsync();
                    },
                    error: onError.bind(this)
                });
            }
        }

        function addCertificateAsync(data) {
            clearErrors();

            $.ajax({
                method: 'POST',
                url: _addCertificateUrl,
                data: data,
                cache: false,
                contentType: false,
                processData: false,
                complete: function (xhr, textStatus) {
                    switch (xhr.status) {
                        case 201:
                        case 409:
                            listCertificatesAsync();
                            break;

                        default:
                            onError(xhr, textStatus);
                            break;
                    }
                }
            });
        }

        function applyModel(data) {
            if (_model) {
                _model.certificates(data);
            } else {
                _model = {
                    certificates: ko.observableArray(data),
                    deleteCertificate: deleteCertificateAsync,
                    hasMissingInfo: function () {
                        var currentCertificates = this.certificates();
                        for (var i = 0; i < currentCertificates.length; i++) {
                            if (!currentCertificates[i].HasInfo) {
                                return true;
                            }
                        }
                        return false;
                    },
                    hasCertificates: function () {
                        return this.certificates().length > 0;
                    }
                };

                ko.applyBindings(_model, document.getElementById('certificates-container'));
            }

            var certificatesHeader;

            if (data) {
                certificatesHeader = data.length + " certificate";

                if (data.length !== 1) {
                    certificatesHeader += "s";
                }
            } else {
                certificatesHeader = "";
            }

            $('#certificates-section-header').text(certificatesHeader);
        }

        function onError(model, resultCodeString) {
            switch (resultCodeString) {
                case "timeout":
                    displayErrors(["The operation timed out. Please try again."]);
                    break;
                case "abort":
                    displayErrors(["The operation was aborted. Please try again."]);
                    break;
                default:
                    displayErrors(model.responseJSON);
                    break;
            }

            if (model.status >= 500) {
                displayErrors(["There was a server error."]);
            }
        }

        function displayErrors(errors) {
            if (errors == null || errors.length === 0) {
                return;
            }

            clearErrors();

            var failureContainer = $("#validation-failure-container");
            var failureListContainer = document.createElement("div");
            $(failureListContainer).attr("id", "validation-failure-list");
            $(failureListContainer).attr("data-bind", "template: { name: 'validation-errors', data: data }");
            failureContainer.append(failureListContainer);
            ko.applyBindings({ data: errors }, failureListContainer);
            failureContainer.removeClass("hidden");
        }

        function clearErrors() {
            $("#validation-failure-container").addClass("hidden");
            $("#validation-failure-list").remove();

            var warnings = $('#warning-container');
            warnings.addClass("hidden");
            warnings.children().remove();
        }
    };
}());