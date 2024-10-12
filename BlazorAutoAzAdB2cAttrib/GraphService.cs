using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Shared;

/*
  "AzureAdB2C": {
"Instance": "https://namadhu.b2clogin.com",
"ClientId": "0a16abcas-11e8-4662-aa25-27c2d55e0e4c", //"{Your Application (client) ID}",
"CallbackPath": "/signin-oidc",
"Domain": "namadhu.onmicrosoft.com",
"SignUpSignInPolicyId": "B2C_1_SocialAndPhoneEmailSignupSignin", //"{Your Signup policy}",
"ResetPasswordPolicyId": "",
"EditProfilePolicyId": ""
},
 */
public class GraphService(ILogger<GraphService> logger, IConfiguration configuration)
{
    int MaxResultsCount = 25;
    private string? AzureAdExtensionsAppId = configuration["GraphApi:AzureAdExtensionsAppId"];//"d7e936cccb39492a0e94b0ae"
    //AzureAdExtensionsAppId should NOt have dashes

    //tenantId,clientId,clientSecret
    GraphServiceClient _graphClient = new GraphServiceClient(new ClientSecretCredential(
        configuration["GraphApi:AzureAdTenantId"],//"44445572-4530-41b5-a916-9f63a6b5ca98"
        configuration["GraphApi:AzureAdClientId"],//"33335572-4530-41b5-a916-9f63a6b5ca98"
        configuration["GraphApi:AzureAdClientSecret"]//"Ocn8Q~8NpumKbT~4JGMpGcMnKADSFDSDBpq2H2qcki"
   ));

    //const string CustomAttributeRoles = "Roles";
    string CustomAttributePrefix => $"extension_{AzureAdExtensionsAppId}_";//AzureAdExtensionsAppId  should NOt have dashes
    string CustomAttributeRolesKey => $"{CustomAttributePrefix}Roles";

    string[] userSelectionColumns => new[]{
        //"id", "displayName","othermails", "createdDateTime",
        nameof(User.Id),nameof(User.DisplayName),nameof(User.OtherMails),nameof(User.CreatedDateTime),
        CustomAttributeRolesKey};
    //string CustomAttributeRolesCardKey => $"extension_{AzureAdExtensionsAppId}_RolesCard";



    //public async Task<List<UserDto>?> SearchUsersByEmail(string? email, CancellationToken cancellationToken)
    //    => string.IsNullOrEmpty(email)?null: await SearchUsersWithPattern($"alternateEmailAddress eq '{email.ToLower()}'", cancellationToken);
    //public async Task<List<UserDto>?> SearchUsersByRole(string role, CancellationToken cancellationToken)
    //    => string.IsNullOrEmpty(role) ? null : await SearchUsersWithPattern($"contains({CustomAttributeRolesKey}, '{role}')", cancellationToken);

    //public async Task<List<UserDto>?> SearchUsersByName(string name, CancellationToken cancellationToken)
    //    => string.IsNullOrEmpty(name) ? null : await SearchUsersWithPattern($"contains(tolower(displayName), '{name.ToLower()}')", cancellationToken);

    public async Task<List<UserDto>?> SearchUsersByNameOrEmail(string? name, string? email, CancellationToken cancellationToken, string joinOperatorAND_OR = "or")
    //since roles are csv so cant do starts with search,so now excluding
    {
        //Currently full text search is not available,so using only startswith search. Later will do contains fulltext
        //here search is case insensitive.so no need of tolower kind of
        if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(email)) return [];

        if (string.IsNullOrEmpty(name))
            name = email;

        string pattern = $"startswith(displayName, '{name}')";
        /* as currently its showing as alternateemailaddress not found ,so  going with name alone
         * bool isOrRequired = false;
        if (!string.IsNullOrEmpty(name))
        {
            pattern += $"startswith(displayName, '{name}')";
            isOrRequired = true;
        }
        if (!string.IsNullOrEmpty(email))
        {
            pattern += $"{(isOrRequired ? $" {joinOperatorAND_OR} " : "")} startswith(alternateEmailAddress, '{email}')";
            isOrRequired = true;
        }*/
        //if (!string.IsNullOrEmpty(role))
        //{
        //    pattern += $"{(isOrRequired ? $" {joinOperatorAND_OR} " : "")} contains({CustomAttributeRolesKey}, '{role}')";
        //    isOrRequired = true;
        //}
        //should not send if name or email is empty
        return await SearchUsersWithPattern(pattern, cancellationToken);

