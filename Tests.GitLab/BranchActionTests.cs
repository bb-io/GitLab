using Apps.Gitlab.Actions;
using Apps.Gitlab.Models.Respository.Requests;
using Apps.GitLab.Constants;
using Blackbird.Applications.Sdk.Common.Invocation;
using Tests.GitLab.Base;

namespace Tests.GitLab
{
    [TestClass]
    public class BranchActionTests : TestBaseWithContext
    {

        [TestMethod, ContextDataSource(ConnectionTypes.PersonalAccessToken)]
        public async Task GetBranch_WithValidData_ReturnsBranch(InvocationContext context)
        {
            var action = new BranchActions(context, FileManagementClient);

            var result = await action.ListRepositoryBranches(new GetRepositoryRequest { RepositoryId = "70992272" });

            foreach (var item in result.Branches)
            {
                Console.WriteLine($"Name {item.Name} ");
            }
            Assert.IsNotNull(result);
        }
    }
}
