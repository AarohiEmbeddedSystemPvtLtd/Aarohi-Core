using Aarohi.Configuration;
using Aarohi.UserManagment;

namespace Aarohi.Wpf.Controls.Lib.Controls.UserManagement;

public static class UserManagerConfiguration
{
    private static readonly object SyncRoot = new();
    private static bool isConfigured;

    public static void EnsureConfigured(
        string userSchema = "dbo",
        string userTable = "Users",
        string permissionSchema = "dbo",
        string permissionTable = "Permissions",
        string userPermissionSchema = "dbo",
        string userPermissionTable = "UserPermissions")
    {
        if (isConfigured)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (isConfigured)
            {
                return;
            }

            RootConfig config = new();

            config.UserMapping = new UserMappingConfig();
            config.PermissionMapping = new PermissionMappingConfig();
            config.UserPermissionMapping = new UserPermissionMappingConfig();

            config.UserMapping.Columns = new UserMappingConfig.UserColumns();
            config.PermissionMapping.Columns = new PermissionMappingConfig.PermissionColumns();
            config.UserPermissionMapping.Columns = new UserPermissionMappingConfig.UserPermissionColumns();

            config.UserMapping.Schema = userSchema;
            config.UserMapping.Table = userTable;
            config.UserMapping.Columns.Id = "UserID";
            config.UserMapping.Columns.Name = "UserName";
            config.UserMapping.Columns.Password = "Password";
            config.UserMapping.Columns.Role = "Role";
            config.UserMapping.Columns.ParentId = "ParentID";
            config.UserMapping.Columns.Email = "Email";

            config.PermissionMapping.Schema = permissionSchema;
            config.PermissionMapping.Table = permissionTable;
            config.PermissionMapping.Columns.Id = "PermissionId";
            config.PermissionMapping.Columns.Name = "PermissionName";

            config.UserPermissionMapping.Schema = userPermissionSchema;
            config.UserPermissionMapping.Table = userPermissionTable;
            config.UserPermissionMapping.Columns.Id = "id";
            config.UserPermissionMapping.Columns.UserId = "User_Id";
            config.UserPermissionMapping.Columns.PermissionId = "Permission_Id";

            UserManager.Configure(config);
            isConfigured = true;
        }
    }
}
