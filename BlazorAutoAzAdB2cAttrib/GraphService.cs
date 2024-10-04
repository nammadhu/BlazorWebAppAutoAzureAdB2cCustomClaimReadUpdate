using Azure.Identity;
using BlazorAutoAzAdB2cAttrib.Client;
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
        //"id", "displayName","othermails", "createdDateTime","ExternalUserStateChangeDateTime",
        nameof(User.Id),nameof(User.DisplayName),nameof(User.OtherMails),nameof(User.CreatedDateTime),
        CustomAttributeRolesKey};
    public async Task<List<UserDto>?> SearchWithFilterStartsWith(string searchString, CancellationToken cancellationToken)
   => await SearchWithFilterStartsWithPattern($"startswith({nameof(User.DisplayName)},'{searchString}')", cancellationToken);
    //or startswith({nameof(User.OtherMails)},'{searchString}')
    //since othermails is a list items,so direct wont work //Todo

    private async Task<List<UserDto>?> SearchWithFilterStartsWithPattern(string pattern, CancellationToken cancellationToken)
    {
        //here search is case insensitive.so no need of tolower kind of
        Console.WriteLine($"pattern is : {pattern}");
        var results = (await _graphClient.Users
            .GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Filter = pattern;// $"mail eq '{email}'";
                requestConfiguration.QueryParameters.Top = 25;
                requestConfiguration.QueryParameters.Select = userSelectionColumns;
            }, cancellationToken))?.Value;
        return results.Select(x=>MapGraphUserToUserDto(x)).ToList();
    }

    public async Task<List<User>?> FullTextSearchUsersNameCurrentlyWontSupportInAllTenants(string name, CancellationToken cancellationToken)
    {
        //currently by default this full text search is not supported by all.so this wont work for all
        var t1 = (await _graphClient.Users
            .GetAsync(requestConfiguration =>
            {
                requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                requestConfiguration.QueryParameters.Search = $"\"{nameof(User.DisplayName)}:{name}\"";
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
    public async Task<UserDto?> GetUserAsync(string userId)
    {
        var userDefault = await _graphClient.Users[userId].GetAsync();
        var user = await _graphClient.Users[userId]
        .GetAsync(config =>
        {
            config.QueryParameters.Select = userSelectionColumns;
        });
        
        return MapGraphUserToUserDto(user);
    }

    public UserDto? MapGraphUserToUserDto(User? graphUser)
    {
        if (graphUser == null) return default;
        var user = new UserDto
        {
            Id = Guid.TryParse(graphUser.Id, out Guid guid) ? guid : Guid.Empty,
            UserName = graphUser.Mail, // Assuming username is the email
            Name = graphUser.DisplayName,
            //Created = graphUser.CreatedDateTime,
            //LastModified = graphUser.LastModifiedDateTime,
            Email = graphUser.OtherMails?[0],
            PhoneNumber = graphUser.MobilePhone
        };
        user.Roles = [];
        if (graphUser.AdditionalData.ContainsKey(CustomAttributeRolesKey))
        {
            var roles = graphUser.AdditionalData[CustomAttributeRolesKey]?.ToString()?.Split(',');
            if (roles != null && roles.Count() > 0)
                foreach (var item in roles)
                {
                    user.Roles.Add(item);
                }
        }
        return user;
    }
}
