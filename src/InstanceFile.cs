// Reads and edits CurseForge's list of game instances
// (%APPDATA%\CurseForge\agent\GameInstances\AddonGameInstance.json, a JSON array
// with one object per instance). Entries are cut out of the array as text, so
// the ones that stay are byte-for-byte unchanged.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace CurseForgeInstanceCleaner
{
    public class GameInstance
    {
        public string Key;      // the instance's guid (or its position if it has none)
        public string Name;
        public string Game;
        public string Folder;
        public bool FolderExists;
        public int Addons;
        public bool Managed;
        public string Raw;      // the entry's JSON text, as in the file
    }

    public class InstanceFile
    {
        static readonly Dictionary<int, string> Games = new Dictionary<int, string> { { 1, "World of Warcraft" } };

        public static string DefaultPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"CurseForge\agent\GameInstances\AddonGameInstance.json");
            }
        }

        public string FilePath { get; private set; }
        public List<GameInstance> Instances { get; private set; }
        bool bom;

        public static InstanceFile Load(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("CurseForge's instance file was not found:\n" + path);
            byte[] bytes = File.ReadAllBytes(path);
            bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            int skip = bom ? 3 : 0;
            string text = new UTF8Encoding(false).GetString(bytes, skip, bytes.Length - skip);

            string[] parts = SplitArray(text);
            var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 1000 };
            object[] objs = js.DeserializeObject(text) as object[];
            if (objs == null || objs.Length != parts.Length)
                throw new InvalidDataException("Could not read the file safely, so nothing was changed.");

            var file = new InstanceFile { FilePath = path, bom = bom, Instances = new List<GameInstance>() };
            for (int i = 0; i < objs.Length; i++)
            {
                var d = objs[i] as IDictionary<string, object>;
                if (d == null) throw new InvalidDataException("Entry " + (i + 1) + " is not an instance.");
                var inst = new GameInstance();
                inst.Key = Str(d, "guid");
                if (string.IsNullOrEmpty(inst.Key)) inst.Key = "#" + i;
                inst.Name = Str(d, "name");
                inst.Folder = Str(d, "installPath");
                inst.FolderExists = FolderExists(inst.Folder);
                var addons = Get(d, "installedAddons") as ICollection;
                inst.Addons = addons == null ? 0 : addons.Count;
                inst.Managed = Get(d, "isEnabled") is bool && (bool)Get(d, "isEnabled");
                int game = 0;
                try { game = Convert.ToInt32(Get(d, "gameTypeID")); } catch { }
                string gameName;
                inst.Game = Games.TryGetValue(game, out gameName) ? gameName : "Game " + game;
                inst.Raw = parts[i];
                file.Instances.Add(inst);
            }
            return file;
        }

        // Writes the file without the given instances; returns the backup's path.
        public string Remove(ICollection<string> keys)
        {
            var keep = new List<string>();
            foreach (var inst in Instances)
                if (!keys.Contains(inst.Key)) keep.Add(inst.Raw);

            string backup = Backup(FilePath);
            try
            {
                File.WriteAllText(FilePath, "[" + string.Join(",", keep) + "]", new UTF8Encoding(bom));
                if (Load(FilePath).Instances.Count != keep.Count) throw new InvalidDataException();
            }
            catch
            {
                File.Copy(backup, FilePath, true);
                throw new InvalidDataException("The new file did not check out, so the original was put back.");
            }
            return backup;
        }

        // Puts a backup back in place (backing up the current file first).
        public static void Restore(string backup, string target)
        {
            Load(backup);
            if (File.Exists(target)) Backup(target);
            File.Copy(backup, target, true);
        }

        public static string Backup(string path)
        {
            string dest = path + ".backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            for (int n = 2; File.Exists(dest); n++)
                dest = path + ".backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + n;
            File.Copy(path, dest);
            return dest;
        }

        // The raw text of each top-level element of a JSON array.
        public static string[] SplitArray(string s)
        {
            var list = new List<string>();
            int i = 0, n = s.Length;
            while (i < n && char.IsWhiteSpace(s[i])) i++;
            if (i >= n || s[i] != '[') throw new InvalidDataException("The file is not a list of instances.");
            i++;
            int depth = 0, start = -1;
            bool str = false, esc = false;
            for (; i < n; i++)
            {
                char c = s[i];
                if (str)
                {
                    if (esc) esc = false;
                    else if (c == '\\') esc = true;
                    else if (c == '"') str = false;
                    continue;
                }
                if (char.IsWhiteSpace(c)) continue;
                if (start < 0) start = i;
                if (c == '"') str = true;
                else if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']')
                {
                    if (depth == 0)
                    {
                        if (c != ']') throw new InvalidDataException("The file is damaged (unexpected }).");
                        if (start < i) list.Add(s.Substring(start, i - start).Trim());
                        return list.ToArray();
                    }
                    depth--;
                }
                else if (c == ',' && depth == 0)
                {
                    list.Add(s.Substring(start, i - start).Trim());
                    start = -1;
                }
            }
            throw new InvalidDataException("The file is damaged (it ends in the middle of the list).");
        }

        static object Get(IDictionary<string, object> d, string key)
        {
            object o;
            return d.TryGetValue(key, out o) ? o : null;
        }

        static string Str(IDictionary<string, object> d, string key)
        {
            object o = Get(d, key);
            return o == null ? "" : o.ToString();
        }

        static bool FolderExists(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try { return Directory.Exists(path); } catch { return false; }
        }
    }
}
