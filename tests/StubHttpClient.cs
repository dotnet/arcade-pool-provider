using System.Net;
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
            private readonly HttpStatusCode _statusCode;

            public StubHttpMessageHandler(HttpStatusCode statusCode, string responseJson)
            {
                _responseJson = responseJson;
                _statusCode = statusCode;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                var responseMessage = new HttpResponseMessage(_statusCode);
                if (!string.IsNullOrEmpty(_responseJson))
                    responseMessage.Content = new StringContent(_responseJson, Encoding.UTF8, "application/json");
                return Task.FromResult(responseMessage);
            }
        }

        public StubHttpClient(HttpStatusCode statusCode, string responseJson = "")
            : base(new StubHttpMessageHandler(statusCode, responseJson))
        {
        }
    }
}
