var formFieldFocusColor = "#e4f1f7";

$(function() {
    setTimeout(function() { $(".message-Information").fadeOut(); }, 6000);

    var searchPlaceholder = "Search Packages";

    $("#searchTerm").focus(function() {
        if (this.value === searchPlaceholder) {
            this.value = '';
        };
    });

    $("#searchTerm").blur(function() {
        if (this.value === '') {
            this.value = searchPlaceholder;
        };
    });

    $("#search-form").submit(function() {
        var searchTerm = $("#searchTerm").val();
        if (searchTerm === searchPlaceholder) {
            $("#searchTerm").val("");
        }

        var sortOrder = $('#search-filter-form').find('#sortOrder').val();
        var pageSize = $('#search-filter-form').find('#pageSize').val();
        
        // TODO: This hidden input hack can go away when we restructure the UI better.
        $('input[name=sortOrder][type=hidden]').val(sortOrder);
        $('input[name=pageSize][type=hidden]').val(pageSize);

        return true;
    });

    $("#sortOrder").change(function() { $(this).closest("form").submit(); });
    $("#pageSize").change(function() { $(this).closest("form").submit(); });
});
