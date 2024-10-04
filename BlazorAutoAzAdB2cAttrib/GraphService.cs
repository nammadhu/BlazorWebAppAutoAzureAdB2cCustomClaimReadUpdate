using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class GraphService(IConfiguration configuration)
{
    GraphServiceClient _graphClient = new GraphServiceClient(new ClientSecretCredential(
       configuration["GraphApi:AzureAdTenantId"],//"44445572-4530-41b5-a916-9f63a6b5ca98"
       configuration["GraphApi:AzureAdClientId"],//"33335572-4530-41b5-a916-9f63a6b5ca98"
       configuration["GraphApi:AzureAdClientSecret"]//"Ocn8Q~8NpumKbT~4JGMpGcMnKADSFDSDBpq2H2qcki"
  ));
    string? AzureAdExtensionsAppId = configuration["GraphApi:AzureAdExtensionsAppId"];//"d7e936cccb39492a0e94b0ae"
    string CustomAttributePrefix => $"extension_{AzureAdExtensionsAppId}_";//AzureAdExtensionsAppId  should NOt have dashes
    string CustomAttributeRolesKey => $"{CustomAttributePrefix}Roles";
    string[] userSelectionColumns => new[]{
        "id", "displayName", "alternateEmailAddress", "createdDateTime", CustomAttributeRolesKey, $"extension_Roles"};
    public async Task<List<User>?> SearchWithFilterStartsWith(string searchString, CancellationToken cancellationToken)
       => await SearchWithFilterStartsWithPattern($"startswith(displayName,'{searchString}') or startswith(mail,'{searchString}')", cancellationToken);

    private async Task<List<User>?> SearchWithFilterStartsWithPattern(string pattern, CancellationToken cancellationToken)
    {
        //here search is case insensitive.so no need of tolower kind of
        Console.WriteLine($"pattern is : {pattern}");
        var t1= (await _graphClient.Users
            .GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Filter = pattern;// $"mail eq '{email}'";
                requestConfiguration.QueryParameters.Top = 25;
                requestConfiguration.QueryParameters.Select = userSelectionColumns;
            }, cancellationToken))?.Value;
        return t1;
    }

    public async Task<List<User>?> FullTextSearchUsersNameCurrentlyWontSupportInAllTenants(string name, CancellationToken cancellationToken)
    {
        //currently by default this full text search is not supported by all.so this wont work for all
        var t1 = (await _graphClient.Users
            .GetAsync(requestConfiguration =>
            {
                requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                requestConfiguration.QueryParameters.Search = $"\"displayName:{name}\"";
                requestConfiguration.QueryParameters.Top = 25;
                requestConfiguration.QueryParameters.Select = userSelectionColumns;
            }, cancellationToken))?.Value;
        return t1;
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
                { "extension_d7e936cccb3943fabcdc992a0e94b0ae_Roles", roles },
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
            config.QueryParameters.Select = new[] { "id", "displayName", "extension_d7e936cccb3943fabcdc992a0e94b0ae_RolesCard", "extension_d7e936cccb3943fabcdc992a0e94b0ae_Roles" };
        });
        return user;
    }
}