        //return   await SearchUsersWithPattern($"contains(tolower(displayName), '{name.ToLower()}') or contains(tolower(alternateEmailAddress), '{email.ToLower()}') or contains({CustomAttributeRolesKey}, '{role}')", cancellationToken);
    }
    private async Task<List<UserDto>?> SearchUsersWithPattern(string pattern, CancellationToken cancellationToken)
    {
        logger.LogWarning($"{nameof(SearchUsersWithPattern)}-'{pattern}'-Started");
        var users = (await _graphClient.Users
            .GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Filter = pattern;// $"mail eq '{email}'";
                requestConfiguration.QueryParameters.Top = MaxResultsCount;
                requestConfiguration.QueryParameters.Select = userSelectionColumns;
            }, cancellationToken))?.Value;
        if (users == null || users.Count == 0) return [];

        var res = users.Where(x => x != null).Select(MapGraphUserToUserDto);
        return res.Where(x => x != null).ToList();
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
            Email = graphUser.OtherMails?.FirstOrDefault(),
            PhoneNumber = graphUser.MobilePhone
        };
        user.Roles = [];
        if (graphUser.AdditionalData.ContainsKey(CustomAttributeRolesKey))
        {
            var roles = graphUser.AdditionalData[CustomAttributeRolesKey]?.ToString()?.Split(',');
            if (roles != null && roles.Count() > 0)
                foreach (var item in roles)
                {
                    user.Roles.Add(new RoleDto() { RoleName = item });
                }
        }
        return user;
    }

    public async Task<UserDto?> GetUserAsync(string userId, CancellationToken cancellationToken)
    => MapGraphUserToUserDto(await GetGraphUserAsync(userId, cancellationToken));

    private async Task<User?> GetGraphUserAsync(string userId, CancellationToken cancellationToken)
    {
        //var userDefault = await _graphClient.Users[userId].GetAsync();//this returns without any additionalData of extensions,so below is the modified with selected data
        try
        {
            logger.LogTrace($"{nameof(GetGraphUserAsync)}-{userId}-Started");
            var user = await _graphClient.Users[userId]
            .GetAsync(config =>
            {
                config.QueryParameters.Select = userSelectionColumns;
            }, cancellationToken: cancellationToken);
            logger.LogTrace($"{nameof(GetGraphUserAsync)}-{userId}-{(user != null ? "Success" : "Failed")}");

            //var roles=user.AdditionalData["extension_12345678123412341234123456789012_customAttribute"];
            //var roles = user?.AdditionalData[CustomAttributeRolesKey]?.ToString()??"";
            return user;
        }
        catch (Exception e)
        {
            logger.LogError($"{nameof(GetGraphUserAsync)}-{userId}-Exception:{e.ToString()}");
            throw;
        }
    }
    //, $"extension_Roles" mostly this wont work out even for fetching

    public async Task<UserDto?> GetLoggedInUserAsync(CancellationToken cancellationToken)
    => MapGraphUserToUserDto(await GetLoggedInGraphUserAsync(cancellationToken));
    private async Task<User?> GetLoggedInGraphUserAsync(CancellationToken cancellationToken) => await _graphClient.Me.GetAsync(config =>
    {
        config.QueryParameters.Select = userSelectionColumns;
    }, cancellationToken: cancellationToken);

    public async Task<List<string>?> GetUserRolesAsync(string userId, CancellationToken cancellationToken)
        => RolesExtraction(await GetGraphUserAsync(userId, cancellationToken: cancellationToken));

    public async Task<List<string>?> GetLoggedInUserRolesAsync(CancellationToken cancellationToken)
    => RolesExtraction(await GetLoggedInGraphUserAsync(cancellationToken: cancellationToken));


    public async Task<bool> AddUserRolesCustomAttribute(string userId, List<string> rolesNew, CancellationToken cancellationToken)
    {
        var user = await GetGraphUserAsync(userId, cancellationToken: cancellationToken);
        rolesNew = UserRolesCombinationChecks(user, rolesNew);
        //if (rolesNew == null || rolesNew?.Count == 0) return user;
        if (rolesNew != null && rolesNew.Any())
            return await UpdateCustomAttribute(userId, CustomAttributeRolesKey, StringExtensions.ToCsv(rolesNew), cancellationToken: cancellationToken);
        return true;
    }
    public async Task<bool> UpdateUserRolesCustomAttributeByOverWriting(string userId, List<string> roles, CancellationToken cancellationToken)
    => await UpdateCustomAttribute(userId, CustomAttributeRolesKey, StringExtensions.ToCsv(roles), cancellationToken: cancellationToken);

    public async Task<bool> UpdateCustomAttribute(string userId, string customAttributeKey, string attributeValue, CancellationToken cancellationToken)
    {//"Creator,Editor,Owner"
     //extension_<AzureAdExtensionsAppId>_<CustomAttributeName>
     //extension_d7e936cccabcde92a0e94b0ae_CardRoles
        try
        {
            if (!customAttributeKey.StartsWith(CustomAttributePrefix))
                customAttributeKey = $"{CustomAttributePrefix}{customAttributeKey}";
            var user = new User
            {
                //Id=userId,//this should not be updated
                AdditionalData = new Dictionary<string, object>
            {
                { customAttributeKey, attributeValue },
                 //{ "extension_RolesCard", roles }//this wont work as this is additional data
            }
            };
            logger.LogWarning($"{nameof(UpdateCustomAttribute)}-{userId}-{customAttributeKey}-{attributeValue}-Started");
            await _graphClient.Users[userId].PatchAsync(user, cancellationToken: cancellationToken);
            //since above pathasync always returns null only
            //so if latest required then do additional getasync separately
            logger.LogWarning($"{nameof(UpdateCustomAttribute)}-{userId}-{customAttributeKey}-{attributeValue}-Updated");
            return true;
        }
        catch (Exception e)
        {
            logger.LogError($"{nameof(UpdateCustomAttribute)}-{userId}-{customAttributeKey}-{attributeValue}-Exception:{e.ToString()}");
            throw;
        }
    }

    public async Task<UserDto?> UpdateUserDisplayNameAsync(string userId, string name, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogWarning($"{nameof(UpdateUserDisplayNameAsync)}-{userId}-{name}-Started");
            var result = await _graphClient.Users[userId].PatchAsync(new User() { DisplayName = name }, cancellationToken: cancellationToken);
            //Id=userId,//this should not be updated
            logger.LogWarning($"{nameof(UpdateUserDisplayNameAsync)}-{userId}-{name}-{(result != null ? "Success" : "Failed")}");
            return MapGraphUserToUserDto(result);
        }
        catch (Exception e)
        {
            logger.LogError($"{nameof(UpdateUserDisplayNameAsync)}-{userId}-{name}-Exception:{e.ToString()}");
            throw;
        }
    }

    //mostly >me wont support update,so use above itself
    //public async Task<UserDto?> UpdateLoggedInUserDisplayNameAsync(string name, CancellationToken cancellationToken) =>
    //       string.IsNullOrEmpty(name) ? null :
    //        MapGraphUserToUserDto(await _graphClient.Me.PatchAsync(new User() { DisplayName = name }, cancellationToken: cancellationToken));

    private List<string>? RolesExtraction(User? me)
    {
        return me?.AdditionalData[CustomAttributeRolesKey]?.ToString()?.Split(',')?.ToList();
        //if (me == null) return null;

        //var roleObject = me.AdditionalData.TryGetValue(Constants_AzureAdB2c.Extension_Roles, out object? roles);
        //if (roles == null || string.IsNullOrEmpty(roles?.ToString())) return null;

        //return roles.ToString()!.Split(',').ToList();
    }



    public async Task<bool> RemoveUserRoles(string userId, List<string> rolesToRemove, CancellationToken cancellationToken)
    {
        var user = await GetGraphUserAsync(userId, cancellationToken: cancellationToken);
        var existingRoles = RolesExtraction(user);
        if (existingRoles == null || existingRoles.Count == 0 || !existingRoles.Intersect(rolesToRemove).Any()) return true;

        existingRoles = existingRoles.Except(rolesToRemove).ToList();

        return (await UpdateUserRolesCustomAttributeByOverWriting(userId, existingRoles, cancellationToken: cancellationToken)) != null;
    }
    public async Task<bool> RemoveLoggedInUserRoles(List<string> rolesToRemove, CancellationToken cancellationToken)
    {
        var user = await GetLoggedInGraphUserAsync(cancellationToken: cancellationToken);
        var existingRoles = RolesExtraction(user);
        if (existingRoles == null || existingRoles.Count == 0 || !existingRoles.Intersect(rolesToRemove).Any()) return true;

        existingRoles = existingRoles.Except(rolesToRemove).ToList();

        return (await UpdateLoggedInUserRoleCustomAttributeAsync(StringExtensions.ToCsv(existingRoles), cancellationToken: cancellationToken)) != null;
    }

    public async Task<bool> DeleteUserCompletely(string userId, CancellationToken cancellationToken)
    {
        try
        {
            await _graphClient.Users[userId].DeleteAsync(cancellationToken: cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private List<string> UserRolesCombinationChecks(User? user, List<string> rolesNew)
    {
        if (user == null) return [];
        if (user.AdditionalData.TryGetValue(CustomAttributeRolesKey, out object? roleObject) && roleObject != null && rolesNew != null)
        {
            var existingRolesCsv = (string)roleObject;
            if (!string.IsNullOrEmpty(existingRolesCsv))
            {
                var existingRolesList = existingRolesCsv.Split(',');
                if (!rolesNew.Any(n => !existingRolesList.Contains(n))) rolesNew = [];
                else
                    rolesNew = existingRolesList.Union(rolesNew).ToList();
            }
        }
        return rolesNew ?? [];
    }

    private async Task<UserDto?> UpdateLoggedInUserRoleCustomAttributeAsync(string rolesAsCsv, CancellationToken cancellationToken) =>
       string.IsNullOrEmpty(rolesAsCsv) ? null :
       MapGraphUserToUserDto(await _graphClient.Me.PatchAsync(new User()
       {
           AdditionalData = new Dictionary<string, object>
            {
                { CustomAttributeRolesKey,rolesAsCsv },
               //{ "extension_RolesCard", roles }//this wont work as this is additional data
            }
       }, cancellationToken: cancellationToken));

    private async Task<List<User>?> SearchUsersNameByFullTextNeedsTenantPermissionCurrentlyNotEnabledLater(string name, CancellationToken cancellationToken)
    {
        var t1 = (await _graphClient.Users
            .GetAsync(requestConfiguration =>
            {
                requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                requestConfiguration.QueryParameters.Search = $"\"{nameof(User.DisplayName)}:{name}\"";
                // $"\"displayName:{searchString}\" OR \"mail:{searchString}\""
                requestConfiguration.QueryParameters.Top = 25;
                requestConfiguration.QueryParameters.Select = userSelectionColumns;
            }, cancellationToken))?.Value;
        return t1;
    }

}
/*
 public async Task<User?> AddLoggedInUserRoleCustomAttribute(string role)
    {
        var user = await GetLoggedInUserAsync();
        if (user == null) return null;
        if (user.AdditionalData.TryGetValue(CustomAttributeRolesKey, out object roleObject) && roleObject != null)
        {
            var t1 = (string)roleObject;
            if (!string.IsNullOrEmpty(t1))
            {
                if (t1.Split(',').Contains(role)) return user;
                role = t1 + $",{role}";
            }
        }
        return await UpdateLoggedInUserRoleCustomAttributeAsync(role);
    }

    public async Task<bool> RemoveUserRole(string userId, string role, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(userId, cancellationToken: cancellationToken);
        var roles = RolesExtraction(user);
        if (roles == null || roles.Count == 0 || !roles.Contains(role)) return true;

        roles.Remove(role);

        roles ??= [];
        return (await UpdateUserRolesCustomAttributeByOverWriting(userId, roles, cancellationToken: cancellationToken)) != null;
    }

    public async Task<bool> RemoveLoggedInUserRole(string role, CancellationToken cancellationToken)
    {
        var user = await GetLoggedInUserAsync(cancellationToken: cancellationToken);
        var roles = RolesExtraction(user);
        if (roles == null || roles.Count == 0 || !roles.Contains(role)) return true;

        roles.Remove(role);

        string roleToUpdate = (roles == null || !roles.Any()) ? string.Empty : StringExtensions.ToCsv(roles);
        return (await UpdateLoggedInUserRoleCustomAttributeAsync(roleToUpdate, cancellationToken: cancellationToken)) != null;
    }
 */ 