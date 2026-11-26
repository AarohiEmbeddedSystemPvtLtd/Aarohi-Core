using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Aarohi.Classes;
using Aarohi.Configuration;
using Aarohi.UserManagment;
using Aarohi.Wpf.Controls.Lib.Controls.MessageBox;

namespace Aarohi.Wpf.Controls.Lib.Controls.UserManagement
{
    public partial class UserManagementControl : UserControl
    {
        #region Fields

        private int _selectedUserId = 0;
        private bool _isLoadingSelection = false;

        private readonly ObservableCollection<UserGridItem> _users = new();
        private readonly ObservableCollection<UserTreeNode> _userTree = new();
        private readonly ObservableCollection<UserComboItem> _parentUsers = new();
        private readonly ObservableCollection<PermissionCheckItem> _permissions = new();

        private ICollectionView? _usersView;

        public string CurrentUserName { get; set; } = string.Empty;
        public bool IsDevUser { get; set; } = true;
        public bool IsOtpEnabled { get; set; } = false;

        public static List<string> SeedPermissions { get; } = new();
        public static Action<string>? LogAction { get; set; }

        #endregion

        #region Registration & Configuration Helpers

        public static void RegisterPermissions(IEnumerable<string> permissions)
        {
            if (permissions == null) return;
            foreach (string p in permissions)
            {
                if (!string.IsNullOrWhiteSpace(p) && !SeedPermissions.Contains(p.Trim(), StringComparer.OrdinalIgnoreCase))
                {
                    SeedPermissions.Add(p.Trim());
                }
            }
        }

        public static void RegisterPermissions<TEnum>() where TEnum : Enum
        {
            RegisterPermissions(Enum.GetNames(typeof(TEnum)));
        }

        #endregion

        #region Constructor

        public UserManagementControl()
        {
            InitializeComponent();

            UserManagerConfiguration.EnsureConfigured();

            Loaded += UserManagementControl_Loaded;
        }

        #endregion

        #region Orientation & Responsive Lifecycle

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (sizeInfo.NewSize.Width > 0 && sizeInfo.NewSize.Height > 0)
            {
                bool isPortrait = sizeInfo.NewSize.Height > sizeInfo.NewSize.Width && sizeInfo.NewSize.Width < 950;
                ApplyOrientationLayout(isPortrait);
            }
        }

        private bool? _lastAppliedPortrait = null;

