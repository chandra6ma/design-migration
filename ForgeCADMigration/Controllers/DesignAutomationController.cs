using Autodesk.Forge;
using Autodesk.Forge.DesignAutomation;
using Autodesk.Forge.DesignAutomation.Model;
using Autodesk.Forge.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Activity = Autodesk.Forge.DesignAutomation.Model.Activity;
using Alias = Autodesk.Forge.DesignAutomation.Model.Alias;
using AppBundle = Autodesk.Forge.DesignAutomation.Model.AppBundle;
using Parameter = Autodesk.Forge.DesignAutomation.Model.Parameter;
using WorkItem = Autodesk.Forge.DesignAutomation.Model.WorkItem;
using WorkItemStatus = Autodesk.Forge.DesignAutomation.Model.WorkItemStatus;

namespace ForgeCADMigration.Controllers
{
    [ApiController]
    public class DesignAutomationController : ControllerBase
    {
        // Used to access the application folder (temp location for files & bundles)
        private IWebHostEnvironment _env;
        // used to access the SignalR Hub
        private IHubContext<DesignAutomationHub> _hubContext;
        // Local folder for bundles
        public string LocalBundlesFolder { get { return Path.Combine(_env.WebRootPath, "bundles"); } }
        /// Prefix for AppBundles and Activities
        public static string NickName { get { return OAuthController.GetAppSetting("FORGE_CLIENT_ID"); } }
        /// Alias for the app (e.g. DEV, STG, PROD). This value may come from an environment variable
        public static string Alias { get { return "dev"; } } 
        // zip file name used to upload bundle
        public static string zipFileName = "TranslatorPlugin";
        // Inventor engine name 
        public static string engineName = "Autodesk.Inventor+24";
        // Design Automation v3 API
        DesignAutomationClient _designAutomation;

        // Constructor, where env and hubContext are specified
        public DesignAutomationController(IWebHostEnvironment env, IHubContext<DesignAutomationHub> hubContext, DesignAutomationClient api)
        {
            _designAutomation = api;
            _env = env;
            _hubContext = hubContext;
        }

        /// <summary>
        /// Names of app bundles on this project
        /// </summary>
        [HttpGet]
        [Route("api/appbundles")]
        public string[] GetLocalBundles()
        {
            // this folder is placed under the public folder, which may expose the bundles
            // but it was defined this way so it be published on most hosts easily
            return Directory.GetFiles(LocalBundlesFolder, "*.zip").Select(Path.GetFileNameWithoutExtension).ToArray();
        }

