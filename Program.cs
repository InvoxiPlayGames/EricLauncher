using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EricLauncher
{
    internal class Program
    {
        static string BaseAppDataFolder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData) + $"/EricLauncher";
        static string RedirectURL = "https://www.epicgames.com/id/api/redirect?clientId=" + EASLogin.LAUNCHER_CLIENT + "&responseType=code";

        static void PrintUsage()
        {
            Console.WriteLine("Usage: EricLauncher.exe [executable path] (options)");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("    --accountId [accountId] - use a specific Epic Games account to sign in.");
            Console.WriteLine("                              omitting this option will use the default account");
            Console.WriteLine("    --noManifest - don't check the local Epic Games Launcher install folder for the manifest.");
            Console.WriteLine("                   this WILL break certain games from launching, e.g. Fortnite");
            Console.WriteLine("    --stayOpen - keeps EricLauncher open in the background until the game is closed");
            Console.WriteLine("                 useful for launching through other launchers, e.g. Steam");
        }

        static async Task Main(string[] args)
        {
            if (args.Length == 0) {
                PrintUsage();
                return;
            }
            bool needs_code_login = false;
            EASLogin login = new EASLogin();
            EASAccount? account = null;

            // parse the cli arguments
            string? account_id = null;
            bool no_manifest = false;
            bool stay_open = false;
            if (args.Length > 1)
            {
                for (int i = 1; i < args.Length; i++)
                {
                    if (args[i] == "--accountId")
                        account_id = args[++i];
                    if (args[i] == "--noManifest")
                        no_manifest = true;
                    if (args[i] == "--stayOpen")
                        stay_open = true;
                }
            }

            // if we're launching fortnite, do an update check
            if (Path.GetFileName(args[0]).ToLower() == "fortnitelauncher.exe")
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
            if (account_id != null) {
                Console.WriteLine($"Using account {account_id}");
                storedInfo = GetAccountInfo(account_id);
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
                    Console.WriteLine($"Logging in as {storedInfo.DisplayName} ({storedInfo.AccountId}) with refresh token...");
                else
                    Console.WriteLine($"Logging in as {storedInfo.AccountId} with refresh token...");

                try
                {
                    account = await login.LoginWithRefreshToken(storedInfo.RefreshToken!);
                } catch { }
                if (account == null)
                {
                    Console.WriteLine("Refresh token expired or invalid!");
                    needs_code_login = true;
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
            StoreAccountInfo(account!.MakeStoredAccountInfo(), account_id == null);

            // fetch the game's manifest from the installed epic games launcher
            EGLManifest? manifest = null;
            if (!no_manifest)
            {
                manifest = GetManifest(Path.GetFileName(args[0]));
            }
            if (manifest == null)
            {
                Console.WriteLine("Manifest wasn't loaded! The game might not work properly.");
                if (!no_manifest)
                    Console.WriteLine("(Try launching the game via the Epic Games Launcher at least once.)");
            }

            // launch the game
            string exchange = await account.GetExchangeCode();
            Console.WriteLine("Launching game...");
            Process game = LaunchGame(args[0], exchange, account, manifest);
            if (stay_open)
            {
                game.WaitForExit();
                Console.WriteLine($"Game exited with code {game.ExitCode}");
            }
        }

        static Process LaunchGame(string filename, string? exchange, EASAccount? account, EGLManifest? manifest)
        {
            Process p = new Process();
            p.StartInfo.FileName = filename;
            p.StartInfo.WorkingDirectory = Path.GetDirectoryName(filename);
            p.StartInfo.ArgumentList.Add($"-AUTH_LOGIN=unused");
            p.StartInfo.ArgumentList.Add($"-AUTH_TYPE=exchangecode");
            if (exchange != null)
                p.StartInfo.ArgumentList.Add($"-AUTH_PASSWORD={exchange}");
            p.StartInfo.ArgumentList.Add($"-epicenv=Prod");
            p.StartInfo.ArgumentList.Add($"-epiclocale=en-US");
            p.StartInfo.ArgumentList.Add($"-EpicPortal");
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
            p.Start();
            return p;
        }

        static EGLManifest? GetManifest(string executable_name)
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData)
                                                                    + @"\Epic\EpicGamesLauncher\Data\Manifests");
            } catch {
                return null;
            }
            foreach (string file in files)
            {
                try
                {
                    string jsonstring = File.ReadAllText(file);
                    EGLManifest? manifest = JsonSerializer.Deserialize<EGLManifest>(jsonstring);
                    if (manifest != null && manifest.LaunchExecutable != null && Path.GetFileName(manifest.LaunchExecutable).ToLower() == executable_name.ToLower())
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
