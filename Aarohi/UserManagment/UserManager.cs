using Aarohi.Classes;
using Aarohi.Classes.Healper;
using Aarohi.Configuration;
using Aarohi.Globals;
using Aarohi.Networking;
using DocumentFormat.OpenXml.Office.CustomXsn;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Aarohi.Classes.Healper.RegistryHelper;
using static Aarohi.Networking.MailHelpers;
using static Aarohi.UserManagment.FormStartUp;
using System.ComponentModel;

namespace Aarohi.UserManagment
{
    public static class UserManager
    {
        private static RootConfig _config;
        private static DynamicClass _userClass;
        private static string _LoginDataColumnName;
        private static string _PasswordDataColumnName;
        private static bool _loginFlowRunning = false;
        public static event EventHandler<LoginSuccessEventArgs>? LoginSuccess;
        public static event EventHandler<EventArgs>? OTPProcedureStart;
        public static event EventHandler<EventArgs>? OTPProcedureStop;
        private static string UserName;
        private static string Password;
        private static bool checkedRememberMe = false;
        private const string SecureUserNameKey = "AESPLXU";
        private const string SecurePasswordKey = "AESPLXP";
        private const string SecurePrefix = "DPAPI:";
        private static string generatedOtp;
        private static DateTime otpExpiry;

        public static void Configure(RootConfig config)
        {
            _config = config;
            _userClass = new DynamicClass(
                config.UserMapping.Schema,
                config.UserMapping.Table);
            _LoginDataColumnName = config.UserMapping.Columns.Name;
            _PasswordDataColumnName = config.UserMapping.Columns.Password;
        }

        public static UserMappingConfig UserMap => _config.UserMapping;
        public static PermissionMappingConfig PermissionMap => _config.PermissionMapping;
        public static UserPermissionMappingConfig UserPermissionMap => _config.UserPermissionMapping;