        /// <summary>
        /// Return a list of available engines
        /// </summary>
        [HttpGet]
        [Route("api/forge/designautomation/engines")]
        public async Task<List<string>> GetAvailableEngines()
        {
            dynamic oauth = await OAuthController.GetInternalAsync();

            // define Engines API
            Page<string> engines = await _designAutomation.GetEnginesAsync();
            engines.Data.Sort();

            return engines.Data; // return list of engines
        }
        /// <summary>
        /// Define a new appbundle
        /// </summary>
        [HttpPost]
        [Route("api/forge/designautomation/appbundles")]
        public async Task<IActionResult> CreateAppBundle( )
        { 
            // standard name for this sample
            string appBundleName = zipFileName + "AppBundle";

            // check if ZIP with bundle is here
            string packageZipPath = Path.Combine(LocalBundlesFolder, zipFileName + ".zip");
            if (!System.IO.File.Exists(packageZipPath)) throw new Exception("Appbundle not found at " + packageZipPath);

            // get defined app bundles
            Page<string> appBundles = await _designAutomation.GetAppBundlesAsync();

            // check if app bundle is already define
            dynamic newAppVersion;
            string qualifiedAppBundleId = string.Format("{0}.{1}+{2}", NickName, appBundleName, Alias);
            if (!appBundles.Data.Contains(qualifiedAppBundleId))
            {
                // create an appbundle (version 1)
                AppBundle appBundleSpec = new AppBundle()
                {
                    Package = appBundleName,
                    Engine = engineName,
                    Id = appBundleName,
                    Description = string.Format("Description for {0}", appBundleName),

                };
                newAppVersion = await _designAutomation.CreateAppBundleAsync(appBundleSpec);
                if (newAppVersion == null) throw new Exception("Cannot create new app");

                // create alias pointing to v1
                Alias aliasSpec = new Alias() { Id = Alias, Version = 1 };
                Alias newAlias = await _designAutomation.CreateAppBundleAliasAsync(appBundleName, aliasSpec);
            }
            else
            {
                // create new version
                AppBundle appBundleSpec = new AppBundle()
                {
                    Engine = engineName,
                    Description = appBundleName
                };
                newAppVersion = await _designAutomation.CreateAppBundleVersionAsync(appBundleName, appBundleSpec);
                if (newAppVersion == null) throw new Exception("Cannot create new version");

                // update alias pointing to v+1
                AliasPatch aliasSpec = new AliasPatch()
                {
                    Version = newAppVersion.Version
                };
                Alias newAlias = await _designAutomation.ModifyAppBundleAliasAsync(appBundleName, Alias, aliasSpec);
            }

            // upload the zip with .bundle
            RestClient uploadClient = new RestClient(newAppVersion.UploadParameters.EndpointURL);
            RestRequest request = new RestRequest(string.Empty, Method.POST);
            request.AlwaysMultipartFormData = true;
            foreach (KeyValuePair<string, string> x in newAppVersion.UploadParameters.FormData) request.AddParameter(x.Key, x.Value);
            request.AddFile("file", packageZipPath);
            request.AddHeader("Cache-Control", "no-cache");
            await uploadClient.ExecuteTaskAsync(request);

            return Ok(new { AppBundle = qualifiedAppBundleId, Version = newAppVersion.Version });
        }
        /// <summary>
        /// Helps identify the engine
        /// </summary>
        private dynamic EngineAttributes(string engine)
        {
            if (engine.Contains("3dsMax")) return new { commandLine = "$(engine.path)\\3dsmaxbatch.exe -sceneFile \"$(args[inputFile].path)\" $(settings[script].path)", extension = "max", script = "da = dotNetClass(\"Autodesk.Forge.Sample.DesignAutomation.Max.RuntimeExecute\")\nda.ModifyWindowWidthHeight()\n" };
            if (engine.Contains("AutoCAD")) return new { commandLine = "$(engine.path)\\accoreconsole.exe /i \"$(args[inputFile].path)\" /al \"$(appbundles[{0}].path)\" /s $(settings[script].path)", extension = "dwg", script = "UpdateParam\n" };
            if (engine.Contains("Inventor")) return new { commandLine = "$(engine.path)\\inventorcoreconsole.exe /i \"$(args[inputFile].path)\" /al \"$(appbundles[{0}].path)\"", extension = "ipt", script = string.Empty };
            if (engine.Contains("Revit")) return new { commandLine = "$(engine.path)\\revitcoreconsole.exe /i \"$(args[inputFile].path)\" /al \"$(appbundles[{0}].path)\"", extension = "rvt", script = string.Empty };
            throw new Exception("Invalid engine");
        }
        /// <summary>
        /// Define a new activity
        /// </summary>
        [HttpPost]
        [Route("api/forge/designautomation/activities")]
        public async Task<IActionResult> CreateActivity(string fileType)
        { 
            // standard name for this sample
            string appBundleName = zipFileName + "AppBundle";
            string activityName = "";
            if (fileType == "part")
            {
                activityName = zipFileName + "Activity";
                Page<string> activities = await _designAutomation.GetActivitiesAsync();
                string qualifiedActivityId = string.Format("{0}.{1}+{2}", NickName, activityName, Alias);
                if (!activities.Data.Contains(qualifiedActivityId))
                {
                    // define the activity
                    // ToDo: parametrize for different engines...
                    dynamic engineAttributes = EngineAttributes(engineName);
                    string commandLine = string.Format(engineAttributes.commandLine, appBundleName);
                    Activity activitySpec = new Activity()
                    {
                        Id = activityName,
                        Appbundles = new List<string>() { string.Format("{0}.{1}+{2}", NickName, appBundleName, Alias) },
                        CommandLine = new List<string>() { commandLine },
                        Engine = engineName,
                        Parameters = new Dictionary<string, Parameter>()
            {
                { "inputFile", new Parameter() { Description = "input file", LocalName = "$(inputFile)", Ondemand = false, Required = true, Verb = Verb.Get, Zip = false } },
                { "outputFile", new Parameter() { Description = "output file", LocalName = "outputFile." + engineAttributes.extension, Ondemand = false, Required = true, Verb = Verb.Put, Zip = false } }
            },
                        Settings = new Dictionary<string, ISetting>()
            {
                { "script", new StringSetting(){ Value = engineAttributes.script } }
            }
                    };
                    Activity newActivity = await _designAutomation.CreateActivityAsync(activitySpec);

                    // specify the alias for this Activity
                    Alias aliasSpec = new Alias() { Id = Alias, Version = 1 };
                    Alias newAlias = await _designAutomation.CreateActivityAliasAsync(activityName, aliasSpec);

                    return Ok(new { Activity = qualifiedActivityId });
                }
            }
            else if (fileType == "assembly")
            {
                activityName = zipFileName + "ActivityAsy";
                Page<string> activities = await _designAutomation.GetActivitiesAsync();
                string qualifiedActivityId = string.Format("{0}.{1}+{2}", NickName, activityName, Alias);
                if (!activities.Data.Contains(qualifiedActivityId))
                {
                    // define the activity
                    // ToDo: parametrize for different engines...
                    dynamic engineAttributes = EngineAttributes(engineName);
                    string commandLine = string.Format(engineAttributes.commandLine, appBundleName);
                    Activity activitySpec = new Activity()
                    {
                        Id = activityName,
                        Appbundles = new List<string>() { string.Format("{0}.{1}+{2}", NickName, appBundleName, Alias) },
                        CommandLine = new List<string>() { commandLine },
                        Engine = engineName,
                        Parameters = new Dictionary<string, Parameter>()
            {
                { "inputFile", new Parameter() { Description = "input file", LocalName = "inputFile", Ondemand = false, Required = true, Verb = Verb.Get, Zip = true  } },
                { "outputFile", new Parameter() { Description = "output file", LocalName = "inputFile", Ondemand = false, Required = true, Verb = Verb.Put, Zip = true  } }
            },
                        Settings = new Dictionary<string, ISetting>()
            {
                { "script", new StringSetting(){ Value = engineAttributes.script } }
            }
                    };
                    Activity newActivity = await _designAutomation.CreateActivityAsync(activitySpec);

                    // specify the alias for this Activity
                    Alias aliasSpec = new Alias() { Id = Alias, Version = 1 };
                    Alias newAlias = await _designAutomation.CreateActivityAliasAsync(activityName, aliasSpec);

                    return Ok(new { Activity = qualifiedActivityId });
                }
            }
            // 
            

            // as this activity points to a AppBundle "dev" alias (which points to the last version of the bundle),
            // there is no need to update it (for this sample), but this may be extended for different contexts
            return Ok(new { Activity = "Activity already defined" });
        }

