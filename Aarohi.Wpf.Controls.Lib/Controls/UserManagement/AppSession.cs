using Aarohi.UserManagment;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Aarohi.Wpf.Controls.Lib.Controls.UserManagement;

public sealed class AppSession
{
    private readonly HashSet<string> grantedPermissions = new(StringComparer.OrdinalIgnoreCase);

    public static AppSession Current { get; } = new();

    public event EventHandler? PermissionsChanged;

    public string CurrentUserName { get; private set; } = string.Empty;

    public int CurrentUserId { get; private set; }

    public string CurrentUserRole { get; private set; } = string.Empty;

    public string CurrentShift { get; private set; } = string.Empty;

    public bool IsDevUser { get; private set; }

    public IReadOnlyCollection<string> GrantedPermissions => grantedPermissions;

    public bool HasPermission(string permissionName)
    {
        if (string.IsNullOrWhiteSpace(permissionName))
            return true;

        return IsDevUser || grantedPermissions.Contains(permissionName.Trim());
    }

    public bool HasPermission<TEnum>(TEnum permission) where TEnum : Enum
    {
        return HasPermission(permission.ToString());
    }

    public void Clear()
    {
        CurrentUserName = string.Empty;
        CurrentUserId = 0;
        CurrentUserRole = string.Empty;
        CurrentShift = string.Empty;
        IsDevUser = false;
        grantedPermissions.Clear();
        PermissionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetCurrentShift(string shiftName)
    {
        CurrentShift = shiftName?.Trim() ?? string.Empty;
    }

    public async Task<bool> LoadAsync(string userName, string password, bool isDevUser = false)
    {
        UserManagerConfiguration.EnsureConfigured();

        CurrentUserName = userName?.Trim() ?? string.Empty;
        CurrentUserId = 0;
        CurrentUserRole = string.Empty;
        CurrentShift = string.Empty;
        IsDevUser = isDevUser;
        grantedPermissions.Clear();

        if (IsDevUser)
        {
            if (!PasswordHasher.IsDevPassword(password))
            {
                PermissionsChanged?.Invoke(this, EventArgs.Empty);
                return false;
            }

            try
            {
                DataTable catalog = UserManager.LoadPermissions(CurrentUserName, IsDevUser);
                Dictionary<int, string> permissionNamesById = BuildPermissionNameMap(catalog);
                foreach (string permissionName in permissionNamesById.Values)
                {
                    grantedPermissions.Add(permissionName);
                }
            }
            catch
            {
                // Catalog load optional for dev
            }

            CurrentUserRole = "Developer";
            PermissionsChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        if (string.IsNullOrWhiteSpace(CurrentUserName))
        {
            PermissionsChanged?.Invoke(this, EventArgs.Empty);
            return false;
        }

        try
        {
            DataTable users = UserManager.GetUsersFromDB();
            CurrentUserId = FindUserId(users, CurrentUserName, password, out string userRole);

            if (CurrentUserId <= 0)
            {
                PermissionsChanged?.Invoke(this, EventArgs.Empty);
                return false;
            }

            CurrentUserRole = userRole;
            DataTable permissionCatalog = UserManager.LoadPermissions(CurrentUserName, IsDevUser);
            HashSet<int> userPermissionIds = await UserManager.LoadUserPermissionsAsync(CurrentUserId, CurrentUserName);
            Dictionary<int, string> permissionNamesById = BuildPermissionNameMap(permissionCatalog);

            foreach (int permissionId in userPermissionIds)
            {
                if (!permissionNamesById.TryGetValue(permissionId, out string? permissionName))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(permissionName))
                {
                    grantedPermissions.Add(permissionName);
                }
            }

            PermissionsChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch
        {
            grantedPermissions.Clear();
            PermissionsChanged?.Invoke(this, EventArgs.Empty);
            return false;
        }
    }

    private static int FindUserId(DataTable users, string userName, string password, out string userRole)
    {
        userRole = string.Empty;

        if (users == null || users.Rows.Count == 0)
        {
            return 0;
        }

        string userIdColumn = UserManager.UserMap.Columns.Id;
        string userNameColumn = UserManager.UserMap.Columns.Name;
        string passwordColumn = UserManager.UserMap.Columns.Password;
        string roleColumn = UserManager.UserMap.Columns.Role;

        foreach (DataRow row in users.Rows)
        {
            string currentUserName = Convert.ToString(row[userNameColumn])?.Trim() ?? string.Empty;
            if (!string.Equals(currentUserName, userName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (row[userIdColumn] != DBNull.Value && int.TryParse(Convert.ToString(row[userIdColumn]), out int userId))
            {
                string storedPassword = Convert.ToString(row[passwordColumn])?.Trim() ?? string.Empty;
                if (!PasswordHasher.Sha256Matches(password, storedPassword))
                {
                    userRole = string.Empty;
                    return 0;
                }

                if (!string.IsNullOrWhiteSpace(roleColumn) && users.Columns.Contains(roleColumn))
                {
                    userRole = Convert.ToString(row[roleColumn])?.Trim() ?? string.Empty;
                }

                return userId;
            }
        }

        return 0;
    }

    private static Dictionary<int, string> BuildPermissionNameMap(DataTable permissionCatalog)
    {
        Dictionary<int, string> permissionNamesById = new();

        if (permissionCatalog == null || permissionCatalog.Rows.Count == 0)
        {
            return permissionNamesById;
        }

        string permissionIdColumn = UserManager.PermissionMap.Columns.Id;
        string permissionNameColumn = UserManager.PermissionMap.Columns.Name;

        foreach (DataRow row in permissionCatalog.Rows)
        {
            if (row[permissionIdColumn] == DBNull.Value)
            {
                continue;
            }

            if (!int.TryParse(Convert.ToString(row[permissionIdColumn]), out int permissionId))
            {
                continue;
            }

            string permissionName = Convert.ToString(row[permissionNameColumn])?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(permissionName))
            {
                continue;
            }

            permissionNamesById[permissionId] = permissionName;
        }

        return permissionNamesById;
    }
}
