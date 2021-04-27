using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.HelixPoolProvider.Tests
{
    public class DelayedResponseHttpClient : HttpClient
    {
        private class DelayedResponseHttpMessageHandler : HttpMessageHandler
        {
            private readonly TimeSpan _sleepDuration;

            public DelayedResponseHttpMessageHandler(TimeSpan sleepDuration)
            {
                _sleepDuration = sleepDuration;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return Task.Run(async () =>
                {
                    await Task.Delay(_sleepDuration, cancellationToken);
                    return new HttpResponseMessage(HttpStatusCode.OK);
                });
            }
        }

        public DelayedResponseHttpClient(TimeSpan sleepDuration)
            : base(new DelayedResponseHttpMessageHandler(sleepDuration))
        {
        }
    }
}
