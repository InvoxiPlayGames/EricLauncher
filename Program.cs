using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EricLauncher
{
    internal class Program
    {
        static string BaseAppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "/EricLauncher";
        static string BaseOVTFolder = BaseAppDataFolder + "/OVT";
        static string RedirectURL = "https://www.epicgames.com/id/api/redirect?clientId=" + EpicLogin.LAUNCHER_CLIENT + "&responseType=code";

        static void PrintUsage()
        {
            Console.WriteLine("Usage: EricLauncher.exe [game executable path or verb] (options) (game arguments)");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --accountId [id]     - use a specific Epic Games account ID to sign in.");
            Console.WriteLine("  --account [username] - use a specific Epic Games account username to sign in.");
            Console.WriteLine("  --setDefault         - when used with either account/accountId, sets the default account.");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Console.WriteLine("  --heroic             - loads the game's manifest from Heroic Games Launcher instead of Epic.");
            Console.WriteLine("  --manifest [path]    - loads an Epic Games Launcher manifest file from the specified path.");
            Console.WriteLine("  --noManifest         - don't check the local Epic Games Launcher install folder for the manifest.");
            Console.WriteLine("  --stayOpen           - keeps EricLauncher open in the background until the game is closed.");
            Console.WriteLine("  --dryRun             - goes through the Epic Games login flow, but does not launch the game.");
            Console.WriteLine("  --offline            - skips the Epic Games login flow, to launch the game in offline mode.");
            Console.WriteLine();
            Console.WriteLine("Verbs:");
            Console.WriteLine("  logout    - Logs out of Epic Games.");
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
            string? account_name = null;
            string? manifest_path = null;
            bool set_default = false;
            bool no_manifest = false;
            bool heroic_manifest = false;
            bool stay_open = false;
            bool dry_run = false;
            bool offline = false;
            bool skip_fortnite_update = false;
            bool caldera = false;
            string extra_args = "";
            if (args.Length > 1)
            {
                for (int i = 1; i < args.Length; i++)
                {
                    if (args[i] == "--accountId")
                        account_id = args[++i];
                    if (args[i] == "--account")
                        account_name = args[++i];
                    else if (args[i] == "--manifest")
                        manifest_path = args[++i];
                    else if (args[i] == "--setDefault")
                        set_default = true;
                    else if (args[i] == "--noManifest")
                        no_manifest = true;
                    else if (args[i] == "--heroic")
                        heroic_manifest = true;
                    else if (args[i] == "--stayOpen")
                        stay_open = true;
                    else if (args[i] == "--dryRun")
                        dry_run = true;
                    else if (args[i] == "--offline")
                        offline = true;
                    else if (args[i] == "--caldera")
                        caldera = true;
                    else if (args[i] == "--noCheckFn")
                        skip_fortnite_update = true;
                    else
                        extra_args += args[i] + " ";
                }
            }
            // both of these being null implies setting a default account
            if (account_id == null && account_name == null)
                set_default = true;

            string exe_name = args[0];

            // handle special exe names
            bool exchange_code_only = false;
            bool caldera_only = false;
            bool access_token_only = false;
            bool logout = false;
            if (exe_name.StartsWith("exchange")) exchange_code_only = true;
            if (exe_name.EndsWith("caldera")) caldera_only = true;
            if (exe_name == "access") access_token_only = true;
            if (exe_name == "logout") logout = true;
            // all these options imply an online dry run with no manifest
            if (exchange_code_only || caldera_only || access_token_only || logout)
            {
                no_manifest = true;
                dry_run = true;
                offline = false;
            }

            // always run as a dry run if we're on Linux or FreeBSD
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
                dry_run = true;

            // if we're launching fortnite, do an update check
            if (!skip_fortnite_update && !offline &&
                Path.GetFileName(exe_name).ToLower() == "fortnitelauncher.exe")
            {
                Console.WriteLine("Checking for Fortnite updates...");
                // traverse back to the cloud content json
                try
                {
                    string cloudcontent_path = Path.GetDirectoryName(exe_name) + @"\..\..\..\Cloud\cloudcontent.json";
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
            StoredAccountInfo? storedInfo = null;
            if (account_name == null && account_id == null)
                account_id = GetDefaultAccount();
            if (account_name != null)
                storedInfo = GetAccountInfoByName(account_name);
            if (account_id != null)
                storedInfo = GetAccountInfo(account_id);
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
            if (!set_default && account_id != null && account!.AccountId != account_id)
            {
                Console.WriteLine($"Logged in, but the account ID ({account.AccountId}) isn't the same as the one selected ({account_id}).");
                // save the account info later just to save time
                StoreAccountInfo(account!.MakeStoredAccountInfo());
                return;
            }
            if (account_name != null && account!.DisplayName != account_name)
            {
                Console.WriteLine($"Logged in, but the account name ({account.DisplayName}) isn't the same as the one selected ({account_name}).");
                // save the account info later just to save time
                StoreAccountInfo(account!.MakeStoredAccountInfo());
                return;
            }

            // we've logged in successfully!
            if (account!.DisplayName != null)
                Console.WriteLine($"Logged in as {account.DisplayName} ({account.AccountId})!");
            else
                Console.WriteLine($"Logged in as {account.AccountId}!");

            if (logout)
            {
                DeleteAccountInfo(account.AccountId!);
                if (GetDefaultAccount() == account.AccountId!)
                    DeleteDefaultAccount();

                bool success = await account.Logout();
                if (success)
                    Console.WriteLine("Successfully logged out!");
                else // hdd still doesn't have session but it exists in backend...
                    Console.WriteLine("Logged out!");
                return;
            }

            // save our refresh token for later usage
            if (!Directory.Exists(BaseAppDataFolder))
                Directory.CreateDirectory(BaseAppDataFolder);
            StoreAccountInfo(account!.MakeStoredAccountInfo(), set_default);

            // fetch the game's manifest from the installed epic games launcher
            EGLManifest? manifest = null;
            if (!no_manifest && manifest_path == null)
            {
                // load the manifest from the legendary cache if specified as an argument
                if (heroic_manifest)
                    manifest = GetLegendaryManifest(Path.GetFileName(exe_name));
                // always use FortniteLauncher's manifest for FortniteClient binaries
                else if (Path.GetFileName(exe_name).ToLower().StartsWith("fortniteclient"))
                    manifest = GetEGLManifest("FortniteLauncher.exe");
                else
                    manifest = GetEGLManifest(Path.GetFileName(exe_name));
            } else if (!no_manifest && manifest_path != null)
            {
                string jsonstring = File.ReadAllText(manifest_path);
                manifest = JsonSerializer.Deserialize<EGLManifest>(jsonstring);
            }
            if (manifest == null && !no_manifest)
            {
                Console.WriteLine("Manifest wasn't loaded! The game might not work properly.");
                Console.WriteLine("(Try launching the game via the Epic Games Launcher at least once.)");
            }

            // launch the game
            string exchange = "";
            if (!offline)
                exchange = await account.GetExchangeCode();

            if (exchange_code_only)
            {
                Console.WriteLine($"Exchange Code: {exchange!}");
                if (!caldera_only) return;
            }

            if (access_token_only)
            {
                EpicLogin fnLogin = new(EpicLogin.FORTNITE_PC_CLIENT, EpicLogin.FORTNITE_PC_SECRET);
                EpicAccount? fnAccount = await fnLogin.LoginWithExchangeCode(exchange);
                if (fnAccount != null)
                {
                    Console.WriteLine($"Access Token: {fnAccount.AccessToken}");
                } else
                {
                    Console.WriteLine("Failed to get access token.");
                }
                return;
            }

            // caldera simulation
            if ((caldera && Path.GetFileName(exe_name).StartsWith("Fortnite")) || caldera_only)
            {
                string? gamedir = Path.GetDirectoryName(exe_name);
                CalderaResponse? cal_resp = await EpicCaldera.GetCalderaResponse(account_id!, exchange, "fortnite");
                string acargs = $" -caldera={cal_resp!.jwt}";
                string acexe = "FortniteClient-Win64-Shipping";
                switch (cal_resp.provider)
                {
                    case "EasyAntiCheatEOS":
                        acargs += " -fromfl=eaceos -noeac -nobe ";
                        acexe += "_EAC_EOS.exe";
                        break;
                    case "EasyAntiCheat":
                        acargs += " -fromfl=eac -noeaceos -nobe ";
                        acexe += "_EAC.exe";
                        break;
                    case "BattlEye":
                        acargs += " -fromfl=be -noeaceos -noeac ";
                        acexe += "_BE.exe";
                        break;
                    default:
                        Console.WriteLine($"Unknown Caldera provider '{cal_resp.provider}'.");
                        return;
                }
                extra_args += acargs;
                exe_name = Path.Combine(gamedir!, acexe);

                if (caldera_only)
                {
                    Console.WriteLine($"AC Provider: {cal_resp.provider!}");
                    Console.WriteLine($"AC JWT: {cal_resp.jwt!}");
                    return;
                }
            }

            Console.WriteLine("Launching game...");
            Process game = await LaunchGame(exe_name, exchange, account, manifest, dry_run, offline, extra_args);
            if (stay_open && !dry_run)
            {
                game.WaitForExit();
                Console.WriteLine($"Game exited with code {game.ExitCode}");
            }
        }

        static async Task<Process> LaunchGame(string filename, string? exchange, EpicAccount? account, EGLManifest? manifest, bool dry_run, bool skip_ovt, string launch_args)
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

            if (launch_args != "")
            {
                string[] split_args = launch_args.Split(' ');
                foreach (string arg in split_args)
                    p.StartInfo.ArgumentList.Add(arg);
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
            string ovt_path = $"{BaseOVTFolder}/{account!.AccountId!}-{manifest.MainGameAppName!}.ovt";
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
                manifestfolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + manifestfolder;

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

        static EGLManifest? GetLegendaryManifest(string executable_name)
        {
            // we don't yet support
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return null;

            string legendaryInstalledPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
                "/heroic/legendaryConfig/legendary/installed.json";

            Dictionary<string, LegendaryManifestEntry>? manifests;
            try
            {
                string manifeststring = File.ReadAllText(legendaryInstalledPath);
                manifests = JsonSerializer.Deserialize<Dictionary<string, LegendaryManifestEntry>>(manifeststring);
                foreach (LegendaryManifestEntry manifest in manifests!.Values)
                {
                    if (manifest.executable != null &&
                        Path.GetFileName(manifest.executable).ToLower() == executable_name.ToLower())
                    {
                        // build a fake EGLManifest i can't be bothered man
                        EGLManifest proper = new();
                        proper.AppName = manifest.app_name;
                        proper.bCanRunOffline = manifest.can_run_offline;
                        proper.LaunchExecutable = manifest.executable;
                        proper.InstallLocation = manifest.install_path;
                        proper.InstallSize = manifest.install_size;
                        proper.InstallTags = manifest.install_tags;
                        proper.bIsApplication = !manifest.is_dlc; // no idea??
                        proper.LaunchCommand = manifest.launch_parameters;
                        proper.OwnershipToken = manifest.requires_ot.ToString();
                        proper.DisplayName = manifest.title; // also just a guess
                        return proper;
                    }
                }
            } catch { }
            return null;
        }

        static void StoreAccountInfo(StoredAccountInfo info, bool set_default = false)
        {
            string jsonstring = JsonSerializer.Serialize(info);
            File.WriteAllText($"{BaseAppDataFolder}/{info.AccountId!}.json", jsonstring);
            if (set_default)
                File.WriteAllText($"{BaseAppDataFolder}/default.json", $"{{\"AccountId\": \"{info.AccountId!}\"}}");
        }

        static void DeleteAccountInfo(string account_id)
        {
            string path = $"{BaseAppDataFolder}/{account_id}.json";
            try
            {
                File.Delete(path);
            }
            catch { }
            return;
        }

        static StoredAccountInfo? GetAccountInfo(string account_id)
        {
            string path = $"{BaseAppDataFolder}/{account_id}.json";
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

        static StoredAccountInfo? GetAccountInfoByName(string display_name)
        {
            IEnumerable<string> files = Directory.EnumerateFiles(BaseAppDataFolder);
            foreach (string filename in files)
            {
                if (Path.GetFileNameWithoutExtension(filename).Length != 32) // account id length + .json
                    continue;
                try
                {
                    string jsonstring = File.ReadAllText(filename);
                    StoredAccountInfo? info = JsonSerializer.Deserialize<StoredAccountInfo>(jsonstring);
                    if (info != null && info.DisplayName != null && display_name == info.DisplayName)
                        return info;
                }
                catch { }
            }
            return null;
        }

        static string? GetDefaultAccount()
        {
            string path = $"{BaseAppDataFolder}/default.json";
            if (!File.Exists(path))
                return null;
            try
            {
                string jsonstring = File.ReadAllText(path);
                StoredAccountInfo? info = JsonSerializer.Deserialize<StoredAccountInfo>(jsonstring);
                if (info != null)
                    return info.AccountId;
            }
            catch { }
            return null;
        }

        static void DeleteDefaultAccount()
        {
            string path = $"{BaseAppDataFolder}/default.json";
            try
            {
                File.Delete(path);
            }
            catch { }
            return;
        }
    }
}
