using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.HelixPoolProvider.Tests
{
    public class StubHttpClient : HttpClient
    {
        private class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly string _responseJson;

            public StubHttpMessageHandler(string responseJson)
                => _responseJson = responseJson;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                var responseMessage = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                responseMessage.Content = new StringContent(_responseJson, Encoding.UTF8, "application/json");
                return Task.FromResult(responseMessage);
            }
        }

        public StubHttpClient(string responseJson)
            : base(new StubHttpMessageHandler(responseJson))
        {
        }
    }
}
