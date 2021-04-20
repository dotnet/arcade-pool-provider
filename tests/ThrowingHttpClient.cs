using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.HelixPoolProvider.Tests
{
    public class ThrowingHttpClient : HttpClient
    {
        private class ThrowingHttpMessageHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                throw new HttpRequestException("Test exception");
            }
        }

        public ThrowingHttpClient()
            : base(new ThrowingHttpMessageHandler())
        {
        }
    }
}
