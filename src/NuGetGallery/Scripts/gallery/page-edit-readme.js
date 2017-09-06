'use strict';

function bindReadMeData(model) {
    $("#import-readme-block").remove();
    $("#readme-collapser-container").addClass("hidden");

    var readMeContainerElement = document.createElement("div");
    $(readMeContainerElement).attr("id", "import-readme-block");
    $(readMeContainerElement).attr("class", "collapse in");
    $(readMeContainerElement).attr("aria-expanded", "true");
    $(readMeContainerElement).attr("data-bind", "template: { name: 'import-readme-template', data: data }");
    $("#import-readme-container").append(readMeContainerElement);
    ko.applyBindings({ data: model }, readMeContainerElement);

    $("#readme-collapser-container").removeClass("hidden");

    window.nuget.configureExpanderHeading("readme-package-form");

    $("#ReadMeUrlInput").on("change blur", function () {
        clearReadMeError();
    });

    $('#ReadMeFileText').on('click', function () {
        $('#ReadMeFileInput').click();
    });

    $('#ReadMeFileInput').on('change', function () {
        clearReadMeError();
        //displayReadMeEditMarkdown();
        var fileName = window.nuget.getFileName($('#ReadMeFileInput').val());

        if (fileName.length > 0) {
            $('#ReadMeFileText').attr('value', fileName);
            // todo: prepare upload data, cancel existing upload (see async-file-upload example)
        }
        else {
            $('#ReadMeFileText').attr('placeholder', 'Browse or Drop files to select a ReadMe.md file...');
            //displayReadMeError("Please select a file.")
        }
    });

    $("#ReadMeTextInput").on("change", function () {
        clearReadMeError();
    })

    $("#preview-readme-button").on('click', function () {
        previewReadMeAsync();
    });

    if ($("#ReadMeTextInput").val() !== "") {
        $('#readme-tabs a[href="#readme-written"]').tab('show');
        //$("#readme-write-btn").button('toggle');
        previewReadMeAsync();
    }

    $("#edit-markdown-button").on('click', function () {
        displayReadMeEditMarkdown();
    });
}

function previewReadMeAsync(callback, error) {
    // Request source type is generated off the ReadMe tab ids.
    var readMeType = $(".readme-tabs div.active")[0].id.substring(7);

    var formData = new FormData();
    formData.append("ReadMeSourceType", readMeType);

    if (readMeType == "written") {
        var readMeWritten = $("#ReadMeTextInput").val();
        formData.append("SourceText", readMeWritten);
    }
    else if (readMeType == "url") {
        var readMeUrl = $("#ReadMeUrlInput").val();
        formData.append("SourceUrl", readMeUrl);
    }
    else if (readMeType == "file") {
        var readMeFileInput = $("#ReadMeFileInput");
        var readMeFileName = readMeFileInput && readMeFileInput[0] ? window.nuget.getFileName(readMeFileInput.val()) : null;
        var readMeFile = readMeFileName ? readMeFileInput[0].files[0] : null;
        formData.append("SourceFile", readMeFile);
    }

    $.ajax({
        url: "/packages/manage/preview-readme",
        type: "POST",
        contentType: false,
        processData: false,
        data: window.nuget.addAjaxAntiForgeryToken(formData),
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

function displayReadMePreview(response) {
    $("#readme-preview-contents").html(response);
    $("#readme-preview").removeClass("hidden");

    $('.readme-tabs').children().hide();
        
    $("#edit-markdown").removeClass("hidden");
    $("#preview-html").addClass("hidden");
    clearReadMeError();
}

function displayReadMeEditMarkdown() {
    $("#readme-preview-contents").html("");
    $("#readme-preview").addClass("hidden");

    $('.readme-tabs').children().show();

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
