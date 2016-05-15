﻿using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace ServerTools
{
    public class Watchlist
    {
        public static bool IsEnabled = false;
        public static bool IsRunning = false;
        private static SortedDictionary<string, string> dict = new SortedDictionary<string, string>();
        private static string file = "Watchlist.xml";
        private static string filePath = string.Format("{0}/{1}", API.ConfigPath, file);
        private static FileSystemWatcher fileWatcher = new FileSystemWatcher(API.ConfigPath, file);

        public static void Load()
        {
            if (IsEnabled && !IsRunning)
            {
                LoadXml();
                InitFileWatcher();
            }
        }

        public static void Unload()
        {
            dict.Clear();
            fileWatcher.Dispose();
            IsRunning = false;
        }

        private static void LoadXml()
        {
            if (!Utils.FileExists(filePath))
            {
                UpdateXml();
            }
            XmlDocument xmlDoc = new XmlDocument();
            try
            {
                xmlDoc.Load(filePath);
            }
            catch (XmlException e)
            {
                Log.Error(string.Format("[SERVERTOOLS] Failed loading {0}: {1}", file, e.Message));
                return;
            }
            XmlNode _XmlNode = xmlDoc.DocumentElement;
            foreach (XmlNode childNode in _XmlNode.ChildNodes)
            {
                if (childNode.Name == "Players")
                {
                    dict.Clear();
                    foreach (XmlNode subChild in childNode.ChildNodes)
                    {
                        if (subChild.NodeType == XmlNodeType.Comment)
                        {
                            continue;
                        }
                        if (subChild.NodeType != XmlNodeType.Element)
                        {
                            Log.Warning(string.Format("[SERVERTOOLS] Unexpected XML node found in 'Players' section: {0}", subChild.OuterXml));
                            continue;
                        }
                        XmlElement _line = (XmlElement)subChild;
                        if (!_line.HasAttribute("SteamId"))
                        {
                            Log.Warning(string.Format("[SERVERTOOLS] Ignoring Player entry because of missing 'SteamId' attribute: {0}", subChild.OuterXml));
                            continue;
                        }
                        if (!_line.HasAttribute("Reason"))
                        {
                            Log.Warning(string.Format("[SERVERTOOLS] Ignoring Player entry because of missing 'Reason' attribute: {0}", subChild.OuterXml));
                            continue;
                        }
                        if (!dict.ContainsKey(_line.GetAttribute("SteamId")))
                        {
                            dict.Add(_line.GetAttribute("SteamId"), _line.GetAttribute("Reason"));
                        }
                    }
                }
            }
        }

        private static void UpdateXml()
        {
            fileWatcher.EnableRaisingEvents = false;
            using (StreamWriter sw = new StreamWriter(filePath))
            {
                sw.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                sw.WriteLine("<Watchlist>");
                sw.WriteLine("    <Players>");
                if (dict.Count > 0)
                {
                    foreach (KeyValuePair<string, string> kvp in dict)
                    {
                        sw.WriteLine(string.Format("        <Player SteamId=\"{0}\" Reason=\"{1}\" />", kvp.Key, kvp.Value));
                    }
                }
                else
                {
                    sw.WriteLine(string.Format("        <!-- Player SteamId=\"123456\" Reason=\"Suspected cheating.\" / -->"));
                }
                sw.WriteLine("    </Players>");
                sw.WriteLine("</Watchlist>");
                sw.Flush();
                sw.Close();
            }
            fileWatcher.EnableRaisingEvents = true;
        }

        private static void InitFileWatcher()
        {
            fileWatcher.Changed += new FileSystemEventHandler(OnFileChanged);
            fileWatcher.Created += new FileSystemEventHandler(OnFileChanged);
            fileWatcher.Deleted += new FileSystemEventHandler(OnFileChanged);
            fileWatcher.EnableRaisingEvents = true;
            IsRunning = true;
        }

        private static void OnFileChanged(object source, FileSystemEventArgs e)
        {
            if (!Utils.FileExists(filePath))
            {
                UpdateXml();
            }
            LoadXml();
        }

        private static void CheckWatchlist(ClientInfo _cInfo)
        {
            if (dict.ContainsKey(_cInfo.playerId))
            {
                List<ClientInfo> _cInfoList = ConnectionManager.Instance.GetClients();
                foreach (ClientInfo _cInfo1 in _cInfoList)
                {
                    if (GameManager.Instance.adminTools.IsAdmin(_cInfo1.playerId))
                    {
                        string _phrase350 = "Player {PlayerName} is on the watchlist for {Reason}.";
                        if (!Phrases.Dict.TryGetValue(350, out _phrase350))
                        {
                            Log.Out("[SERVERTOOLS] Phrase 350 not found using default.");
                        }
                        string _reason = null;
                        if (dict.TryGetValue(_cInfo.playerId, out _reason))
                        {
                            _phrase350 = _phrase350.Replace("{PlayerName}", _cInfo.playerName);
                            _phrase350 = _phrase350.Replace("{Reason}", _reason);
                            GameManager.Instance.GameMessageServer(_cInfo1, EnumGameMessages.Chat, string.Format("[FF8000]{0}[-]", _phrase350), "Server", false, "", false);
                        }
                    }
                }
            }
        }
    }
}