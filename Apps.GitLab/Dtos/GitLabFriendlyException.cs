using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Apps.GitLab.Dtos
{
    public class GitLabFriendlyException : ArgumentException
    {
        public GitLabFriendlyException(string message) : base(JsonConvert.DeserializeObject<GitLabErrorMessageDto>(message).Message)
        {
        }
    }
}
