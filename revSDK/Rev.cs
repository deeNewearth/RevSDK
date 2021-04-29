using Newtonsoft.Json;
using Polly;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using revCore.Utilities;
using System.Dynamic;

namespace revCore
{
    public class Rev
    {
        static Rev()
        {
            ServicePointManager.ServerCertificateValidationCallback +=
            (sender, cert, chain, sslPolicyErrors) => true;

            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;


        }

        readonly HttpClient _httpClient = null;
        readonly Func<string, string> completedRevUrl;
        readonly Config _config;
        readonly Polly.Retry.AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

        public Rev(
            Config config
            )
        {
            _config = config;

            if (string.IsNullOrWhiteSpace(_config.jwt))
                throw new Exception("invalid config.JWT");

            if (string.IsNullOrWhiteSpace(_config.workspace))
                throw new Exception("invalid config.workspace");

            if (string.IsNullOrWhiteSpace(_config.revUrl))
                throw new Exception("invalid config.revUrl");


            var trimmed = _config.revUrl.TrimEnd('/');
            completedRevUrl = (str) => $"{trimmed}{str}";

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("workspace", _config.workspace);
            _httpClient.DefaultRequestHeaders.Authorization
                = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.jwt);


            _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(message =>
            {
                var ret = !message.IsSuccessStatusCode
                && message.StatusCode != HttpStatusCode.NotImplemented
                && message.StatusCode != HttpStatusCode.NotFound
                && message.StatusCode != HttpStatusCode.Unauthorized
                ;

                return ret;
            })
            .WaitAndRetryAsync(8, i => {
				if (i == 1)
					return TimeSpan.FromMilliseconds(1);

				if(i>4)
					return TimeSpan.FromSeconds(10 * (i-4));

				return TimeSpan.FromSeconds(5);

				}, (result, timeSpan, retryCount, context) =>
            {
				//var t = result;
				if (1 == retryCount)
					return;

                Console.WriteLine($"Request failed with {result.Result.StatusCode}. Waiting {timeSpan} before next retry. Retry attempt {retryCount}");
            });

        }

        class ProjectDef
        {
            public string name { get; set; }
        }


        IReadOnlyDictionary<string, string> _mapProjectNamesToId = null;

        /// <summary>
        /// return map of repo name (in lowercase) -> repoID
        /// </summary>
        /// <returns></returns>
        public async Task<IReadOnlyDictionary<string, string>> ensureProjectLoadedAsync()
        {
            if (null != _mapProjectNamesToId)
                return _mapProjectNamesToId;

            Console.WriteLine("Loading projects");
            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.GetAsync(completedRevUrl("/api/projects")));

            var projects = await HttpHelpers.handleHttpResponse(new Dictionary<string, ProjectDef>(), response);

