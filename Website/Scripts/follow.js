
function bindFollowButtons() {
    $(".unfollowbtn").click(event, function () {
        var buttonId = $(this).attr("id");
        var packageId = buttonId.substr("unfollow-".length);
        unfollow(packageId);
    });
    $(".followbtn").click(event, function () {
        var buttonId = $(this).attr("id");
        var packageId = buttonId.substr("follow-".length);
        follow(packageId);
    });
}

function showFollowButtons(packageIds) {
    var input = { ids: packageIds };
    $mvc.JsonApi.WhereIsFollowing(input).success(function (result) {
        if (result.success) {
            $(".followbtn").show();
            $(".unfollowbtn").hide();
            var i;
            for (i = 0; i < result.ids.length; i++) {
                var packageId = result.ids[i];
                $(document.getElementById("unfollow-" + packageId)).show();
                $(document.getElementById("follow-" + packageId)).hide();
            }
        }
    });
}

function follow(packageId) {
    var input = { id: packageId };
    $mvc.JsonApi.FollowPackage(input).success(function (result) {
        if (result.success) {
            $(document.getElementById("follow-" + packageId)).hide();
            $(document.getElementById("unfollow-" + packageId)).show();
        }
    });
}

function unfollow(packageId) {
    var input = { id: packageId };
    $mvc.JsonApi.UnfollowPackage(input).success(function (result) {
        if (result.success) {
            $(document.getElementById("unfollow-" + packageId)).hide();
            $(document.getElementById("follow-" + packageId)).show();
        }
    });
}