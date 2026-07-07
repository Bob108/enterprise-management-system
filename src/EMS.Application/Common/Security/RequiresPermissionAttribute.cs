namespace EMS.Application.Common.Security;

/// <summary>
/// Declares the permission a command/query requires. Enforced by the MediatR
/// authorization behavior — defense in depth behind the API's [HasPermission]
/// endpoint checks (design §9.2).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequiresPermissionAttribute(string permission) : Attribute
{
    public string Permission { get; } = permission;
}
