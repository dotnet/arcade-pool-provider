using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.DotNet.HelixPoolProvider.Tests
{
    public class AssociatedJobInfoClientTests
    {
        public class TestCase
        {
            public string ResponseFile { get; set; }
            public string ExpectedSourceBranch { get; set; }
            public string ExpectedPullRequestTargetBranch { get; set; }
        }

        public static TheoryData<TestCase> TestCases = new TheoryData<TestCase>
        {
            new TestCase
            {
                ResponseFile = "GetAssociatedJobResponse_ForkPR.json",
                ExpectedSourceBranch = "refs/pull/6892/merge",
                ExpectedPullRequestTargetBranch = "main"
            },
            new TestCase
            {
                ResponseFile = "GetAssociatedJobResponse_Manual.json",
                ExpectedSourceBranch = "refs/heads/pool-provider-test",
                ExpectedPullRequestTargetBranch = ""
            },
            new TestCase
            {
                ResponseFile = "GetAssociatedJobResponse_PR.json",
                ExpectedSourceBranch = "refs/pull/6889/merge",
                ExpectedPullRequestTargetBranch = "main"
            },
        };

        [Theory]
        [MemberData(nameof(TestCases))]
        public async Task TestResponseParsing(TestCase testCase)
        {
            var responseData = LoadTestData(testCase.ResponseFile);
            var associatedJobInfoClient = new AssociatedJobInfoClient(
                new StubHttpClientFactory(() => new StubHttpClient(HttpStatusCode.OK, responseData)),
                new NullLogger<AssociatedJobInfoClient>());

            AssociatedJobInfo response = await associatedJobInfoClient.TryGetAssociatedJobInfo(
                getAssociatedJobUrl: "https://dev.azure.com/dnceng/_apis/distributedtask/agentclouds/7/requests/a7344980-1166-4beb-8ab3-70521d838010/job?api-version=5.0-preview",
                authenticationToken: "test");

            Assert.Equal(testCase.ExpectedSourceBranch, response.BuildSourceBranch);
            Assert.Equal(testCase.ExpectedPullRequestTargetBranch, response.SystemPullRequestTargetBranch);
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task TestErrorHandling(HttpStatusCode azdoResponseStatusCode)
        {
            var associatedJobInfoClient = new AssociatedJobInfoClient(
                new StubHttpClientFactory(() => new StubHttpClient(azdoResponseStatusCode)),
                new NullLogger<AssociatedJobInfoClient>());

            AssociatedJobInfo response = await associatedJobInfoClient.TryGetAssociatedJobInfo(
                getAssociatedJobUrl: "https://dev.azure.com/dnceng/_apis/distributedtask/agentclouds/7/requests/a7344980-1166-4beb-8ab3-70521d838010/job?api-version=5.0-preview",
                authenticationToken: "test");

            Assert.NotNull(response);
            Assert.Equal(string.Empty, response.BuildSourceBranch);
            Assert.Equal(string.Empty, response.SystemPullRequestTargetBranch);
        }

        [Fact]
        public async Task TestExceptionHandling()
        {
            var associatedJobInfoClient = new AssociatedJobInfoClient(
                new StubHttpClientFactory(() => new ThrowingHttpClient()),
                new NullLogger<AssociatedJobInfoClient>());

            AssociatedJobInfo response = await associatedJobInfoClient.TryGetAssociatedJobInfo(
                getAssociatedJobUrl: "https://dev.azure.com/dnceng/_apis/distributedtask/agentclouds/7/requests/a7344980-1166-4beb-8ab3-70521d838010/job?api-version=5.0-preview",
                authenticationToken: "test");

            Assert.NotNull(response);
            Assert.Equal(string.Empty, response.BuildSourceBranch);
            Assert.Equal(string.Empty, response.SystemPullRequestTargetBranch);
        }

        [Fact]
        public async Task TestTimeoutHandling()
        {
            var httpRequestDuration = TimeSpan.FromSeconds(2);
            var timeout = TimeSpan.FromMilliseconds(100);
            var associatedJobInfoClient = new AssociatedJobInfoClient(
                new StubHttpClientFactory(() => new DelayedResponseHttpClient(httpRequestDuration)),
                new NullLogger<AssociatedJobInfoClient>());

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            AssociatedJobInfo response = await associatedJobInfoClient.TryGetAssociatedJobInfo(
                getAssociatedJobUrl: "https://dev.azure.com/dnceng/_apis/distributedtask/agentclouds/7/requests/a7344980-1166-4beb-8ab3-70521d838010/job?api-version=5.0-preview",
                authenticationToken: "test",
                timeout);

            stopwatch.Stop();

            Assert.True(stopwatch.Elapsed < httpRequestDuration, "HTTP call should timeout before completion");
            Assert.NotNull(response);
            Assert.Equal(string.Empty, response.BuildSourceBranch);
            Assert.Equal(string.Empty, response.SystemPullRequestTargetBranch);
        }

        private string LoadTestData(string name)
        {
            Type thisType = GetType();
            string fullName = $"{thisType.FullName}Data.{name}";

            using Stream resourceStream = thisType.Assembly.GetManifestResourceStream(fullName);
            using var reader = new StreamReader(resourceStream);
            return reader.ReadToEnd();
        }
    }
}
