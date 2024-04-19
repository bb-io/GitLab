using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Apps.Gitlab
{
    public class ApplicationConstants
    {
        public const string ClientId = "#{APP_GITLAB_CLIENT_ID}#";

        //public const string ClientSecret = "#{APP_GITLAB_SECRET}#";

        public const string Scope = "api read_user";//"#{APP_GITLAB_SCOPE}#";

        //public const string BlackbirdToken = "#{APP_GITLAB_BLACKBIRD_TOKEN}#"; // bridge validates this token

    }
}
