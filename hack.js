var url = require('url');
var express = require("express");
var app = express();

app.set('view engine', 'jade')
// #Session stuff:
app.use(express.static(__dirname + '/public'))
app.use(express.cookieParser());
app.use(express.bodyParser());

var models = [];
function new_model(req, res){
    var temp_model = {
        name: req.body.name,
        url: "/view/" + req.files.model.path
    }
    models.unshift(temp_model);
    models.push("new");
    console.log(req.body);
    console.log(req.files.model.path);
    File.move(req.files.model.path, obj_file_name, function(err){
        if(err)
        {
            console.log("There was an error moving the file to uploads");
            console.log(err);
            callback(err);
        }
        else
        {
            console.log("File saved!");
            req.redirect("/view/" + req.files.model.path);
        } 
    });
}
function view_model(req, res){
    var name = req.params.filename;
    console.log("name: " + name);
    res.render("model", {filenamer: name});
}

app.get("/", function(req, res){
    res.render("main", {models: models});
});

app.post("/new", new_model);
app.get("/view/:filename",view_model);
app.listen(3030);