$(document).ready(function () {
     
    $('#startWorkitem').click(startWorkitem);

    startConnection();
}); 

function startWorkitem() { 
    var fileType;
    var n;
    var formats;
    var extension;
    var file;
    var a_inputFilesField;
    var activityName;
    if (document.getElementById("part").checked === true) {
        fileType = "part";
        var p_inputFileField = document.getElementById('p_inputFile');
        if (p_inputFileField.files.length === 0) { alert('Please select input files to translate'); return; }
        extension = "";
        if (p_inputFileField.files[0].name.lastIndexOf(".") > 0) {
            extension = p_inputFileField.files[0].name.substring(p_inputFileField.files[0].name.lastIndexOf("."), p_inputFileField.files[0].name.length).toLowerCase();
        }
        formats = ".cgr,.prt,.sldprt,.catpart,.model,.session,.exp,.dlv3,.igs,.ige,.iges,.jt,.rvt,.3dm,.stp,.ste,.step,.stpz,.sat,.smt,.stl,.stla,.stlb,.par, .psm,.asm,.dwf,.dwfx,.dxf,.dwg";
        n = formats.search(extension);
        file = p_inputFileField.files[0];
        activityName = $('#p_activity').text();
    }
    else if (document.getElementById("assembly").checked === true) {
        fileType = "assembly";
        a_inputFilesField = document.getElementById('a_inputFiles');
        var a_inputFileField = document.getElementById('a_inputFile');
        if (a_inputFileField.files.length === 0 && a_inputFilesField.files.length === 0) { alert('Please select input files to translate'); return; }
        if (a_inputFileField.files[0].name.lastIndexOf(".") > 0) {
            extension = a_inputFileField.files[0].name.substring(a_inputFileField.files[0].name.lastIndexOf("."), a_inputFileField.files[0].name.length).toLowerCase();
        }
        formats = ".cgr,.prt,.zip,.sldprt,.catpart,.catproduct,.model,.session,.exp,.dlv3,.igs,.ige,.iges,.jt,.rvt,.3dm,.stp,.ste,.step,.stpz,.sat,.smt,.stl,.stla,.stlb,.par, .psm,.asm,.dwf,.dwfx,.dxf,.asm,.sldasm,.dwg";
        n = formats.search(extension);
        file = a_inputFileField.files[0];
        activityName = $('#a_activity').text();
    }
    
    if (n < 0) { alert('File extension (' + extension + ') is invalid to migrate.Please upload valid file'); return; }
      
    var i;    
    startConnection(function () {
        var formData = new FormData();
        formData.append('inputFile', file);
        if (fileType === "assembly") {
            for (i = 0; i < a_inputFilesField.files.length; i++) {
                formData.append('inputFiles', a_inputFilesField.files[i]);
            }
        }       
        formData.append('data', JSON.stringify({
            activityName: activityName,
            browerConnectionId: connectionId,
            fileType: fileType
        })); 
        $.ajax({
            url: 'api/forge/designautomation/workitems',
            data: formData,
            processData: false,
            contentType: false,
            type: 'POST',
            success: function (res) {
                writeLog('Migration started with id: ' + res.workItemId);
            }
        });
    });
}

function writeLog(text) {
    $('#outputlog').append('<div style="border-top: 1px dashed #C0C0C0">' + text + '</div>');
    var elem = document.getElementById('outputlog');
    elem.scrollTop = elem.scrollHeight;
}

var connection;
var connectionId;

    function startConnection(onReady) {
        if (connection && connection.connectionState) { if (onReady) onReady(); return; }
        connection = new signalR.HubConnectionBuilder().withUrl("/api/signalr/designautomation").build();
        connection.start()
            .then(function () {
                connection.invoke('getConnectionId')
                    .then(function (id) {
                        connectionId = id; // we'll need this...
                        if (onReady) onReady();
                    });
            });

        connection.on("downloadResult", function (url) {
            writeLog('<a href="' + url + '">Download result file here</a>');
        });
        connection.on("onTracing", function (message) {
            writeLog(message);
        });

        connection.on("onComplete", function (urnipt) {
            launchViewer(urnipt);
        });
    }