        /// <summary>
        /// Get all Activities defined for this account
        /// </summary>
        [HttpGet]
        [Route("api/forge/designautomation/activities")]
        public async Task<List<string>> GetDefinedActivities()
        {
            // filter list of 
            Page<string> activities = await _designAutomation.GetActivitiesAsync();
            List<string> definedActivities = new List<string>();
            foreach (string activity in activities.Data)
                if (activity.StartsWith(NickName) && activity.IndexOf("$LATEST") == -1)
                    definedActivities.Add(activity.Replace(NickName + ".", String.Empty));

            return definedActivities;
        }
        /// <summary>
        /// Start a new workitem
        /// </summary>
        [HttpPost]
        [Route("api/forge/designautomation/workitems")]
        public async Task<IActionResult> StartWorkitem([FromForm] StartWorkitemInput input)
        {
            // basic input validation
            WorkItemStatus workItemStatus = null;
            JObject workItemData = JObject.Parse(input.data);

            string browerConnectionId = workItemData["browerConnectionId"].Value<string>();
            string fileType = workItemData["fileType"].Value<string>();
            string activityName = string.Format("{0}.{1}", NickName, workItemData["activityName"].Value<string>());
            List<string> activityList = await GetDefinedActivities();
            if (!activityList.Contains(workItemData["activityName"].Value<string>()))
            {
                await CreateAppBundle();
                await CreateActivity(fileType);
            }
            if (fileType == "assembly")
            {
                string path = _env.ContentRootPath;
                Trace.TraceInformation("Zipping started");
                string fileSavePath = await CreateZipFileStreamAsync(input.inputFile, input.inputFiles, path);

                // OAuth token
                dynamic oauth = await OAuthController.GetInternalAsync();

                // upload file to OSS Bucket
                // 1. ensure bucket existis
                string bucketKey = NickName.ToLower() + "-designautomation";
                BucketsApi buckets = new BucketsApi();
                buckets.Configuration.AccessToken = oauth.access_token;
                try
                {
                    PostBucketsPayload bucketPayload = new PostBucketsPayload(bucketKey, null, PostBucketsPayload.PolicyKeyEnum.Transient);
                    await buckets.CreateBucketAsync(bucketPayload, "US");
                }
                catch { }; // in case bucket already exists
                           // 2. upload inputFile
                string inputFileNameOSS = string.Format("{0}_input_{1}", DateTime.Now.ToString("yyyyMMddhhmmss"), Path.GetFileName(fileSavePath)); // avoid overriding
                ObjectsApi objects = new ObjectsApi();
                objects.Configuration.AccessToken = oauth.access_token;
                using (StreamReader streamReader = new StreamReader(fileSavePath))
                    await objects.UploadObjectAsync(bucketKey, inputFileNameOSS, (int)streamReader.BaseStream.Length, streamReader.BaseStream, "application/octet-stream");
                System.IO.File.Delete(fileSavePath);// delete server copy

                // prepare workitem arguments
                // 1. input file
                XrefTreeArgument inputFileArgument = new XrefTreeArgument()
                {
                    Url = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", bucketKey, inputFileNameOSS),
                    PathInZip = input.inputFile.FileName,
                    Headers = new Dictionary<string, string>()
        {
            { "Authorization", "Bearer " + oauth.access_token }
        }
                };

                // 2. output file
                //string outputFileNameOSS = string.Format("{0}_output_{1}", DateTime.Now.ToString("yyyyMMddhhmmss"), Path.GetFileName(input.inputFile.FileName)); // avoid overriding
                //string outputFileNameOSS = string.Format("{0}_output_{1}.zip", DateTime.Now.ToString("yyyyMMddhhmmss"), Path.GetFileName(input.inputFile.FileName)); // avoid overriding
                string outputFileNameOSS = string.Format("{0}_output_{1}.zip", DateTime.Now.ToString("yyyyMMddhhmmss"), Path.GetFileName(input.inputFile.FileName)); // avoid overriding
                XrefTreeArgument outputFileArgument = new XrefTreeArgument()
                {
                    Url = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", bucketKey, outputFileNameOSS),
                    Verb = Verb.Put,
                    Headers = new Dictionary<string, string>()
            {
                {"Authorization", "Bearer " + oauth.access_token }
            }
                };

                // prepare & submit workitem
                // the callback contains the connectionId (used to identify the client) and the outputFileName of this workitem
                string callbackUrl = string.Format("{0}/api/forge/callback/designautomation?id={1}&outputFileName={2}&name={3}&type={4}", OAuthController.GetAppSetting("FORGE_WEBHOOK_URL"), browerConnectionId, outputFileNameOSS, Path.GetFileNameWithoutExtension(input.inputFile.FileName),fileType);
                WorkItem workItemSpec = new WorkItem()
                {
                    ActivityId = activityName,
                    Arguments = new Dictionary<string, IArgument>()
        {
            { "inputFile", inputFileArgument },
            { "outputFile", outputFileArgument },
            { "onComplete", new XrefTreeArgument { Verb = Verb.Post, Url = callbackUrl } }
        }
                };
                workItemStatus = await _designAutomation.CreateWorkItemAsync(workItemSpec);
            }
            else if (fileType == "part")
            {
                // save the file on the server
                var fileSavePath = Path.Combine(_env.ContentRootPath, Path.GetFileName(input.inputFile.FileName));
                using (var stream = new FileStream(fileSavePath, FileMode.Create)) await input.inputFile.CopyToAsync(stream);

                // OAuth token
                dynamic oauth = await OAuthController.GetInternalAsync();

                // upload file to OSS Bucket
                // 1. ensure bucket existis
                string bucketKey = NickName.ToLower() + "-designautomation";
                BucketsApi buckets = new BucketsApi();
                buckets.Configuration.AccessToken = oauth.access_token;
                try
                {
                    PostBucketsPayload bucketPayload = new PostBucketsPayload(bucketKey, null, PostBucketsPayload.PolicyKeyEnum.Transient);
                    await buckets.CreateBucketAsync(bucketPayload, "US");
                }
                catch { }; // in case bucket already exists
                           // 2. upload inputFile
                string inputFileNameOSS = string.Format("{0}_input_{1}", DateTime.Now.ToString("yyyyMMddhhmmss"), Path.GetFileName(input.inputFile.FileName)); // avoid overriding
                ObjectsApi objects = new ObjectsApi();
                objects.Configuration.AccessToken = oauth.access_token;
                using (StreamReader streamReader = new StreamReader(fileSavePath))
                    await objects.UploadObjectAsync(bucketKey, inputFileNameOSS, (int)streamReader.BaseStream.Length, streamReader.BaseStream, "application/octet-stream");
                System.IO.File.Delete(fileSavePath);// delete server copy

                // prepare workitem arguments
                // 1. input file
                XrefTreeArgument inputFileArgument = new XrefTreeArgument()
                {
                    Url = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", bucketKey, inputFileNameOSS),
                    Headers = new Dictionary<string, string>()
        {
            { "Authorization", "Bearer " + oauth.access_token }
        }
                };

                // 2. output file
                //string outputFileNameOSS = string.Format("{0}_output_{1}", DateTime.Now.ToString("yyyyMMddhhmmss"), Path.GetFileName(input.inputFile.FileName)); // avoid overriding
                string outputFileNameOSS = string.Format("{0}_output_{1}.ipt", DateTime.Now.ToString("yyyyMMddhhmmss"), Path.GetFileName(input.inputFile.FileName)); // avoid overriding
                XrefTreeArgument outputFileArgument = new XrefTreeArgument()
                {
                    Url = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", bucketKey, outputFileNameOSS),
                    Verb = Verb.Put,
                    Headers = new Dictionary<string, string>()
            {
                {"Authorization", "Bearer " + oauth.access_token }
            }
                };

                // prepare & submit workitem
                // the callback contains the connectionId (used to identify the client) and the outputFileName of this workitem
                string callbackUrl = string.Format("{0}/api/forge/callback/designautomation?id={1}&outputFileName={2}&name={3}&type={4}", OAuthController.GetAppSetting("FORGE_WEBHOOK_URL"), browerConnectionId, outputFileNameOSS, "",fileType);
                WorkItem workItemSpec = new WorkItem()
                {
                    ActivityId = activityName,
                    Arguments = new Dictionary<string, IArgument>()
        {
            { "inputFile", inputFileArgument },
            { "outputFile", outputFileArgument },
            { "onComplete", new XrefTreeArgument { Verb = Verb.Post, Url = callbackUrl } }
        }
                };
                workItemStatus = await _designAutomation.CreateWorkItemAsync(workItemSpec);
            }

            

            return Ok(new { WorkItemId = workItemStatus.Id });
        }


