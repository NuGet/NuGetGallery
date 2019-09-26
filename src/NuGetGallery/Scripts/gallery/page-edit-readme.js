var EditReadMeManager = (function () {
    'use strict';

    return new function () {
        var _currVersion;
        var _selectVersion;
        var _viewModel;
        var _changedState;
        var _submitting;
        var _submitted = true;

        this.init = function (model, previewUrl) {
            _submitting = false;
            _submitted = false;
            _viewModel = model;
            _changedState = {};
            BindReadMeDataManager.init(previewUrl);

            bindData(_viewModel);

            $(window).on('beforeunload', confirmLeave);

            _selectVersion = $('.page-edit-package #input-select-readme');
            var defaultVersion = _selectVersion.val();
            
            _selectVersion.change(function () {
                var selectedVersion = $(this).val();
                if (!selectedVersion) {
                    // No version is selected. This usually indicates that there are no versions of this package that can have their ReadMe edited.
                    return;
                }

                var version = _viewModel.Versions[selectedVersion];
                var cachedReadMe = version.ReadMe;
                if (cachedReadMe === null) {
                    var url = version.GetReadMeUrl;
                    $.ajax({
                        url: url,
                        type: 'GET',
                        statusCode: {
                            200: function (data) {
                                version.ReadMe = data;
                                _viewModel.Edit = data;
                                bindData(_viewModel);
                            },

                            403: function () {
                                displayErrors(['You do not have permission to edit the documentation of this package.']);
                                $(this).val(defaultVersion);
                            },

                            404: function () {
                                displayErrors(['The selected package does not exist.']);
                                $(this).val(defaultVersion);
                            }
                        },

                        error: function () {
                            displayErrors(['An unknown error occured.']);
                            $(this).val(defaultVersion);
                        }
                    });
                } else {
                    _viewModel.Edit = cachedReadMe;
                    bindData(_viewModel);
                }
            });

            _selectVersion.change();
        };

        function confirmLeave() {
            var message = "";
            if (_submitting) {
                message = "Your request is being submitted. Are you sure you want to leave?";
            }

            if (message !== "") {
                return message;
            }
        }

        function submitAsync(callback, error) {
            if (!_submitting) {
                _submitting = true;
                var selectedVersion = _selectVersion.val();
                if (!selectedVersion) {
                    // No version is selected. This usually indicates that there are no versions of this package that can have their ReadMe edited.
                    return;
                }

                var url = _viewModel.Versions[selectedVersion].SubmitUrl;
                $.ajax({
                    url: url,
                    type: 'POST',

                    data: new FormData($('#edit-readme-form')[0]),

                    cache: false,
                    contentType: false,
                    processData: false,

                    success: function (model) {
                        _submitting = false;
                        _submitted = true;
                        if (callback) {
                            callback(model);
                        }
                    },

                    error: handleErrors.bind(this, error)
                });
            }
        }

        function navigateToPage(editReadmeResponse) {
            document.location = editReadmeResponse.location;
        }

        function displayErrors(errors) {
            $("#validation-failure-list").remove();

            if (errors === null || errors.length < 1) {
                return;
            }

            var failureContainer = $("#validation-failure-container");
            var failureListContainer = document.createElement("div");
            $(failureListContainer).attr("id", "validation-failure-list");
            $(failureListContainer).attr("data-bind", "template: { name: 'validation-errors', data: data }");
            failureContainer.append(failureListContainer);
            ko.applyBindings({ data: errors }, failureListContainer);

            failureContainer.removeClass("hidden");
        }

        function handleErrors(errorCallback, model, resultCodeString, fullResponse) {
            bindData(null);

            _submitting = false;
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

            if ((fullResponse && fullResponse.status >= 500) || (model && model.status >= 500)) {
                displayErrors(["There was a server error."]);
            }

            if (errorCallback) {
                errorCallback();
            }
        }

        function bindData(model) {
            $("#submit-block").remove();

            if (model === null) {
                return;
            }

            var submitContainerElement = document.createElement("div");
            $(submitContainerElement).attr("id", "submit-block");
            $(submitContainerElement).attr("class", "collapse in");
            $(submitContainerElement).attr("aria-expanded", "true");
            $(submitContainerElement).attr("data-bind", "template: { name: 'submit-package-template', data: data }");
            $("#submit-package-container").append(submitContainerElement);
            ko.applyBindings({ data: model }, submitContainerElement);

            $('#verify-submit-button').on('click', function () {
                $('#verify-submit-button').attr('disabled', 'disabled');
                $('#verify-submit-button').attr('value', 'Submitting');
                $('#verify-submit-button').addClass('.loading');
                submitAsync(navigateToPage);
            });

            if (model === null || !model.IsSymbolsPackage) {
                BindReadMeDataManager.bindReadMeData(model);
            }
        }
    };
}());

