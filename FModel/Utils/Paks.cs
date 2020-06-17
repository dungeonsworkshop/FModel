using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using FModel.Grabber.Paks;
using FModel.Logger;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PakReader.Parsers.Objects;


namespace FModel.Utils
{
    internal static class Paks
    {
        /// <summary>
        ///     1. AppName
        ///     2. AppVersion
        ///     3. AppFilesPath
        /// </summary>
        /// <returns></returns>
        public static (string, string, string) GetUEGameFilesPath(string game)
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                var launcher = $"{drive.Name}ProgramData\\Epic\\UnrealEngineLauncher\\LauncherInstalled.dat";
                if (File.Exists(launcher))
                {
                    DebugHelper.WriteLine("{0} {1} {2}", "[FModel]", "[LauncherInstalled.dat]", launcher);
                    var launcherDat = JsonConvert.DeserializeObject<LauncherDat>(File.ReadAllText(launcher));
                    if (launcherDat?.InstallationList != null)
                    {
                        foreach (var installationList in launcherDat.InstallationList)
                            if (installationList.AppName.Equals(game))
                                return (installationList.AppName, installationList.AppVersion,
                                    installationList.InstallLocation);

                        DebugHelper.WriteLine("{0} {1} {2}", "[FModel]", "[LauncherInstalled.dat]",
                            $"{game} not found");
                    }
                }
            }

            DebugHelper.WriteLine("{0} {1} {2}", "[FModel]", "[LauncherInstalled.dat]", "File not found");
            return (string.Empty, string.Empty, string.Empty);
        }

        public static string GetFortnitePakFilesPath()
        {
            var (_, _, fortniteFilesPath) = GetUEGameFilesPath("Fortnite");
            if (!string.IsNullOrEmpty(fortniteFilesPath))
                return $"{fortniteFilesPath}\\FortniteGame\\Content\\Paks";
            return string.Empty;
        }

        public static string GetValorantPakFilesPath()
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                var installs = $"{drive.Name}ProgramData\\Riot Games\\RiotClientInstalls.json";
                if (File.Exists(installs))
                {
                    DebugHelper.WriteLine("{0} {1} {2}", "[FModel]", "[RiotClientInstalls.json]", installs);
                    var installsJson = JsonConvert.DeserializeObject<InstallsJson>(File.ReadAllText(installs));
                    if (installsJson?.AssociatedClient.Count > 0)
                    {
                        foreach (var KvP in installsJson.AssociatedClient)
                            if (KvP.Key.Contains("VALORANT/live/"))
                                return $"{KvP.Key.Replace("/", "\\")}ShooterGame\\Content\\Paks";

                        DebugHelper.WriteLine("{0} {1} {2}", "[FModel]", "[RiotClientInstalls.json]",
                            "Valorant not found");
                    }
                }
            }

            DebugHelper.WriteLine("{0} {1} {2}", "[FModel]", "[RiotClientInstalls.json]", "File not found");
            return string.Empty;
        }

        public static string GetBorderlands3PakFilesPath()
        {
            var (_, _, borderlands3FilesPath) = GetUEGameFilesPath("Catnip");
            if (!string.IsNullOrEmpty(borderlands3FilesPath))
                return $"{borderlands3FilesPath}\\OakGame\\Content\\Paks";
            return string.Empty;
        }

        public static string GetMinecraftDungeonsPakFilesPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var install = $"{appData}/.minecraft_dungeons/launcher_settings.json";
            if (File.Exists(install))
            { 
                DebugHelper.WriteLine("{0} {1} {2}", "[FModel]", "[launcher_settings.json]", install);
                var launcherSettings = (JObject)JsonConvert.DeserializeObject(File.ReadAllText(install));
                string location = launcherSettings["productLibraryDir"].Value<string>();
            
                if(!string.IsNullOrEmpty(location))
                    return $"{location}\\dungeons\\dungeons\\Dungeons\\Content\\Paks";
                
                DebugHelper.WriteLine("{0} {1} {2}", "[FModel]", "[launcher_settings.json]", "Minecraft Dungeons not found");
            }
            
            DebugHelper.WriteLine("{0} {1} {2}", "[FModel]", "[launcher_settings.json]", "Launcher version not found, attempting to find modded MS Store installation.");

            // PowerShell ps = PowerShell.Create();
            // ps.AddCommand("Get-AppxPackage");
            // ps.AddParameter("Microsoft.Lovika.mod");
            // ps.AddCommand("select");
            // ps.AddParameter("ExpandProperty", "InstallLocation");
            // string storeInstall = ps.Invoke().ToString();
            //
            // if(!string.IsNullOrEmpty(storeInstall))
            //     return $"{storeInstall}\\Dungeons\\Content\\Paks";
            // DebugHelper.WriteLine("{0} {1} {2}", "[FModel]", "[Microsoft Store]", "Modded Minecraft Dungeons installation not found.");
            return string.Empty;
        }

        public static void Merge(Dictionary<string, FPakEntry> tempFiles, out Dictionary<string, FPakEntry> files,
            string mount)
        {
            files = new Dictionary<string, FPakEntry>();
            foreach (var entry in tempFiles.Values)
            {
                if (files.ContainsKey(mount + entry.GetPathWithoutExtension()) || entry.GetExtension().Equals(".uptnl"))
                    continue;

                if (entry.IsUE4Package()) // if .uasset
                {
                    if (!tempFiles.ContainsKey(Path.ChangeExtension(entry.Name, ".umap"))) // but not including a .umap
                    {
                        var e = Path.ChangeExtension(entry.Name, ".uexp");
                        var uexp = tempFiles.ContainsKey(e) ? tempFiles[e] : null; // add its uexp
                        if (uexp != null)
                            entry.Uexp = uexp;

                        var u = Path.ChangeExtension(entry.Name, ".ubulk");
                        var ubulk = tempFiles.ContainsKey(u) ? tempFiles[u] : null; // add its ubulk
                        if (ubulk != null)
                        {
                            entry.Ubulk = ubulk;
                        }
                        else
                        {
                            var f = Path.ChangeExtension(entry.Name, ".ufont");
                            var ufont = tempFiles.ContainsKey(f) ? tempFiles[f] : null; // add its ufont
                            if (ufont != null)
                                entry.Ubulk = ufont;
                        }
                    }
                }
                else if (entry.IsUE4Map()) // if .umap
                {
                    var e = Path.ChangeExtension(entry.Name, ".uexp");
                    var u = Path.ChangeExtension(entry.Name, ".ubulk");
                    var uexp = tempFiles.ContainsKey(e) ? tempFiles[e] : null; // add its uexp
                    if (uexp != null)
                        entry.Uexp = uexp;
                    var ubulk = tempFiles.ContainsKey(u) ? tempFiles[u] : null; // add its ubulk
                    if (ubulk != null)
                        entry.Ubulk = ubulk;
                }

                files[mount + entry.GetPathWithoutExtension()] = entry;
            }
        }

        public static bool IsFileReadLocked(FileInfo PakFileInfo)
        {
            FileStream stream = null;
            try
            {
                stream = PakFileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }
            catch (IOException)
            {
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            return false;
        }

        public static bool IsFileWriteLocked(FileInfo PakFileInfo)
        {
            FileStream stream = null;
            try
            {
                stream = PakFileInfo.Open(FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            }
            catch (IOException)
            {
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            return false;
        }
    }
}