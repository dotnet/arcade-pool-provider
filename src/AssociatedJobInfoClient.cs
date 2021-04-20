using Microsoft.DotNet.HelixPoolProvider.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Microsoft.DotNet.HelixPoolProvider
{
    public class AssociatedJobInfo
    {
        public string BuildSourceBranch { get; }
        public string SystemPullRequestTargetBranch { get; }

        public static readonly AssociatedJobInfo Empty
            = new AssociatedJobInfo("", "");

        public AssociatedJobInfo(
            string buildSourceBranch,
            string systemPullRequestTargetBranch)
        {
            BuildSourceBranch = buildSourceBranch;
            SystemPullRequestTargetBranch = systemPullRequestTargetBranch;
        }
    }

    public class AssociatedJobInfoClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AssociatedJobInfoClient> _logger;

        public AssociatedJobInfoClient(
            IHttpClientFactory httpClientFactory,
            ILogger<AssociatedJobInfoClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<AssociatedJobInfo> TryGetAssociatedJobInfo(
            string getAssociatedJobUrl,
            string authenticationToken)
        {
            try
            {
                var agentRequestJob = await TryGetAssociatedAgentRequestJob(
                    getAssociatedJobUrl,
                    authenticationToken);

                if (agentRequestJob != null)
                {
                    return ParseAssociatedJobInfo(agentRequestJob);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Unable to get associated job info from {getAssociatedJobUrl} because of an exception",
                    getAssociatedJobUrl);
            }

            return AssociatedJobInfo.Empty;
        }

        private async Task<AgentRequestJob> TryGetAssociatedAgentRequestJob(
            string getAssociatedJobUrl,
            string authenticationToken)
        {
            if (!string.IsNullOrEmpty(getAssociatedJobUrl))
            {
                _logger.LogInformation("Getting associated job from {AssociatedJobUrl}", getAssociatedJobUrl);

                var httpClient = _httpClientFactory.CreateClient();
                var message = new HttpRequestMessage(HttpMethod.Get, getAssociatedJobUrl);
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authenticationToken);
                var response = await httpClient.SendAsync(message);
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var responseData = JsonConvert.DeserializeObject<AgentRequestJob>(responseJson);
                    return responseData;
                }
                else
                {
                    _logger.LogInformation("GetAssociatedJob responded with error code {StatusCode}", response.StatusCode);
                }
            }
            else
            {
                _logger.LogInformation("Unable to get associated job info because getAssociatedJobUrl is not set");
            }

            return null;
        }

        private AssociatedJobInfo ParseAssociatedJobInfo(AgentRequestJob job)
        {
            var buildSourceBranch = GetVarValueOrEmpty("build.sourceBranch", job.Job.Variables);
            var pullRequestTargetBranchName = GetVarValueOrEmpty("system.pullRequest.targetBranch", job.Job.Variables);

            return new AssociatedJobInfo(
                buildSourceBranch: buildSourceBranch,
                systemPullRequestTargetBranch: pullRequestTargetBranchName
            );
        }

        private static string GetVarValueOrEmpty(
            string name,
            IList<AgentRequestJobVariable> variables)
        {
            var variable = variables.FirstOrDefault(v =>
                string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));

            if (variable != null)
                return variable.Value ?? string.Empty;
            else
                return string.Empty;
        }
    }
}