        private void ApplyOrientationLayout(bool isPortrait)
        {
            if (_lastAppliedPortrait.HasValue && _lastAppliedPortrait.Value == isPortrait)
            {
                return;
            }
            _lastAppliedPortrait = isPortrait;

            if (RootDockPanel == null || HeaderGrid == null || SummaryCardsGrid == null || BodyGrid == null)
            {
                return;
            }

            if (isPortrait)
            {
                RootDockPanel.Margin = new Thickness(12);

                HeaderRow0.Height = GridLength.Auto;
                HeaderRow1.Height = GridLength.Auto;
                HeaderCol0.Width = new GridLength(1, GridUnitType.Star);
                HeaderCol1.Width = new GridLength(0);

                Grid.SetRow(HeaderTitleStack, 0);
                Grid.SetColumn(HeaderTitleStack, 0);
                Grid.SetColumnSpan(HeaderTitleStack, 2);

                Grid.SetRow(HeaderButtonsStack, 1);
                Grid.SetColumn(HeaderButtonsStack, 0);
                Grid.SetColumnSpan(HeaderButtonsStack, 2);
                HeaderButtonsStack.HorizontalAlignment = HorizontalAlignment.Left;
                HeaderButtonsStack.Margin = new Thickness(0, 12, 0, 0);

                SummaryRow0.Height = GridLength.Auto;
                SummaryRow1.Height = GridLength.Auto;
                SummaryCol0.Width = new GridLength(1, GridUnitType.Star);
                SummaryCol1.Width = new GridLength(1, GridUnitType.Star);
                SummaryCol2.Width = new GridLength(0);
                SummaryCol3.Width = new GridLength(0);

                Grid.SetRow(BorderTotalUsers, 0);
                Grid.SetColumn(BorderTotalUsers, 0);
                BorderTotalUsers.Margin = new Thickness(0, 0, 8, 8);

                Grid.SetRow(BorderRootUsers, 0);
                Grid.SetColumn(BorderRootUsers, 1);
                BorderRootUsers.Margin = new Thickness(0, 0, 0, 8);

                Grid.SetRow(BorderSelectedUser, 1);
                Grid.SetColumn(BorderSelectedUser, 0);
                BorderSelectedUser.Margin = new Thickness(0, 0, 8, 0);

                Grid.SetRow(BorderPermissions, 1);
                Grid.SetColumn(BorderPermissions, 1);
                BorderPermissions.Margin = new Thickness(0);

                BodyRow0.Height = new GridLength(1.0, GridUnitType.Star);
                BodyRow1.Height = new GridLength(1.15, GridUnitType.Star);
                BodyCol0.Width = new GridLength(1.0, GridUnitType.Star);
                BodyCol1.Width = new GridLength(1.4, GridUnitType.Star);
                BodyCol2.Width = new GridLength(0);

                Grid.SetRow(BorderUserDataGrid, 0);
                Grid.SetColumn(BorderUserDataGrid, 0);
                Grid.SetColumnSpan(BorderUserDataGrid, 2);
                BorderUserDataGrid.Margin = new Thickness(0, 0, 0, 12);

                Grid.SetRow(BorderUserHierarchy, 1);
                Grid.SetColumn(BorderUserHierarchy, 0);
                Grid.SetColumnSpan(BorderUserHierarchy, 1);
                BorderUserHierarchy.Margin = new Thickness(0, 0, 12, 0);

                Grid.SetRow(BorderUserDetails, 1);
                Grid.SetColumn(BorderUserDetails, 1);
                Grid.SetColumnSpan(BorderUserDetails, 1);
                BorderUserDetails.Margin = new Thickness(0);
            }
            else
            {
                RootDockPanel.Margin = new Thickness(18);

                HeaderRow0.Height = GridLength.Auto;
                HeaderRow1.Height = new GridLength(0);
                HeaderCol0.Width = new GridLength(1, GridUnitType.Star);
                HeaderCol1.Width = GridLength.Auto;

                Grid.SetRow(HeaderTitleStack, 0);
                Grid.SetColumn(HeaderTitleStack, 0);
                Grid.SetColumnSpan(HeaderTitleStack, 1);

                Grid.SetRow(HeaderButtonsStack, 0);
                Grid.SetColumn(HeaderButtonsStack, 1);
                Grid.SetColumnSpan(HeaderButtonsStack, 1);
                HeaderButtonsStack.HorizontalAlignment = HorizontalAlignment.Right;
                HeaderButtonsStack.Margin = new Thickness(0);

                SummaryRow0.Height = GridLength.Auto;
                SummaryRow1.Height = new GridLength(0);
                SummaryCol0.Width = new GridLength(1, GridUnitType.Star);
                SummaryCol1.Width = new GridLength(1, GridUnitType.Star);
                SummaryCol2.Width = new GridLength(1, GridUnitType.Star);
                SummaryCol3.Width = new GridLength(1, GridUnitType.Star);

                Grid.SetRow(BorderTotalUsers, 0);
                Grid.SetColumn(BorderTotalUsers, 0);
                BorderTotalUsers.Margin = new Thickness(0, 0, 12, 0);

                Grid.SetRow(BorderRootUsers, 0);
                Grid.SetColumn(BorderRootUsers, 1);
                BorderRootUsers.Margin = new Thickness(0, 0, 12, 0);

                Grid.SetRow(BorderSelectedUser, 0);
                Grid.SetColumn(BorderSelectedUser, 2);
                BorderSelectedUser.Margin = new Thickness(0, 0, 12, 0);

                Grid.SetRow(BorderPermissions, 0);
                Grid.SetColumn(BorderPermissions, 3);
                BorderPermissions.Margin = new Thickness(0);

                BodyRow0.Height = new GridLength(1, GridUnitType.Star);
                BodyRow1.Height = new GridLength(0);
                BodyCol0.Width = new GridLength(310);
                BodyCol1.Width = new GridLength(1, GridUnitType.Star);
                BodyCol2.Width = new GridLength(410);

                Grid.SetRow(BorderUserHierarchy, 0);
                Grid.SetColumn(BorderUserHierarchy, 0);
                Grid.SetColumnSpan(BorderUserHierarchy, 1);
                BorderUserHierarchy.Margin = new Thickness(0, 0, 14, 0);

                Grid.SetRow(BorderUserDataGrid, 0);
                Grid.SetColumn(BorderUserDataGrid, 1);
                Grid.SetColumnSpan(BorderUserDataGrid, 1);
                BorderUserDataGrid.Margin = new Thickness(0, 0, 14, 0);

                Grid.SetRow(BorderUserDetails, 0);
                Grid.SetColumn(BorderUserDetails, 2);
                Grid.SetColumnSpan(BorderUserDetails, 1);
                BorderUserDetails.Margin = new Thickness(0);
            }
        }

