﻿namespace BlazorAutoAzAdB2cAttrib.Client;

public class UserDto
{
    public Guid? Id { get; set; }
    public string? UserName { get; set; }
    public string? Name { get; set; }
    public DateTime? Created { get; set; }
    public DateTime? LastModified { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public List<string>? Roles { get; set; }
}
