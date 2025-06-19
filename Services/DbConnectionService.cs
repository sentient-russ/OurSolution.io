#nullable disable
using os.Models;
using os.Areas.Identity.Data;
using MySql.Data.MySqlClient;
using System.Security.Claims;
using MySqlX.XDevAPI.CRUD;
using System.Net;
using Microsoft.EntityFrameworkCore;

namespace os.Services
{
    public class DbConnectionService
    {

        private readonly IHttpContextAccessor _httpContextAccessor;

        public DbConnectionService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }
        /*
         * Gets a list of all speakers for list management
         */
        public List<SpeakerModel> GetSpeakersList()
        {
            List<SpeakerModel> foundSpeakers = new List<SpeakerModel>();
            try
            {
                using var conn1 = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "SELECT * FROM os.Speakers";
                conn1.Open();
                MySqlCommand cmd1 = new MySqlCommand(command, conn1);
                MySqlDataReader reader1 = cmd1.ExecuteReader();
                while (reader1.Read())
                {
                    SpeakerModel nextSpeaker = new SpeakerModel();
                    nextSpeaker.SpeakerId = reader1.IsDBNull(0) ? null : reader1.GetInt32(0);
                    nextSpeaker.FirstName = reader1.IsDBNull(1) ? null : reader1.GetString(1);
                    nextSpeaker.LastName = reader1.IsDBNull(2) ? null : reader1.GetString(2);
                    nextSpeaker.Description = reader1.IsDBNull(3) ? null : reader1.GetString(3);
                    nextSpeaker.NumUpVotes = reader1.IsDBNull(4) ? null : reader1.GetInt32(4);
                    nextSpeaker.DateRecorded = reader1.IsDBNull(5) ? null : reader1.GetDateTime(5);
                    nextSpeaker.UploadDate = reader1.IsDBNull(6) ? null : reader1.GetDateTime(6);
                    nextSpeaker.UploadedBy = reader1.IsDBNull(7) ? null : reader1.GetString(7);
                    nextSpeaker.SpeakerStatus = reader1.IsDBNull(8) ? null : reader1.GetString(8);
                    nextSpeaker.DisplayFileName = reader1.IsDBNull(9) ? null : reader1.GetString(9);
                    nextSpeaker.SecretFileName = reader1.IsDBNull(10) ? null : reader1.GetString(10);
                    nextSpeaker.UploadedById = reader1.IsDBNull(11) ? null : reader1.GetString(11);
                    nextSpeaker.Visibility = reader1.IsDBNull(12) ? null : reader1.GetString(12);
                    foundSpeakers.Add(nextSpeaker);
                }
                reader1.Close();
                conn1.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            return foundSpeakers;
        }
        /*
         * Gets a list of all enabled speakers for internal player display
         */
        public List<SpeakerModel> GetAllSpeakersList()
        {
            List<SpeakerModel> foundSpeakers = new List<SpeakerModel>();
            try
            {
                using var conn1 = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "SELECT * FROM os.Speakers";
                conn1.Open();
                MySqlCommand cmd1 = new MySqlCommand(command, conn1);
                MySqlDataReader reader1 = cmd1.ExecuteReader();
                while (reader1.Read())
                {
                    SpeakerModel nextSpeaker = new SpeakerModel();
                    nextSpeaker.SpeakerId = reader1.IsDBNull(0) ? null : reader1.GetInt32(0);
                    nextSpeaker.FirstName = reader1.IsDBNull(1) ? null : reader1.GetString(1);
                    nextSpeaker.LastName = reader1.IsDBNull(2) ? null : reader1.GetString(2);
                    nextSpeaker.Description = reader1.IsDBNull(3) ? null : reader1.GetString(3);
                    nextSpeaker.NumUpVotes = reader1.IsDBNull(4) ? null : reader1.GetInt32(4);
                    nextSpeaker.DateRecorded = reader1.IsDBNull(5) ? null : reader1.GetDateTime(5);
                    nextSpeaker.UploadDate = reader1.IsDBNull(6) ? null : reader1.GetDateTime(6);
                    nextSpeaker.UploadedBy = reader1.IsDBNull(7) ? null : reader1.GetString(7);
                    nextSpeaker.SpeakerStatus = reader1.IsDBNull(8) ? null : reader1.GetString(8);
                    nextSpeaker.DisplayFileName = reader1.IsDBNull(9) ? null : reader1.GetString(9);
                    nextSpeaker.SecretFileName = reader1.IsDBNull(10) ? null : reader1.GetString(10);
                    nextSpeaker.UploadedById = reader1.IsDBNull(11) ? null : reader1.GetString(11);
                    nextSpeaker.Visibility = reader1.IsDBNull(12) ? null : reader1.GetString(12);
                    foundSpeakers.Add(nextSpeaker);
                }
                reader1.Close();
                conn1.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            // remove disabled speakers
            List<SpeakerModel> EnabledSpeakers = new List<SpeakerModel>();
            foreach (SpeakerModel speaker in foundSpeakers)
            {
                if (speaker.SpeakerStatus == "Active")
                {
                    EnabledSpeakers.Add(speaker);
                }
            }
            return EnabledSpeakers;
        }
        /*
         * Gets a list of all speakers
         */
        public List<SpeakerModel> GetPublicSpeakersList()
        {
            List<SpeakerModel> foundSpeakers = new List<SpeakerModel>();
            try
            {
                using var conn1 = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "SELECT * FROM os.Speakers";
                conn1.Open();
                MySqlCommand cmd1 = new MySqlCommand(command, conn1);
                MySqlDataReader reader1 = cmd1.ExecuteReader();
                while (reader1.Read())
                {
                    SpeakerModel nextSpeaker = new SpeakerModel();
                    nextSpeaker.SpeakerId = reader1.IsDBNull(0) ? null : reader1.GetInt32(0);
                    nextSpeaker.FirstName = reader1.IsDBNull(1) ? null : reader1.GetString(1);
                    nextSpeaker.LastName = reader1.IsDBNull(2) ? null : reader1.GetString(2);
                    nextSpeaker.Description = reader1.IsDBNull(3) ? null : reader1.GetString(3);
                    nextSpeaker.NumUpVotes = reader1.IsDBNull(4) ? null : reader1.GetInt32(4);
                    nextSpeaker.DateRecorded = reader1.IsDBNull(5) ? null : reader1.GetDateTime(5);
                    nextSpeaker.UploadDate = reader1.IsDBNull(6) ? null : reader1.GetDateTime(6);
                    nextSpeaker.UploadedBy = reader1.IsDBNull(7) ? null : reader1.GetString(7);
                    nextSpeaker.SpeakerStatus = reader1.IsDBNull(8) ? null : reader1.GetString(8);
                    nextSpeaker.DisplayFileName = reader1.IsDBNull(9) ? null : reader1.GetString(9);
                    nextSpeaker.SecretFileName = reader1.IsDBNull(10) ? null : reader1.GetString(10);
                    nextSpeaker.UploadedById = reader1.IsDBNull(11) ? null : reader1.GetString(11);
                    nextSpeaker.Visibility = reader1.IsDBNull(12) ? null : reader1.GetString(12);
                    foundSpeakers.Add(nextSpeaker);
                }
                reader1.Close();
                conn1.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            List<SpeakerModel> publicSpeakers = new List<SpeakerModel>();
            foreach (SpeakerModel speaker in foundSpeakers)
            {
                if (speaker.Visibility == "Universal" && speaker.SpeakerStatus == "Active")
                {
                    publicSpeakers.Add(speaker);
                }
            }
            return publicSpeakers;
        }
        /*
         * Gets a single speaker details by id
         */
        public SpeakerModel GetSpeakerById(int id)
        {
            List<SpeakerModel> speakers = GetSpeakersList();
            SpeakerModel foundSpeaker = new SpeakerModel();
            foreach (SpeakerModel speaker in speakers)
            {
                if (speaker.SpeakerId == id) { foundSpeaker = speaker; break; }
            }
            return foundSpeaker;

        }
        /*
         * Updates allowed fields for a specific speaker
         */
        public bool UpdateSpeakerDetails(SpeakerModel speakerIn)
        {
            // Log the event; must take place before the update to capture before and after state
            var email = _httpContextAccessor.HttpContext?.User?.Identity.Name;
            AppUser userDetails = GetUserDetailsByEmail(email);
            List<SpeakerModel> speakers = GetSpeakersList();
            SpeakerModel previouseSpeakerModel = new SpeakerModel();
            foreach (SpeakerModel speaker in speakers)
            {
                if (speaker.SpeakerId == speakerIn.SpeakerId)
                {
                    previouseSpeakerModel = speaker;
                }
            }
            string beforeUpdate = "";
            if (previouseSpeakerModel.SpeakerId == 0)
            {
                beforeUpdate = "No previous record";
            }
            else
            {
                beforeUpdate = SpeakerDetailsString(previouseSpeakerModel);
            }
            string afterUpdate = SpeakerDetailsString(speakerIn);
            CreateLog(email, "UpdateSpeakerDetails() called.", beforeUpdate, afterUpdate);

            bool Succeeded = false;
            try
            {
                using var conn1 = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "UPDATE os.Speakers SET FirstName = @FirstName, LastName = @LastName," +
                    " Description = @Description ,DateRecorded = @DateRecorded, SpeakerStatus = @SpeakerStatus, " +
                    " Visibility = @Visibility WHERE SpeakerId LIKE @SpeakerId";
                conn1.Open();
                MySqlCommand cmd1 = new MySqlCommand(command, conn1);
                cmd1.Parameters.AddWithValue("@SpeakerId", speakerIn.SpeakerId);
                cmd1.Parameters.AddWithValue("@FirstName", speakerIn.FirstName);
                cmd1.Parameters.AddWithValue("@LastName", speakerIn.LastName);
                cmd1.Parameters.AddWithValue("@Description", speakerIn.Description);
                cmd1.Parameters.AddWithValue("@DateRecorded", speakerIn.DateRecorded);
                cmd1.Parameters.AddWithValue("@SpeakerStatus", speakerIn.SpeakerStatus);
                cmd1.Parameters.AddWithValue("@Visibility", speakerIn.Visibility);

                MySqlDataReader reader1 = cmd1.ExecuteReader();
                reader1.Close();
                conn1.Close();
                Succeeded = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return Succeeded;
        }
        /*
         * Adds new speaker's details
         */
        public int AddSpeaker(SpeakerModel speakerIn)
        {
            // Log the event; must take place before the update to capture before and after state.
            var email = _httpContextAccessor.HttpContext?.User?.Identity.Name;
            AppUser userDetails = GetUserDetailsByEmail(email);
            string beforeUpdate = "No previous record";
            string afterUpdate = SpeakerDetailsString(speakerIn);
            CreateLog(email, "AddSpeaker() called.", beforeUpdate, afterUpdate);
            int newSpeakerId = 0;

            try
            {
                using var conn1 = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "INSERT INTO os.Speakers (FirstName, LastName, Description, NumUpVotes, DateRecorded, UploadDate, UploadedBy, SpeakerStatus, DisplayFileName, SecretFileName, UploadedById, Visibility) " +
                    "VALUES (@FirstName, @LastName, @Description, @NumUpVotes, @DateRecorded, @UploadDate, @UploadedBy, @SpeakerStatus, @DisplayFileName, @SecretFileName, @UploadedById, @Visibility)";
                conn1.Open();
                MySqlCommand cmd1 = new MySqlCommand(command, conn1);
                cmd1.Parameters.AddWithValue("@FirstName", speakerIn.FirstName);
                cmd1.Parameters.AddWithValue("@LastName", speakerIn.LastName);
                cmd1.Parameters.AddWithValue("@Description", speakerIn.Description);
                cmd1.Parameters.AddWithValue("@NumUpVotes", speakerIn.NumUpVotes);
                cmd1.Parameters.AddWithValue("@DateRecorded", speakerIn.DateRecorded);
                cmd1.Parameters.AddWithValue("@UploadDate", speakerIn.UploadDate);
                cmd1.Parameters.AddWithValue("@UploadedBy", speakerIn.UploadedBy);
                cmd1.Parameters.AddWithValue("@SpeakerStatus", speakerIn.SpeakerStatus);
                cmd1.Parameters.AddWithValue("@DisplayFileName", speakerIn.DisplayFileName);
                cmd1.Parameters.AddWithValue("@SecretFileName", speakerIn.SecretFileName);
                cmd1.Parameters.AddWithValue("@UploadedById", speakerIn.UploadedById);
                cmd1.Parameters.AddWithValue("@Visibility", speakerIn.Visibility);
                newSpeakerId = Convert.ToInt32(cmd1.ExecuteScalar());

                MySqlDataReader reader1 = cmd1.ExecuteReader();
                reader1.Close();
                conn1.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            
            return newSpeakerId;
        }
        public string SpeakerDetailsString(SpeakerModel speakerIn)
        {
            string speakerString = "";
            SpeakerModel speaker = speakerIn;
            speakerString += speaker.SpeakerId + ", ";
            speakerString += speaker.FileName + ", ";
            speakerString += speaker.FirstName + ", ";
            speakerString += speaker.LastName + ", ";
            speakerString += speaker.Description + ", ";
            speakerString += speaker.NumUpVotes + ", ";
            speakerString += speaker.DateRecorded.ToString() + ", ";
            speakerString += speaker.UploadDate.ToString() + ", ";
            speakerString += speaker.UploadedBy + ", ";
            speakerString += speaker.SpeakerStatus + ", ";
            speakerString += speaker.Visibility;
            return speakerString;
        }
        /*
         * Gets the next speaker id
         */
        public int GetNextSpeakerId()
        {
            int nextSpeakerId = 0;
            try
            {
                using var conn1 = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "SELECT MAX(SpeakerId) FROM os.Speakers";
                conn1.Open();
                MySqlCommand cmd1 = new MySqlCommand(command, conn1);
                MySqlDataReader reader1 = cmd1.ExecuteReader();
                while (reader1.Read())
                {
                    int highestId = reader1.GetInt32(0);
                    nextSpeakerId = highestId + 1;
                }
                reader1.Close();
                conn1.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return nextSpeakerId;
        }
        /*
         * Updates the database with a users details
         */
        public async Task<bool> UpdateUserDetailsAsync(AppUser userIn)
        {
            //log the event, must take place before the update to capture before and after state
            string beforeUpdate = UserDetailsToString(GetUserDetailsById(userIn.Id));
            string afterUpdate = UserDetailsToString(userIn);
            AppUser user = GetUserDetailsById(userIn.Id);
            CreateLog(user.Email, "UpdateUserDetailsAsync() called.", beforeUpdate, afterUpdate);

            bool Succeeded = false;
            try
            {
                using var conn1 = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "UPDATE os.Users SET FirstName = @FirstName, LastName = @LastName, PhoneNumber = @PhoneNumber, " +
                    "BellyButtonBirthday = @BellyButtonBirthday, AABirthday = @AABirthday, Address = @Address, City = @City, State = @State, Zip = @Zip," +
                    " UserRole = @UserRole, ActiveStatus = @ActiveStatus, ProfileImage = @ProfileImage, EmailConfirmed = @EmailConfirmed, NormalizedEmail = @NormalizedEmail" +
                    " WHERE Id LIKE @Id";
                conn1.Open();
                MySqlCommand cmd1 = new MySqlCommand(command, conn1);
                cmd1.Parameters.AddWithValue("@Id", userIn.Id);
                cmd1.Parameters.AddWithValue("@FirstName", userIn.FirstName);
                cmd1.Parameters.AddWithValue("@LastName", userIn.LastName);
                cmd1.Parameters.AddWithValue("@PhoneNumber", userIn.PhoneNumber);
                cmd1.Parameters.AddWithValue("@BellyButtonBirthday", userIn.BellyButtonBirthday);
                cmd1.Parameters.AddWithValue("@AABirthday", userIn.AABirthday);
                cmd1.Parameters.AddWithValue("@Address", userIn.Address);
                cmd1.Parameters.AddWithValue("@City", userIn.City);
                cmd1.Parameters.AddWithValue("@State", userIn.State);
                cmd1.Parameters.AddWithValue("@Zip", userIn.Zip);
                //ensure no duplicate roles are assigned to a user
                DeleteUserRole(userIn.Id);
                AssignUserRole(userIn.Id, userIn.UserRole);
                // ensure admin and shared accounts remain intact
                if (userIn.Email.ToLower() == "admin@oursolution.io")
                {
                    cmd1.Parameters.AddWithValue("@ActiveStatus", "Active");
                    cmd1.Parameters.AddWithValue("@UserRole", "Administrator");
                }
                else if (userIn.Email.ToLower() == "shareduser@oursolution.io")
                {
                    cmd1.Parameters.AddWithValue("@ActiveStatus", "Active");
                    cmd1.Parameters.AddWithValue("@UserRole", "Member");
                }
                else
                {
                    cmd1.Parameters.AddWithValue("@UserRole", userIn.UserRole);
                    cmd1.Parameters.AddWithValue("@ActiveStatus", userIn.ActiveStatus);
                }
                if (userIn.ActiveStatus == "Active")
                {
                    cmd1.Parameters.AddWithValue("EmailConfirmed", 1);
                }
                cmd1.Parameters.AddWithValue("@ProfileImage", userIn.ProfileImage);
                // bug fix to add back the normalized email durring password reset process.
                AppUser foundUser = GetUserDetailsById(userIn.Id);
                cmd1.Parameters.AddWithValue("@NormalizedEmail", foundUser.Email.ToUpper());
                MySqlDataReader reader1 = cmd1.ExecuteReader();
                reader1.Close();
                conn1.Close();
                Succeeded = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return Succeeded;
        }
        public string UserDetailsToString(AppUser appUserIn)
        {
            string userString = "";
            AppUser user = appUserIn;
            userString += user.Id + ", ";
            userString += user.FirstName + ", ";
            userString += user.LastName + ", ";
            userString += user.PhoneNumber + ", ";
            userString += user.BellyButtonBirthday.ToString() + ", ";
            userString += user.AABirthday.ToString() + ", ";
            userString += user.Address + ", ";
            userString += user.City + ", ";
            userString += user.State + ", ";
            userString += user.Zip + ", ";
            userString += user.UserRole + ", ";
            userString += user.ActiveStatus + ", ";
            userString += user.ProfileImage;
            return userString;
        }
        /*
         * Updates a users role in the UserRoles juntion table
         * This action is logged in the update user method
         */
        public void AssignUserRole(string uidIn, string uRoleIn)
        {
            //get role id from role name
            string newRoleId = GetRoleId(uRoleIn);
            if (UserRolePresent(uidIn))
            {
                try
                {
                    using var conn1 = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                    string command = "UPDATE os.UserRoles SET RoleId = @RoleId WHERE UserId = @UserId;";
                    MySqlCommand cmd1 = new MySqlCommand(command, conn1);
                    conn1.Open();
                    cmd1.Parameters.AddWithValue("@UserId", uidIn);
                    cmd1.Parameters.AddWithValue("@RoleId", newRoleId);
                    cmd1.ExecuteNonQuery();
                    conn1.Close();

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
            else
            {
                try
                {
                    using var conn1 = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                    string command = "INSERT INTO os.UserRoles (RoleId, UserId) VALUES (@RoleId, @UserId)";
                    MySqlCommand cmd1 = new MySqlCommand(command, conn1);
                    conn1.Open();
                    cmd1.Parameters.AddWithValue("@UserId", uidIn);
                    cmd1.Parameters.AddWithValue("@RoleId", newRoleId);
                    cmd1.ExecuteNonQuery();
                    conn1.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }

        }
        /*
         * checks for a userid in the role juntion table
         */
        public bool UserRolePresent(string uidIn)
        {
            //get role id
            bool userPresent = false;
            try
            {
                using var conn1 = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "SELECT * FROM os.UserRoles WHERE UserId = @UserId;";
                conn1.Open();
                MySqlCommand cmd1 = new MySqlCommand(command, conn1);
                cmd1.Parameters.AddWithValue("@UserId", uidIn);
                MySqlDataReader reader1 = cmd1.ExecuteReader();
                while (reader1.Read())
                {
                    if (reader1.GetString(1) == uidIn)
                    {
                        userPresent = true;
                    }
                }
                conn1.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return userPresent;
        }
        /*
         * Gets a role's id based on the role type as a string
         */
        public string GetRoleId(string userRoleIn)
        {
            string foundId = "";
            try
            {
                using var conn1 = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "SELECT * FROM os.Roles;";
                conn1.Open();
                MySqlCommand cmd1 = new MySqlCommand(command, conn1);
                MySqlDataReader reader1 = cmd1.ExecuteReader();
                while (reader1.Read())
                {
                    if (reader1.GetString(1) == userRoleIn)
                    {
                        foundId = reader1.GetString(0);
                    }
                }
                reader1.Close();
                conn1.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return foundId;
        }
        /*
         * Deletes one users record from the UserRoles table. This helps to prevent the user from having more than on role asssigned to a single account.
         */
        public void DeleteUserRole(string uidIn)
        {
            try
            {
                using var conn1 = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "DELETE FROM os.UserRoles WHERE UserId = @UserId;";
                MySqlCommand cmd1 = new MySqlCommand(command, conn1);
                conn1.Open();
                cmd1.Parameters.AddWithValue("@UserId", uidIn);
                cmd1.ExecuteNonQuery();
                conn1.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
        /*
         * Gets a list of user roles
         */
        public List<RoleModel> GetUserRole(string userRoleIn)
        {
            List<RoleModel> foundRoles = new List<RoleModel>();
            try
            {
                using var conn1 = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "SELECT FirstName,LastName,Username FROM os.Users Where UserRole=@Role;";
                conn1.Open();
                MySqlCommand cmd1 = new MySqlCommand(command, conn1);
                cmd1.Parameters.AddWithValue("@Role", userRoleIn);
                MySqlDataReader reader1 = cmd1.ExecuteReader();
                while (reader1.Read())
                {
                    RoleModel role = new RoleModel();
                    role.firstName = reader1.GetString(0);
                    role.lastName = reader1.GetString(1);
                    role.email = reader1.GetString(2);
                    foundRoles.Add(role);
                }
                reader1.Close();
                conn1.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return foundRoles;
        }
        /*
         * Gets a users details from db by Id
         */
        public AppUser GetUserDetailsById(string idIn)
        {
            var foundUser = new AppUser();
            try
            {
                using var conn1 = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "SELECT * FROM os.Users WHERE Id = @Id;";
                conn1.Open();
                MySqlCommand cmd1 = new MySqlCommand(command, conn1);
                cmd1.Parameters.AddWithValue("@Id", idIn);
                MySqlDataReader reader1 = cmd1.ExecuteReader();
                while (reader1.Read())
                {
                    foundUser.Id = reader1.IsDBNull(0) ? null : reader1.GetString(0);
                    foundUser.FirstName = reader1.IsDBNull(1) ? null : reader1.GetString(1);
                    foundUser.LastName = reader1.IsDBNull(2) ? null : reader1.GetString(2);
                    foundUser.PhoneNumber = reader1.IsDBNull(3) ? null : reader1.GetString(3);
                    foundUser.BellyButtonBirthday = reader1.IsDBNull(4) ? null : reader1.GetDateTime(4);
                    foundUser.AABirthday = reader1.IsDBNull(5) ? null : reader1.GetDateTime(5);
                    foundUser.Address = reader1.IsDBNull(6) ? null : reader1.GetString(6);
                    foundUser.City = reader1.IsDBNull(7) ? null : reader1.GetString(7);
                    foundUser.State = reader1.IsDBNull(8) ? null : reader1.GetString(8);
                    foundUser.Zip = reader1.IsDBNull(9) ? null : reader1.GetString(9);
                    foundUser.UserRole = reader1.IsDBNull(10) ? null : reader1.GetString(10);
                    foundUser.ActiveStatus = reader1.IsDBNull(11) ? null : reader1.GetString(11);
                    foundUser.ProfileImage = reader1.IsDBNull(12) ? null : reader1.GetString(12);
                    foundUser.UserName = reader1.IsDBNull(13) ? null : reader1.GetString(13);
                    foundUser.NormalizedUserName = reader1.IsDBNull(14) ? null : reader1.GetString(14);
                    foundUser.Email = reader1.IsDBNull(15) ? null : reader1.GetString(15);
                }
                reader1.Close();
                conn1.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return foundUser;
        }
        /*
         * Gets a users details from db by Email
         */
        public AppUser GetUserDetailsByEmail(string emailIn)
        {
            var foundUser = new AppUser();
            try
            {
                using var conn1 = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "SELECT * FROM os.Users WHERE Email = @Email;";
                conn1.Open();
                MySqlCommand cmd1 = new MySqlCommand(command, conn1);
                cmd1.Parameters.AddWithValue("@Email", emailIn.ToLower());
                MySqlDataReader reader1 = cmd1.ExecuteReader();
                while (reader1.Read())
                {
                    foundUser.Id = reader1.IsDBNull(0) ? null : reader1.GetString(0);
                    foundUser.FirstName = reader1.IsDBNull(1) ? null : reader1.GetString(1);
                    foundUser.LastName = reader1.IsDBNull(2) ? null : reader1.GetString(2);
                    foundUser.PhoneNumber = reader1.IsDBNull(3) ? null : reader1.GetString(3);
                    foundUser.BellyButtonBirthday = reader1.IsDBNull(4) ? null : reader1.GetDateTime(4);
                    foundUser.AABirthday = reader1.IsDBNull(5) ? null : reader1.GetDateTime(5);
                    foundUser.Address = reader1.IsDBNull(6) ? null : reader1.GetString(6);
                    foundUser.City = reader1.IsDBNull(7) ? null : reader1.GetString(7);
                    foundUser.State = reader1.IsDBNull(8) ? null : reader1.GetString(8);
                    foundUser.Zip = reader1.IsDBNull(9) ? null : reader1.GetString(9);
                    foundUser.UserRole = reader1.IsDBNull(10) ? null : reader1.GetString(10);
                    foundUser.ActiveStatus = reader1.IsDBNull(11) ? null : reader1.GetString(11);
                    foundUser.ProfileImage = reader1.IsDBNull(12) ? null : reader1.GetString(12);
                    foundUser.UserName = reader1.IsDBNull(13) ? null : reader1.GetString(13);
                    foundUser.NormalizedUserName = reader1.IsDBNull(14) ? null : reader1.GetString(14);
                    foundUser.Email = reader1.IsDBNull(15) ? null : reader1.GetString(15);
                }
                reader1.Close();
                conn1.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return foundUser;
        }
        /*
         * creates an event log
         */
        public void CreateLog(string userIdIn, string descriptionIn, string changedFromIn, string changedToIn)
        {

            LogModel newLog = new LogModel();
            newLog.EventDate = DateTime.Now;
            newLog.UserId = userIdIn;
            newLog.Description = descriptionIn;
            newLog.ChangedFrom = changedFromIn;
            newLog.ChangedTo = changedToIn;
            AddLog(newLog);
        }
        /*
         * Adds a single log to the db
         */
        public void AddLog(LogModel logIn)
        {
            try
            {
                using var conn1 = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "INSERT INTO os.SecurityLog (EventDate, UserId, Description, ChangedFrom, ChangedTo)" +
                    " VALUES (@EventDate, @UserId, @Description, @ChangedFrom, @ChangedTo)";
                conn1.Open();
                MySqlCommand cmd1 = new MySqlCommand(command, conn1);
                cmd1.Parameters.AddWithValue("@EventDate", logIn.EventDate);
                cmd1.Parameters.AddWithValue("@UserId", logIn.UserId);
                cmd1.Parameters.AddWithValue("@Description", logIn.Description);
                cmd1.Parameters.AddWithValue("@ChangedFrom", logIn.ChangedFrom);
                cmd1.Parameters.AddWithValue("@ChangedTo", logIn.ChangedTo);
                MySqlDataReader reader1 = cmd1.ExecuteReader();
                reader1.Close();
                conn1.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
        /*
         * Gets a list of the logs account based on its name
         */
        public List<LogModel> GetLogs()
        {
            List<LogModel> logs = new List<LogModel>();
            try
            {
                using var conn1 = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "SELECT * FROM os.SecurityLog";
                conn1.Open();
                MySqlCommand cmd1 = new MySqlCommand(command, conn1);
                MySqlDataReader reader1 = cmd1.ExecuteReader();
                while (reader1.Read())
                {
                    LogModel log = new LogModel();
                    log.EventId = reader1.IsDBNull(0) ? null : reader1.GetInt32(0);
                    log.EventDate = reader1.IsDBNull(1) ? null : reader1.GetDateTime(1);
                    log.UserId = reader1.IsDBNull(2) ? null : reader1.GetString(2);
                    log.Description = reader1.IsDBNull(3) ? null : reader1.GetString(3);
                    log.ChangedFrom = reader1.IsDBNull(4) ? null : reader1.GetString(4);
                    log.ChangedTo = reader1.IsDBNull(5) ? null : reader1.GetString(5);
                    logs.Add(log);

                }
                reader1.Close();
                conn1.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return logs;
        }
        /*
        * Gets a list of the announcements
        */
        public List<AnnouncementModel> GetAnnouncementList()
        {
            List<AnnouncementModel> announcements = new List<AnnouncementModel>();
            try
            {
                using var conn1 = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "SELECT * FROM os.Announcements";
                conn1.Open();
                MySqlCommand cmd1 = new MySqlCommand(command, conn1);
                MySqlDataReader reader1 = cmd1.ExecuteReader();
                while (reader1.Read())
                {
                    AnnouncementModel announcement = new AnnouncementModel();
                    announcement.Id = reader1.IsDBNull(0) ? null : reader1.GetInt32(0);
                    announcement.AnnouncementTxt = reader1.IsDBNull(1) ? null : reader1.GetString(1);
                    announcement.AnnouncementDate = reader1.IsDBNull(2) ? null : reader1.GetDateTime(2);
                    announcement.Status = reader1.IsDBNull(3) ? null : reader1.GetString(3);
                    announcements.Add(announcement);
                }
                reader1.Close();
                conn1.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return announcements;
        }
        public List<AnnouncementModel> AddAnnouncement(AnnouncementModel newAnnouncement)
        {
            List<AnnouncementModel> announcements = new List<AnnouncementModel>();
            try
            {
                using var conn1 = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "INSERT INTO os.Announcements (AnnouncementTxt, AnnouncementDate, Status) " +
                    "VALUES (@AnnouncementTxt, @AnnouncementDate, @Status)";
                conn1.Open();
                MySqlCommand cmd1 = new MySqlCommand(command, conn1);
                cmd1.Parameters.AddWithValue("@AnnouncementTxt", newAnnouncement.AnnouncementTxt);
                cmd1.Parameters.AddWithValue("@AnnouncementDate", DateTime.Now);
                cmd1.Parameters.AddWithValue("@Status", newAnnouncement.Status);
                MySqlDataReader reader1 = cmd1.ExecuteReader();
                reader1.Close();
                conn1.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return announcements;
        }
        public List<AnnouncementModel> UpdateAnnouncement(AnnouncementModel updatedAnnouncement)
        {
            try
            {
                using var conn = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "UPDATE os.Announcements SET AnnouncementTxt = @AnnouncementTxt, AnnouncementDate = @AnnouncementDate, Status = @Status WHERE Id = @Id";
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(command, conn);
                cmd.Parameters.AddWithValue("@Id", updatedAnnouncement.Id);
                cmd.Parameters.AddWithValue("@AnnouncementTxt", updatedAnnouncement.AnnouncementTxt);
                cmd.Parameters.AddWithValue("@AnnouncementDate", updatedAnnouncement.AnnouncementDate ?? DateTime.Now);
                cmd.Parameters.AddWithValue("@Status", updatedAnnouncement.Status);

                int rowsAffected = cmd.ExecuteNonQuery();
                conn.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return GetAnnouncementList();
        }
        public List<AnnouncementModel> DeleteAnnouncement(int announcementId)
        {
            List<AnnouncementModel> announcements = new List<AnnouncementModel>();
            try
            {
                using var conn1 = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "DELETE FROM os.Announcements WHERE Id = @Id";
                conn1.Open();
                MySqlCommand cmd1 = new MySqlCommand(command, conn1);
                cmd1.Parameters.AddWithValue("@Id", announcementId);
                MySqlDataReader reader1 = cmd1.ExecuteReader();
                reader1.Close();
                conn1.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return GetAnnouncementList();
        }
        /*
* Gets a list of all meetings
*/
        public List<MeetingModel> GetMeetingList()
        {
            List<MeetingModel> meetings = new List<MeetingModel>();
            try
            {
                using var conn = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "SELECT * FROM os.Meetings";
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(command, conn);
                MySqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    MeetingModel meeting = new MeetingModel();
                    meeting.Id = reader.IsDBNull(0) ? null : reader.GetInt32(0);
                    meeting.MeetingName = reader.IsDBNull(1) ? null : reader.GetString(1);
                    meeting.Weekday = reader.IsDBNull(2) ? null : reader.GetString(2);
                    meeting.StartTime = reader.IsDBNull(3) ? null : reader.GetString(3);
                    meeting.StartTimeAMPM = reader.IsDBNull(4) ? null : reader.GetString(4);
                    meeting.EndTime = reader.IsDBNull(5) ? null : reader.GetString(5);
                    meeting.EndTimeAMPM = reader.IsDBNull(6) ? null : reader.GetString(6);
                    meeting.StreetAddress = reader.IsDBNull(7) ? null : reader.GetString(7);
                    meeting.City = reader.IsDBNull(8) ? null : reader.GetString(8);
                    meeting.State = reader.IsDBNull(9) ? null : reader.GetString(9);
                    meeting.Zip = reader.IsDBNull(10) ? null : reader.GetString(10);
                    meeting.Status = reader.IsDBNull(11) ? null : reader.GetString(11);
                    meeting.GoogleMapsLink = reader.IsDBNull(12) ? null : reader.GetString(12);
                    meeting.LocationName = reader.IsDBNull(13) ? null : reader.GetString(13);
                    meetings.Add(meeting);
                }
                reader.Close();
                conn.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return meetings;
        }

        public List<MeetingModel> AddMeeting(MeetingModel newMeeting)
        {
            List<MeetingModel> meetings = new List<MeetingModel>();
            try
            {
                using var conn = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "INSERT INTO os.Meetings (MeetingName, Weekday, StartTime, StartTimeAMPM, EndTime, EndTimeAMPM, StreetAddress, City, State, Zip, Status, GoogleMapsLink, LocationName) " +
                    "VALUES (@MeetingName, @Weekday, @StartTime, @StartTimeAMPM, @EndTime, @EndTimeAMPM, @StreetAddress, @City, @State, @Zip, @Status, @GoogleMapsLink, @LocationName)";
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(command, conn);
                cmd.Parameters.AddWithValue("@MeetingName", newMeeting.MeetingName);
                cmd.Parameters.AddWithValue("@Weekday", newMeeting.Weekday);
                cmd.Parameters.AddWithValue("@StartTime", newMeeting.StartTime);
                cmd.Parameters.AddWithValue("@StartTimeAMPM", newMeeting.StartTimeAMPM);
                cmd.Parameters.AddWithValue("@EndTime", newMeeting.EndTime);
                cmd.Parameters.AddWithValue("@EndTimeAMPM", newMeeting.EndTimeAMPM);
                cmd.Parameters.AddWithValue("@StreetAddress", newMeeting.StreetAddress);
                cmd.Parameters.AddWithValue("@City", newMeeting.City);
                cmd.Parameters.AddWithValue("@State", newMeeting.State);
                cmd.Parameters.AddWithValue("@Zip", newMeeting.Zip);
                cmd.Parameters.AddWithValue("@Status", newMeeting.Status);
                cmd.Parameters.AddWithValue("GoogleMapsLink", newMeeting.GoogleMapsLink);
                cmd.Parameters.AddWithValue("LocationName", newMeeting.LocationName);

                MySqlDataReader reader = cmd.ExecuteReader();
                reader.Close();
                conn.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return meetings;
        }

        public List<MeetingModel> UpdateMeeting(MeetingModel updatedMeeting)
        {
            List<MeetingModel> meetings = new List<MeetingModel>();
            try
            {
                using var conn = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "UPDATE os.Meetings SET MeetingName = @MeetingName, Weekday = @Weekday, StartTime = @StartTime, " +
                    "StartTimeAMPM = @StartTimeAMPM, EndTime = @EndTime, EndTimeAMPM = @EndTimeAMPM, StreetAddress = @StreetAddress, " +
                    "City = @City, State = @State, Zip = @Zip, Status = @Status, GoogleMapsLink = @GoogleMapsLink, LocationName = @LocationName WHERE Id = @Id";
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(command, conn);
                cmd.Parameters.AddWithValue("@Id", updatedMeeting.Id);
                cmd.Parameters.AddWithValue("@MeetingName", updatedMeeting.MeetingName);
                cmd.Parameters.AddWithValue("@Weekday", updatedMeeting.Weekday);
                cmd.Parameters.AddWithValue("@StartTime", updatedMeeting.StartTime);
                cmd.Parameters.AddWithValue("@StartTimeAMPM", updatedMeeting.StartTimeAMPM);
                cmd.Parameters.AddWithValue("@EndTime", updatedMeeting.EndTime);
                cmd.Parameters.AddWithValue("@EndTimeAMPM", updatedMeeting.EndTimeAMPM);
                cmd.Parameters.AddWithValue("@StreetAddress", updatedMeeting.StreetAddress);
                cmd.Parameters.AddWithValue("@City", updatedMeeting.City);
                cmd.Parameters.AddWithValue("@State", updatedMeeting.State);
                cmd.Parameters.AddWithValue("@Zip", updatedMeeting.Zip);
                cmd.Parameters.AddWithValue("@Status", updatedMeeting.Status);
                cmd.Parameters.AddWithValue("@GoogleMapsLink", updatedMeeting.GoogleMapsLink);
                cmd.Parameters.AddWithValue("@LocationName", updatedMeeting.LocationName);
                MySqlDataReader reader = cmd.ExecuteReader();
                reader.Close();
                conn.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return meetings;
        }

        public List<MeetingModel> DeleteMeeting(int id)
        {
            List<MeetingModel> meetings = new List<MeetingModel>();
            try
            {
                using var conn = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "DELETE FROM os.Meetings WHERE Id = @Id";
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(command, conn);
                cmd.Parameters.AddWithValue("@Id", id);
                MySqlDataReader reader = cmd.ExecuteReader();
                reader.Close();
                conn.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return meetings;
        }
        /*
        * Stores a speaker removal request in the database
        */
        public bool StoreRemovalRequest(SpeakerRemovalRequestModel removalRequestIn)
        {
            bool succeeded = false;
            try
            {
                using var conn = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));

                string command = "INSERT INTO os.SpeakerRemovalRequests " +
                    "(FirstName, LastName, SpeakerFirstName, SpeakerLast, Description, SpeakerId, " +
                    "RelationToSpeaker, EmailAddress, PhoneNumber, RemovalReason, RequestDate, Status) " +
                    "VALUES (@FirstName, @LastName, @SpeakerFirstName, @SpeakerLast, @Description, @SpeakerId, " +
                    "@RelationToSpeaker, @EmailAddress, @PhoneNumber, @RemovalReason, @RequestDate, @Status)";
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(command, conn);
                cmd.Parameters.AddWithValue("@FirstName", removalRequestIn.FirstName);
                cmd.Parameters.AddWithValue("@LastName", removalRequestIn.LastName);
                cmd.Parameters.AddWithValue("@SpeakerFirstName", removalRequestIn.SpeakerFirstName);
                cmd.Parameters.AddWithValue("@SpeakerLast", removalRequestIn.SpeakerLast);
                cmd.Parameters.AddWithValue("@Description", removalRequestIn.Description);
                cmd.Parameters.AddWithValue("@SpeakerId", removalRequestIn.SpeakerId);
                cmd.Parameters.AddWithValue("@RelationToSpeaker", removalRequestIn.RelationToSpeaker);
                cmd.Parameters.AddWithValue("@EmailAddress", removalRequestIn.EmailAddress);
                cmd.Parameters.AddWithValue("@PhoneNumber", removalRequestIn.PhoneNumber);
                cmd.Parameters.AddWithValue("@RemovalReason", removalRequestIn.RemovalReason);
                cmd.Parameters.AddWithValue("@RequestDate", removalRequestIn.RequestDate);
                cmd.Parameters.AddWithValue("@Status", removalRequestIn.Status);

                MySqlDataReader reader = cmd.ExecuteReader();
                reader.Close();

                // Log the event
                var email = _httpContextAccessor.HttpContext?.User?.Identity.Name;
                string beforeUpdate = "No previous record";
                string afterUpdate = $"Removal request for speaker: {removalRequestIn.SpeakerFirstName} {removalRequestIn.SpeakerLast}";
                CreateLog(email, "StoreRemovalRequest() called.", beforeUpdate, afterUpdate);

                conn.Close();
                succeeded = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return succeeded;
        }
        /*
        * Gets a list of all speaker removal requests
        */
        public List<SpeakerRemovalRequestModel> GetRemovalRequests()
        {
            List<SpeakerRemovalRequestModel> removalRequests = new List<SpeakerRemovalRequestModel>();
            try
            {
                using var conn = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "SELECT * FROM os.SpeakerRemovalRequests";
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(command, conn);
                MySqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    SpeakerRemovalRequestModel request = new SpeakerRemovalRequestModel();
                    request.Id = reader.IsDBNull(0) ? null : reader.GetInt32(0);
                    request.FirstName = reader.IsDBNull(1) ? null : reader.GetString(1);
                    request.LastName = reader.IsDBNull(2) ? null : reader.GetString(2);
                    request.SpeakerFirstName = reader.IsDBNull(3) ? null : reader.GetString(3);
                    request.SpeakerLast = reader.IsDBNull(4) ? null : reader.GetString(4);
                    request.Description = reader.IsDBNull(5) ? null : reader.GetString(5);
                    request.SpeakerId = reader.IsDBNull(6) ? null : reader.GetString(6);
                    request.RelationToSpeaker = reader.IsDBNull(7) ? null : reader.GetString(7);
                    request.EmailAddress = reader.IsDBNull(8) ? null : reader.GetString(8);
                    request.PhoneNumber = reader.IsDBNull(9) ? null : reader.GetString(9);
                    request.RemovalReason = reader.IsDBNull(10) ? null : reader.GetString(10);
                    request.RequestDate = reader.IsDBNull(11) ? DateTime.MinValue : reader.GetDateTime(11);
                    request.Status = reader.IsDBNull(12) ? null : reader.GetString(12);
                    removalRequests.Add(request);
                }
                reader.Close();
                conn.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return removalRequests;
        }

        /*
        * Gets a single speaker removal request by ID
        */
        public SpeakerRemovalRequestModel GetRemovalRequestBySpeakerId(string? id)
        {
            SpeakerRemovalRequestModel request = new SpeakerRemovalRequestModel();
            try
            {
                using var conn = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "SELECT * FROM os.SpeakerRemovalRequests WHERE SpeakerId = @SpeakerId";
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(command, conn);
                cmd.Parameters.AddWithValue("@SpeakerId", Int32.Parse(id));
                MySqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    request.Id = reader.IsDBNull(0) ? null : reader.GetInt32(0);
                    request.FirstName = reader.IsDBNull(1) ? null : reader.GetString(1);
                    request.LastName = reader.IsDBNull(2) ? null : reader.GetString(2);
                    request.SpeakerFirstName = reader.IsDBNull(3) ? null : reader.GetString(3);
                    request.SpeakerLast = reader.IsDBNull(4) ? null : reader.GetString(4);
                    request.Description = reader.IsDBNull(5) ? null : reader.GetString(5);
                    request.SpeakerId = reader.IsDBNull(6) ? null : reader.GetString(6);
                    request.RelationToSpeaker = reader.IsDBNull(7) ? null : reader.GetString(7);
                    request.EmailAddress = reader.IsDBNull(8) ? null : reader.GetString(8);
                    request.PhoneNumber = reader.IsDBNull(9) ? null : reader.GetString(9);
                    request.RemovalReason = reader.IsDBNull(10) ? null : reader.GetString(10);
                    request.RequestDate = reader.IsDBNull(11) ? DateTime.MinValue : reader.GetDateTime(11);
                    request.Status = reader.IsDBNull(12) ? null : reader.GetString(12);
                }
                reader.Close();
                conn.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return request;
        }
        /*
* Gets a single speaker removal request by ID
*/
        public SpeakerRemovalRequestModel GetRemovalRequestByTableId(int? id)
        {
            SpeakerRemovalRequestModel request = new SpeakerRemovalRequestModel();
            try
            {
                using var conn = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "SELECT * FROM os.SpeakerRemovalRequests WHERE Id = @Id";
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(command, conn);
                cmd.Parameters.AddWithValue("@Id", id);
                MySqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    request.Id = reader.IsDBNull(0) ? null : reader.GetInt32(0);
                    request.FirstName = reader.IsDBNull(1) ? null : reader.GetString(1);
                    request.LastName = reader.IsDBNull(2) ? null : reader.GetString(2);
                    request.SpeakerFirstName = reader.IsDBNull(3) ? null : reader.GetString(3);
                    request.SpeakerLast = reader.IsDBNull(4) ? null : reader.GetString(4);
                    request.Description = reader.IsDBNull(5) ? null : reader.GetString(5);
                    request.SpeakerId = reader.IsDBNull(6) ? null : reader.GetString(6);
                    request.RelationToSpeaker = reader.IsDBNull(7) ? null : reader.GetString(7);
                    request.EmailAddress = reader.IsDBNull(8) ? null : reader.GetString(8);
                    request.PhoneNumber = reader.IsDBNull(9) ? null : reader.GetString(9);
                    request.RemovalReason = reader.IsDBNull(10) ? null : reader.GetString(10);
                    request.RequestDate = reader.IsDBNull(11) ? DateTime.MinValue : reader.GetDateTime(11);
                    request.Status = reader.IsDBNull(12) ? null : reader.GetString(12);
                }
                reader.Close();
                conn.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return request;
        }

        /*
        * Updates a speaker removal request
        */
        public bool UpdateRemovalRequest(SpeakerRemovalRequestModel requestIn)
        {
            bool succeeded = false;
            try
            {
                using var conn = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "UPDATE os.SpeakerRemovalRequests SET FirstName = @FirstName, LastName = @LastName, " +
                    "SpeakerFirstName = @SpeakerFirstName, SpeakerLast = @SpeakerLast, Description = @Description, " +
                    "SpeakerId = @SpeakerId, RelationToSpeaker = @RelationToSpeaker, EmailAddress = @EmailAddress, " +
                    "PhoneNumber = @PhoneNumber, RemovalReason = @RemovalReason, Status = @Status " +
                    "WHERE Id = @Id";

                conn.Open();
                MySqlCommand cmd = new MySqlCommand(command, conn);
                cmd.Parameters.AddWithValue("@Id", requestIn.Id);
                cmd.Parameters.AddWithValue("@FirstName", requestIn.FirstName);
                cmd.Parameters.AddWithValue("@LastName", requestIn.LastName);
                cmd.Parameters.AddWithValue("@SpeakerFirstName", requestIn.SpeakerFirstName);
                cmd.Parameters.AddWithValue("@SpeakerLast", requestIn.SpeakerLast);
                cmd.Parameters.AddWithValue("@Description", requestIn.Description);
                cmd.Parameters.AddWithValue("@SpeakerId", requestIn.SpeakerId);
                cmd.Parameters.AddWithValue("@RelationToSpeaker", requestIn.RelationToSpeaker);
                cmd.Parameters.AddWithValue("@EmailAddress", requestIn.EmailAddress);
                cmd.Parameters.AddWithValue("@PhoneNumber", requestIn.PhoneNumber);
                cmd.Parameters.AddWithValue("@RemovalReason", requestIn.RemovalReason);
                cmd.Parameters.AddWithValue("@Status", requestIn.Status);

                int rowsAffected = cmd.ExecuteNonQuery();
                conn.Close();

                succeeded = rowsAffected > 0;
                if (succeeded)
                {
                    // Log the event
                    var email = _httpContextAccessor.HttpContext?.User?.Identity.Name;
                    SpeakerRemovalRequestModel previousRequest = GetRemovalRequestBySpeakerId(requestIn.Id.ToString());
                    string beforeUpdate = $"Request ID: {previousRequest.Id}, Status: {previousRequest.Status}";
                    string afterUpdate = $"Request ID: {requestIn.Id}, Status: {requestIn.Status}";
                    CreateLog(email, "UpdateRemovalRequest() called.", beforeUpdate, afterUpdate);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return succeeded;
        }

        /*
        * Updates just the status of a speaker removal request
        */
        public bool UpdateRemovalRequestStatus(int? id, string status)
        {
            bool succeeded = false;
            try
            {


                using var conn = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "UPDATE os.SpeakerRemovalRequests SET Status = @Status WHERE Id = @Id";

                conn.Open();
                MySqlCommand cmd = new MySqlCommand(command, conn);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@Status", status);

                int rowsAffected = cmd.ExecuteNonQuery();
                conn.Close();

                succeeded = rowsAffected > 0;

                if (succeeded)
                {
                    // Log the event
                    var email = _httpContextAccessor.HttpContext?.User?.Identity.Name;
                    SpeakerRemovalRequestModel previousRequest = GetRemovalRequestByTableId(id);
                    string beforeUpdate = $"Request ID: {previousRequest.Id}, Status: {previousRequest.Status}";
                    string afterUpdate = $"Request ID: {id}, Status: {status}";
                    CreateLog(email, "UpdateRemovalRequestStatus() called.", beforeUpdate, afterUpdate);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return succeeded;
        }

        /*
        * Deletes a speaker removal request
        */
        public bool DeleteRemovalRequest(int id)
        {
            bool succeeded = false;
            try
            {
                using var conn = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "DELETE FROM os.SpeakerRemovalRequests WHERE Id = @Id";

                conn.Open();
                MySqlCommand cmd = new MySqlCommand(command, conn);
                cmd.Parameters.AddWithValue("@Id", id);

                int rowsAffected = cmd.ExecuteNonQuery();
                conn.Close();

                succeeded = rowsAffected > 0;

                // Log the event
                if (succeeded)
                {
                    var email = _httpContextAccessor.HttpContext?.User?.Identity.Name;
                    SpeakerRemovalRequestModel previousRequest = GetRemovalRequestByTableId(id);
                    string beforeDelete = $"Request ID: {id}, Speaker: {previousRequest.SpeakerFirstName} {previousRequest.SpeakerLast}, Status: {previousRequest.Status}";
                    CreateLog(email, "DeleteRemovalRequest() called.", beforeDelete, "Record deleted");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return succeeded;
        }

    }

}
