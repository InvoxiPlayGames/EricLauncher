using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EricLauncher
{
    internal class Program
    {
        static string BaseAppDataFolder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData) + "/EricLauncher";
        static string BaseOVTFolder = BaseAppDataFolder + "/OVT";
        static string RedirectURL = "https://www.epicgames.com/id/api/redirect?clientId=" + EpicLogin.LAUNCHER_CLIENT + "&responseType=code";

        static void PrintUsage()
        {
            Console.WriteLine("Usage: EricLauncher.exe [executable path] (options)");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --accountId [id]  - use a specific Epic Games account ID to sign in.");
            Console.WriteLine("  --noManifest      - don't check the local Epic Games Launcher install folder for the manifest.");
            Console.WriteLine("  --stayOpen        - keeps EricLauncher open in the background until the game is closed.");
            Console.WriteLine("  --dryRun          - goes through the Epic Games login flow, but does not launch the game.");
            Console.WriteLine("  --offline         - skips the Epic Games login flow, to launch the game in offline mode.");
            Console.WriteLine("  --manifest [file] - specify a specific manifest file to use.");
            Console.WriteLine();
        }

        static async Task Main(string[] args)
        {
            if (args.Length == 0) {
                PrintUsage();
                return;
            }
            bool needs_code_login = false;
            EpicLogin login = new EpicLogin();
            EpicAccount? account = null;

            // parse the cli arguments
            string? account_id = null;
            string? manifest_path = null;
            bool set_default = false;
            bool no_manifest = false;
            bool stay_open = false;
            bool dry_run = false;
            bool offline = false;
            bool skip_fortnite_update = false;
            if (args.Length > 1)
            {
                for (int i = 1; i < args.Length; i++)
                {
                    if (args[i] == "--accountId")
                        account_id = args[++i];
                    if (args[i] == "--manifest")
                        manifest_path = args[++i];
                    if (args[i] == "--setDefault")
                        set_default = true;
                    if (args[i] == "--noManifest")
                        no_manifest = true;
                    if (args[i] == "--stayOpen")
                        stay_open = true;
                    if (args[i] == "--dryRun")
                        dry_run = true;
                    if (args[i] == "--offline")
                        offline = true;
                    if (args[i] == "--noCheckFn")
                        skip_fortnite_update = true;
                }
            }

            // always run as a dry run if we're on Linux or FreeBSD
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
                dry_run = true;

            // if we're launching fortnite, do an update check
            if (!skip_fortnite_update && !offline &&
                Path.GetFileName(args[0]).ToLower() == "fortnitelauncher.exe")
            {
                Console.WriteLine("Checking for Fortnite updates...");
                // traverse back to the cloud content json
                try
                {
                    string cloudcontent_path = Path.GetDirectoryName(args[0]) + @"\..\..\..\Cloud\cloudcontent.json";
                    string jsonstring = File.ReadAllText(cloudcontent_path);
                    FortniteCloudContent? cloudcontent = JsonSerializer.Deserialize<FortniteCloudContent>(jsonstring);
                    Console.WriteLine($"Current version: {cloudcontent!.BuildVersion!} ({cloudcontent!.Platform!})");
                    bool is_up_to_date = await FortniteUpdateCheck.IsUpToDate(cloudcontent!.BuildVersion!, cloudcontent!.Platform!);
                    if (!is_up_to_date)
                    {
                        Console.WriteLine("Fortnite is not the latest version!");
                        Console.WriteLine("Please open the Epic Games Launcher to start updating the game.");
                        Thread.Sleep(2500);
                        return;
                    }
                } catch
                {
                    Console.WriteLine("There was an error checking for Fortnite updates.");
                    Console.WriteLine("The game might not let you online. Continuing anyway...");
                }
            }

            // check if we have an account saved already
            StoredAccountInfo? storedInfo;
            bool is_default = account_id == null || set_default;
            if (account_id != null) {
                Console.WriteLine($"Using account {account_id}");
                storedInfo = GetAccountInfo(account_id);
                // check if the account id specified is the current default account
                if (!set_default)
                {
                    StoredAccountInfo? default_account = GetAccountInfo();
                    if (default_account?.AccountId == storedInfo?.AccountId)
                        is_default = true;
                }
            } else
            {
                Console.WriteLine("Using default account");
                storedInfo = GetAccountInfo();
            }
            if (storedInfo == null)
            {
                needs_code_login = true;
            } else
            {
                if (storedInfo.DisplayName != null)
                    Console.Write($"Logging in as {storedInfo.DisplayName} ({storedInfo.AccountId})...");
                else
                    Console.Write($"Logging in as {storedInfo.AccountId}...");

                account = new(storedInfo);
                if (!offline)
                {
                    // check the expiry date, if the access token has expired then just refresh straight away, otherwise verify our access token
                    bool verified = account.AccessExpiry >= DateTime.UtcNow ? await account.VerifyToken() : false;
                    if (!verified)
                    {
                        Console.Write("refreshing...");
                        account = null;
                        try
                        {
                            account = await login.LoginWithRefreshToken(storedInfo.RefreshToken!);
                            Console.WriteLine("success!");
                        }
                        catch { }
                        if (account == null)
                        {
                            Console.WriteLine("failed.");
                            Console.WriteLine("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
                            Console.WriteLine("@    WARNING: EPIC GAMES REFRESH TOKEN HAS CHANGED!     @");
                            Console.WriteLine("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
                            Console.WriteLine("IT IS POSSIBLE THAT SWEENEY IS DOING SOMETHING EPIC!");
                            needs_code_login = true;
                        }
                    } else
                    {
                        Console.WriteLine("success!");
                    }
                } else
                {
                    Console.WriteLine("offline.");
                }
            }

            // we don't have an account so refresh credentials with an authorization code
            if (needs_code_login)
            {
                Console.WriteLine($"Open this URL in a web browser (signed into your Epic Games account): {RedirectURL}");
                Console.Write("Paste the 'authorizationCode' value: ");
                string? auth_code = Console.ReadLine();
                if (auth_code == null || auth_code == "")
                {
                    Console.WriteLine("Invalid code!");
                    return;
                }

                try
                {
                    account = await login.LoginWithAuthorizationCode(auth_code);
                }
                catch { }
                if (account == null)
                {
                    Console.WriteLine("Failed to log in!");
                    return;
                }
            }

            // if the user provided an account id at the command line but this isn't the same account, quit out
            if (account_id != null && account!.AccountId != account_id)
            {
                Console.WriteLine($"Logged in, but the account ID ({account.AccountId}) isn't the same as the one provided at the command line ({account_id}).");
                // save the account info later just to save time
                StoreAccountInfo(account!.MakeStoredAccountInfo(), false);
                return;
            }

            // we've logged in successfully!
            if (account!.DisplayName != null)
                Console.WriteLine($"Logged in as {account.DisplayName} ({account.AccountId})!");
            else
                Console.WriteLine($"Logged in as {account.AccountId}!");

            // save our refresh token for later usage
            if (!Directory.Exists(BaseAppDataFolder))
                Directory.CreateDirectory(BaseAppDataFolder);
            StoreAccountInfo(account!.MakeStoredAccountInfo(), is_default);

            // fetch the game's manifest from the installed epic games launcher
            EGLManifest? manifest = null;
            if (!no_manifest && manifest_path == null)
            {
                manifest = GetEGLManifest(Path.GetFileName(args[0]));
            } else if (!no_manifest && manifest_path != null)
            {
                string jsonstring = File.ReadAllText(manifest_path);
                manifest = JsonSerializer.Deserialize<EGLManifest>(jsonstring);
            }
            if (manifest == null)
            {
                Console.WriteLine("Manifest wasn't loaded! The game might not work properly.");
                if (!no_manifest)
                    Console.WriteLine("(Try launching the game via the Epic Games Launcher at least once.)");
            }

            // launch the game
            string exchange = "";
            if (!offline)
                exchange = await account.GetExchangeCode();
            Console.WriteLine("Launching game...");
            Process game = await LaunchGame(args[0], exchange, account, manifest, dry_run, offline);
            if (stay_open && !dry_run)
            {
                game.WaitForExit();
                Console.WriteLine($"Game exited with code {game.ExitCode}");
            }
        }

        static async Task<Process> LaunchGame(string filename, string? exchange, EpicAccount? account, EGLManifest? manifest, bool dry_run, bool skip_ovt)
        {
            Process p = new Process();
            p.StartInfo.FileName = filename;
            p.StartInfo.WorkingDirectory = Path.GetDirectoryName(filename);
            p.StartInfo.ArgumentList.Add($"-epicenv=Prod");
            p.StartInfo.ArgumentList.Add($"-epiclocale=en-US");
            p.StartInfo.ArgumentList.Add($"-EpicPortal");
            p.StartInfo.ArgumentList.Add($"-AUTH_LOGIN=unused");

            if (exchange != null)
            {
                p.StartInfo.ArgumentList.Add($"-AUTH_TYPE=exchangecode");
                p.StartInfo.ArgumentList.Add($"-AUTH_PASSWORD={exchange}");
            }

            if (account != null)
            {
                p.StartInfo.ArgumentList.Add($"-epicuserid={account.AccountId}");
                if (account.DisplayName != null)
                    p.StartInfo.ArgumentList.Add($"-epicusername=\"{account.DisplayName}\"");
            }

            if (manifest != null)
            {
                p.StartInfo.ArgumentList.Add($"-epicsandboxid={manifest.MainGameCatalogNamespace}");
                p.StartInfo.ArgumentList.Add($"-epicapp={manifest.MainGameAppName}");
                if (manifest.LaunchCommand != null && manifest.LaunchCommand.Length > 0)
                {
                    string[] split_args = manifest.LaunchCommand.Split(' ');
                    foreach (string arg in split_args)
                        p.StartInfo.ArgumentList.Add(arg);
                }
            }

            if (account != null && manifest != null && !skip_ovt &&
                manifest.OwnershipToken == "true")
            {
                string? epicovt_path = await GetOwnershipTokenPath(account, manifest);
                if (epicovt_path != null)
                    p.StartInfo.ArgumentList.Add($"-epicovt=\"{epicovt_path}\"");
            }

            if (!dry_run)
                p.Start();
            else
            {
                string full_command = filename + " ";
                foreach (string arg in p.StartInfo.ArgumentList)
                {
                    full_command += arg + " ";
                }
                Console.WriteLine("Launch: " + full_command);
            }

            return p;
        }

        static async Task<string?> GetOwnershipTokenPath(EpicAccount account, EGLManifest manifest)
        {
            Directory.CreateDirectory(BaseOVTFolder);
            string ovt_path = $"{BaseOVTFolder}/{manifest.MainGameAppName!}.ovt";
            EpicEcom ecom = new(account);
            string? epicovt = await ecom.GetOwnershipToken(manifest.CatalogNamespace!, manifest.CatalogItemId!);
            if (epicovt != null)
            {
                File.WriteAllText(ovt_path, epicovt);
                return ovt_path;
            } else return null;
        }

        static EGLManifest? GetEGLManifest(string executable_name)
        {
            IEnumerable<string> files;
            string manifestfolder = "/Epic/EpicGamesLauncher/Data/Manifests";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) // .NET 7 doesn't make SpecialFolder.LocalAppliactionData go to the correct folder :)
                manifestfolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "Application Support") + manifestfolder;
            else
                manifestfolder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData) + manifestfolder;

            try
            {
                files = Directory.EnumerateFiles(manifestfolder);
            } catch {
                return null;
            }
            foreach (string file in files)
            {
                try
                {
                    string jsonstring = File.ReadAllText(file);
                    EGLManifest? manifest = JsonSerializer.Deserialize<EGLManifest>(jsonstring);
                    if (manifest != null && manifest.LaunchExecutable != null &&
                        Path.GetFileName(manifest.LaunchExecutable).ToLower() == executable_name.ToLower())
                    {
                        return manifest;
                    }
                }
                catch { }
            }
            return null;
        }

        static void StoreAccountInfo(StoredAccountInfo info, bool is_default = true)
        {
            string jsonstring = JsonSerializer.Serialize(info);
            File.WriteAllText($"{BaseAppDataFolder}/{info.AccountId!}.json", jsonstring);
            if (is_default)
                File.WriteAllText($"{BaseAppDataFolder}/default.json", jsonstring);
        }

        static StoredAccountInfo? GetAccountInfo(string? account_id = null)
        {
            string path = $"{BaseAppDataFolder}/default.json";
            if (account_id != null && File.Exists($"{BaseAppDataFolder}/{account_id}.json"))
                path = $"{BaseAppDataFolder}/{account_id}.json";
            if (!File.Exists(path))
                return null;
            try
            {
                string jsonstring = File.ReadAllText(path);
                StoredAccountInfo? info = JsonSerializer.Deserialize<StoredAccountInfo>(jsonstring);
                if (account_id == null || (info != null && account_id == info.AccountId))
                    return info;
            } catch { }
            return null;
        }
    }
}