        private static async Task<string> CreateZipFileStreamAsync(IFormFile file, IFormFile[] files, string contentPath)
        {
            Trace.TraceInformation("Zip entered");
            string assyZipPath = "";
            string  fileSavePath = Path.Combine(contentPath, Path.GetFileName(file.FileName));
            using (var stream = new FileStream(fileSavePath, FileMode.Create)) await file.CopyToAsync(stream);
            List<FileInfo> fileInfos = new List<FileInfo>();
            FileInfo info = new FileInfo(fileSavePath);
            fileInfos.Add(info);
            foreach (var f in files)
            {
                string temp = Path.Combine(contentPath, Path.GetFileName(f.FileName));
                using (var stream = new FileStream(temp, FileMode.Create)) await f.CopyToAsync(stream);
                info = new FileInfo(temp);
                fileInfos.Add(info);
            }
            ZipArchiveEntry zipArchiveEntry;
            string archiveName = Path.GetFileName(file.FileName);
            using (var archiveStream = new MemoryStream())
            { 
                using (ZipArchive archive = new ZipArchive(archiveStream, System.IO.Compression.ZipArchiveMode.Create, true))
                {
                    foreach (var item in fileInfos)
                    {
                        zipArchiveEntry = archive.CreateEntryFromFile(item.FullName, item.Name, CompressionLevel.Fastest);
                        item.Delete();
                    }

                    archive.Dispose();
                    
                }
                
                assyZipPath = contentPath + @"\" + archiveName + ".zip";
                using (var fileStream = new FileStream(assyZipPath, FileMode.Create))
                {
                    archiveStream.Seek(0, SeekOrigin.Begin);
                    archiveStream.CopyTo(fileStream);
                }
            }
            Trace.TraceInformation("Zip exited");
            return assyZipPath;
        }
        /// <summary>
        /// Translate object
        /// </summary>
        private async Task<dynamic> TranslateObject(dynamic objModel, string outputFileName, bool compressed)
        {
            dynamic oauth = await OAuthController.GetInternalAsync();
            string objectIdBase64 = ToBase64(objModel.objectId);
            // prepare the payload
            List<JobPayloadItem> postTranslationOutput = new List<JobPayloadItem>()
            {
            new JobPayloadItem(
                JobPayloadItem.TypeEnum.Svf,
                new List<JobPayloadItem.ViewsEnum>()
                {
                JobPayloadItem.ViewsEnum._2d,
                JobPayloadItem.ViewsEnum._3d
                })
            };
            JobPayload job;
            job = new JobPayload(
                new JobPayloadInput(objectIdBase64, compressed, outputFileName),
                new JobPayloadOutput(postTranslationOutput)
                );

            // start the translation
            DerivativesApi derivative = new DerivativesApi();
            derivative.Configuration.AccessToken = oauth.access_token;
            dynamic jobPosted = await derivative.TranslateAsync(job, true);
            // check if it is complete.
            dynamic manifest = null;
            do
            {
                System.Threading.Thread.Sleep(1000); // wait 1 second
                try
                {
                    manifest = await derivative.GetManifestAsync(objectIdBase64);
                }
                catch (Exception) { }
            } while (manifest.progress != "complete");
            return jobPosted.urn;
        }