        [Obsolete("Use LogoutSecureRememberMeSameKeys() for new applications. Old logout() is kept only for backward compatibility.", false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool logout(bool WantConfirmationMessage = true)
        {
            try
            {
                DialogResult result;

                if (WantConfirmationMessage)
                {
                    result = MessageBox.Show(
                       "Are you sure you want to logout?",
                       "Logout Confirmation",
                       MessageBoxButtons.YesNo,
                       MessageBoxIcon.Question
                    );
                }
                else
                {
                    result = DialogResult.Yes;
                }

                if (result == DialogResult.Yes)
                {
                    RegistryHelper.SaveString(RegistryHelper.storeLocs.Credentials, "AESPLXU", "");
                    RegistryHelper.SaveString(RegistryHelper.storeLocs.Credentials, "AESPLXP", "");

                    _loginFlowRunning = false;

                    return true;
                }
                else
                {
                    return false;
                }

            }
            catch
            {
                return false;
            }
        }

        [Obsolete("Use LoginWithSecureRememberMeSameKeys() for new applications. Old Login() stores SHA256-style remember-me values and cannot restore username/password.", false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool Login(string userName, string password, bool rememberMe)
        {
            if (TryAuthenticate(userName, password))
            {
                HandleRememberMe(userName, password, rememberMe);

                if (!_loginFlowRunning)
                {
                    _loginFlowRunning = true;
                    LoginSuccess?.Invoke(null, new LoginSuccessEventArgs(userName, password));
                }
                return true;
            }
            return false;
        }

        public static string HashPassword(string password)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();

                foreach (byte b in bytes)
                    builder.Append(b.ToString("x2"));

                return builder.ToString();
            }
        }

        public static bool TryAuthenticate(string userName, string password)
        {
            try
            {
                if (userName == AGLobals.Utils.DevName)
                {
                    if (password == DateTime.Now.ToString("ddMMyyyyHH"))
                        return true;

                    MessageBox.Show("Incorrect password.", "Login Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                var values = _userClass.GetRowAsDictionary(_LoginDataColumnName, userName);

                if (values == null || values.Count == 0)
                {
                    MessageBox.Show("Username not found.", "Login Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                var dbUserName = values[_LoginDataColumnName]?.ToString() ?? string.Empty;
                var dbPassword = values[_PasswordDataColumnName]?.ToString() ?? string.Empty;

                if (!string.Equals(userName, dbUserName, StringComparison.Ordinal))
                {
                    MessageBox.Show("Username does not match.", "Login Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                if (!string.Equals(password, dbPassword, StringComparison.Ordinal))
                {
                    MessageBox.Show("Incorrect password.", "Login Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "An error occurred while checking login. Please contact support.\n\n" + ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
        }

        [Obsolete("Use SaveSecureRememberMeSameKeys() for new applications. SetRegistryHashes() stores SHA256 hashes, which cannot be decrypted back to original values.", false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void SetRegistryHashes(string userName, string password)
        {
            using (var sha = SHA256.Create())
            {
                byte[] userBytes = Encoding.UTF8.GetBytes(userName ?? string.Empty);
                string userHash = BitConverter.ToString(sha.ComputeHash(userBytes)).Replace("-", "");


                byte[] passwordBytes = Encoding.UTF8.GetBytes(password ?? string.Empty);
                string passwordHash = BitConverter.ToString(sha.ComputeHash(passwordBytes)).Replace("-", "");

                //Added
                RegistryHelper.SaveString(RegistryHelper.storeLocs.Credentials, "AESPLXU", userHash);
                RegistryHelper.SaveString(RegistryHelper.storeLocs.Credentials, "AESPLXP", passwordHash);
            }
        }

        [Obsolete("Use TryGetLastValuesFromSecureRememberMeSameKeys() for new applications. Old TryGetLastValues() is kept only for backward compatibility.", false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool TryGetLastValues(out string userName, out string password)
        {
            userName = string.Empty;
            password = string.Empty;
            try
            {
                string encryptedName = RegistryHelper.LoadString(RegistryHelper.storeLocs.Credentials, "AESPLXU");
                string encryptedPassword = RegistryHelper.LoadString(RegistryHelper.storeLocs.Credentials, "AESPLXP");

                string realName = RegistryHelper.Decrypt(encryptedName);
                string realPassword = RegistryHelper.Decrypt(encryptedPassword);

                if (string.IsNullOrWhiteSpace(realName) ||
                    string.IsNullOrWhiteSpace(realPassword))
                {
                    if(RegistryHelper.LoadBool(RegistryHelper.storeLocs.Credentials, "IsDevPC"))
                    {
                        userName = AGLobals.Utils.DevName;
                        password = DateTime.Now.ToString("ddMMyyyyHH");
                        return true; // For Directly use Of Developer Credentials without showing error message when the app is used in developer's PC and there are no credentials stored in registry
                    }
                    else 
                        return false;
                }

                UserName= realName;
                Password = realPassword;
                checkedRememberMe = true;

                if (!TryAuthenticate(realName, realPassword))
                {
                    RegistryHelper.SaveString(RegistryHelper.storeLocs.Credentials, "AESPLXU", "");
                    RegistryHelper.SaveString(RegistryHelper.storeLocs.Credentials, "AESPLXP", "");
                }

                if (_loginFlowRunning) return true;

                _loginFlowRunning = true;
                LoginSuccess?.Invoke(null, new LoginSuccessEventArgs(realName, realPassword));//null=this
                return true;
            }
            catch
            {
                try
                {
                    RegistryHelper.SaveString(RegistryHelper.storeLocs.Credentials, "AESPLXU", "");
                    RegistryHelper.SaveString(RegistryHelper.storeLocs.Credentials, "AESPLXP", "");
                }
                catch { }
                return false;
            }
        }

        [Obsolete("Use SaveSecureRememberMeSameKeys() for new applications. Old HandleRememberMe() is kept only for backward compatibility.", false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void HandleRememberMe(string userName, string password, bool rememberMe)
        {
            if (rememberMe)
            {
                if (!string.Equals(userName, AGLobals.Utils.DevName, StringComparison.OrdinalIgnoreCase))
                {
                    SetRegistryHashes(userName, password);
                }
                else
                {
                    SetRegistryHashes(string.Empty, string.Empty);
                }
            }
            else
            {
                SetRegistryHashes(string.Empty, string.Empty);
            }
        }

        public async static void AddUserProcedure(string username, string mail, string pass, string pass2, bool isOtpEnabled)
        {
            if (_userClass.GetColumnValues(_config.UserMapping.Columns.Email).Contains(mail))
            {
                throw new Exception("Email already exists in the database.");
            }

            string errors = string.Empty;
            bool isValid = true;
            if (string.IsNullOrWhiteSpace(mail))
            {
                errors += "- Email is required.\n";
                isValid = false;
            }
            if (string.IsNullOrWhiteSpace(username))
            {
                errors += "- Username is required.\n";
                isValid = false;
            }
            if (string.IsNullOrWhiteSpace(pass))
            {
                errors += "- Password is required.\n";
                isValid = false;
            }
            if (string.IsNullOrWhiteSpace(pass2))
            {
                errors += "- Confirm Password is required.\n";
                isValid = false;
            }

            if (!isValid)
            {
                throw new Exception("Please fill in the following required fields:\n" + errors);
            }

            if (!MailAddress.TryCreate(mail.Trim(), out _))
            {
                throw new Exception("Invalid email format.\nExample: user@gmail.com");
            }

            if (pass != pass2)
            {
                throw new Exception("Passwords do not match. Please make sure both passwords are the same.");
            }

            bool isDomainValid = await HasValidEmailDomain(mail.Trim());
            if (!isDomainValid)
            {
                throw new Exception("Email domain does not exist or is not valid.");
            }

            if (isOtpEnabled)
            {
                OTPProcedureStart.Invoke(null, new EventArgs());

                Random rnd = new Random();
                generatedOtp = rnd.Next(100000, 999999).ToString();
                otpExpiry = DateTime.Now.AddMinutes(5);

                await SendOtpEmail(mail.Trim(), "Aarohi Embedded Systems Pvt. Ltd.", "Email Verification Code", MailTemplates.OtpEmailBody(generatedOtp));

                OTPProcedureStop.Invoke(null, new EventArgs());
            }
        }

        public static bool VerifyOtp(string inputOtp)
        {
            if (DateTime.Now > otpExpiry)
            {
                throw new Exception("OTP has expired. Please request a new one.");
            }
            if (inputOtp != generatedOtp)
            {
                throw new Exception("Invalid OTP. Please check the code sent to your email and try again.");
            }
            return true;

        }

        public static bool SaveUser(string username, string mail, string pass, string role, int? parentId)
        {
            try
            {
                using (DynamicClass userClass = new DynamicClass(UserMap.Schema,UserMap.Table,UserMap.Columns.Id))
                {
                   string hashedPass = HashPassword(pass);

                    // Add values
                    userClass.Values.Add(UserMap.Columns.Name, username);
                    userClass.Values.Add(UserMap.Columns.Email, mail);
                    userClass.Values.Add(UserMap.Columns.Password, hashedPass);
                    userClass.Values.Add(UserMap.Columns.Role, role);
                    userClass.Values.Add(UserMap.Columns.ParentId, parentId);
                    // Save to database (IMPORTANT LINE)
                    userClass.Insert();   // or Save() depending on your class
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Handle error (log or show message)
                MessageBox.Show("An error occurred while saving the user. Please try again.\n\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        public static bool DeleteUserProcedure(int selectedUserID, int? parentID)
        {
            try
            {
                var umap = UserManager.UserMap;
                var upmap = UserManager.UserPermissionMap;

                using DynamicClass dcUsers =
                    new DynamicClass(umap.Schema, umap.Table, umap.Columns.Id);

                using DynamicClass dcUserPermissions =
                    new DynamicClass(upmap.Schema, upmap.Table, upmap.Columns.UserId);

                //  STEP 0: CHECK IF USER IS ROOT (ParentId == NULL)
                DataTable selectedUser = dcUsers.Select($"{umap.Columns.Id} = {selectedUserID}");

                if (selectedUser == null || selectedUser.Rows.Count == 0)
                    throw new Exception("User not found.");

                DataRow selectedRow = selectedUser.Rows[0];

                if (selectedRow[umap.Columns.ParentId] == DBNull.Value)
                {
                   throw new Exception("Cannot delete root user. Please reassign child users to another parent before deleting.");
                }

                //  STEP 1: GET CHILD USERS
                DataTable dtUsers = dcUsers
                    .Select($"{umap.Columns.ParentId} = {selectedUserID}") ?? new DataTable();

                foreach (DataRow row in dtUsers.Rows)
                {
                    var childId = row[umap.Columns.Id];
                    if (childId == DBNull.Value) continue;

                    DataTable fullUser = dcUsers.Select($"{umap.Columns.Id} = {childId}");
                    if (fullUser == null || fullUser.Rows.Count == 0)
                        continue;

                    DataRow userRow = fullUser.Rows[0];

                    dcUsers.Values.Clear();

                    foreach (DataColumn col in fullUser.Columns)
                    {
                        dcUsers.Values[col.ColumnName] =
                            userRow[col] == DBNull.Value ? DBNull.Value : userRow[col];
                    }

                    //  Reassign parent
                    dcUsers.Values[umap.Columns.ParentId] = parentID ?? (object)DBNull.Value;

                    var result = dcUsers.Save(false, false);

                    if (result == null)
                        throw new Exception("Failed to update child user: " + childId);
                }

                //  STEP 2: DELETE USER PERMISSIONS
                DataTable dtPerms = dcUserPermissions
                    .Select($"{upmap.Columns.UserId} = {selectedUserID}") ?? new DataTable();

                foreach (DataRow row in dtPerms.Rows)
                {
                    var permUserId = row[upmap.Columns.UserId];
                    if (permUserId == DBNull.Value) continue;

                    dcUserPermissions.DeleteByKey(permUserId);
                }

                //  STEP 3: DELETE USER
                dcUsers.DeleteByKey(selectedUserID);

                return true;
            }
            catch (Exception ex)
            {
              MessageBox.Show("You cannot delete the root user.\n\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
        public static DataTable ViewUserProcedure(int userId)
        {
            try
            {
                var umap = UserManager.UserMap;

                using DynamicClass dcUsers =
                    new DynamicClass(umap.Schema, umap.Table, umap.Columns.Id);

                DataTable dt = dcUsers.Select($"{umap.Columns.Id} = {userId}");

                return dt;
            }
            catch (Exception ex)
            {
                throw new Exception("Error fetching user data.\n\n" + ex.Message);
            }
        }

        public static bool UpdateUser(int userId, string username, string email, string pass, string role, int? parentId)
        {
            try
            {
                var umap = UserManager.UserMap;

                using DynamicClass dcUsers =
                    new DynamicClass(umap.Schema, umap.Table, umap.Columns.Id);

                dcUsers.Values.Clear();

                dcUsers.Values[umap.Columns.Id] = userId;
                dcUsers.Values[umap.Columns.Name] = username;
                dcUsers.Values[umap.Columns.Email] = email;
                dcUsers.Values[umap.Columns.Role] = role;
                dcUsers.Values[umap.Columns.ParentId] = parentId ?? (object)DBNull.Value;

                // 🔐 Only update password if entered
                if (!string.IsNullOrWhiteSpace(pass))
                {
                    dcUsers.Values[umap.Columns.Password] = HashPassword(pass);
                }

                var result = dcUsers.Save(false, false);

                return result != null;
            }
            catch
            {
                return false;
            }
        }

        #region Secure Remember Me Same Registry Keys - New Separate Implementation

        /// <summary>
        /// Authenticates the user and saves Remember-Me credentials securely using DPAPI
        /// in the same standard registry keys: AESPLXU and AESPLXP.
        /// </summary>
        /// <remarks>
        /// Use this method in new applications instead of the old Login method.
        /// This method keeps the same registry key names but stores recoverable encrypted values
        /// using Windows DPAPI with CurrentUser scope.
        ///
        /// Registry output when Remember Me is enabled:
        /// AESPLXU = DPAPI:encrypted_username
        /// AESPLXP = DPAPI:encrypted_password
        ///
        /// If Remember Me is false, credentials are cleared from registry.
        /// Developer login is not saved because its password is time-based.
        /// </remarks>
        /// <param name="userName">The username entered by the user.</param>
        /// <param name="password">The password entered by the user.</param>
        /// <param name="rememberMe">True to securely store credentials for future auto-fill; otherwise false.</param>
        /// <returns>True if authentication succeeds; otherwise false.</returns>
        public static bool LoginWithSecureRememberMeSameKeys(string userName, string password, bool rememberMe)
        {
            if (TryAuthenticateForSecureRememberMe(userName, password, showMessage: true))
            {
                SaveSecureRememberMeSameKeys(userName, password, rememberMe);

                if (!_loginFlowRunning)
                {
                    _loginFlowRunning = true;
                    LoginSuccess?.Invoke(null, new LoginSuccessEventArgs(userName, password));
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to read previously saved Remember-Me credentials from registry using
        /// the same standard keys: AESPLXU and AESPLXP.
        /// </summary>
        /// <remarks>
        /// This method expects AESPLXU and AESPLXP values to be stored in the new DPAPI format:
        /// DPAPI:encrypted_value.
        ///
        /// If the registry contains old SHA256 hash values, this method cannot recover the
        /// original username or password because SHA256 is one-way. In that case, it falls back
        /// to developer login only if IsDevPC is enabled in registry.
        ///
        /// Priority:
        /// 1. Read valid DPAPI values from AESPLXU/AESPLXP.
        /// 2. If not found, check IsDevPC.
        /// 3. If IsDevPC is false, return false.
        /// </remarks>
        /// <param name="userName">Returns the decrypted username if available.</param>
        /// <param name="password">Returns the decrypted password if available.</param>
        /// <returns>True if valid saved credentials or developer credentials are found; otherwise false.</returns>
        public static bool TryGetLastValuesFromSecureRememberMeSameKeys(out string userName, out string password)
        {
            userName = string.Empty;
            password = string.Empty;

            try
            {
                string savedUser = RegistryHelper.LoadString(
                    RegistryHelper.storeLocs.Credentials,
                    "AESPLXU");

                string savedPassword = RegistryHelper.LoadString(
                    RegistryHelper.storeLocs.Credentials,
                    "AESPLXP");

                if (IsSecureRememberMeValue(savedUser) && IsSecureRememberMeValue(savedPassword))
                {
                    string realUser = DecryptRememberMeValue(savedUser);
                    string realPassword = DecryptRememberMeValue(savedPassword);

                    if (!string.IsNullOrWhiteSpace(realUser) &&
                        !string.IsNullOrWhiteSpace(realPassword))
                    {
                        userName = realUser;
                        password = realPassword;

                        UserName = realUser;
                        Password = realPassword;
                        checkedRememberMe = true;

                        return true;
                    }
                }

                // If old SHA256 values are stored in AESPLXU/AESPLXP,
                // original username/password cannot be recovered.
                // So fallback to Dev PC only.
                return TryGetDevPcLoginForSecureRememberMe(out userName, out password);
            }
            catch
            {
                ClearSecureRememberMeSameKeys();
                return TryGetDevPcLoginForSecureRememberMe(out userName, out password);
            }
        }

        /// <summary>
        /// Saves or clears Remember-Me credentials using the standard registry keys
        /// AESPLXU and AESPLXP with DPAPI encryption.
        /// </summary>
        /// <remarks>
        /// This method is used by LoginWithSecureRememberMeSameKeys.
        /// When rememberMe is true, it encrypts username and password using Windows DPAPI.
        /// When rememberMe is false, it clears AESPLXU and AESPLXP.
        ///
        /// This method intentionally does not save developer credentials because developer
        /// passwords are time-based.
        /// </remarks>
        /// <param name="userName">Username to save.</param>
        /// <param name="password">Password to save.</param>
        /// <param name="rememberMe">True to save encrypted values; false to clear saved values.</param>
        public static void SaveSecureRememberMeSameKeys(string userName, string password, bool rememberMe)
        {
            try
            {
                if (rememberMe &&
                    !string.IsNullOrWhiteSpace(userName) &&
                    !string.IsNullOrWhiteSpace(password) &&
                    !string.Equals(userName, AGLobals.Utils.DevName, StringComparison.OrdinalIgnoreCase))
                {
                    RegistryHelper.SaveString(
                        RegistryHelper.storeLocs.Credentials,
                        "AESPLXU",
                        EncryptRememberMeValue(userName));

                    RegistryHelper.SaveString(
                        RegistryHelper.storeLocs.Credentials,
                        "AESPLXP",
                        EncryptRememberMeValue(password));

                    Debug.WriteLine("Secure RememberMe saved in AESPLXU/AESPLXP.");
                }
                else
                {
                    ClearSecureRememberMeSameKeys();
                }
            }
            catch
            {
                ClearSecureRememberMeSameKeys();
            }
        }

        /// <summary>
        /// Clears saved secure Remember-Me credentials from the standard registry keys
        /// AESPLXU and AESPLXP.
        /// </summary>
        /// <remarks>
        /// This method only clears the saved username/password values.
        /// It does not remove IsDevPC.
        /// If IsDevPC is true, developer fallback can still work after credentials are cleared.
        /// </remarks>
        public static void ClearSecureRememberMeSameKeys()
        {
            try
            {
                RegistryHelper.SaveString(
                    RegistryHelper.storeLocs.Credentials,
                    "AESPLXU",
                    string.Empty);

                RegistryHelper.SaveString(
                    RegistryHelper.storeLocs.Credentials,
                    "AESPLXP",
                    string.Empty);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Authenticates a user for the secure Remember-Me login flow.
        /// </summary>
        /// <remarks>
        /// This method supports:
        /// 1. Developer login using AGLobals.Utils.DevName and time-based password.
        /// 2. Normal database login.
        /// 3. Plain password comparison for old records.
        /// 4. SHA256 password comparison for new hashed database records.
        /// 
        /// This method is private because external applications should call
        /// LoginWithSecureRememberMeSameKeys instead.
        /// </remarks>
        /// <param name="userName">Username entered by the user.</param>
        /// <param name="password">Password entered by the user.</param>
        /// <param name="showMessage">True to show MessageBox errors; false to suppress messages.</param>
        /// <returns>True if authentication succeeds; otherwise false.</returns>
        private static bool TryAuthenticateForSecureRememberMe(string userName, string password, bool showMessage)
        {
            try
            {
                if (userName == AGLobals.Utils.DevName)
                {
                    if (password == DateTime.Now.ToString("ddMMyyyyHH"))
                        return true;

                    if (showMessage)
                    {
                        MessageBox.Show(
                            "Incorrect password.",
                            "Login Failed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }

                    return false;
                }

                var values = _userClass.GetRowAsDictionary(_LoginDataColumnName, userName);

                if (values == null || values.Count == 0)
                {
                    if (showMessage)
                    {
                        MessageBox.Show(
                            "Username not found.",
                            "Login Failed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }

                    return false;
                }

                string dbUserName = values[_LoginDataColumnName]?.ToString() ?? string.Empty;
                string dbPassword = values[_PasswordDataColumnName]?.ToString() ?? string.Empty;

                if (!string.Equals(userName, dbUserName, StringComparison.Ordinal))
                {
                    if (showMessage)
                    {
                        MessageBox.Show(
                            "Username does not match.",
                            "Login Failed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }

                    return false;
                }

                string enteredPasswordHash = HashPassword(password ?? string.Empty);

                bool passwordOk =
                    string.Equals(password, dbPassword, StringComparison.Ordinal) ||
                    string.Equals(enteredPasswordHash, dbPassword, StringComparison.OrdinalIgnoreCase);

                if (!passwordOk)
                {
                    if (showMessage)
                    {
                        MessageBox.Show(
                            "Incorrect password.",
                            "Login Failed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }

                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                if (showMessage)
                {
                    MessageBox.Show(
                        "An error occurred while checking login. Please contact support.\n\n" + ex.Message,
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }

                return false;
            }
        }

        /// <summary>
        /// Attempts to return developer credentials when the current machine is marked as a developer PC.
        /// </summary>
        /// <remarks>
        /// This method checks the registry key IsDevPC.
        /// 
        /// If IsDevPC is true, it returns:
        /// Username = AGLobals.Utils.DevName
        /// Password = current date-hour password in ddMMyyyyHH format.
        /// 
        /// This is used only as a fallback when valid DPAPI Remember-Me credentials are not found.
        /// </remarks>
        /// <param name="userName">Returns developer username if IsDevPC is true.</param>
        /// <param name="password">Returns developer time-based password if IsDevPC is true.</param>
        /// <returns>True if developer fallback credentials are available; otherwise false.</returns>
        private static bool TryGetDevPcLoginForSecureRememberMe(out string userName, out string password)
        {
            userName = string.Empty;
            password = string.Empty;

            bool isDevPc = RegistryHelper.LoadBool(
                RegistryHelper.storeLocs.Credentials,
                "IsDevPC");

            if (!isDevPc)
                return false;

            userName = AGLobals.Utils.DevName;
            password = DateTime.Now.ToString("ddMMyyyyHH");

            return true;
        }

        /// <summary>
        /// Checks whether a registry value is in the new secure DPAPI Remember-Me format.
        /// </summary>
        /// <remarks>
        /// Secure Remember-Me values must start with DPAPI:.
        /// Old SHA256 hash values do not start with DPAPI: and cannot be decrypted.
        /// </remarks>
        /// <param name="value">Registry value to check.</param>
        /// <returns>True if the value starts with DPAPI:; otherwise false.</returns>
        private static bool IsSecureRememberMeValue(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.Trim().StartsWith("DPAPI:", StringComparison.Ordinal);
        }

        /// <summary>
        /// Encrypts a plain text value using Windows DPAPI CurrentUser scope.
        /// </summary>
        /// <remarks>
        /// The returned value is prefixed with DPAPI: so the system can identify that it is
        /// a secure Remember-Me value.
        /// 
        /// DPAPI CurrentUser scope means the encrypted value can be decrypted only by the
        /// same Windows user profile that encrypted it.
        /// </remarks>
        /// <param name="plainText">Plain text username or password to encrypt.</param>
        /// <returns>Encrypted text in DPAPI:Base64 format, or empty string if encryption fails.</returns>
        private static string EncryptRememberMeValue(string plainText)
        {
            if (string.IsNullOrWhiteSpace(plainText))
                return string.Empty;

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

                byte[] encryptedBytes = ProtectedData.Protect(
                    plainBytes,
                    optionalEntropy: null,
                    scope: DataProtectionScope.CurrentUser);

                return "DPAPI:" + Convert.ToBase64String(encryptedBytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Decrypts a secure Remember-Me registry value created by EncryptRememberMeValue.
        /// </summary>
        /// <remarks>
        /// This method only decrypts values that start with DPAPI:.
        /// Plain text values and old SHA256 hash values are rejected and return empty string.
        /// 
        /// DPAPI CurrentUser scope requires the same Windows user profile that encrypted
        /// the value.
        /// </remarks>
        /// <param name="encryptedText">Encrypted registry value in DPAPI:Base64 format.</param>
        /// <returns>Original plain text value if decryption succeeds; otherwise empty string.</returns>
        private static string DecryptRememberMeValue(string encryptedText)
        {
            if (string.IsNullOrWhiteSpace(encryptedText))
                return string.Empty;

            try
            {
                encryptedText = encryptedText.Trim();

                if (!encryptedText.StartsWith("DPAPI:", StringComparison.Ordinal))
                    return string.Empty;

                string base64 = encryptedText.Substring("DPAPI:".Length);

                byte[] encryptedBytes = Convert.FromBase64String(base64);

                byte[] plainBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    optionalEntropy: null,
                    scope: DataProtectionScope.CurrentUser);

                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Logs out the current user and clears secure Remember-Me credentials from
        /// AESPLXU and AESPLXP.
        /// </summary>
        /// <remarks>
        /// Use this method in new applications instead of the old logout method.
        /// It follows the same basic logout behavior:
        /// 1. Optionally asks for confirmation.
        /// 2. Clears AESPLXU and AESPLXP.
        /// 3. Resets internal login state.
        ///
        /// This method does not clear IsDevPC.
        /// </remarks>
        /// <param name="wantConfirmationMessage">True to show logout confirmation; false to logout directly.</param>
        /// <returns>True if logout is completed; false if cancelled or failed.</returns>
        public static bool LogoutSecureRememberMeSameKeys(bool wantConfirmationMessage = true)
        {
            try
            {
                DialogResult result;

                if (wantConfirmationMessage)
                {
                    result = MessageBox.Show(
                        "Are you sure you want to logout?",
                        "Logout Confirmation",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                }
                else
                {
                    result = DialogResult.Yes;
                }

                if (result != DialogResult.Yes)
                    return false;

                ClearSecureRememberMeSameKeys();

                UserName = string.Empty;
                Password = string.Empty;
                checkedRememberMe = false;
                _loginFlowRunning = false;

                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion
    }
}