var BindReadMeDataManager = (function () {
    'use strict';

    return new function () {
        var _previewUrl;

        this.init = function (previewUrl) {
            _previewUrl = previewUrl;
        };

        this.bindReadMeData = function (model) {
            $("#import-readme-block").remove();

            if (model === null) {
                return;
            }

            model.SelectedTab = ko.observable('written');
            model.OnReadmeTabChange = function (_, e) {
                model.SelectedTab($(e.target).data('source-type'));
                return true;
            };

            var readMeContainerElement = document.createElement("div");
            $(readMeContainerElement).attr("id", "import-readme-block");
            $(readMeContainerElement).attr("class", "collapse in");
            $(readMeContainerElement).attr("aria-expanded", "true");
            $(readMeContainerElement).attr("data-bind", "template: { name: 'import-readme-template', data: data }");
            $("#import-readme-container").append(readMeContainerElement);
            ko.applyBindings({ data: model }, readMeContainerElement);

            $("#ReadMeUrlInput").on("change blur", function () {
                clearReadMeError();
            });

            $('#ReadMeFileText').on('click', function () {
                $('#ReadMeFileInput').click();
            });

            $('#ReadMeFileInput').on('change', function () {
                clearReadMeError();

                displayReadMeEditMarkdown();
                var fileName = window.nuget.getFileName($('#ReadMeFileInput').val());

                if (fileName.length > 0) {
                    $('#ReadMeFileText').attr('value', fileName);
                }
                else {
                    $('#ReadMeFileText').attr('placeholder', 'Browse or Drop files to select a ReadMe.md file...');
                }
            });

            $("#ReadMeTextInput").on("change", function () {
                clearReadMeError();
            })

            $("#preview-readme-button").on('click', function () {
                previewReadMeAsync();
            });

            if ($("#ReadMeTextInput").val() !== "") {
                previewReadMeAsync();
            }

            $("#edit-markdown-button").on('click', function () {
                clearReadMeError();
                displayReadMeEditMarkdown();
            });

            window.nuget.configureFileInputButton("browse-for-readme-button");
        }

        function previewReadMeAsync(callback, error) {
            // Request source type is generated off the ReadMe tab ids.
            var readMeType = $(".readme-tabs li.active a").data("source-type")

            var formData = new FormData();
            formData.append("SourceType", readMeType);

            if (readMeType === "written") {
                var readMeWritten = $("#ReadMeTextInput").val();
                formData.append("SourceText", readMeWritten);
            }
            else if (readMeType === "url") {
                var readMeUrl = $("#ReadMeUrlInput").val();
                formData.append("SourceUrl", readMeUrl);
            }
            else if (readMeType === "file") {
                var readMeFileInput = $("#ReadMeFileInput");
                var readMeFileName = readMeFileInput && readMeFileInput[0] ? window.nuget.getFileName(readMeFileInput.val()) : null;
                var readMeFile = readMeFileName ? readMeFileInput[0].files[0] : null;
                formData.append("SourceFile", readMeFile);
            }

            $.ajax({
                url: _previewUrl,
                type: "POST",
                contentType: false,
                processData: false,
                data: window.nuget.addAjaxAntiForgeryToken(formData),
                success: function (model, resultCodeString, fullResponse) {
                    clearReadMeError();
                    displayReadMePreview(model);
                },
                error: function (jqXHR, exception) {
                    var message = "";
                    if (jqXHR.status === 400) {
                        try {
                            message = JSON.parse(jqXHR.responseText);
                        } catch (err) {
                            message = "Bad request. [400]";
                        }
                    }
                    displayReadMeError(message);
                }
            });
        }

        function displayReadMePreview(response) {
            $("#readme-preview-contents").html(response.Content);
            $("#readme-preview").removeClass("hidden");

            $('.readme-tabs').children().hide();

            $("#edit-markdown").removeClass("hidden");
            $("#preview-html").addClass("hidden");
            clearReadMeError();

            if (response.ImagesRewritten) {
                displayReadMeWarning("Some images were automatically rewritten to use secure links and might be broken.");
            }
        }

        function displayReadMeEditMarkdown() {
            $("#readme-preview-contents").html("");
            $("#readme-preview").addClass("hidden");

            $('.readme-tabs').children().show();

            $("#edit-markdown").addClass("hidden");
            $("#preview-html").removeClass("hidden");
        }

        function displayReadMeWarning(errorMsg) {
            $("#readme-warnings").removeClass("hidden");
            $("#readme-warning-content").text(errorMsg);
        }

        function displayReadMeError(errorMsg) {
            $("#readme-errors").removeClass("hidden");
            $("#preview-readme-button").attr("disabled", "disabled");
            $("#readme-error-content").text(errorMsg);
        }

        function clearReadMeError() {
            if (!$("#readme-warnings").hasClass("hidden")) {
                $("#readme-warnings").addClass("hidden");
                $("#readme-warning-content").text("");
            }

            if (!$("#readme-errors").hasClass("hidden")) {
                $("#readme-errors").addClass("hidden");
                $("#readme-error-content").text("");
            }
            $("#preview-readme-button").prop("disabled", false);
        }
    };
}());