
function bindFavoriteButtons() {
    $(".unfavoritebtn").click(event, function () {
        var buttonId = $(this).attr("id");
        var packageId = buttonId.substr("unfavorite-".length);
        unfavorite(packageId);
    });
    $(".favoritebtn").click(event, function () {
        var buttonId = $(this).attr("id");
        var packageId = buttonId.substr("favorite-".length);
        favorite(packageId);
    });
}

function showfavoritebuttons(packageIds) {
    var input = { id: packageIds };
    $mvc.JsonApi.IsFavorite(input).success(function (result) {
        if (result.favorite) {
            $(".favoritebtn").hide();
            $(".unfavoritebtn").show();
        } else {
            $(".unfavoritebtn").hide();
            $(".favoritebtn").show();
        }
    });
}

function showFavorite(packageId) {
    var input = { id: packageId };
    $mvc.JsonApi.IsFavorite(input).success(function (result) {
        if (result.favorite) {
            $(".favoritebtn").hide();
            $(".unfavoritebtn").show();
        } else {
            $(".unfavoritebtn").hide();
            $(".favoritebtn").show();
        }
    });
}

function showFavorites(packageIds) {
    var input = { ids: packageIds };
    $mvc.JsonApi.WhereIsFavorite(input).success(function (result) {
        if (result.success) {
            $(".favoritebtn").show();
            $(".unfavoritebtn").hide();
            var i;
            for (i = 0; i < result.favorites.length; i++) {
                var packageId = result.favorites[i];
                $(document.getElementById("unfavorite-" + packageId)).show();
                $(document.getElementById("favorite-" + packageId)).hide();
            }
        }
    });
}

function favorite(packageId) {
    var input = { id: packageId };
    $mvc.JsonApi.FavoritePackage(input).success(function () {
        $(document.getElementById("favorite-" + packageId)).hide();
        $(document.getElementById("unfavorite-" + packageId)).show();
    });
}

function unfavorite(packageId) {
    var input = { id: packageId };
    $mvc.JsonApi.UnfavoritePackage(input).success(function () {
        $(document.getElementById("unfavorite-" + packageId)).hide();
        $(document.getElementById("favorite-" + packageId)).show();
    });
}