        #endregion

        #region Page Load

        private async void UserManagementControl_Loaded(object sender, RoutedEventArgs e)
        {
            CurrentUserName = AppSession.Current.CurrentUserName;
            IsDevUser = AppSession.Current.IsDevUser;

            if (string.IsNullOrWhiteSpace(CurrentUserName))
            {
                CurrentUserName = Environment.UserName;
            }

            await LoadPageAsync();
        }

        public async Task LoadPageAsync()
        {
            try
            {
                LoadUsers();
                LoadParentUsers();
                BuildUserHierarchy();

                await EnsurePermissionEnumNamesStoredAsync();
                await LoadPermissionsAsync();

                DataGridUsers.ItemsSource = _usersView;
                TreeViewUsers.ItemsSource = _userTree;
                ComboBoxParentUser.ItemsSource = _parentUsers;
                ItemsControlPermissions.ItemsSource = _permissions;

                UpdateSummary();

                await SelectLoggedInUserAfterLoadAsync();
            }
            catch (Exception ex)
            {
                ShowError("Failed to load user management.", ex);
            }
        }

        #endregion

        #region Permission Store / Display Helpers

        private async Task EnsurePermissionEnumNamesStoredAsync()
        {
            if (SeedPermissions.Count == 0)
                return;

            await Task.Run(() =>
            {
                var pmap = UserManager.PermissionMap;
                using DynamicClass dcPermissions = new(pmap.Schema, pmap.Table, pmap.Columns.Id);
                DataTable dt = dcPermissions.Select() ?? new DataTable();

                HashSet<string> existingPermissions = dt
                    .AsEnumerable()
                    .Where(row => row.Table.Columns.Contains(pmap.Columns.Name))
                    .Select(row => Convert.ToString(row[pmap.Columns.Name])?.Trim() ?? string.Empty)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (string permissionName in SeedPermissions)
                {
                    if (existingPermissions.Contains(permissionName))
                        continue;

                    dcPermissions.Values.Clear();
                    dcPermissions.Values[pmap.Columns.Name] = permissionName;

                    object? insertedId = dcPermissions.Insert();
                    if (insertedId == null)
                    {
                        continue;
                    }

                    existingPermissions.Add(permissionName);
                }
            });
        }

        private static string MakeReadablePermissionName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return string.Empty;

