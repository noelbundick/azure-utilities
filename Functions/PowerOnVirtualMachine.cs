using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using System.Configuration;
using Microsoft.Azure.Management.Compute.Fluent;
using System.IO;
using System.Text;
using Microsoft.Azure.Management.Compute.Fluent.Models;
using System.Net.Http.Headers;

namespace Functions
{
    public static class StartVirtualMachine
    {
        private static IAzure _azure = GetAzure();

        [FunctionName("StartVirtualMachine")]
        public static async Task<HttpResponseMessage> RunAsync([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            var vmName = await GetNameAsync(req, "vm");
            if (vmName == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, "Please pass a virtual machine name on the query string or in the request body");

            var username = await GetNameAsync(req, "username");
            var redirectToLogin = false;
            if (!string.IsNullOrWhiteSpace(username))
                redirectToLogin = true;
            
            var vms = await _azure.VirtualMachines.ListAsync();
            var targetVm = vms.FirstOrDefault(vm => vm.Name == vmName);

            var startTask = (targetVm.PowerState == PowerState.Running)
                ? Task.CompletedTask
                : targetVm.StartAsync();

            if (redirectToLogin)
            {
                var fqdn = targetVm.GetPrimaryPublicIPAddress().Fqdn;
                await startTask;

                if (targetVm.OSType == OperatingSystemTypes.Linux)
                {
                    // Hacky redirect to ssh protocol for Linux
                    // Can't just use HTTP redirect because .NET will always treat the Location header as a Uri and add a trailing slash
                    var redirectLocation = $"ssh://{username}@{fqdn}";
                    var content = $"<html><head><meta http-equiv=\"refresh\" content=\"0;URL={redirectLocation}\"></head><body><a href=\"{redirectLocation}\">{redirectLocation}</a></body></html>";

                    var response = req.CreateResponse(HttpStatusCode.OK);
                    response.Content = new StringContent(content, Encoding.UTF8, "text/html");
                    return response;
                }
                else if (targetVm.OSType == OperatingSystemTypes.Windows)
                {
                    // Download an .rdp file for Windows
                    var rdpFileContents = $"full address:s:{fqdn}:3389\r\nprompt for credentials:i:1\r\nadministrative session:i:1\r\nusername: s:{username}";
                    var content = new StringContent(rdpFileContents);
                    content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                    {
                        FileName = $"{vmName}.rdp"
                    };
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/rdp")
                    {
                        CharSet = "utf-8"
                    };

                    var response = req.CreateResponse(HttpStatusCode.OK);
                    response.Content = content;
                    return response;
                }
                else
                {
                    return req.CreateResponse(HttpStatusCode.OK, $"{vmName} is running");
                }
            }
            else
            {
                await startTask;
                return req.CreateResponse(HttpStatusCode.OK, $"{vmName} is running");
            }
        }

        private static async Task<string> GetNameAsync(HttpRequestMessage req, string key)
        {
            // parse query parameter
            var name = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Equals(q.Key, key, StringComparison.OrdinalIgnoreCase))
                .Value;

            // Get request body
            dynamic data = await req.Content.ReadAsAsync<object>();

            // Set name to query string or body data
            return name ?? data?.name;
        }

        private static IAzure GetAzure()
        {
            var tenantId = ConfigurationManager.AppSettings["tenantId"];
            var sp = new ServicePrincipalLoginInformation
            {
                ClientId = ConfigurationManager.AppSettings["clientId"],
                ClientSecret = ConfigurationManager.AppSettings["clientSecret"]
            };
            return Azure.Authenticate(new AzureCredentials(sp, tenantId, AzureEnvironment.AzureGlobalCloud)).WithDefaultSubscription();
        }
    }
}
