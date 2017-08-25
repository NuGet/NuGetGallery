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

    window.nuget.configureExpander(
        "readme-package-form",
        "ChevronRight",
        "Import ReadMe",
        "ChevronDown",
        "Import ReadMe");

    $(".readme-file").hide();
    $(".readme-write").hide();

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

    if ($("#readme-written").val() !== "") {
        $("#readme-write-btn").button('toggle');
        previewReadMeAsync();
    }

    $("#edit-markdown-button").on('click', function () {
        displayReadMeEditMarkdown();
    });
}

function previewReadMeAsync(callback, error) {
    // collect and validate readme data
    var readMeType = $("input[name='ReadMe.ReadMeType']:checked").val();
    var readMeUrl = $("#ReadMeUrlInput").val();
    var readMeWritten = $("#readme-written").val();
    var readMeFileInput = $("#readme-select-file");
    var readMeFileName = readMeFileInput && readMeFileInput[0] ? readMeFileInput.val().split("\\").pop() : null;
    var readMeFile = readMeFileName && validateReadMeFileName(readMeFileName) ? readMeFileInput[0].files[0] : null;

    if (readMeType === undefined) {
        if (readMeUrl) {
            readMeType = "Url";
        }
        else if (readMeWritten) {
            readMeType = "Written";
        }
        else if (readMeFile) {
            readMeType = "File";
        }
    }

    // prepare form data with antiforgery token
    var formData = new FormData();
    var token = $('[name=__RequestVerificationToken]').val();
    formData.append("__RequestVerificationToken", token);
    formData.append("ReadMeType", readMeType);

    if (readMeType === "Url" && readMeUrl) {
        formData.append("ReadMeUrl", readMeUrl);
    }
    else if (readMeType === "Written" && readMeWritten) {
        formData.append("ReadMeWritten", readMeWritten);
    }
    else if (readMeType === "File" && readMeFile) {
        formData.append("ReadMeFile", readMeFile);
    }
    else {
        console.warn("Skipping preview: Missing or invalid README data.")
        return;
    }

    $.ajax({
        url: "/packages/manage/preview-readme",
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