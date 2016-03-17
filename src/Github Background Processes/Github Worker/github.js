var GitHubApi = require("github");
var http = require('https');
var github = new GitHubApi({ version: "3.0.0" });
var credentials = require('./credentials');

github.authenticate({
    type: "basic",
    username: credentials.githubUsername,
    password: credentials.githubPassword
});

module.exports.getRepo = function(ghrepo, cb){

    github.repos.get(ghrepo, function(err, data){
        if (err || !data) return cb("not found");

        if (data.message == 'Moved Permanently'){

            http.get("https://github.com/" + ghrepo.user + "/" + ghrepo.repo + "/", function(response){
                if (response && response.headers && response.headers.location){
                    return cb("moved", response.headers.location);
                }
                cb("moved");
            });
            return;
        }

        github.repos.getReadme(ghrepo, function(err, readme){
            if (!readme) return cb(null, data);

            try{
                var text = new Buffer(readme.content, 'base64').toString();
            } catch (e){
                console.log("cannot decode", readme);
                return cb(null, data, text);
            }

            github.markdown.render({text:text}, function(err, x){
                data.readme = x;
                return cb(null, data, text);
            });
        });
    });
}

/*
readme:  { message: 'Moved Permanently',
  url: 'https://api.github.com/repositories/37428843/readme',
  documentation_url: 'https://developer.github.com/v3/#http-redirects',
  meta:
   { 'x-ratelimit-limit': '5000',
     'x-ratelimit-remaining': '4947',
     'x-ratelimit-reset': '1438241001',
     location: 'https://api.github.com/repositories/37428843/readme',
     status: '301 Moved Permanently' } }
machx0r/CCSWE-500px-API 4948
nverinaud/CCHMapClusterController-Mono 494
*/
