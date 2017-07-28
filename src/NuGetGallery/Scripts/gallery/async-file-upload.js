'use strict';

var AsyncFileUploadManager = new function () {
    var _actionUrl;
    var _cancelUrl;
    var _submitVerifyUrl;
    var _isWebkitBrowser = false; // $.browser.webkit is not longer supported on jQuery
    var _iframeId = '__fileUploadFrame';
    var _uploadFormId;
    var _uploadFormData;
    var _pollingInterval = 250; // in ms
    var _pingUrl;
    var _failureCount;
    var _isUploadInProgress;

    this.init = function (pingUrl, formId, jQueryUrl, actionUrl, cancelUrl, submitVerifyUrl) {
        _pingUrl = pingUrl;
        _uploadFormId = formId;
        _actionUrl = actionUrl;
        _cancelUrl = cancelUrl;
        _submitVerifyUrl = submitVerifyUrl;

        $('#file-select-feedback').on('dragenter', function (e) {
            e.preventDefault();
            e.stopPropagation();

            $(this).removeAttr('readonly');
        });

        $('#file-select-feedback').on('dragleave', function (e) {
            e.preventDefault();
            e.stopPropagation();

            $(this).attr('readonly', 'readonly');
        });


        $('#file-select-feedback').on('dragover', function (e) {
            e.preventDefault();
            e.stopPropagation();
        });


        $('#file-select-feedback').on('drop', function (e) {
            e.preventDefault();
            e.stopPropagation();
            $(this).attr('readonly', 'readonly');

            clearErrors();
            var droppedFile = e.originalEvent.dataTransfer.files[0];
            $('#file-select-feedback').attr('value', droppedFile.name);

            prepareUploadFormData();
            _uploadFormData.set("UploadFile", droppedFile);
            cancelUploadAsync(startUploadAsync, startUploadAsync);
        });

        $('#file-select-feedback').on('click', function () {
            $('#input-select-file').click();
        });

        $('#input-select-file').on('change', function () {
            clearErrors();
            var fileName = $('#input-select-file').val().split("\\").pop();

            if (fileName.length > 0) {
                $('#file-select-feedback').attr('value', fileName);
                prepareUploadFormData();
                // Cancel any ongoing upload, and then start the new upload.
                // If the cancel fails, still try to upload the new one.
                cancelUploadAsync(startUploadAsync, startUploadAsync);
            } else {
                resetFileSelectFeedback();
            }
        });

        if (InProgressPackage != null) {
            bindData(InProgressPackage);
        }
    }

    function resetFileSelectFeedback() {
        $('#file-select-feedback').attr('value', 'Browse or Drop files to select a package...');
    }

    function prepareUploadFormData() {
        var formData = new FormData($('#' + _uploadFormId)[0]);
        _uploadFormData = formData;
    }

    function startUploadAsync(callback, error) {
        // Shortcut the upload if the nupkg input doesn't have a value
        if ($('#input-select-file').val() == null) {
            return;
        }

        startProgressBar();

        $.ajax({
            url: _actionUrl,
            type: 'POST',

            data: _uploadFormData,

            cache: false,
            contentType: false,
            processData: false,

            success: function (model, resultCodeString, fullResponse) {
                bindData(model);
                endProgressBar();
                if (callback) {
                    callback();
                }
            },

            error: handleErrors.bind(this, error)
        });
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

    function submitVerifyAsync(callback, error) {
        $.ajax({
            url: _submitVerifyUrl,
            type: 'POST',

            data: new FormData($('#verify-metadata-form')[0]),

            cache: false,
            contentType: false,
            processData: false,

            success: function (model, resultCodeString, fullResponse) {
                if (callback) {
                    callback(model);
                }
            },

            error: handleErrors.bind(this, error)
        });
    }

    function cancelUploadAsync(callback, error) {
        $('#warning-container').addClass("hidden");
        $.ajax({
            url: _cancelUrl,
            type: 'POST',

            data: new FormData($('#cancel-form')[0]),

            cache: false,
            contentType: false,
            processData: false,

            success: function (model, resultCodeString, fullResponse) {
                bindData(model);
                if (callback) {
                    callback();
                }
            },

            error: handleErrors.bind(this, error)
        });
    }

    function handleErrors(errorCallback, model, resultCodeString, fullResponse) {
        bindData(null);

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

        if (fullResponse.status >= 500) {
            displayErrors(["There was a server error."]);
        }

        endProgressBar();
        if (errorCallback) {
            errorCallback();
        }
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

    function clearErrors() {
        $("#validation-failure-container").addClass("hidden");
        $("#validation-failure-list").remove();
    }

    function bindData(model) {
        $("#verify-package-block").remove();
        $("#import-readme-block").remove();
        $("#submit-block").remove();
        $("#verify-collapser-container").addClass("hidden");
        $("#readme-collapser-container").addClass("hidden");
        $("#submit-collapser-container").addClass("hidden");
        if (model == null) {
            return;
        }

        var reportContainerElement = document.createElement("div");
        $(reportContainerElement).attr("id", "verify-package-block");
        $(reportContainerElement).attr("class", "collapse in");
        $(reportContainerElement).attr("aria-expanded", "true");
        $(reportContainerElement).attr("data-bind", "template: { name: 'edit-metadata-template', data: data }");
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
            $('#verify-cancel-button').attr('disabled', 'disabled');
            $('#verify-cancel-button').attr('value', 'Cancelling');
            $('#verify-cancel-button').addClass('.loading');
            $('#verify-submit-button').attr('disabled', 'disabled');
            $('#input-select-file').val("");
            resetFileSelectFeedback();
            cancelUploadAsync();
        });

        $('#verify-submit-button').on('click', function () {
            $('#verify-cancel-button').attr('disabled', 'disabled');
            $('#verify-submit-button').attr('disabled', 'disabled');
            $('#verify-submit-button').attr('value', 'Submitting');
            $('#verify-submit-button').addClass('.loading');
            submitVerifyAsync(navigateToPage);
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

    function navigateToPage(verifyResponse) {
        document.location = verifyResponse.location;
    }

    function startProgressBar() {
        _isUploadInProgress = true;
        _failureCount = 0;

        setProgressIndicator(0, '');
        $("#upload-progress-bar-container").removeClass("hidden");
        setTimeout(getProgress, 100);
    }

    function endProgressBar() {
        $("#upload-progress-bar-container").addClass("hidden");
        _isUploadInProgress = false;
    }

    function getProgress() {
        $.ajax({
            type: 'GET',
            dataType: 'json',
            url: _pingUrl,
            success: onGetProgressSuccess,
            error: onGetProgressError
        });
    }

    function onGetProgressSuccess(result) {
        if (!result) {
            return;
        }

        var percent = result.Progress;

        if (!result.FileName) {
            return;
        }

        setProgressIndicator(percent, result.FileName);
        if (percent < 100) {
            setTimeout(getProgress, _pollingInterval);
        }
        else {
            _isUploadInProgress = false;
        }
    }

    function onGetProgressError(result) {
        if (++_failureCount < 3) {
            setTimeout(getProgress, _pollingInterval);
        }
    }

    function setProgressIndicator(percentComplete, fileName) {
        $("#upload-progress-bar").width(percentComplete + "%")
            .attr("aria-valuenow", percentComplete)
            .text(percentComplete + "%");
    }

    // obsolete
    function constructIframe(jQueryUrl) {
        var iframe = document.getElementById(_iframeId);
        if (iframe) {
            return;
        }

        iframe = document.createElement('iframe');
        iframe.setAttribute('id', _iframeId);
        iframe.setAttribute('style', 'display: none; visibility: hidden;');

        $(iframe).load(function () {
            var scriptRef = document.createElement('script');
            scriptRef.setAttribute("src", jQueryUrl);
            scriptRef.setAttribute("type", "text/javascript");
            iframe.contentDocument.body.appendChild(scriptRef);

            var scriptContent = document.createElement('script');
            scriptContent.setAttribute("type", "text/javascript");
            scriptContent.innerHTML = "var _callback,_error, _key, _pingUrl, _fcount;function start(b,c,e){_callback=c;_pingUrl=b;_error=e;_fcount=0;setTimeout(getProgress,200)}function getProgress(){$.ajax({type:'GET',dataType:'json',url:_pingUrl,success:onSuccess,error:_error})}function onSuccess(a){if(!a){return}var b=a.Progress;var d=a.FileName;if(!d){return}_callback(b,d);if(b<100){setTimeout(getProgress,200)}}function onError(a){if(++_fcount<3){setTimeout(getProgress,200)}}";
            iframe.contentDocument.body.appendChild(scriptContent);
        });

        document.body.appendChild(iframe);
    }
};