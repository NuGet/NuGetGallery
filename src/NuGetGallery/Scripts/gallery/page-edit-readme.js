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

    $('#readme-select-feedback').on('click', function () {
        $('#readme-select-file').click();
    });

    $('#readme-select-file').on('change', function () {
        clearReadMeError();
        //displayReadMeEditMarkdown();
        var fileName = window.nuget.getFileName($('#readme-select-file').val());

        if (fileName.length > 0) {
            $('#readme-select-feedback').attr('value', fileName);
            // todo: prepare upload data, cancel existing upload (see async-file-upload example)
        }
        else {
            $('#readme-select-feedback').attr('placeholder', 'Browse or Drop files to select a ReadMe.md file...');
            //displayReadMeError("Please select a file.")
        }
    });

    $("#readme-written").on("change", function () {
        clearReadMeError();
    })

    $("#preview-readme-button").on('click', function () {
        previewReadMeAsync();
    });

    if ($("#readme-written").val() !== "") {
        $('#readme-tabs a[href="#readme-write"]').tab('show');
        //$("#readme-write-btn").button('toggle');
        previewReadMeAsync();
    }

    $("#edit-markdown-button").on('click', function () {
        displayReadMeEditMarkdown();
    });
}

function previewReadMeAsync(callback, error) {
    var readMeType = $(".readme-tabs div.active")[0].id.substring(7);
    var readMeUrl = $("#ReadMeUrlInput").val();
    var readMeWritten = $("#readme-written").val();
    var readMeFileInput = $("#readme-select-file");
    var readMeFileName = readMeFileInput && readMeFileInput[0] ? window.nuget.getFileName(readMeFileInput.val()) : null;
    var readMeFile = readMeFileName ? readMeFileInput[0].files[0] : null;
        
    if (readMeType == "write") { readMeType = "Written" } // hack to remove

    // Review: FormData is required for ajax file upload support?
    var readmeInputModel = {
        "ReadMeSourceType": readMeType,
        "SourceUrl": readMeUrl,
        "SourceText": readMeWritten,
        "SourceFile": readMeFile
    };

    $.ajax({
        url: "/packages/manage/preview-readme",
        type: "POST",
        data: window.nuget.addAjaxAntiForgeryToken(readmeInputModel),
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
