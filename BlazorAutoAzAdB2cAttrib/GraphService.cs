using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class GraphService
{
   
    private readonly GraphServiceClient _graphClient;

    public GraphService(IConfiguration configuration)
    {
        var clientSecretCredential = new ClientSecretCredential(
            "774a5572-dubbmy-here-9f63a6b5ca98",
            "0a169a64--dubbmy-here-aa25-27c2d55e0e4c",
            "Ocn8Q~8-dubbmy-here-XAQBpq2H2qcki"
        );

        _graphClient = new GraphServiceClient(clientSecretCredential);
    }

    public async Task UpdateUserRolesAsync(string userId, string roles = "test1Role")
    {
        //extension_<AzureAdExtensionsAppId>_<CustomAttributeName>
        //extension_d7e936cccb3943fabcdc992a0e94b0ae_CardRoles
        var user = new User
        {
            //Id=userId,//this should not be updated
            DisplayName = "Madhu at " + DateTime.Now,
            AdditionalData = new Dictionary<string, object>
            {
                { "extension_d7e936cccb3943fabcdc992a0e94b0ae_RolesCard", roles },
                 //{ "extension_RolesCard", roles }
            }
        };
        //var u1 = await GetUserAsync(userId);
        await _graphClient.Users[userId].PatchAsync(user);
    }
    public async Task<User?> GetUserAsync(string userId)
    {
        var userDefault = await _graphClient.Users[userId].GetAsync();
        var user = await _graphClient.Users[userId]
        .GetAsync(config =>
        {
            config.QueryParameters.Select = new[] { "id", "displayName", "extension_d7e936cccb3943fabcdc992a0e94b0ae_RolesCard" };
        });
        return user;
    }
}
