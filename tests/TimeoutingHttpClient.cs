using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.HelixPoolProvider.Tests
{
    public class TimeoutingHttpClient : HttpClient
    {
        private class TimeoutingHttpMessageHandler : HttpMessageHandler
        {
            private readonly TimeSpan _sleepDuration;

            public TimeoutingHttpMessageHandler(TimeSpan sleepDuration)
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

        public TimeoutingHttpClient(TimeSpan sleepDuration)
            : base(new TimeoutingHttpMessageHandler(sleepDuration))
        {
        }
    }
}
