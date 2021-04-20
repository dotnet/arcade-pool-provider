using Microsoft.Extensions.Logging.Abstractions;
using System.IO;
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
        public async Task Test(TestCase testCase)
        {
            var responseData = LoadTestData(testCase.ResponseFile);
            var associatedJobInfoClient = new AssociatedJobInfoClient(
                new StubHttpClientFactory(() => new StubHttpClient(responseData)),
                new NullLogger<AssociatedJobInfoClient>());

            var response = await associatedJobInfoClient.TryGetAssociatedJobInfo(
                getAssociatedJobUrl: "https://dev.azure.com/dnceng/_apis/distributedtask/agentclouds/7/requests/a7344980-1166-4beb-8ab3-70521d838010/job?api-version=5.0-preview",
                authenticationToken: "test");

            Assert.Equal(testCase.ExpectedSourceBranch, response.BuildSourceBranch);
            Assert.Equal(testCase.ExpectedPullRequestTargetBranch, response.SystemPullRequestTargetBranch);
        }

        private string LoadTestData(string name)
        {
            var thisType = GetType();
            var assembly = GetType().Assembly;
            var fullName = $"{thisType.FullName}Data.{name}";

            using var resourceStream = assembly.GetManifestResourceStream(fullName);
            using var reader = new StreamReader(resourceStream);
            return reader.ReadToEnd();
        }
    }
}
