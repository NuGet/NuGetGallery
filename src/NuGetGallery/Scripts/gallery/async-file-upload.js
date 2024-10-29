var AsyncFileUploadManager = (function () {
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
        var _slowerPollingInterval = 1000; // in ms
        var _pingUrl;
        var _isUploadInProgress;
        var _uploadStartTime;
        var _uploadId;

        this.init = function (pingUrl, formId, jQueryUrl, actionUrl, cancelUrl, submitVerifyUrl, uploadTracingKey, previewUrl) {
            _uploadId = uploadTracingKey;
            _pingUrl = pingUrl;
            _uploadFormId = formId;
            _actionUrl = actionUrl;
            _cancelUrl = cancelUrl;
            _submitVerifyUrl = submitVerifyUrl;

            BindReadMeDataManager.init(previewUrl);

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
        };

        function resetFileSelectFeedback() {
            $('#file-select-feedback').attr('value', 'Browse or Drop files to select a package (.nupkg) or symbols package (.snupkg)...');
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

                headers: {
                    "upload-id": _uploadId
                },

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

                headers: {
                    "upload-id": _uploadId
                },

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

                headers: {
                    "upload-id": _uploadId
                },

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
                case "error":
                    // IIS returns 404.13 (NotFound) when maxAllowedContentLength limit is exceeded.
                    if (fullResponse === "Not Found" || fullResponse === "Request Entity Too Large") {
                        displayErrors(["The package file exceeds the size limit of 250 MB. Please reduce the package size and try again."]);
                    }
                    else {
                        displayErrors(model.responseJSON);
                    }
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
            if (!errors || errors.length < 1) {
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
            $('#symbols-replace-warning-container').addClass('hidden');

            var warnings = $('#warning-container');
            warnings.addClass("hidden");
            warnings.children().remove();
        }

        function bindData(model) {
            $("#verify-package-block").remove();
            $("#submit-block").remove();
            $("#verify-collapser-container").addClass("hidden");
            $("#submit-collapser-container").addClass("hidden");
            $("#readme-collapser-container").addClass("hidden");

            if (model != null) {
                var reportContainerElement = document.createElement("div");
                $(reportContainerElement).attr("id", "verify-package-block");
                $(reportContainerElement).attr("class", "collapse in");
                $(reportContainerElement).attr("data-bind", "template: { name: 'verify-metadata-template', data: data }");
                $("#verify-package-container").append(reportContainerElement);
                ko.applyBindings({ data: model }, reportContainerElement);
                //Content of ReadmeFileContents indicates if embedded readme exists in the package.
                //Support legacy readme by displaying readme container if ReadmeFileContents is null.
                //Disable legacy readme by hiding readme container only if embedded readme content is not null.
                if (model.ReadmeFileContents == null) {
                    $('#import-readme-container').removeClass('hidden');
                } else if (model.ReadmeFileContents.Content) {
                    $('#import-readme-container').addClass('hidden');
                }

                var submitContainerElement = document.createElement("div");
                $(submitContainerElement).attr("id", "submit-block");
                $(submitContainerElement).attr("class", "collapse in");
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

                if (model.IsSymbolsPackage && model.HasExistingAvailableSymbols) {
                    $('#symbols-replace-warning-container').removeClass('hidden');
                } else {
                    $('#symbols-replace-warning-container').addClass('hidden');
                }

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

                $("#verify-collapser-container").removeClass("hidden");
                $("#submit-collapser-container").removeClass("hidden");
                $("#readme-collapser-container").removeClass("hidden");
                
                if (model != null && model.IsDisplayUploadWarningV2Enabled) {
                    $('#upload-package-form').collapse('hide');
                    $('#warning-container').addClass('hidden');
                }
 
                window.nuget.configureExpanderHeading("verify-package-section");
                window.nuget.configureExpanderHeading("submit-package-form");
            }

            if (model === null || !model.IsSymbolsPackage) {
                BindReadMeDataManager.bindReadMeData(model);
            }

            if (model != null && model.IsMarkdigMdSyntaxHighlightEnabled) {
                syntaxHighlight();
            }

            document.getElementById("validation-failure-container").scrollIntoView();
        }

        function navigateToPage(verifyResponse) {
            document.location = verifyResponse.location;
        }

        function startProgressBar() {
            _isUploadInProgress = true;
            _uploadStartTime = new Date();

            setProgressIndicator(0, '');
            $("#upload-progress-bar-container").removeClass("hidden");
            setTimeout(getProgress, 100);
        }

        function endProgressBar() {
            $("#upload-progress-bar-container").addClass("hidden");
            _isUploadInProgress = false;
            _uploadStartTime = null;
        }

        function getProgress() {
            $.ajax({
                type: 'GET',
                dataType: 'json',
                url: _pingUrl,
                headers: {
                    "upload-id": _uploadId
                },
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
            if (_uploadStartTime) {
                var currentTime = new Date();
                var uploadDuration = currentTime - _uploadStartTime;

                // Continue polling as if no errors have occurred for the first 5 seconds of the upload.
                // After that, poll at a slower pace for 5 minutes.
                if (uploadDuration < 5 * 1000) {
                    setTimeout(getProgress, _pollingInterval);
                } else if (uploadDuration < 5 * 60 * 1000) {
                    setTimeout(getProgress, _slowerPollingInterval);
                }
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
