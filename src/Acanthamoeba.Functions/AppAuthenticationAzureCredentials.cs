using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;
using Microsoft.Rest.Azure.Authentication;
using System;
using System.Collections.Generic;
using Microsoft.Rest;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Acanthamoeba.Functions
{
    public class AppAuthenticationAzureCredentials : AzureCredentials
    {
        private readonly AzureServiceTokenProvider _tokenProvider;
        private IDictionary<Uri, ServiceClientCredentials> _credentialsCache = new Dictionary<Uri, ServiceClientCredentials>();
        
        public AppAuthenticationAzureCredentials(AzureServiceTokenProvider tokenProvider, AzureEnvironment environment) : base(null, null, null, environment)
        {
            _tokenProvider = tokenProvider;
        }

        public async override Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var adSettings = new ActiveDirectoryServiceSettings
            {
                AuthenticationEndpoint = new Uri(Environment.AuthenticationEndpoint),
                TokenAudience = new Uri(Environment.ManagementEndpoint),
                ValidateAuthority = true
            };
            string url = request.RequestUri.ToString();
            if (url.StartsWith(Environment.GraphEndpoint, StringComparison.OrdinalIgnoreCase))
            {
                adSettings.TokenAudience = new Uri(Environment.GraphEndpoint);
            }

            if (!_credentialsCache.ContainsKey(adSettings.TokenAudience))
            {
                if (_tokenProvider != null)
                {
                    var token = await _tokenProvider.GetAccessTokenAsync(adSettings.TokenAudience.OriginalString);
                    _credentialsCache[adSettings.TokenAudience] = new TokenCredentials(token);
                }
                // no token available for communication
                else
                {
                    throw new RestException($"Cannot communicate with server. No authentication token available for '{adSettings.TokenAudience}'.");
                }
            }
            await _credentialsCache[adSettings.TokenAudience].ProcessHttpRequestAsync(request, cancellationToken);
        }
    }
}
