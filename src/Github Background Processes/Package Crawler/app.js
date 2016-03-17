var request = require('request');
var url = require('url');
var specialCases = require('./github-special-cases');
var queue = require('./queue');
var termite = require('termite');
var credentials = require('./credentials');

var ghlist = {};
var pageQueue = [];

start();

function start(){
    var readingIndex = termite.write("reading nuget index");
    request.get("http://api.nuget.org/v3/catalog0/index.json", function(err, response, data){
        if (err) return console.log(err);
        var index = JSON.parse(data);
        pageQueue = index.items;
        readingIndex.ok();
        readPage();
    });
}

function readPage(){
    if (pageQueue.length === 0) return;
    var queueItem = pageQueue.pop();
    var pageRead = termite.write("reading page " + queueItem["@id"])
    request.get(queueItem["@id"], function(err, response, data){
        if (err) return console.log(err);
        pageRead("READ");
        var packages = JSON.parse(data).items;
        var done = false;

        var read = function(){
            if (packages.length === 0) {
                if (done) return;
                done = true;
                pageRead.ok();
                setTimeout(readPage, 0);
                return;
            }
            readPackage(packages.pop(), function(){
                read();
            });
        };

        // start multiple read operations
        for (var i = 0; i < 8; i++) read();

    });
}

function readPackage(package, cb){
    //var packageRead = termite.write(package["@id"])
    request.get(package["@id"], function(err, response, data){
        if (err) return console.log(err);
        var x = JSON.parse(data);
        //packageRead.ok();

        if (specialCases[x.id]){
            var ghcreds = specialCases[x.id];
        } else {
            var ghcreds = getGithubRepo(x);
        }

        if (ghcreds && x.listed && !x.isPrerelease && !ghlist[x.Id]){

            // if we haven't looked at this github repo, go and grab it
            ghlist[x.Id] = true;

            // push the repo onto the queue for indexing
            queue.push({
                Id: x.id,
                Version: x.version,
                Description : x.description.split(". ")[0],
                user: ghcreds.user,
                repo : ghcreds.repo});
        }

        cb();
    });
}

function getGithubRepo(value){
	if (!value.projectUrl) return false;
	var ghurl = url.parse(value.projectUrl);
	if (ghurl.hostname !== 'github.com') return false;
	var pathParts = ghurl.pathname.split('/').filter(notEmpty);
	if (pathParts.length >= 2) return {user:pathParts[0], repo: pathParts[1].replace(".git", "")};
	return false;
}

function notEmpty(value){
	return value != "";
}

var writeCount = 0;