            return rawName.Replace("_", " ").Trim();
        }

        #endregion

        #region Load Users

        private void LoadUsers()
        {
            _users.Clear();
            DataTable dt = UserManager.GetUsersFromDB();
            List<UserGridItem> allUsers = new();

            foreach (DataRow row in dt.Rows)
            {
                int userId = ToInt(row, UserManager.UserMap.Columns.Id);
                if (userId <= 0) continue;

                allUsers.Add(new UserGridItem
                {
                    UserID = userId,
                    UserName = ToStringValue(row, UserManager.UserMap.Columns.Name),
                    Email = ToStringValue(row, UserManager.UserMap.Columns.Email),
                    Password = ToStringValue(row, UserManager.UserMap.Columns.Password),
                    Role = ToStringValue(row, UserManager.UserMap.Columns.Role),
                    ParentID = ToNullableInt(row, UserManager.UserMap.Columns.ParentId),
                    CreatedAt = ToNullableDateTime(row, "CreatedAt"),
                    ModifiedAt = ToNullableDateTime(row, "ModifiedAt")
                });
            }

            if (AppSession.Current.IsDevUser)
            {
                foreach (UserGridItem user in allUsers.OrderBy(x => x.UserName))
                {
                    user.ParentUserName = GetParentUserNameFromList(user.ParentID, allUsers);
                    _users.Add(user);
                }

                _usersView = CollectionViewSource.GetDefaultView(_users);
                _usersView.Filter = FilterUsers;
                return;
            }

            UserGridItem? loggedInUser = FindLoggedInUser(allUsers);
            if (loggedInUser == null)
            {
                foreach (UserGridItem user in allUsers.OrderBy(x => x.UserName))
                {
                    user.ParentUserName = GetParentUserNameFromList(user.ParentID, allUsers);
                    _users.Add(user);
                }

                _usersView = CollectionViewSource.GetDefaultView(_users);
                _usersView.Filter = FilterUsers;
                return;
            }

            HashSet<int> hierarchyUserIds = GetUserHierarchyIds(loggedInUser.UserID, allUsers);
            List<UserGridItem> visibleUsers = allUsers
                .Where(x => hierarchyUserIds.Contains(x.UserID))
                .OrderBy(x => x.UserID == loggedInUser.UserID ? 0 : 1)
                .ThenBy(x => x.UserName)
                .ToList();

            foreach (UserGridItem user in visibleUsers)
            {
                user.ParentUserName = user.UserID == loggedInUser.UserID ? "Logged In User" : GetParentUserNameFromList(user.ParentID, visibleUsers);
                _users.Add(user);
            }

            _usersView = CollectionViewSource.GetDefaultView(_users);
            _usersView.Filter = FilterUsers;
        }

        private void LoadParentUsers()
        {
            _parentUsers.Clear();
            _parentUsers.Add(new UserComboItem
            {
                UserID = 0,
                UserName = "-- No Parent / Root User --"
            });

            foreach (UserGridItem user in _users.OrderBy(x => x.UserName))
            {
                _parentUsers.Add(new UserComboItem
                {
                    UserID = user.UserID,
                    UserName = $"{user.UserName} ({user.Role})"
                });
            }

            ComboBoxParentUser.ItemsSource = null;
            ComboBoxParentUser.ItemsSource = _parentUsers;
            ComboBoxParentUser.DisplayMemberPath = "UserName";
            ComboBoxParentUser.SelectedValuePath = "UserID";
            ComboBoxParentUser.SelectedValue = 0;
        }

        #endregion

        #region Load Permissions

        private async Task LoadPermissionsAsync()
        {
            _permissions.Clear();
            HashSet<string> loggedInPermissions = new(AppSession.Current.GrantedPermissions, StringComparer.OrdinalIgnoreCase);

            var pmap = UserManager.PermissionMap;
            using DynamicClass dcPermissions = new(pmap.Schema, pmap.Table, pmap.Columns.Id);
            DataTable dt = dcPermissions.Select() ?? new DataTable();

            foreach (DataRow row in dt.Rows)
            {
                int permissionId = ToInt(row, pmap.Columns.Id);
                if (permissionId <= 0) continue;

                string permissionKey = ToStringValue(row, pmap.Columns.Name);
                if (string.IsNullOrWhiteSpace(permissionKey)) continue;

                // Non-developer login only sees permissions they themselves have
                if (!AppSession.Current.IsDevUser && loggedInPermissions.Count > 0 && !loggedInPermissions.Contains(permissionKey))
                    continue;

                PermissionCheckItem item = new()
                {
                    PermissionId = permissionId,
                    PermissionKey = permissionKey,
                    PermissionName = MakeReadablePermissionName(permissionKey),
                    IsSelected = false
                };

                item.PropertyChanged += PermissionItem_PropertyChanged;
                _permissions.Add(item);
            }

            UpdateSelectedPermissionCount();
            await Task.CompletedTask;
        }

        private async Task SelectLoggedInUserAfterLoadAsync()
        {
            if (AppSession.Current.IsDevUser)
            {
                _isLoadingSelection = true;
                DataGridUsers.SelectedItem = null;
                TextBlockSelectedUser.Text = "New";
                _isLoadingSelection = false;
                return;
            }

            UserGridItem? loggedInUser = FindLoggedInUser(_users.ToList());
            if (loggedInUser == null) return;

            _isLoadingSelection = true;
            DataGridUsers.SelectedItem = loggedInUser;
            DataGridUsers.ScrollIntoView(loggedInUser);
            _isLoadingSelection = false;

            await LoadSelectedUserAsync(loggedInUser.UserID, syncGrid: false);
        }

        private void PermissionItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PermissionCheckItem.IsSelected))
            {
                UpdateSelectedPermissionCount();
            }
        }

        private async Task LoadSelectedUserPermissionsAsync(int userId, string userName)
        {
            HashSet<int> selectedPermissions = await UserManager.LoadUserPermissionsAsync(userId, userName);
            foreach (PermissionCheckItem permission in _permissions)
            {
                permission.IsSelected = selectedPermissions.Contains(permission.PermissionId);
            }

            UpdateSelectedPermissionCount();
        }

        #endregion

        #region Build Hierarchy

        private void BuildUserHierarchy()
        {
            _userTree.Clear();

            List<UserGridItem> visibleUsers = _users.ToList();
            if (visibleUsers.Count == 0) return;

            List<UserGridItem> rootUsers;
            if (AppSession.Current.IsDevUser)
            {
                rootUsers = visibleUsers
                    .Where(x => !x.ParentID.HasValue || x.ParentID.Value <= 0)
                    .OrderBy(x => x.UserName)
                    .ToList();

                if (rootUsers.Count == 0) rootUsers = visibleUsers.OrderBy(x => x.UserName).ToList();
            }
            else
            {
                UserGridItem? loggedInUser = FindLoggedInUser(visibleUsers);
                rootUsers = loggedInUser != null ? new List<UserGridItem> { loggedInUser } : visibleUsers.Where(x => !x.ParentID.HasValue || x.ParentID.Value <= 0).ToList();
            }

            foreach (UserGridItem rootUser in rootUsers)
            {
                UserTreeNode rootNode = CreateTreeNode(rootUser);
                AddChildNodes(rootNode, visibleUsers);
                _userTree.Add(rootNode);
            }
        }

        private static UserTreeNode CreateTreeNode(UserGridItem user)
        {
            return new UserTreeNode
            {
                UserID = user.UserID,
                UserName = user.UserName,
                Role = user.Role
            };
        }

        private static void AddChildNodes(UserTreeNode parentNode, List<UserGridItem> allUsers)
        {
            List<UserGridItem> children = allUsers
                .Where(x => x.ParentID == parentNode.UserID)
                .OrderBy(x => x.UserName)
                .ToList();

            foreach (UserGridItem child in children)
            {
                UserTreeNode childNode = CreateTreeNode(child);
                parentNode.Children.Add(childNode);
                AddChildNodes(childNode, allUsers);
            }
        }

        #endregion

        #region Selection

        private async void DataGridUsers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSelection) return;

            if (DataGridUsers.SelectedItem is UserGridItem selectedUser)
            {
                await LoadSelectedUserAsync(selectedUser.UserID, syncGrid: false);
            }
        }

        private async void TreeViewUsers_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_isLoadingSelection) return;

            if (TreeViewUsers.SelectedItem is UserTreeNode selectedNode)
            {
                await LoadSelectedUserAsync(selectedNode.UserID, syncGrid: true);
            }
        }

        private async Task LoadSelectedUserAsync(int userId, bool syncGrid)
        {
            try
            {
                _isLoadingSelection = true;
                UserGridItem? user = _users.FirstOrDefault(x => x.UserID == userId);
                if (user == null) return;

                _selectedUserId = user.UserID;
                TextBlockSelectedUser.Text = $"{user.UserName} (ID: {user.UserID})";

                TextBoxUserName.Text = user.UserName;
                TextBoxEmail.Text = user.Email;
                PasswordBoxPassword.Password = string.Empty;
                PasswordBoxConfirmPassword.Password = string.Empty;

                SetRole(user.Role);
                ComboBoxParentUser.SelectedValue = user.ParentID ?? 0;

                if (syncGrid)
                {
                    DataGridUsers.SelectedItem = user;
                    DataGridUsers.ScrollIntoView(user);
                }

                await LoadSelectedUserPermissionsAsync(user.UserID, user.UserName);
            }
            finally
            {
                _isLoadingSelection = false;
            }
        }

        private void SetRole(string role)
        {
            foreach (var item in ComboBoxRole.Items)
            {
                if (item is ComboBoxItem cbi && string.Equals(Convert.ToString(cbi.Content), role, StringComparison.OrdinalIgnoreCase))
                {
                    ComboBoxRole.SelectedItem = cbi;
                    return;
                }
            }
        }

        #endregion

        #region Button Events

        private void ButtonNewUser_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private async void ButtonRefresh_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
            await LoadPageAsync();
        }

        private async void ButtonSaveUser_Click(object sender, RoutedEventArgs e)
        {
            ButtonSaveUser.IsEnabled = false;

            try
            {
                string userName = TextBoxUserName.Text.Trim();
                string email = TextBoxEmail.Text.Trim();
                string password = PasswordBoxPassword.Password;
                string confirmPassword = PasswordBoxConfirmPassword.Password;
                string role = GetSelectedRole();
                int? parentId = GetSelectedParentId(role);

                if (_selectedUserId <= 0)
                {
                    await AddNewUserAsync(userName, email, password, confirmPassword, role, parentId);
                }
                else
                {
                    await UpdateExistingUserAsync(_selectedUserId, userName, email, password, confirmPassword, role, parentId);
                }

                ClearForm();
                await LoadPageAsync();
            }
            catch (Exception ex)
            {
                ShowError("Save failed.", ex);
            }
            finally
            {
                ButtonSaveUser.IsEnabled = true;
            }
        }

        private async Task AddNewUserAsync(string userName, string email, string password, string confirmPassword, string role, int? parentId)
        {
            if (IsOtpEnabled)
            {
                bool otpVerified = await SendAndVerifyOtpAsync(async () =>
                {
                    await UserManager.AddUserProcedure(userName, email, password, confirmPassword, role, isOtpEnabled: true);
                });

                if (!otpVerified)
                    throw new Exception("OTP verification failed. User was not saved.");
            }
            else
            {
                await UserManager.AddUserProcedure(userName, email, password, confirmPassword, role, isOtpEnabled: false);
            }

            bool saved = UserManager.SaveUser(userName, email, password, role, parentId);
            if (!saved) throw new Exception("User was not saved.");

            int newUserId = FindUserIdByUserName(userName);
            if (newUserId > 0)
            {
                List<int> selectedPermissions = GetSelectedPermissionIds();
                UserManager.SaveUserPermissions(newUserId, selectedPermissions);
            }

            ModernMessageBox.Show(
                IsOtpEnabled ? "User added successfully after OTP verification." : "User added successfully.",
                "User Management",
                ModernMessageBoxButton.OK,
                ModernMessageBoxIcon.Information);

            LogAction?.Invoke($"User with UserId:{newUserId} added successfully.");
        }

        private async Task UpdateExistingUserAsync(int userId, string userName, string email, string password, string confirmPassword, string role, int? parentId)
        {
            bool updated;
            if (IsOtpEnabled)
            {
                bool otpVerified = await SendAndVerifyOtpAsync(async () =>
                {
                    await UserManager.UpdateUserProcedure(userId, userName, email, password, confirmPassword, role, isOtpEnabled: true);
                });

                if (!otpVerified)
                    throw new Exception("OTP verification failed. User was not updated.");

                updated = UserManager.UpdateUser(userId, userName, email, password, role);
            }
            else
            {
                updated = await UserManager.UpdateUserProcedure(userId, userName, email, password, confirmPassword, role, isOtpEnabled: false);
            }

            if (!updated) throw new Exception("User update failed.");

            UserManager.UpdateUserParent(userId, parentId);
            List<int> selectedPermissions = GetSelectedPermissionIds();
            UserManager.SaveUserPermissions(userId, selectedPermissions);

            ModernMessageBox.Show(
                IsOtpEnabled ? "User updated successfully after OTP verification." : "User updated successfully.",
                "User Management",
                ModernMessageBoxButton.OK,
                ModernMessageBoxIcon.Information);

            LogAction?.Invoke($"User ID:{userId} updated successfully.");
        }

        private async Task<bool> SendAndVerifyOtpAsync(Func<Task> otpProcedureCall)
        {
            TaskCompletionSource<bool> otpResultSource = new();
            EventHandler<EventArgs>? stopHandler = null;

            stopHandler = (s, ev) =>
            {
                UserManager.OTPProcedureStop -= stopHandler;

                try
                {
                    Dispatcher.Invoke(() =>
                    {
                        FormOtpVerify otpForm = new()
                        {
                            Owner = Window.GetWindow(this)
                        };

                        bool? dialogResult = otpForm.ShowDialog();
                        bool verified = dialogResult == true && otpForm.IsOtpVerified;
                        otpResultSource.TrySetResult(verified);
                    });
                }
                catch (Exception ex)
                {
                    otpResultSource.TrySetException(ex);
                }
            };

            UserManager.OTPProcedureStop += stopHandler;

            try
            {
                await otpProcedureCall();

                Task completedTask = await Task.WhenAny(
                    otpResultSource.Task,
                    Task.Delay(TimeSpan.FromMinutes(2)));

                if (completedTask != otpResultSource.Task)
                    throw new Exception("OTP verification timeout. Please try again.");

                return await otpResultSource.Task;
            }
            finally
            {
                UserManager.OTPProcedureStop -= stopHandler;
            }
        }

        private void ButtonDeleteUser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedUserId <= 0)
                {
                    ModernMessageBox.Show("Please select a user first.", "User Management", ModernMessageBoxButton.OK, ModernMessageBoxIcon.Warning);
                    return;
                }

                ModernMessageBoxResult confirm = ModernMessageBox.Show(
                    "Are you sure you want to delete this user?",
                    "Delete User",
                    ModernMessageBoxButton.YesNo,
                    ModernMessageBoxIcon.Warning);

                if (confirm != ModernMessageBoxResult.Yes) return;

                int? reassignParentId = GetSelectedParentId(GetSelectedRole());
                bool deleted = UserManager.DeleteUserProcedure(_selectedUserId, reassignParentId);

                if (!deleted) return;

                ModernMessageBox.Show("User deleted successfully.", "User Management", ModernMessageBoxButton.OK, ModernMessageBoxIcon.Information);
                LogAction?.Invoke($"User ID:{_selectedUserId} deleted.");
                ClearForm();
                _ = LoadPageAsync();
            }
            catch (Exception ex)
            {
                ShowError("Delete failed.", ex);
            }
        }

        private void ButtonSelectAllPermissions_Click(object sender, RoutedEventArgs e)
        {
            bool shouldSelectAll = _permissions.Any(x => !x.IsSelected);
            foreach (PermissionCheckItem permission in _permissions)
            {
                permission.IsSelected = shouldSelectAll;
            }

            UpdateSelectedPermissionCount();
        }

        #endregion

        #region Search & Helpers

        private void TextBoxSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _usersView?.Refresh();
        }

        private bool FilterUsers(object obj)
        {
            if (obj is not UserGridItem user) return false;

            string search = TextBoxSearch.Text.Trim();
            if (string.IsNullOrWhiteSpace(search)) return true;

            return Contains(user.UserName, search) ||
                   Contains(user.Email, search) ||
                   Contains(user.Role, search) ||
                   Contains(user.ParentUserName, search) ||
                   user.UserID.ToString().Contains(search);
        }

        private static bool Contains(string? source, string value)
        {
            return (source ?? string.Empty).IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string GetSelectedRole()
        {
            if (ComboBoxRole.SelectedItem is ComboBoxItem item)
                return Convert.ToString(item.Content)?.Trim() ?? string.Empty;

            return string.Empty;
        }

        private int? GetSelectedParentId(string role)
        {
            bool isAdminRole = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);

            if (ComboBoxParentUser.SelectedValue == null)
            {
                if (isAdminRole) return null;
                throw new Exception("Parent user is required for non-admin users.");
            }

            if (!int.TryParse(ComboBoxParentUser.SelectedValue.ToString(), out int parentId))
            {
                if (isAdminRole) return null;
                throw new Exception("Please select a valid parent user.");
            }

            if (parentId <= 0)
            {
                if (isAdminRole) return null;
                throw new Exception("Parent user is required for non-admin users.");
            }

            if (_selectedUserId > 0 && parentId == _selectedUserId)
                throw new Exception("User cannot be parent of itself.");

            if (_selectedUserId > 0 && IsCircularParent(_selectedUserId, parentId))
                throw new Exception("Invalid hierarchy. Child user cannot become parent of its own parent.");

            return parentId;
        }

        private bool IsCircularParent(int userId, int newParentId)
        {
            int? currentParentId = newParentId;
            while (currentParentId.HasValue && currentParentId.Value > 0)
            {
                if (currentParentId.Value == userId) return true;

                UserGridItem? parentUser = _users.FirstOrDefault(x => x.UserID == currentParentId.Value);
                if (parentUser == null) return false;
                currentParentId = parentUser.ParentID;
            }

            return false;
        }

        private List<int> GetSelectedPermissionIds()
        {
            return _permissions.Where(x => x.IsSelected).Select(x => x.PermissionId).ToList();
        }

        private static int FindUserIdByUserName(string userName)
        {
            DataTable dt = UserManager.GetUsersFromDB();
            string nameCol = UserManager.UserMap.Columns.Name;
            string idCol = UserManager.UserMap.Columns.Id;

            foreach (DataRow row in dt.Rows)
            {
                string dbUserName = ToStringValue(row, nameCol);
                if (string.Equals(dbUserName, userName, StringComparison.OrdinalIgnoreCase))
                {
                    return ToInt(row, idCol);
                }
            }

            return 0;
        }

        private void UpdateSelectedPermissionCount()
        {
            TextBlockTotalPermissions.Text = _permissions.Count(x => x.IsSelected).ToString();
        }

        private void ClearForm()
        {
            _selectedUserId = 0;
            TextBoxUserName.Text = string.Empty;
            TextBoxEmail.Text = string.Empty;
            PasswordBoxPassword.Password = string.Empty;
            PasswordBoxConfirmPassword.Password = string.Empty;

            ComboBoxRole.SelectedIndex = -1;
            ComboBoxParentUser.SelectedValue = 0;

            foreach (PermissionCheckItem permission in _permissions)
                permission.IsSelected = false;

            UpdateSelectedPermissionCount();
            DataGridUsers.SelectedItem = null;
            TextBlockSelectedUser.Text = "New";
        }

        private void UpdateSummary()
        {
            TextBlockTotalUsers.Text = _users.Count.ToString();
            TextBlockRootUsers.Text = _users.Count(x => !x.ParentID.HasValue || x.ParentID.Value <= 0).ToString();
            UpdateSelectedPermissionCount();
        }

        private static void ShowError(string title, Exception ex)
        {
            ModernMessageBox.Show(title + Environment.NewLine + Environment.NewLine + ex.Message, "User Management", ModernMessageBoxButton.OK, ModernMessageBoxIcon.Error);
        }

        private static string ToStringValue(DataRow row, string columnName)
        {
            if (row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value)
            {
                return Convert.ToString(row[columnName]) ?? string.Empty;
            }
            return string.Empty;
        }

        private static int ToInt(DataRow row, string columnName)
        {
            if (row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value && int.TryParse(Convert.ToString(row[columnName]), out int val))
            {
                return val;
            }
            return 0;
        }

        private static int? ToNullableInt(DataRow row, string columnName)
        {
            if (row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value && int.TryParse(Convert.ToString(row[columnName]), out int val))
            {
                return val;
            }
            return null;
        }

        private static DateTime? ToNullableDateTime(DataRow row, string columnName)
        {
            if (row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value && DateTime.TryParse(Convert.ToString(row[columnName]), out DateTime val))
            {
                return val;
            }
            return null;
        }

        private UserGridItem? FindLoggedInUser(List<UserGridItem> users)
        {
            if (AppSession.Current.CurrentUserId > 0)
            {
                UserGridItem? userById = users.FirstOrDefault(x => x.UserID == AppSession.Current.CurrentUserId);
                if (userById != null) return userById;
            }

            return users.FirstOrDefault(x => string.Equals(x.UserName?.Trim(), CurrentUserName.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private static HashSet<int> GetUserHierarchyIds(int loggedInUserId, List<UserGridItem> allUsers)
        {
            HashSet<int> result = new();
            AddUserAndChildren(loggedInUserId, allUsers, result);
            return result;
        }

        private static void AddUserAndChildren(int userId, List<UserGridItem> allUsers, HashSet<int> result)
        {
            if (!result.Add(userId)) return;

            List<UserGridItem> children = allUsers.Where(x => x.ParentID == userId).ToList();
            foreach (UserGridItem child in children)
            {
                AddUserAndChildren(child.UserID, allUsers, result);
            }
        }

        private static string GetParentUserNameFromList(int? parentId, List<UserGridItem> visibleUsers)
        {
            if (!parentId.HasValue || parentId.Value <= 0) return "Root";

            UserGridItem? parent = visibleUsers.FirstOrDefault(x => x.UserID == parentId.Value);
            return parent == null ? "Outside Hierarchy" : parent.UserName;
        }

        #endregion
    }

    public class UserGridItem
    {
        public int UserID { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int? ParentID { get; set; }
        public string ParentUserName { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public string CreatedAtText => CreatedAt.HasValue ? CreatedAt.Value.ToString("dd-MM-yyyy HH:mm") : "-";
        public string ModifiedAtText => ModifiedAt.HasValue ? ModifiedAt.Value.ToString("dd-MM-yyyy HH:mm") : "-";
    }

    public class UserComboItem
    {
        public int UserID { get; set; }
        public string UserName { get; set; } = string.Empty;
        public override string ToString() => UserName;
    }

    public class UserTreeNode
    {
        public int UserID { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Initial => string.IsNullOrWhiteSpace(UserName) ? "?" : UserName.Trim().Substring(0, 1).ToUpper();
        public ObservableCollection<UserTreeNode> Children { get; set; } = new();
    }

    public class PermissionCheckItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        public int PermissionId { get; set; }
        public string PermissionKey { get; set; } = string.Empty;
        public string PermissionName { get; set; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
