/*Program.cs*/
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using os.Areas.Identity.Data;
using os.Areas.Identity.Services;
using os.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using System.Security.Authentication;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
//using WebPWrecover.Services;

var builder = WebApplication.CreateBuilder(args);
Environment.SetEnvironmentVariable("ASPNETCORE_FORWARDEDHEADERS_ENABLED", "true");
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>();

// Required for Apache reverse proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.All;
    options.RequireHeaderSymmetry = false;
    options.ForwardLimit = 2;
    options.KnownProxies.Add(IPAddress.Parse("127.0.0.1")); // Reverse proxy, Kestrel defaults to port 5000 which is also set in appsettings.json
    options.KnownProxies.Add(IPAddress.Parse("162.205.232.101")); // Server IP public
});

// Configure listen protocols and assert SSL/TLS requirement
builder.WebHost.ConfigureKestrel((context, serverOptions) =>
{
    serverOptions.ConfigureHttpsDefaults(listenOptions =>
    {
        listenOptions.SslProtocols = SslProtocols.Tls13;
        listenOptions.ClientCertificateMode = ClientCertificateMode.AllowCertificate; // Requires certificate from client
    });
});
var environ = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
var connectionString = "";
var emailPass = "";
var serverVersion = new MySqlServerVersion(new Version(8, 8, 39));
if (builder.Configuration["ASPNETCORE_ENVIRONMENT"] == "Production")
{
    connectionString = Environment.GetEnvironmentVariable("OS_Local");
    emailPass = Environment.GetEnvironmentVariable("OS_Email_Pass");
    if (connectionString == "")
    {
        throw new Exception("ProgramCS: The connection string was null!");
    }

    // DB context that auto retries but does not allow migrations with MySQL
    builder.Services.AddDbContext<ApplicationDbContext>(
    dbContextOptions => dbContextOptions
        .UseMySql(connectionString, serverVersion, options => options.EnableRetryOnFailure())
        .LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information)
        .EnableSensitiveDataLogging()
        .EnableDetailedErrors()
    );
}
else
{
    // Pulls connection string from development local version of secrets.json
    connectionString = builder.Configuration.GetConnectionString("OS_Remote");
    emailPass = builder.Configuration["OS_Email_Pass"];

    // DB context which allows migrations but does not auto retry with MySQL
    builder.Services.AddDbContext<ApplicationDbContext>(
    dbContextOptions => dbContextOptions
        .UseMySql(connectionString, serverVersion, options => options.SchemaBehavior(Pomelo.EntityFrameworkCore.MySql.Infrastructure.MySqlSchemaBehavior.Ignore))
        .LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information)
        .EnableSensitiveDataLogging()
        .EnableDetailedErrors()
    );
}
Environment.SetEnvironmentVariable("DbConnectionString", connectionString); // This is used in services to access the string
Environment.SetEnvironmentVariable("OS_Email_Pass", emailPass); // This is used in services to access the string

builder.Services.AddDefaultIdentity<AppUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddDefaultTokenProviders()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.Configure<IdentityOptions>(options =>
{
    // Default Lockout settings.
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 3;
    options.Lockout.AllowedForNewUsers = true;
    options.SignIn.RequireConfirmedAccount = true;
});
builder.Services.AddAuthorization();

// Addition of encryption methods for deployment on Linux
builder.Services.AddDataProtection().UseCryptographicAlgorithms(
    new AuthenticatedEncryptorConfiguration
    {
        EncryptionAlgorithm = EncryptionAlgorithm.AES_256_CBC,
        ValidationAlgorithm = ValidationAlgorithm.HMACSHA256
    });

builder.Services.AddSingleton<DbConnectionService>(); // Cannot be a singleton because it will miss the conn str
builder.Services.AddTransient<IEmailSender, EmailService>();
builder.Services.AddAuthorization();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor(); // This is required to inject the UserService into cshtml files
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

builder.Services.AddResponseCompression(options =>
    options.MimeTypes = ResponseCompressionDefaults
    .MimeTypes.Concat(new[] { "application/octet-stream" })
);
builder.Services.AddMvc();

/*builder.Services.AddSignalR(options =>
{
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(2);
});
*/


/*builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "OurSolutionCookie"; // Set a custom cookie name
        options.Cookie.HttpOnly = true; // Prevent client-side JavaScript from accessing the cookie
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Use secure cookies in production
        options.Cookie.SameSite = SameSiteMode.Lax; // Protect against CSRF attacks
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30); // Set cookie expiration time
        options.SlidingExpiration = true; // Renew the cookie expiration time on each request
        options.LoginPath = "/Account/Login"; // Redirect to this path if the user is not authenticated
        options.LogoutPath = "/Account/Logout"; // Redirect to this path after logout
        options.AccessDeniedPath = "/Account/AccessDenied"; // Redirect to this path if access is denied
    });
*/


var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders(); // Ensure this is called before other middleware
    app.UseDeveloperExceptionPage(); // This can be enabled to enable http error reporting. Disable for production!
    // app.UseHttpsRedirection(); // <-- Do not use! This is retained as a reminder. Appache2 is responsible for https.
    // app.UseHsts(); <-- Do not use! This is retained as a reminder.
}
else
{
    // app.UseDeveloperExceptionPage(); // This can be enabled to enable http error reporting. Disable for production!
    // app.UseHttpsRedirection(); // <-- Do not use! This is retained as a reminder. Appache2 is responsible for https.
    // app.UseHsts(); <-- Do not use! This is retained as a reminder.
}

// app.UseResponseCompression();
app.UseCookiePolicy();
app.UseStaticFiles();
app.UseRouting();

app.MapControllers();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "Admin",
    pattern: "{controller=AdminController}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "Member",
    pattern: "{controller=MemberController}/{action=Index}/{id?}");

app.MapRazorPages();
app.Run();