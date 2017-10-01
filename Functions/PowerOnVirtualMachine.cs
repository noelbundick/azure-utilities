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
                var redirectLocation = $"ssh://{username}@{fqdn}";
                await startTask;

                var response = req.CreateResponse(HttpStatusCode.OK);
                var content = $"<html><head><meta http-equiv=\"refresh\" content=\"0;URL={redirectLocation}\"></head><body><a href=\"{redirectLocation}\">{redirectLocation}</a></body></html>";
                response.Content = new StringContent(content, Encoding.UTF8, "text/html");
                return response;
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
