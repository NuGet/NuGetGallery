﻿var AsyncFileUploadManager = (function () {
    'use strict';

    return new function () {
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
                var fileName = window.nuget.getFileName($('#input-select-file').val());

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
            clearErrors();

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

            clearErrors()

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

        function bindData(model) {
            $("#verify-package-block").remove();
            $("#submit-block").remove();
            $("#verify-warning-container").addClass("hidden");
            $("#verify-collapser-container").addClass("hidden");
            $("#submit-collapser-container").addClass("hidden");

            if (model != null) {
                var reportContainerElement = document.createElement("div");
                $(reportContainerElement).attr("id", "verify-package-block");
                $(reportContainerElement).attr("class", "collapse in");
                $(reportContainerElement).attr("aria-expanded", "true");
                $(reportContainerElement).attr("data-bind", "template: { name: 'verify-metadata-template', data: data }");
                $("#verify-package-container").append(reportContainerElement);
                ko.applyBindings({ data: model }, reportContainerElement);

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
                    submitVerifyAsync(navigateToPage, bindData.bind(this, model));
                });

                $('#iconurl-field').on('change', function () {
                    $('#icon-preview').attr('src', $('#iconurl-field').val());
                });

                $("#verify-warning-container").removeClass("hidden");
                $("#verify-collapser-container").removeClass("hidden");
                $("#submit-collapser-container").removeClass("hidden");

                window.nuget.configureExpanderHeading("verify-package-section");
                window.nuget.configureExpanderHeading("submit-package-form");
            }

            bindReadMeData(model);
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
}());
