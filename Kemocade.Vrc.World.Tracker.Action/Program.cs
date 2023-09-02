using CommandLine;
using Kemocade.Vrc.World.Tracker.Action;
using OtpNet;
using System.Text.Json;
using System.Text.Json.Serialization;
using VRChat.API.Api;
using VRChat.API.Client;
using VRChat.API.Model;
using static Kemocade.Vrc.World.Tracker.Action.TrackedData;
using static System.Console;
using static System.IO.File;
using static System.Text.Json.JsonSerializer;

// Configure Cancellation
using CancellationTokenSource tokenSource = new();
CancelKeyPress += delegate { tokenSource.Cancel(); };

// Configure Inputs
ParserResult<ActionInputs> parser = Parser.Default.ParseArguments<ActionInputs>(args);
if (parser.Errors.ToArray() is { Length: > 0 } errors)
{
    foreach (CommandLine.Error error in errors)
    { WriteLine($"{nameof(error)}: {error.Tag}"); }
    Environment.Exit(2);
    return;
}
ActionInputs inputs = parser.Value;

string[] worldIds = !string.IsNullOrEmpty(inputs.Worlds) ?
     inputs.Worlds.Split(',') : Array.Empty<string>();

// Find Local Files
DirectoryInfo workspace = new(inputs.Workspace);
DirectoryInfo output = workspace.CreateSubdirectory(inputs.Output);


// World Data
Dictionary<string, World> vrcWorldIdsToWorldModels = new();
// Handle API exceptions
try
{
    // Authentication credentials
    Configuration config = new()
    {
        Username = inputs.Username,
        Password = inputs.Password,
        UserAgent = "kemocade/0.0.1 admin%40kemocade.com"
    };

    // Create instances of APIs we'll need
    AuthenticationApi authApi = new(config);
    WorldsApi worldsApi = new(config);

    // Log in
    WriteLine("Logging in...");
    CurrentUser currentUser = authApi.GetCurrentUser();
    await WaitSeconds(1);

    // Check if 2FA is needed
    if (currentUser == null)
    {
        WriteLine("2FA needed...");

        // Generate a 2FA code with the stored secret
        string key = inputs.Key.Replace(" ", string.Empty);
        Totp totp = new(Base32Encoding.ToBytes(key));

        // Make sure there's enough time left on the token
        int remainingSeconds = totp.RemainingSeconds();
        if (remainingSeconds < 5)
        {
            WriteLine("Waiting for new token...");
            await Task.Delay(TimeSpan.FromSeconds(remainingSeconds + 1));
        }

        // Verify 2FA
        WriteLine("Using 2FA code...");
        authApi.Verify2FA(new(totp.ComputeTotp()));
        currentUser = authApi.GetCurrentUser();
        await WaitSeconds(1);

        if (currentUser == null)
        {
            WriteLine("Failed to validate 2FA!");
            Environment.Exit(2);
            return;
        }
    }
    WriteLine($"Logged in as {currentUser.DisplayName}");

    // Get all info from all tracked worlds
    foreach (string worldId in worldIds)
    {
        // Get World
        World world = worldsApi.GetWorld(worldId);
        WriteLine($"Got World: {worldId}");
        vrcWorldIdsToWorldModels.Add(worldId, world);
        await WaitSeconds(1);
    }
}
catch (ApiException e)
{
    WriteLine("Exception when calling API: {0}", e.Message);
    WriteLine("Status Code: {0}", e.ErrorCode);
    WriteLine(e.ToString());
    Environment.Exit(2);
    return;
}

TrackedData data = new()
{
    FileTimeUtc = DateTime.Now.ToFileTimeUtc(),
    VrcWorldsById = vrcWorldIdsToWorldModels.
        ToDictionary
        (
            kvp => kvp.Key,
            kvp => new TrackedVrcWorld
            {
                Name = kvp.Value.Name,
                Visits = kvp.Value.Visits,
                Favorites = kvp.Value.Favorites,
                Occupants = kvp.Value.Occupants
            }
        )
};

// Build Json from data
JsonSerializerOptions options = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
};
string dataJsonString = Serialize(data, options);
WriteLine(dataJsonString);

// Write Json to file
FileInfo dataJsonFile = new(Path.Join(output.FullName, "data.json"));
WriteAllText(dataJsonFile.FullName, dataJsonString);

WriteLine("Done!");
Environment.Exit(0);

static async Task WaitSeconds(int seconds) =>
    await Task.Delay(TimeSpan.FromSeconds(seconds));