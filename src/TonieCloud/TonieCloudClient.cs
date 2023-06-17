using HtmlAgilityPack;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace TonieCloud
{
    public class TonieCloudClient
    {
        private const string TONIE_API_URL = "https://api.tonie.cloud";
        private const string AMAZON_UPLOAD_URL = "https://bxn-toniecloud-prod-upload.s3.amazonaws.com";
        private const string TONIE_AUTH_URL = "https://login.tonies.com";
        private Login _Login;
        private readonly HttpClient _Client;
        private readonly JsonSerializerSettings _JsonSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
        private readonly SemaphoreSlim _Semaphore;

        public TonieCloudClient(Login login, int maxParallelFileUploades)
        {
            _Login = login;
            _Client = new HttpClient { BaseAddress = new Uri(TONIE_API_URL) };
            _Semaphore = new SemaphoreSlim(maxParallelFileUploades, maxParallelFileUploades);
        }

        public Login Login { get => _Login; set => _Login = value; }

        public Task<Household[]?> GetHouseholds() => Get<Household[]>("/v2/households");

        public Task<CreativeTonie[]?> GetCreativeTonies(string householdId) => Get<CreativeTonie[]>($"/v2/households/{householdId}/creativetonies");

        public Task<CreativeTonie?> GetCreativeTonie(string householdId, string creativeTonieId) => Get<CreativeTonie>($"/v2/households/{householdId}/creativetonies/{creativeTonieId}");

        public Task<Toniebox[]?> GetTonieboxes(string householdId) => Get<Toniebox[]>($"/v2/households/{householdId}/tonieboxes");

        public async Task<AmazonToken> UploadFile(Stream file, IProgress<long> progress)
        {
            try
            {
                await _Semaphore.WaitAsync();

                // get upload token
                var amazonFile = await Post<AmazonToken>("/v2/file", new ByteArrayContent(new byte[] { }));
                if (amazonFile == null)
                    throw new Exception("Error retrieving AmazonToken in UploadFile");
                // create payload
                var payload = new MultipartContent("form-data");
                payload.AddFormContent("key", amazonFile.Request?.Fields?.Key ?? "");
                payload.AddFormContent("x-amz-algorithm", amazonFile.Request?.Fields?.AmazonAlgorithm ?? "");
                payload.AddFormContent("x-amz-credential", amazonFile.Request?.Fields?.AmazonCredential ?? "");
                payload.AddFormContent("x-amz-date", amazonFile.Request?.Fields?.AmazonDate ?? "");
                payload.AddFormContent("policy", amazonFile.Request?.Fields?.Policy ?? "");
                payload.AddFormContent("x-amz-signature", amazonFile.Request?.Fields?.AmazonSignature ?? "");
                payload.AddFormContent("x-amz-security-token", amazonFile.Request?.Fields?.AmazonSecurityToken ?? "");
                payload.AddStreamContent("file", amazonFile.FileId ?? "", file, "application/octet-stream");

                // upload to S3
                var response = new HttpClient().PostAsync(AMAZON_UPLOAD_URL, payload);
                while (!response.IsCompleted)
                {
                    await Task.Delay(1000);
                    if (progress != null)
                        progress.Report(file.Position);
                }

                if (!response.Result.IsSuccessStatusCode)
                {
                    throw new Exception("Error while upload to Amazon S3");
                }

                return amazonFile;
            }
            finally
            {
                _Semaphore.Release();
            }
        }

        public Task<CreativeTonie?> PatchCreativeTonie(string householdId, string creativeTonieId, string name, IEnumerable<Chapter> chapters)
        {
            var payload = new
            {
                chapters = chapters,
                name = name?.Substring(0, name.Length > 100 ? 100 : name.Length)
            };
            foreach (var c in payload.chapters)
                c.Title = c.Title?.Substring(0, c.Title.Length > 100 ? 100: c.Title.Length);
            return Patch<CreativeTonie>($"/v2/households/{householdId}/creativetonies/{creativeTonieId}", payload);
        }

        private async Task UpdateJwtToken()
        {
            var authClient = new HttpClient()
            {
                BaseAddress = new Uri(TONIE_AUTH_URL)
            };

            // get login url
            var response = await authClient.GetAsync("/auth/realms/tonies/protocol/openid-connect/auth?client_id=my-tonies&redirect_uri=https://my.tonies.com/login&response_type=code&scope=openid");

            // grab login url from content
            var pageDocument = new HtmlDocument();
            pageDocument.LoadHtml(await response.Content.ReadAsStringAsync());

            var loginUrl = new Uri(HttpUtility.HtmlDecode(pageDocument.GetElementbyId("root").Attributes["data-action-url"].Value));

            // login
            var loginRequestData = new Dictionary<string, string>() {
                { "grant_type", "password" },
                { "client_id", "my-tonies" },
                { "username", _Login.Email ?? "" },
                { "password", _Login.Password ?? "" }
            };

            var loginResponse = await authClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, loginUrl) { Content = new FormUrlEncodedContent(loginRequestData) });

            // extract auth code from redirect url
            var auth = QueryHelpers.ParseQuery(loginResponse.RequestMessage.RequestUri.Query);

            if (!auth.ContainsKey("code"))
                throw new Exception("Login failed!");

            // get access token
            var tokenRequestData = new Dictionary<string, string>() {
                { "code", auth["code"].ToString() },
                { "grant_type", "authorization_code" },
                { "client_id", "my-tonies" },
                { "redirect_uri", "https://my.tonies.com/login" }
            };

            var tokenResponse = await authClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/auth/realms/tonies/protocol/openid-connect/token") { Content = new FormUrlEncodedContent(tokenRequestData) });

            var token = JsonConvert.DeserializeObject<Token>(await tokenResponse.Content.ReadAsStringAsync());

            // set authorization
            _Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token?.AccessToken ?? "");
        }

        private Task<T?> Get<T>(string path) where T : class => ExecuteRequest<T>(() => _Client.GetAsync(path)); 

        private Task<T?> Post<T>(string path, HttpContent content) where T : class => ExecuteRequest<T>(() => _Client.PostAsync(path, content));

        private Task<T?> Patch<T>(string path, object content) where T : class 
        {
            var payload = new StringContent(JsonConvert.SerializeObject(content, _JsonSettings), Encoding.UTF8, "application/json");
            Console.WriteLine(JsonConvert.SerializeObject(content, _JsonSettings));
            return ExecuteRequest<T>(() => _Client.PatchAsync(path, payload));
        }

        private async Task<T?> ExecuteRequest<T>(Func<Task<HttpResponseMessage>> action) where T : class
        {
            if (_Client.DefaultRequestHeaders.Authorization == null)
            {
                try
                {
                    await UpdateJwtToken();
                }
                catch {
                    return default(T);
                }
            }
            HttpResponseMessage response = null;
            try
            {
                response = await action.Invoke();
            }
            catch { }

            // refresh jwt token in case of 401
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await UpdateJwtToken();

                response = await action.Invoke();
            }

            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Request failed with {response.StatusCode}: {content}");
            }

            return JsonConvert.DeserializeObject<T>(content ?? "") ?? Activator.CreateInstance<T>();
        }
    }
}
