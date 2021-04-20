using System;
using System.Net.Http;

namespace Microsoft.DotNet.HelixPoolProvider.Tests
{
    public class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly Func<HttpClient> _factoryCallback;

        public StubHttpClientFactory(Func<HttpClient> factoryCallback)
            => _factoryCallback = factoryCallback;

        public HttpClient CreateClient(string name)
            => _factoryCallback();
    }
}
