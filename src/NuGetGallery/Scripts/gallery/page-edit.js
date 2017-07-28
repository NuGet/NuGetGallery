'use strict';

var EditViewManager = new function () {
    var _currVersion;
    var _viewModel;
    var _changedState;
    var _resetFunctions;
    var _submitEditUrl;
    var _cancelEditUrl;
    var _submitting;
    var _submitted = true;

    this.init = function (model, submitEditUrl, cancelEditUrl) {
        _submitting = false;
        _submitted = false;
        _submitEditUrl = submitEditUrl;
        _cancelEditUrl = cancelEditUrl;
        _viewModel = model;
        _changedState = {};
        _resetFunctions = {};
        bindData(_viewModel);

        $(window).on('beforeunload', confirmLeave);

        $('#verify-submit-button').attr('disabled', 'disabled');

        $('#input-select-version').on('change', function () {
            document.location = $(this).val();
        });

        $('input[type="text"], input[type="checkbox"], textarea').on('change keydown', function () {
            $(this).addClass("edited");
            _changedState[$(this).attr('id')] = true;
            $('#verify-submit-button').removeAttr('disabled');
        });

        // This sets up a series of functions that are capable of "resetting" the values in the inputs
        // Currently unused.
        $('input[type="text"], input[type="checkbox"], textarea').each(function (index) {
            _resetFunctions[$(this).attr('id')] = function (newValue) {
                _changedState[$(this).attr('id')] = false;
                $(this).val(newValue);
            }.bind(this, $(this).val());
        });
    }

    this.isEdited = function () {
        return Object.keys(_changedState).reduce(function (previous, key) { return previous || _changedState[key]; }, false);
    }

    function confirmLeave() {
        var message = "";
        if (_submitting) {
            message = "Your edit is being submitted. Are you sure you want to leave?";
        } else if (EditViewManager.isEdited() && !_submitted) {
            message = "You have unsaved changes. Are you sure you want to leave?";
        }

        if (message !== "") {
            return message;
        }
    }

    function previewReadMeAsync(callback, error) {
        var formData = new FormData();

        // Validate anti-forgery token
        var token = $('[name=__RequestVerificationToken]').val();
        formData.append("__RequestVerificationToken", token);

        // Assemble ReadMe data
        formData.append("ReadMeType", $("input[name='ReadMe.ReadMeType']:checked").val());

        formData.append("ReadMeUrl", $("#ReadMeUrlInput").val());

        var readMeFile = $("#readme-select-file");
        if (readMeFile && readMeFile[0] && validateReadMeFileName(readMeFile.val().split("\\").pop())) {
            formData.append("ReadMeFile", readMeFile[0].files[0]);
        } else {
            formData.append("ReadMeFile", null);
        }

        formData.append("ReadMeWritten", $("#readme-written").val());

        $.ajax({
            url: "preview-readme",
            type: 'POST',
            contentType: false,
            processData: false,
            data: formData,
            success: function (model, resultCodeString, fullResponse) {
                displayReadMePreview(model);
            },
            error: function (jqXHR, exception) {
                var message = "";
                if (jqXHR.status == 400) {
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

    function submitEditAsync(callback, error) {
        if (EditViewManager.isEdited()) {
            if (!_submitting) {
                _submitting = true;
                $.ajax({
                    url: _submitEditUrl,
                    type: 'POST',

                    data: new FormData($('#verify-metadata-form')[0]),

                    cache: false,
                    contentType: false,
                    processData: false,

                    success: function (model, resultCodeString, fullResponse) {
                        _submitting = false;
                        _submitted = true;
                        if (callback) {
                            callback(model);
                        }
                    },

                    error: handleErrors.bind(this, error)
                });
            }
        } else {
            if (callback) {
                callback();
            }
        }
    }

    function cancelEdit() {
        navigateToPage({ location: _cancelEditUrl });
    }

    function navigateToPage(verifyResponse) {
        document.location = verifyResponse.location;
    }

    function displayErrors(errors) {
        if (errors == null || errors.length < 1) {
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
                displayErrors(["The operation timed out. Please try again."])
                break;
            case "abort":
                displayErrors(["The operation was aborted. Please try again."])
                break;
            default:
                displayErrors(model.responseJSON);
                break;
        }

        if ((fullResponse && fullResponse.status >= 500) || (model && model.status >= 500)) {
            displayErrors(["There was a server error."])
        }

        if (errorCallback) {
            errorCallback();
        }
    }

    function bindData(model) {
        $("#verify-package-block").remove();
        $("#verify-collapser-container").addClass("hidden");
        if (model == null) {
            return;
        }

        var reportContainerElement = document.createElement("div");
        $(reportContainerElement).attr("id", "verify-package-block");
        $(reportContainerElement).attr("class", "collapse in");
        $(reportContainerElement).attr("aria-expanded", "true");
        $(reportContainerElement).attr("data-bind", "template: { name: 'verify-metadata-template', data: data }");
        $("#verify-package-container").append(reportContainerElement);
        ko.applyBindings({ data: model }, reportContainerElement);

        var readMeContainerElement = document.createElement("div");
        $(readMeContainerElement).attr("id", "import-readme-block");
        $(readMeContainerElement).attr("class", "collapse in");
        $(readMeContainerElement).attr("aria-expanded", "true");
        $(readMeContainerElement).attr("data-bind", "template: { name: 'import-readme-template', data: data }");
        $("#import-readme-container").append(readMeContainerElement);
        ko.applyBindings({ data: model }, readMeContainerElement);

        var submitContainerElement = document.createElement("div");
        $(submitContainerElement).attr("id", "submit-block");
        $(submitContainerElement).attr("class", "collapse in");
        $(submitContainerElement).attr("aria-expanded", "true");
        $(submitContainerElement).attr("data-bind", "template: { name: 'submit-package-template', data: data }");
        $("#submit-package-container").append(submitContainerElement);
        ko.applyBindings({ data: model }, submitContainerElement);

        $('#verify-cancel-button').on('click', function () {
            cancelEdit();
        });

        $('#verify-submit-button').on('click', function () {
            $('#verify-cancel-button').attr('disabled', 'disabled');
            $('#verify-submit-button').attr('disabled', 'disabled');
            $('#verify-submit-button').attr('value', 'Submitting');
            $('#verify-submit-button').addClass('.loading');
            submitEditAsync(navigateToPage);
        });

        $('#iconurl-field').on('change', function () {
            $('#icon-preview').attr('src', $('#iconurl-field').val());
        });

        $("#verify-collapser-container").removeClass("hidden");
        $("#readme-collapser-container").removeClass("hidden");
        $("#submit-collapser-container").removeClass("hidden");

        window.nuget.configureExpander(
            "verify-package-form",
            "ChevronRight",
            "Verify",
            "ChevronDown",
            "Verify");
        window.nuget.configureExpander(
            "readme-package-form",
            "ChevronRight",
            "Import ReadMe",
            "ChevronDown",
            "Import ReadMe");
        window.nuget.configureExpander(
            "submit-package-form",
            "ChevronRight",
            "Submit",
            "ChevronDown",
            "Submit");
        $(".readme-file").hide();
        $(".readme-write").hide();
        $(".markdown-popover").popover({
            trigger: 'click focus',
            html: true,
            placement: 'bottom',
            content: "# Heading<br />## Sub-heading<br />Paragraphs are separated by a blank line.<br />--- Horizontal Rule<br />* Bullet List<br />1. Numbered List<br />A [link](http://www.example.com)<br />`Code Snippet`<br />_italic_ *italic*<br />__bold__ **bold** "
        });

        $(".readme-btn-group").change(changeReadMeFormTab);

        $("#repositoryurl-field").blur(function () {
            if (!$("#ReadMeUrlInput").val()) {
                $("#ReadMeUrlInput").val(fillReadMeUrl($("#repositoryurl-field").val()));
            }
        });

        $("#ReadMeUrlInput").on("change blur", function () {
            clearReadMeError();
        });

        $('#readme-select-feedback').on('click', function () {
            $('#readme-select-file').click();
        });

        $('#readme-select-file').on('change', function () {
            clearErrors();
            clearReadMeError();
            displayReadMeEditMarkdown();
            var fileName = $('#readme-select-file').val().split("\\").pop();
            if (fileName.length > 0 && validateReadMeFileName(fileName)) {
                $('#readme-select-feedback').attr('placeholder', fileName);
                clearReadMeError();
            } else if (fileName.length > 0) {
                $('#readme-select-feedback').attr('placeholder', 'Browse to select a package file...');
                displayReadMeError("Please enter a markdown file with one of the following extensions: '.md', '.mkdn', '.mkd', '.mdown', '.markdown', '.txt' or '.text'.");
            }
            else {
                $('#readme-select-feedback').attr('placeholder', 'Browse to select a package file...');
                displayReadMeError("Please select a file.")
            }
        });

        $("#readme-written").on("change", function () {
            clearReadMeError();
        })

        $("#preview-readme-button").on('click', function () {
            previewReadMeAsync();
        });

        $("#edit-markdown-button").on('click', function () {
            displayReadMeEditMarkdown();
        });
    }

    function displayReadMePreview(response) {
        $("#readme-preview-contents").html(response);
        $("#readme-preview").removeClass("hidden");
        $(".readme-write").addClass("hidden");
        $(".readme-file").addClass("hidden");
        $(".readme-url").addClass("hidden");
        $("#edit-markdown").removeClass("hidden");
        $("#preview-html").addClass("hidden");
        clearReadMeError();
    }

    function displayReadMeEditMarkdown() {
        $("#readme-preview-contents").html("");
        $("#readme-preview").addClass("hidden");
        $(".readme-write").removeClass("hidden");
        $(".readme-file").removeClass("hidden");
        $(".readme-url").removeClass("hidden");
        $("#edit-markdown").addClass("hidden");
        $("#preview-html").removeClass("hidden");
    }

    function displayReadMeError(errorMsg) {
        $("#readme-errors").removeClass("hidden");
        $("#preview-readme-button").attr("disabled", "disabled");
        $("#readme-error-content").text(errorMsg);
    }

    function clearReadMeError() {
        if (!$("#readme-errors").hasClass("hidden")) {
            $("#readme-errors").addClass("hidden");
            $("#readme-error-content").text("");
        }
        $("#preview-readme-button").removeAttr("disabled");
    }

    function changeReadMeFormTab() {
        if ($("#readme-url-btn").hasClass("active")) {
            $(".readme-url").show();
            $(".readme-file").hide();
            $(".readme-write").hide();
        } else if ($("#readme-file-btn").hasClass("active")) {
            $(".readme-url").hide();
            $(".readme-file").show();
            $(".readme-write").hide();
        } else if ($("#readme-write-btn").hasClass("active")) {
            $(".readme-url").hide();
            $(".readme-file").hide();
            $(".readme-write").show();
        }
        clearReadMeError();
    }

    function validateReadMeFileName(fileName) {
        var markdownExtensions = ["md", "mkdn", "mkd", "mdown", "markdown", "txt", "text"];
        return markdownExtensions.includes(fileName.split('.').pop());
    }

    function fillReadMeUrl(repositoryUrl) {
        var readMeUrl;

        if (!repositoryUrl.endsWith("/")) {
            repositoryUrl += "/";
        }

        var githubRegex = /^((https?:\/\/)([a-zA-Z0-9]+\.)?github\.com\/)([a-zA-Z0-9])+\/([a-zA-Z0-9])+(\/)?$/;
        if (githubRegex.test(repositoryUrl)) {
            readMeUrl = repositoryUrl.replace(githubRegex.exec(repositoryUrl)[1], "https://raw.githubusercontent.com/")
            return readMeUrl + "master/README.md";
        } else {
            return "";
        }
    }

}