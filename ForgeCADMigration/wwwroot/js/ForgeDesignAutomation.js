$(document).ready(function () {
     
    $('#startWorkitem').click(startWorkitem);

    startConnection();
}); 

function startWorkitem() {
    var inputFileField = document.getElementById('inputFile');
    if (inputFileField.files.length === 0) { alert('Please select an input file'); return; }
    var extension = "";
    if (inputFileField.files[0].name.lastIndexOf(".") > 0) {
        extension = inputFileField.files[0].name.substring(inputFileField.files[0].name.lastIndexOf("."), inputFileField.files[0].name.length).toLowerCase();
    }
    var formats = ".cgr,.prt,.sldprt,.catpart,.catproduct,.model,.session,.exp,.dlv3,.igs,.ige,.iges,.jt,.rvt,.3dm,.stp,.ste,.step,.stpz,.sat,.smt,.stl,.stla,.stlb,.par, .psm,.asm,.dwf,.dwfx,.dxf,.asm,.sldasm,.dwg";
    var n = formats.search(extension);
    if (n < 0) { alert('File extension (' + extension + ') is invalid to migrate.Please upload valid file'); return; }
    if ($('#activity').val() === null) { alert('Please select an activity'); return };
    var file = inputFileField.files[0];
    startConnection(function () {
        var formData = new FormData();
        formData.append('inputFile', file);
        formData.append('data', JSON.stringify({
            activityName: $('#activity').text(),
            browerConnectionId: connectionId
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

    connection.on("onComplete", function (urnipt) {
        launchViewer(urnipt);
    }); 
}
