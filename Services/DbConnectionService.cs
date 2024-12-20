#nullable disable
using os.Models;
using os.Areas.Identity.Data;
using MySql.Data.MySqlClient;
using System.Security.Claims;

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
        * Gets a list of speakers
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
         * Gets a single speaker details by id
         */
        public SpeakerModel GetSpeakerById(int id)
        {
            List<SpeakerModel> speakers = GetSpeakersList();
            SpeakerModel foundSpeaker = new SpeakerModel();
            foreach(SpeakerModel speaker in speakers)
            {
                if(speaker.SpeakerId == id) { foundSpeaker = speaker; break; }
            }
            return foundSpeaker;

        }
        /*
         * Updates a speaker's details
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
            } else {
                beforeUpdate = SpeakerDetailsString(previouseSpeakerModel);
            }
            string afterUpdate = SpeakerDetailsString(speakerIn);
            CreateLog(email, "UpdateSpeakerDetails() called.", beforeUpdate, afterUpdate);

            bool Succeeded = false;
            try
            {
                using var conn1 = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "UPDATE os.Speakers SET FirstName = @FirstName, LastName = @LastName," +
                    " Description = @Description ,DateRecorded = @DateRecorded, SpeakerStatus = @SpeakerStatus " +
                    " WHERE SpeakerId LIKE @SpeakerId";
                conn1.Open();
                MySqlCommand cmd1 = new MySqlCommand(command, conn1);
                cmd1.Parameters.AddWithValue("@SpeakerId", speakerIn.SpeakerId);
                cmd1.Parameters.AddWithValue("@FirstName", speakerIn.FirstName);
                cmd1.Parameters.AddWithValue("@LastName", speakerIn.LastName);
                cmd1.Parameters.AddWithValue("@Description", speakerIn.Description);
                cmd1.Parameters.AddWithValue("@DateRecorded", speakerIn.DateRecorded);
                cmd1.Parameters.AddWithValue("@SpeakerStatus", speakerIn.SpeakerStatus);

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
        public bool AddSpeaker(SpeakerModel speakerIn)
        {
            // Log the event; must take place before the update to capture before and after state.
            var email = _httpContextAccessor.HttpContext?.User?.Identity.Name;
            AppUser userDetails = GetUserDetailsByEmail(email);
            string beforeUpdate = "No previous record";
            string afterUpdate = SpeakerDetailsString(speakerIn);
            CreateLog(email, "AddSpeaker() called.", beforeUpdate, afterUpdate);

            bool Succeeded = false;
            
            try
            {
                using var conn1 = new MySqlConnection(Environment.GetEnvironmentVariable("DbConnectionString"));
                string command = "INSERT INTO os.Speakers (FirstName, LastName, Description, NumUpVotes, DateRecorded, UploadDate, UploadedBy, SpeakerStatus, DisplayFileName, SecretFileName, UploadedById) " +
                    "VALUES (@FirstName, @LastName, @Description, @NumUpVotes, @DateRecorded, @UploadDate, @UploadedBy, @SpeakerStatus, @DisplayFileName, @SecretFileName, @UploadedById)";
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
            speakerString += speaker.SpeakerStatus;
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
                    " UserRole = @UserRole, ActiveStatus = @ActiveStatus, ProfileImage = @ProfileImage " +
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
                cmd1.Parameters.AddWithValue("@UserRole", userIn.UserRole);
                cmd1.Parameters.AddWithValue("@ActiveStatus", userIn.ActiveStatus);
                cmd1.Parameters.AddWithValue("@ProfileImage", userIn.ProfileImage);
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
            if (UserRolePresent(uidIn)){
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
            } else
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
                cmd1.Parameters.AddWithValue("@Email", emailIn);
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
    }
}