            try
            {
                _mapProjectNamesToId = projects.ToDictionary(kv => kv.Value.name.ToLower(), kv => kv.Key);
                return _mapProjectNamesToId;
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to create repository maps. Please check if there are duplicated names", ex);
            }



        }

        public async Task ResetSearchIndexes()
        {
            var response = await _httpClient.GetAsync(
                            completedRevUrl($"/api/ReIndexAll"));

            await HttpHelpers.handleHttpResponse(response);

        }

        public async Task<string> CreateRepoAsync(string name, RevField[] fields)
        {
            dynamic revProject = new ExpandoObject();
            revProject.name = name;
            revProject.fields = fields;

            var jsonData = JsonConvert.SerializeObject(revProject);

            var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

            //create the REV field
            var response = await _httpClient.PostAsync(
                            completedRevUrl($"/api/projects"), content
                            );

            var uddatedProject = await HttpHelpers.handleHttpResponse(new
            {
                id = "",
                name = "",
                fields = new[]{
                        new
                        {
                            id="",
                            displayName="",
                            fieldType = "",
                        }
                    }
            }, response);

            //reload the projects
            _mapProjectNamesToId = null;
            await ensureProjectLoadedAsync();

            return uddatedProject.name;
        }

        string _defCartId= null;

        async Task<string> ensureDefaultCartAsync()
        {
            if (null != _defCartId)
                return _defCartId;

            Console.WriteLine("Loading default");
            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.GetAsync(completedRevUrl("/api/carts")));

            var carts = await HttpHelpers.handleHttpResponse(new[] { new { id=""} }, response);

            if (carts.Length == 0)
                throw new Exception("No upload carts. Please upload one test image to initialize uploads");

            _defCartId = carts[0].id;

            return _defCartId;

        }

        
        public IEnumerable<RevDoc> SearchDocs(string repoName, Dictionary<string, string> values =null)
        {
            if (string.IsNullOrWhiteSpace(repoName))
                throw new ArgumentNullException("repo name is needed");

            repoName = repoName.ToLower();

            var projects = ensureProjectLoadedAsync().Result;
            if (!projects.ContainsKey(repoName))
                throw new Exception($"repository {repoName} not found in list");


            if (null == values)
                values = new Dictionary<string, string>();

            var searchRequest = new
            {
                fields = values.ToDictionary(kv=>kv.Key,kv=> new[] { kv.Value }),
                
                selectedProjects = new[] { projects[repoName] }
            };

            var jsonData = JsonConvert.SerializeObject(searchRequest);
            Console.WriteLine($"Query : {jsonData}");


            var currentPage = 0;
            var totalCount = -1;
            var totalpages = -1;
            var pageSize = 500;
            while (true)
            {
                var postUrl = completedRevUrl($"/api/search?page={currentPage}&pagesize={pageSize}");
                Console.WriteLine($"Loading documents -> {postUrl}");

                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                var response = _retryPolicy.ExecuteAsync(() => _httpClient.PostAsync(
                           postUrl, content
               )).Result;

                var documents = HttpHelpers.handleHttpResponse(new
                {
                    totalCount = 0,
                    documents = new[] { new RevDoc() }

                }, response).Result;

                if (-1 == totalCount)
                {
                    totalCount = documents.totalCount;
                    totalpages = (int)Math.Ceiling((totalCount * 1.0) / pageSize);

                    Console.WriteLine($"found {totalCount} documents in {totalpages} pages");
                }

                foreach (var d in documents.documents)
                    yield return d;

                if (++currentPage >= totalpages)
                {
                    break;
                }
            }
        }

        public async Task DeleteDocumentAsync(string docId)
        {
            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.DeleteAsync(
                            completedRevUrl($"/api/document/{docId}")));

            await HttpHelpers.handleHttpResponse("", response);
        }

        public async Task<string> CreateDocumentAsync(string projectName,
            IReadOnlyDictionary<string, string> indexes,
            ExistingPage[] pages,
            Dictionary<string,object> otherParams = null)
        {
            projectName = projectName.ToLower();

            var projects = await ensureProjectLoadedAsync();
            if (!projects.ContainsKey(projectName))
                throw new Exception($"repository {projectName} not found in list");

            await ensureDefaultCartAsync();

            var docToSave = new Dictionary<string, object>() ;
            docToSave["projectId"] = projects[projectName];
            docToSave["indexes"] = indexes;
            docToSave["pages"] = pages.Select(u =>
            {
                dynamic thepage = new ExpandoObject();
                thepage.id = u.id;
                thepage.orderNumber = u.orderNumber;
                thepage.pageType = u.pageType;
                thepage.originalPageName = u.originalPageName;
                thepage.created = DateTime.Now;
                thepage.modified = DateTime.Now;

                //just point to a dummy holder, Note the holder needs to be a real holder Id
                thepage.path = $"invalidPath?holder={_defCartId}&junk=funk";

                return thepage;

            });

            if (null != otherParams)
            {
                foreach (var kv in otherParams)
                    docToSave[kv.Key] = kv.Value;
            }


            var jsonData = JsonConvert.SerializeObject(new[] { docToSave });
            var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsync(
                            completedRevUrl($"/api/document"), content
                            ));

            var uddatedDocs = await HttpHelpers.handleHttpResponse(new[] { new { error = "", document = new { id = "" } } }, response);

            if (uddatedDocs.Length != 1)
                throw new Exception(" bad save result");

            if (!string.IsNullOrWhiteSpace(uddatedDocs[0].error))
                throw new Exception($"Failed to save doc  -> {uddatedDocs[0].error}");

            return uddatedDocs[0].document.id;

        }

        public async Task<string> CreateDocument(string projectName, IReadOnlyDictionary<string, string> indexes, FileInfo[] files)
        {
            var uploads = await Task.WhenAll( files.Select(async fi => await uploadFilesAsync(fi)));

            var pages = uploads.Select(u => new ExistingPage
            {
                id = u.pageId,
                orderNumber = 0,
                originalPageName = u.id,
                pageType = "unprocessed"
            }).ToArray();

            return await CreateDocumentAsync(projectName, indexes, pages);
        }

        async Task<UploadedFile> uploadFilesAsync(FileInfo fi)
        {
            var ext = Uri.EscapeDataString(Path.GetExtension(fi.Name).Trim('.'));

            var upUrl = completedRevUrl($"/api/Pages/newpageid/{ext}?createSignedURL=true&randomId={Guid.NewGuid()}");

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.GetAsync(upUrl));

            var url = await HttpHelpers.handleHttpResponse(new { id = "", keyForDirectUpload = "" }, response);

            upUrl = url.keyForDirectUpload;


            var ret = new UploadedFile
            {
                id = fi.FullName,
                pageId = url.id
            };

            

            {
                HttpWebRequest httpRequest = WebRequest.Create(upUrl) as HttpWebRequest;
                httpRequest.Method = "PUT";
                httpRequest.Timeout = (_config?.timeOut ?? 100) * 1000;

                using (Stream dataStream = httpRequest.GetRequestStream())
                {
                    //var buffer = new byte[8000];
                    var buffer = new byte[200 * 1024]; //80KB
                    using (FileStream fileStream = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read))
                    {
                        int bytesRead = 0;
                        while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await dataStream.WriteAsync(buffer, 0, bytesRead);
                        }
                    }
                }

                try
                {
                    HttpWebResponse webResponse = (await httpRequest.GetResponseAsync()) as HttpWebResponse;

                    if (HttpStatusCode.OK != webResponse.StatusCode)
                    {
                        throw new Exception($"failed to upload file, code:{webResponse.StatusCode}");
                    }

                    return ret;
                }
                catch (WebException e)
                {
                    Console.WriteLine($"S3 upload failed {e}");
                    if (e.Status == WebExceptionStatus.ProtocolError)
                    {
                        Console.WriteLine("Status Code : {0}", ((HttpWebResponse)e.Response).StatusCode);
                        Console.WriteLine("Status Description : {0}", ((HttpWebResponse)e.Response).StatusDescription);
                    }

                    throw new RetryException("S3 upload failed", e);
                }
            }


        }

    }
}