        /// <summary>
        /// Convert a string into Base64 (source http://stackoverflow.com/a/11743162).
        /// </summary>  
        private static string ToBase64(string input)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(input);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        /// <summary>
        /// Input for StartWorkitem
        /// </summary>
        public class StartWorkitemInput
        {
            public IFormFile inputFile { get; set; }
            public IFormFile[] inputFiles { get; set;}
            public string data { get; set; }
        }
        /// <summary>
        /// Callback from Design Automation Workitem (onProgress or onComplete)
        /// </summary>
        [HttpPost]
        [Route("/api/forge/callback/designautomation")]
        public async Task<IActionResult> OnCallback(string id, string outputFileName, string name,string type, [FromBody] dynamic body)
        {
            try
            {
                dynamic oauth = await OAuthController.GetInternalAsync();
                string bucketkey = NickName.ToLower() + "-designautomation";

                ObjectsApi objectsApi = new ObjectsApi();
                objectsApi.Configuration.AccessToken = oauth.access_token;
                dynamic objIPT = await objectsApi.GetObjectDetailsAsync(bucketkey, outputFileName);

                dynamic urnIPT = null;
                if (type == "part")
                {
                    urnIPT = TranslateObject(objIPT, outputFileName, false);
                }
                else if (type == "assembly")
                {
                    urnIPT = TranslateObject(objIPT, name + ".iam", true);
                }

                await _hubContext.Clients.Client(id).SendAsync("onComplete", (string)await urnIPT);

                //generate a signed URL to download the result file and send to the client
                dynamic signedUrl = await objectsApi.CreateSignedResourceAsyncWithHttpInfo(NickName.ToLower() + "-designautomation", outputFileName, new PostBucketsSigned(10), "read");
                await _hubContext.Clients.Client(id).SendAsync("downloadResult", (string)(signedUrl.Data.signedUrl));
            }
            catch {
                
            }

            // ALWAYS return ok (200)
            return Ok();
        }
        /// <summary>
        /// Clear the accounts (for debugging purpouses)
        /// </summary>
        [HttpDelete]
        [Route("api/forge/designautomation/account")]
        public async Task<IActionResult> ClearAccount()
        {
            // clear account
            await _designAutomation.DeleteForgeAppAsync("me");
            return Ok();
        }



    }

    /// <summary>
    /// Class uses for SignalR
    /// </summary>
    public class DesignAutomationHub : Microsoft.AspNetCore.SignalR.Hub
    {
        public string GetConnectionId() { return Context.ConnectionId; }
    }



}
