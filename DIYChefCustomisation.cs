using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Team17.Online;
using Team17.Online.Multiplayer;
using Team17.Online.Multiplayer.Messaging;
using BitStream;
using OC2DIYChef.Extension;
using System.Collections;

namespace OC2DIYChef
{
    public static class DIYChefCustomisation
    {
        public static KeyValuePair<string, string>[] preferredChefs;
        public static uint defaultChefID = 127;
        public static List<DIYChefAvatarData> diyChefs = new List<DIYChefAvatarData>();
        public static List<HatData> diyHats = new List<HatData>();
        public static byte[] userDIYChefID = Enumerable.Repeat((byte)255, 4).ToArray();
        private static Dictionary<string, string> dialogText;
        public static bool enableLobbySwitchChef = false;
        public static ChefAvatarData defaultTemplate;

        public static Client localClient = null;
        public static Server localServer = null;
        public const MessageType diyChefMessageType = (MessageType)67;

        public static GUIStyle guiStyle;
        static IEnumerator loadingCoroutine1, loadingCoroutine2;
        static string loadingName;

        public static void SendClientDIYChefMessage(byte diyChefID, User user)
        {
            var message = new DIYChefMessage();
            message.InitialiseClient(diyChefID, user);
            localClient.SendMessageToServer(diyChefMessageType, message, true);
        }

        public static void SendServerDIYChefMessage()
        {
            if (localServer == null) return;
            var message = new DIYChefMessage();
            message.InitialiseServer();
            localServer.BroadcastMessageToAll(diyChefMessageType, message, true);
        }

        public static void OnMessageReceived(DIYChefMessage message)
        {
            if (!message.server2client)
            {
                FastList<User> users = ServerUserSystem.m_Users;
                User.MachineID machine = message.chefAvatarMessage.m_Machine;
                EngagementSlot engagementSlot = message.chefAvatarMessage.m_EngagementSlot;
                User.SplitStatus split = message.chefAvatarMessage.m_Split;
                User user = UserSystemUtils.FindUser(users, null, machine, engagementSlot, TeamID.Count, split);
                if (user == null) return;
                int player = users.FindIndex(x => x == user);
                user.ChangedThisFrame = true;
                userDIYChefID[player] = message.userDIYChefID[0];
                SendServerDIYChefMessage();
            }
            else
            {
                var multiplayerController = GameUtils.RequestManager<MultiplayerController>();
                if (multiplayerController == null || multiplayerController.IsServer()) return;
                byte[] newDIYChefID = new byte[4];
                Array.Copy(message.userDIYChefID, newDIYChefID, 4);
                FastList<User> users = ClientUserSystem.m_Users;

                for (int i = 0; i < message.usersChangedMessage.m_Users.Count; i++)
                {
                    var userData = message.usersChangedMessage.m_Users._items[i];
                    var Machine = userData.machine;
                    if (Machine != ClientUserSystem.s_LocalMachineId) continue;
                    var Engagement = userData.slot;
                    var Split = userData.splitStatus;
                    User user = UserSystemUtils.FindUser(users, null, Machine, Engagement, TeamID.Count, Split);
                    if (user == null && Split != User.SplitStatus.SplitPadGuest)
                        user = UserSystemUtils.FindUser(users, null, Machine, Engagement, TeamID.Count);
                    if (user != null)
                    {
                        int i1 = users.FindIndex(x => x == user);
                        if (newDIYChefID[i] != userDIYChefID[i1])
                        {
                            newDIYChefID[i] = userDIYChefID[i1];
                            SendClientDIYChefMessage(userDIYChefID[i1], user);
                        }
                    }
                }
                Array.Copy(newDIYChefID, userDIYChefID, 4);
                Patch.flag_ClientUserSystemOnUsersChangedPatch = false;
                localClient.GetUserSystem().OnUsersChanged(null, message.usersChangedMessage);
            }
        }

        private static void OnUserRemoved(User _user, int _idx)
        {
            for (int i = _idx; i < 3; i++) userDIYChefID[i] = userDIYChefID[i + 1];
            userDIYChefID[3] = 255;
            SendServerDIYChefMessage();
        }

        public static void OnUserAdded(User _user)
        {
            int player = ServerUserSystem.m_Users.FindIndex((User x) => x == _user);
            if (player != -1)
            {
                for (int i = 3; i > player; i--)
                    userDIYChefID[i] = userDIYChefID[i - 1];
                userDIYChefID[player] = 255;
            }
            SendServerDIYChefMessage();
        }

        public static void OnAwake(MetaGameProgress metaGameProgress)
        {
            dialogText = Localization.GetLanguage() == SupportedLanguages.Chinese ?
                new Dictionary<string, string>()
                {
                    {"DefaultInvalid", "默认厨师不可用" },
                    {"MissingINFO", "缺失 INFO 文件" },
                    {"MissingTexture", "缺失主贴图" },
                    {"IDConflict", "ID 冲突" },
                    {"HatInvalid", "帽子不可用" },
                } :
                new Dictionary<string, string>()
                {
                    {"DefaultInvalid", "Invalid default chef" },
                    {"MissingINFO", "Missing INFO file" },
                    {"MissingTexture", "Missing main texture" },
                    {"IDConflict", "ID conflict" },
                    {"HatInvalid", "Invalid hat" },
                };
            defaultTemplate = metaGameProgress.AvatarDirectory.Avatars[15];
            LoadPreferredChefs(metaGameProgress);
            loadingCoroutine1 = LoadAllDIYHat();
            loadingCoroutine2 = LoadAllDIYChef(metaGameProgress);
            ServerUserSystem.OnUserRemovedWithIndex = (GenericVoid<User, int>)Delegate.Combine(ServerUserSystem.OnUserRemovedWithIndex, new GenericVoid<User, int>(OnUserRemoved));
            ServerUserSystem.OnUserAdded = (GenericVoid<User>)Delegate.Combine(ServerUserSystem.OnUserAdded, new GenericVoid<User>(OnUserAdded));
        }

        public static void Update()
        {
            if (loadingCoroutine1 != null || loadingCoroutine2 != null)
            {
                Time.timeScale = 0f;
                if (loadingCoroutine1 != null)
                {
                    if (!loadingCoroutine1.MoveNext())
                        loadingCoroutine1 = null;
                }
                else if (!loadingCoroutine2.MoveNext())
                {
                    loadingCoroutine2 = null;
                    Time.timeScale = 1f;
                }
            }
        }

        public static void OnGUI()
        {
            if (guiStyle == null)
            {
                Texture2D consoleBackground = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
                consoleBackground.SetPixel(0, 0, new Color(0.23f, 0.65f, 0.79f, 1f));
                consoleBackground.Apply();
                guiStyle = new GUIStyle(GUIStyle.none);
                guiStyle.normal.textColor = new Color(1f, 1f, 1f);
                guiStyle.normal.background = consoleBackground;
                guiStyle.fontStyle = FontStyle.Bold;
                guiStyle.fontSize = 40;
                guiStyle.alignment = TextAnchor.MiddleLeft;
                guiStyle.padding = new RectOffset(50, 50, 20, 20);
            }
            if (loadingCoroutine1 == null && loadingCoroutine2 == null) return;
            string info = Localization.GetLanguage() == SupportedLanguages.Chinese ? 
                $"加载模型 [{loadingName}]" : $"Loading Chef [{loadingName}]";
            var guiContent = new GUIContent(info);
            var labelSize = guiStyle.CalcSize(guiContent);
            GUI.Label(new Rect(50, Screen.height * 0.8f, labelSize.x, labelSize.y), guiContent, guiStyle);
        }

        private static void LoadPreferredChefs(MetaGameProgress metaGameProgress)
        {
            preferredChefs = new KeyValuePair<string, string>[0];
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/prefer.txt";
            if (!File.Exists(path))
                path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/prefer.txt.txt";
            if (!File.Exists(path))
            { 
                defaultChefID = 127U;
                return;
            }
            var lines = File.ReadAllLines(path);
            foreach (var line in lines)
            {
                if (line.Contains("LOBBYSWITCHCHEF=TRUE"))
                {
                    enableLobbySwitchChef = true;
                    continue;
                }
                var parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ConvertAll(s => s.Trim());
                if (parts.Length == 0) continue;
                string hat = "";
                if (parts.Length > 1 && parts[1].StartsWith("HAT="))
                {
                    hat = parts[1].Substring(4);
                    if (hat == "Festive") hat = "Santa";
                    if (hat == "Baseball") hat = "Baseballcap";
                }
                var pair = new KeyValuePair<string, string>(parts[0], hat);
                if (!preferredChefs.Any(x => x.Key == pair.Key))
                {
                    preferredChefs = preferredChefs.AddToArray(pair);
                    if (preferredChefs.Length == 1)
                    {
                        ChefAvatarData[] unlocked = metaGameProgress.GetUnlockedAvatars();
                        int num = unlocked.FindIndex_Predicate(x => x.HeadName.Equals(pair.Key));
                        if (num < 0)
                        {
                            preferredChefs = preferredChefs.AddItem(
                                new KeyValuePair<string, string>("Chef_Male_Asian", "")).ToArray();
                            defaultChefID = 15;
                            ShowWarningDialog(dialogText["DefaultInvalid"]);
                        }
                        else
                        {
                            defaultChefID = (uint)metaGameProgress.AvatarDirectory.Avatars.FindIndex_Predicate(x => x == unlocked[num]);
                        }
                    }
                }
            }
            
            if (preferredChefs.Length == 0)
            {
                defaultChefID = 127U;
                return;
            }
        }

        static IEnumerator LoadAllDIYHat()
        {
            diyHats.Clear();
            if (!Enabled)
                yield break;
            diyHats.Add(HatData.Create("Santa"));
            diyHats.Add(HatData.Create("Baseballcap"));

            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string path = dir + "/Resources/HATS";
            if (!Directory.Exists(path))
                yield break;

            DirectoryInfo parent = new DirectoryInfo(path);
            foreach (DirectoryInfo folder in parent.GetDirectories())
            {
                string name = folder.Name;
                if (File.Exists($"{path}/{name}/t_{name}.png") &&
                    File.Exists($"{path}/{name}/m_{name}.txt") &&
                    preferredChefs.Any(x => x.Value == name))
                {
                    loadingName = "HATS/" + name;
                    yield return null;
                    diyHats.Add(HatData.Create(name));
                }
            }
            yield break;
        }

        static void FixBodyMeshBug(MetaGameProgress metaGameProgress)
        {
            var avatar = metaGameProgress.AvatarDirectory.Avatars[15];
            foreach (var prefab in new GameObject[] { avatar.FrontendModelPrefab, avatar.ModelPrefab, avatar.UIModelPrefab })
            {
                Mesh mesh = prefab.transform.Find("Mesh/Body").GetComponent<SkinnedMeshRenderer>().sharedMesh;
                BoneWeight[] boneWeights = new BoneWeight[mesh.boneWeights.Length];
                for (int i = 0; i < mesh.boneWeights.Length; i++)
                    boneWeights[i] = mesh.boneWeights[i];
                boneWeights[166] = mesh.boneWeights[255];
                boneWeights[256] = mesh.boneWeights[255];
                mesh.boneWeights = boneWeights;
            }
        }

        static IEnumerator LoadAllDIYChef(MetaGameProgress metaGameProgress)
        {
            var newPreferredChefs = new List<KeyValuePair<string, string>>();
            diyChefs.Clear();
            if (!Enabled) 
                yield break;
            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string conflictMessage = string.Empty;
            FixBodyMeshBug(metaGameProgress);

            foreach (var pair in preferredChefs)
            {
                if (metaGameProgress.AvatarDirectory.Avatars.Any(x => x.HeadName == pair.Key))
                {
                    if (!HatData.originalHats.Contains(pair.Value) && !diyHats.Any(x => x.HatName == "Hat_" + pair.Value))
                    {
                        conflictMessage += $"{dialogText["HatInvalid"]}: {pair.Value}\n";
                        newPreferredChefs.Add(new KeyValuePair<string, string>(pair.Key, ""));
                    }
                    else
                    {
                        newPreferredChefs.Add(pair);
                    }
                    continue;
                }
                string path = dir + "/Resources/" + pair.Key;
                if (!Directory.Exists(path))
                    continue;
                if (!File.Exists(path + "/INFO"))
                {
                    conflictMessage += $"{dialogText["MissingINFO"]}: {pair.Key}\n";
                    continue;
                }

                if (!File.Exists(path + "/t_Head.png") || !File.Exists(path + "/m_Head.txt"))
                {
                    conflictMessage += $"{dialogText["MissingTexture"]}: {pair.Key}\n";
                    continue;
                }

                var lines = File.ReadAllLines(path + "/INFO");
                byte id = 255;
                foreach (var line in lines)
                {
                    if (line.StartsWith("ID="))
                        byte.TryParse(line.Substring(3), out id);
                }
                var conflictChef = diyChefs.Find(x => x.id == id);
                if (id == 255)
                    conflictMessage += $"{dialogText["IDConflict"]}: {pair.Key}, 255\n";
                else if (conflictChef != null)
                    conflictMessage += $"{dialogText["IDConflict"]}: {conflictChef.name}, {pair.Key}\n";
                else
                {
                    loadingName = pair.Key;
                    yield return null;
                    DIYChefAvatarData avatar = DIYChefAvatarData.Create(pair.Key);
                    diyChefs.Add(avatar);
                    if (!HatData.originalHats.Contains(pair.Value) && !diyHats.Any(x => x.HatName == "Hat_" + pair.Value))
                    {
                        conflictMessage += $"{dialogText["HatInvalid"]}: {pair.Value}\n";
                        newPreferredChefs.Add(new KeyValuePair<string, string>(avatar.HeadName, ""));
                    }
                    else
                    {
                        newPreferredChefs.Add(new KeyValuePair<string, string>(avatar.HeadName, pair.Value));
                    }
                }
            }

            if (conflictMessage != string.Empty)
                ShowWarningDialog(conflictMessage.TrimEnd());
            preferredChefs = newPreferredChefs.ToArray();
            yield break;
        }

        public static void SwitchChef(int player, int step, out uint uAvatarID, out byte uDIYAvatarID)
        {
            FastList<User> users = ClientUserSystem.m_Users;
            MetaGameProgress metaGameProgress = GameUtils.GetMetaGameProgress();
            var unlockedAvatars = metaGameProgress.GetUnlockedAvatars();
            var allAvatars = GameUtils.GetAvatarDirectoryData().Avatars;

            int id = (int)users._items[player].SelectedChefAvatar;
            int id1 = userDIYChefID[player];
            string headName = (diyChefs.Find(x => x.id == id1)?.HeadName) ?? allAvatars[id].HeadName;
            int newIndex = preferredChefs.FindIndex_Predicate(x => x.Key == headName);
            while (true)
            {
                newIndex = MathUtils.Wrap(newIndex + step, 0, preferredChefs.Length);
                if (unlockedAvatars.Any(x => x.HeadName == preferredChefs[newIndex].Key))
                    break;
                if (diyChefs.Any(x => x.HeadName == preferredChefs[newIndex].Key))
                    break;
            }

            int avatarID = allAvatars.FindIndex_Predicate(x => x.HeadName == preferredChefs[newIndex].Key);
            uAvatarID = avatarID < 0 ? defaultChefID : (uint)avatarID;
            var diyAvatar = diyChefs.Find(x => x.HeadName == preferredChefs[newIndex].Key);
            uDIYAvatarID = diyAvatar == null ? (byte)255 : diyAvatar.id;
        }

        private static void ShowWarningDialog(string message)
        {
            T17DialogBox dialog = T17DialogBoxManager.GetDialog(false);
            if (dialog != null)
            {
                dialog.Initialize("Text.Warning", $"\"{message}\"", "Text.Button.Continue", 
                    string.Empty, string.Empty, T17DialogBox.Symbols.Warning, true, true, false);
                dialog.Show();
            }
        }

        public static bool Enabled
        {
            get { return defaultChefID != 127U; }
        }

        public static void SetHideFlagsRecursive(GameObject gameObject, HideFlags hideFlags)
        {
            gameObject.hideFlags = hideFlags;
            foreach (Transform child in gameObject.transform)
                SetHideFlagsRecursive(child.gameObject, hideFlags);
        }
    }

    public class HatData : ScriptableObject
    {
        GameObject prefab;
        public string HatName;
        public static readonly string[] originalHats = new string[] { "", "None", "Santa", "Fancy", "Baseballcap" };
        public const int hatLayer = 6;

        public static HatData Create(string name)
        {
            HatData hatData = CreateInstance<HatData>();
            hatData.Init(name);
            return hatData;
        }

        void Init(string name)
        {
            string hatName = name.StartsWith("Hat_") ? name : "Hat_" + name;
            this.name = hatName;
            HatName = hatName;

            if (name == "Baseballcap")
            {
                prefab = DIYChefCustomisation.defaultTemplate.ModelPrefab.transform.FindChildStartsWithRecursive("Hat_Baseballcap").gameObject;
                return;
            }
            if (name == "Santa")
            {
                prefab = DIYChefCustomisation.defaultTemplate.ModelPrefab.transform.FindChildStartsWithRecursive("Hat_Santa").gameObject;
                return;
            }

            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string path = dir + "/Resources/HATS/" + name;

            byte[] rawData = File.ReadAllBytes($"{path}/t_{name}.png");
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(rawData);

            Mesh mesh = ObjImporter.ImportFile($"{path}/{name}.obj");
            mesh.name = hatName;
            Dictionary<string, float> matParams = new Dictionary<string, float>();
            var lines = File.ReadAllText($"{path}/m_{name}.txt").Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                if (!line.Contains("=")) continue;
                string[] parts = line.Split('=');
                if (float.TryParse(parts[1], out float result))
                    matParams.SafeAdd(parts[0], result);
            }

            GameObject hatPrefab = DIYChefCustomisation.defaultTemplate.ModelPrefab.transform.FindChildStartsWithRecursive("Hat_Baseballcap").gameObject;
            prefab = GameObject.Instantiate(hatPrefab);
            DIYChefCustomisation.SetHideFlagsRecursive(prefab, HideFlags.HideAndDontSave);
            prefab.SetActive(false);
            prefab.SetObjectLayer(hatPrefab.layer);
            prefab.name = hatName;
            prefab.transform.SetParent(null, false);

            SkinnedMeshRenderer hat = prefab.GetComponent<SkinnedMeshRenderer>();
            Material material = hat.material;
            material.SetTexture("_DiffuseMap", texture);
            foreach (var pair in matParams)
                material.SetFloat(pair.Key, pair.Value);
            
            mesh.bindposes = hat.bones.Select(x => BindPoses.GetMatrix(x.name)).ToArray();
            BoneWeight[] boneWeights = new BoneWeight[mesh.vertexCount];
            int i = hat.bones.FindIndex_Predicate(x => x.name.Equals("HatBase"));
            if (i < 0) i = 0;
            for (int j = 0; j < mesh.vertexCount; j++)
            {
                boneWeights[j].weight0 = 1;
                boneWeights[j].boneIndex0 = i;
            }
            mesh.boneWeights = boneWeights;
            hat.sharedMesh = mesh;
        }

        public static void SetChefHat(GameObject chef, ref HatMeshVisibility.VisState _hat)
        {
            ChefMeshReplacer replacer = chef.GetComponent<ChefMeshReplacer>();
            if (replacer == null) return;
            string headName = replacer.get_m_currentHeadName();
            int index = DIYChefCustomisation.preferredChefs.FindIndex_Predicate(x => x.Key == headName);
            if (index < 0) return;
            string hatName = DIYChefCustomisation.preferredChefs[index].Value;
            if (hatName != "")
                _hat = 
                    !originalHats.Contains(hatName) ? HatMeshVisibility.VisState.None : (
                    hatName == "Baseballcap" ? HatMeshVisibility.VisState.Baseball : (
                    hatName == "Santa" ? HatMeshVisibility.VisState.Festive : (
                    hatName == "Fancy" ? HatMeshVisibility.VisState.Fancy : HatMeshVisibility.VisState.None)));

            Transform parent = chef.transform.FindChildRecursive("Mesh");
            bool isUI = chef.GetComponent<HatMeshVisibility>() == null;
            GameObject hat = parent.FindChildRecursive("Hat_" + hatName)?.gameObject;
            if (hat == null && DIYChefCustomisation.diyHats.Find(x => x.HatName == "Hat_" + hatName) != null)
            {
                hat = GameObject.Instantiate(DIYChefCustomisation.diyHats.Find(x => x.HatName == "Hat_" + hatName).prefab);
                hat.SetObjectLayer(parent.gameObject.layer);
                hat.name = "Hat_" + hatName;
                hat.transform.SetParent(parent, false);
                var renderer = hat.GetComponent<SkinnedMeshRenderer>();
                renderer.rootBone = chef.transform.FindChildRecursive("HatBase");
                renderer.bones = renderer.bones.Select(x => chef.transform.FindChildRecursive(x.name)).ToArray();
                if (isUI)
                {
                    Material material = renderer.material;
                    material.shader = Shader.Find("Overcooked_2/OC2_Character_Clothes_UI");
                }
            }

            if (isUI && hat != null && hatName != "Fancy")
                hat.SetObjectLayer(hatLayer);

            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name.StartsWith("Hat_"))
                    child.gameObject.SetActive(child.name.Substring(4) == hatName);
            }
            if (hatName == "")
            {
                parent.FindChildRecursive("Hat_Fancy")?.gameObject.SetActive(true);
                parent.FindChildRecursive("Hat_Baseballcap")?.gameObject.SetActive(!isUI);
                parent.FindChildRecursive("Hat_Santa")?.gameObject.SetActive(!isUI);
            }
        }
    }

    public class DIYChefAvatarData : ChefAvatarData
    {
        public byte id = 255;
        ChefAvatarData template;
        Dictionary<string, Texture2D> textureDict; 
        Dictionary<string, Mesh> meshDict;
        Dictionary<string, Dictionary<string, float>> materialDict;

        private static readonly Dictionary<string, string> allowedPartToBone = new Dictionary<string, string>
        {
            { "Eyes", "Eyes" },
            { "Eyebrows", "Eyebrows" },
            { "Eyes2_Blinks", "Eyes2_Blinks" },
            { "Hand_Grip_L", "LeftHand" },
            { "Hand_Grip_R", "RightHand" },
            { "Hand_Open_L", "LeftHand" },
            { "Hand_Open_R", "RightHand" },
            { "Tail", "Jnt_Tail" },
            { "Head", "Head" },
            { "Head1", "Head" },
            { "Head2", "Head" },
            { "Body_NeckTie", "NeckTie" },
            { "Body_Top", "Body_Top" },
            { "Body_Bottom", "Jnt_Body" },
            { "Body_Tail", "Jnt_Tail" },
            { "Body_Body", "" },
            { "Wheelchair", "Jnt_Wheelchair" },
        };

        public static DIYChefAvatarData Create(string name)
        {
            var chefAvatarData = CreateInstance<DIYChefAvatarData>();
            chefAvatarData.Init(name);
            return chefAvatarData;
        }

        void Init(string name)
        {
            string chefName = name.StartsWith("Chef_") ? name : "Chef_" + name;
            this.name = chefName;
            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string path = dir + "/Resources/" + name;
            textureDict = new Dictionary<string, Texture2D>();
            meshDict = new Dictionary<string, Mesh>();
            materialDict = new Dictionary<string, Dictionary<string, float>>();
            DirectoryInfo folder = new DirectoryInfo(path);
            template = DIYChefCustomisation.defaultTemplate;

            var lines = File.ReadAllLines(path + "/INFO");
            foreach (var line in lines)
            {
                if (line.StartsWith("ID=") && byte.TryParse(line.Substring(3), out byte id))
                    this.id = id;
                if (line.StartsWith("BODY="))
                {
                    var bodyName = line.Substring(5);
                    var allAvatars = GameUtils.GetAvatarDirectoryData().Avatars;
                    int i = allAvatars.FindIndex_Predicate(x => x.HeadName == bodyName);
                    if (i >= 0) template = allAvatars[i];
                }
            }

            foreach (FileInfo file in folder.GetFiles("t_*.png"))
            {
                string partName = file.Name.Remove(file.Name.Length - 4, 4);
                byte[] rawData = File.ReadAllBytes(file.FullName);
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(rawData);
                textureDict.Add(partName, texture);
            }

            foreach (FileInfo file in folder.GetFiles("*.obj"))
            {
                string partName = file.Name.Remove(file.Name.Length - 4, 4);
                if (!allowedPartToBone.Any(x => partName.Equals(x.Key))) continue;
                Mesh mesh = ObjImporter.ImportFile(file.FullName);
                meshDict.Add(partName, mesh);
            }

            foreach (FileInfo file in folder.GetFiles("m_*.txt"))
            {
                string partName = file.Name.Remove(file.Name.Length - 4, 4);
                Dictionary<string, float> matParams = new Dictionary<string, float>();
                lines = File.ReadAllText(file.FullName).Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    if (!line.Contains("=")) continue;
                    string[] parts = line.Split('=');
                    if (float.TryParse(parts[1], out float result))
                        matParams.SafeAdd(parts[0], result);
                }
                materialDict.Add(partName, matParams);
            }

            HeadName = chefName;
            ColourisationMode = template.ColourisationMode;
            CreatePrefabModel(ChefMeshReplacer.ChefModelType.FrontEnd);
            CreatePrefabModel(ChefMeshReplacer.ChefModelType.UI);
            CreatePrefabModel(ChefMeshReplacer.ChefModelType.InGame);
        }

        void CreatePrefabModel(ChefMeshReplacer.ChefModelType modelType)
        {
            GameObject templatePrefab =
                modelType == ChefMeshReplacer.ChefModelType.FrontEnd ? template.FrontendModelPrefab : (
                modelType == ChefMeshReplacer.ChefModelType.UI ? template.UIModelPrefab : 
                template.ModelPrefab);
            string postfix =
                modelType == ChefMeshReplacer.ChefModelType.FrontEnd ? "_FrontEnd" : (
                modelType == ChefMeshReplacer.ChefModelType.UI ? "_UI" : "");

            GameObject prefab = GameObject.Instantiate(templatePrefab);
            DIYChefCustomisation.SetHideFlagsRecursive(prefab, HideFlags.HideAndDontSave);
            prefab.SetActive(false);
            prefab.SetObjectLayer(templatePrefab.layer);
            prefab.name = name + postfix;
            prefab.transform.SetParent(null, false);
            if (modelType == ChefMeshReplacer.ChefModelType.FrontEnd)
                FrontendModelPrefab = prefab;
            else if (modelType == ChefMeshReplacer.ChefModelType.UI)
                UIModelPrefab = prefab;
            else
                ModelPrefab = prefab;

            Transform tMesh = prefab.transform.Find("Mesh");
            for (int i = tMesh.childCount - 1; i >= 0; i--)
            {
                var child = tMesh.GetChild(i);
                if (child.name.StartsWith("Chef_") && !child.name.Equals(template.HeadName))
                    GameObject.DestroyImmediate(child.gameObject);
            }

            SkinnedMeshRenderer head = tMesh.Find(template.HeadName).GetComponent<SkinnedMeshRenderer>();
            head.gameObject.name = name;
            SkinnedMeshRenderer body = tMesh.Find("Body").GetComponent<SkinnedMeshRenderer>();

            foreach (string partName in meshDict.Keys)
            {
                Transform tPart = partName.Equals("Head") ? head.transform : head.transform.FindChildStartsWithRecursive(partName);
                string[] boneNames = partName == "Body_Body" ? 
                    new string[] { "NeckTie", "Body_Top", "Jnt_Body", "Jnt_Tail" } :
                    new string[] { allowedPartToBone[partName] };
                bool isBody = partName.StartsWith("Body_");
                if (tPart == null)
                {
                    if (partName.StartsWith("Hand")) continue;
                    SkinnedMeshRenderer partTemplate = isBody ? body : head;
                    GameObject insPart = GameObject.Instantiate(partTemplate.gameObject);
                    insPart.hideFlags = HideFlags.HideAndDontSave;
                    insPart.DestroyChildren();
                    insPart.SetObjectLayer(partTemplate.gameObject.layer);
                    insPart.name = partName;
                    insPart.transform.SetParent(isBody ? body.transform.parent : head.transform, false);
                    insPart.transform.localPosition = Vector3.zero;
                    insPart.transform.localRotation = Quaternion.identity;
                    insPart.transform.localScale = Vector3.one;
                    Transform[] bones = insPart.GetComponent<SkinnedMeshRenderer>().bones;
                    foreach (var boneName in boneNames)
                        if (!bones.Any(x => x.name.Equals(boneName)))
                        {
                            Transform bone = prefab.transform.Find("Skeleton").FindChildRecursive(boneName);
                            bones = bones.AddToArray(bone);
                        }
                    insPart.GetComponent<SkinnedMeshRenderer>().bones = bones;
                    tPart = insPart.transform;
                }

                SkinnedMeshRenderer part = tPart.GetComponent<SkinnedMeshRenderer>();

                Material material = part.material;
                if (textureDict.ContainsKey("t_" + partName))
                    material.SetTexture("_DiffuseMap", textureDict["t_" + partName]);
                else if (textureDict.ContainsKey("t_Body") && isBody)
                    material.SetTexture("_DiffuseMap", textureDict["t_Body"]);
                else
                    material.SetTexture("_DiffuseMap", textureDict["t_Head"]);
                var materialFloat =
                    materialDict.ContainsKey("m_" + partName) ? materialDict["m_" + partName] : (
                    materialDict.ContainsKey("m_Body") && isBody ? materialDict["m_Body"] :
                    materialDict["m_Head"]);
                foreach (var pair in materialFloat)
                    material.SetFloat(pair.Key, pair.Value);
            }

            if (meshDict.ContainsKey("Body_Body"))
                GameObject.DestroyImmediate(body.gameObject);

            foreach (string partName in meshDict.Keys)
            {
                Transform tPart = partName.Equals("Head") ? head.transform : tMesh.FindChildStartsWithRecursive(partName);
                string boneName = allowedPartToBone[partName];
                if (tPart == null) continue;

                SkinnedMeshRenderer part = tPart.GetComponent<SkinnedMeshRenderer>();

                Mesh meshPart = meshDict[partName];
                meshPart.name = partName;
                meshPart.bindposes = part.bones.Select(x => BindPoses.GetMatrix(x.name)).ToArray();

                BoneWeight[] boneWeights = new BoneWeight[meshPart.vertexCount];
                if (partName != "Body_Body")
                {
                    int i = part.bones.FindIndex_Predicate(x => x.name.Equals(boneName));
                    if (i < 0) i = 0;
                    for (int j = 0; j < meshPart.vertexCount; j++)
                    {
                        boneWeights[j].weight0 = 1;
                        boneWeights[j].boneIndex0 = i;
                    }
                }
                else
                {
                    int i1 = part.bones.FindIndex_Predicate(x => x.name.Equals("Body_Top"));
                    int i2 = part.bones.FindIndex_Predicate(x => x.name.Equals("Jnt_Body"));
                    for (int j = 0; j < meshPart.vertexCount; j++)
                    {
                        boneWeights[j].boneIndex0 = i1;
                        boneWeights[j].boneIndex1 = i2;
                        float y = meshPart.vertices[j].y;
                        if (y > 0.54)
                        {
                            boneWeights[j].weight0 = 1.0f;
                            boneWeights[j].weight1 = 0.0f;
                        }
                        else if (y > 0.46)
                        {
                            boneWeights[j].weight0 = 0.75f;
                            boneWeights[j].weight1 = 0.25f;
                        }
                        else if (y > 0.38)
                        {
                            boneWeights[j].weight0 = 0.5f;
                            boneWeights[j].weight1 = 0.5f;
                        }
                        else if (y > 0.30)
                        {
                            boneWeights[j].weight0 = 0.3f;
                            boneWeights[j].weight1 = 0.7f;
                        }
                        else if (y > 0.22)
                        {
                            boneWeights[j].weight0 = 0.1f;
                            boneWeights[j].weight1 = 0.9f;
                        }
                        else
                        {
                            boneWeights[j].weight0 = 0.0f;
                            boneWeights[j].weight1 = 1.0f;
                        }
                    }
                }
                meshPart.boneWeights = boneWeights;
                part.sharedMesh = meshPart;
            }
        }
    }

    public class DIYChefMessage : Serialisable
    {
        public void InitialiseClient(byte diyChefID, User user)
        {
            chefAvatarMessage.Initialise(127U, user.Machine, user.Engagement, user.Split);
            server2client = false;
            userDIYChefID[0] = diyChefID;
        }

        public void InitialiseServer()
        {
            usersChangedMessage.Initialise(ServerUserSystem.m_Users);
            server2client = true;
            Array.Copy(DIYChefCustomisation.userDIYChefID, userDIYChefID, 4);
        }

        public void Serialise(BitStreamWriter writer)
        {
            writer.Write(server2client);
            if (server2client)
            {
                usersChangedMessage.Serialise(writer);
                for (int i = 0; i < 4; i++)
                    writer.Write(userDIYChefID[i], 8);
            }
            else
            {
                chefAvatarMessage.Serialise(writer);
                writer.Write(userDIYChefID[0], 8);
            }
        }

        public bool Deserialise(BitStreamReader reader)
        {
            server2client = reader.ReadBit();
            if (server2client)
            {
                usersChangedMessage.Deserialise(reader);
                for (int i = 0; i < 4; i++)
                    userDIYChefID[i] = reader.ReadByte(8);
            }
            else
            {
                chefAvatarMessage.Deserialise(reader);
                userDIYChefID[0] = reader.ReadByte(8);
            }
            return true;
        }

        public ChefAvatarMessage chefAvatarMessage = new ChefAvatarMessage();
        public UsersChangedMessage usersChangedMessage = new UsersChangedMessage();
        public bool server2client;
        public byte[] userDIYChefID = new byte[4];
    }

    public static class BindPoses
    {
        static Dictionary<string, Matrix4x4> bindposesMatrices;

        public static Matrix4x4 GetMatrix(string name)
        {
            if (bindposesMatrices == null)
            {
                bindposesMatrices = new Dictionary<string, Matrix4x4>();
                bindposesMatrices.Add("HeldItem", new Matrix4x4(
                    new Vector4(0.90715f, 0.36555f, 0.20844f, 0.03940f),
                    new Vector4(-0.17699f, -0.11794f, 0.97712f, 0.66710f),
                    new Vector4(0.38177f, -0.92329f, -0.04229f, 0.24967f),
                    new Vector4(0.00000f, 0.00000f, 0.00000f, 1.00000f)));
                bindposesMatrices.Add("HatBase", new Matrix4x4(
                    new Vector4(0.00000f, -0.80064f, 0.59915f, 0.00000f),
                    new Vector4(0.00000f, 0.59915f, 0.80064f, 0.00000f),
                    new Vector4(-1.00000f, 0.00000f, 0.00000f, 0.00000f),
                    new Vector4(0.00000f, 0.00000f, 0.00000f, 1.00000f)));
                bindposesMatrices.Add("Head", new Matrix4x4(
                    new Vector4(0.00000f, -1.00000f, 0.00000f, 0.58614f),
                    new Vector4(-1.00000f, 0.00000f, 0.00000f, 0.00000f),
                    new Vector4(0.00000f, 0.00000f, -1.00000f, 0.00000f),
                    new Vector4(0.00000f, 0.00000f, 0.00000f, 1.00000f)));
                bindposesMatrices.Add("Eyes", new Matrix4x4(
                    new Vector4(0.00000f, -1.00000f, 0.00000f, 0.91415f),
                    new Vector4(-1.00000f, 0.00000f, 0.00000f, 0.00000f),
                    new Vector4(0.00000f, 0.00000f, -1.00000f, 0.26908f),
                    new Vector4(0.00000f, 0.00000f, 0.00000f, 1.00000f)));
                bindposesMatrices.Add("Eyes2_Blinks", new Matrix4x4(
                    new Vector4(0.00000f, -1.00000f, 0.00000f, 0.91415f),
                    new Vector4(-1.00000f, 0.00000f, 0.00000f, 0.00000f),
                    new Vector4(0.00000f, 0.00000f, -1.00000f, 0.26908f),
                    new Vector4(0.00000f, 0.00000f, 0.00000f, 1.00000f)));
                bindposesMatrices.Add("LeftHand", new Matrix4x4(
                    new Vector4(0.84462f, -0.50944f, -0.16458f, 0.66203f),
                    new Vector4(0.28076f, 0.15975f, 0.94639f, -0.02605f),
                    new Vector4(-0.45583f, -0.84555f, 0.27796f, 0.11712f),
                    new Vector4(0.00000f, 0.00000f, 0.00000f, 1.00000f)));
                bindposesMatrices.Add("RightHand", new Matrix4x4(
                    new Vector4(0.58976f, -0.07015f, -0.80453f, -0.21272f),
                    new Vector4(-0.71165f, -0.51609f, -0.47667f, 0.66973f),
                    new Vector4(-0.38177f, 0.85366f, -0.35429f, -0.15180f),
                    new Vector4(0.00000f, 0.00000f, 0.00000f, 1.00000f)));
                bindposesMatrices.Add("Jnt_Body", new Matrix4x4(
                    new Vector4(0.00000f, -1.00000f, 0.00000f, 0.00000f),
                    new Vector4(-1.00000f, 0.00000f, 0.00000f, 0.00000f),
                    new Vector4(0.00000f, 0.00000f, -1.00000f, 0.00000f),
                    new Vector4(0.00000f, 0.00000f, 0.00000f, 1.00000f)));
                bindposesMatrices.Add("Jnt_Tail", new Matrix4x4(
                    new Vector4(-0.08938f, -0.96321f, 0.25346f, 0.43950f),
                    new Vector4(-0.94307f, 0.00000f, -0.33258f, -0.13868f),
                    new Vector4(0.32035f, -0.26875f, -0.90838f, -0.28563f),
                    new Vector4(0.00000f, 0.00000f, 0.00000f, 1.00000f)));
                bindposesMatrices.Add("NeckTie", new Matrix4x4(
                    new Vector4(0.00000f, -1.00000f, 0.00000f, 0.57805f),
                    new Vector4(-1.00000f, 0.00000f, 0.00000f, 0.00000f),
                    new Vector4(0.00000f, 0.00000f, -1.00000f, 0.27746f),
                    new Vector4(0.00000f, 0.00000f, 0.00000f, 1.00000f)));
                bindposesMatrices.Add("Body_Top", new Matrix4x4(
                    new Vector4(0.00000f, -1.00000f, 0.00000f, 0.58614f),
                    new Vector4(-1.00000f, 0.00000f, 0.00000f, 0.00000f),
                    new Vector4(0.00000f, 0.00000f, -1.00000f, 0.00000f),
                    new Vector4(0.00000f, 0.00000f, 0.00000f, 1.00000f)));
                bindposesMatrices.Add("HatTop", new Matrix4x4(
                    new Vector4(0.99971f, -0.02359f, 0.00477f, 0.02496f),
                    new Vector4(0.02364f, 0.99966f, -0.01064f, -1.38675f),
                    new Vector4(-0.00452f, 0.01075f, 0.99993f, 0.35745f),
                    new Vector4(0.00000f, 0.00000f, 0.00000f, 1.00000f)));
                bindposesMatrices.Add("Hair", new Matrix4x4(
                    new Vector4(0.00000f, -1.00000f, 0.00000f, 1.16386f),
                    new Vector4(-1.00000f, 0.00000f, 0.00000f, 0.00000f),
                    new Vector4(0.00000f, 0.00000f, -1.00000f, -0.10810f),
                    new Vector4(0.00000f, 0.00000f, 0.00000f, 1.00000f)));
                bindposesMatrices.Add("Eyebrows", new Matrix4x4(
                    new Vector4(0.00000f, -1.00000f, 0.00000f, 0.98427f),
                    new Vector4(-1.00000f, 0.00000f, 0.00000f, 0.00000f),
                    new Vector4(0.00000f, 0.00000f, -1.00000f, 0.26070f),
                    new Vector4(0.00000f, 0.00000f, 0.00000f, 1.00000f)));
                bindposesMatrices.Add("Jnt_Wheelchair", new Matrix4x4(
                    new Vector4(1.00000f, 0.00000f, 0.00000f, -0.00383f),
                    new Vector4(0.00000f, 1.00000f, 0.00000f, -0.43046f),
                    new Vector4(0.00000f, 0.00000f, 1.00000f, 0.22224f),
                    new Vector4(0.00000f, 0.00000f, 0.00000f, 1.00000f)));
                bindposesMatrices.Add("Tentacles", new Matrix4x4(
                    new Vector4(0.00000f, -1.00000f, 0.00000f, 0.61447f),
                    new Vector4(-1.00000f, 0.00000f, 0.00000f, 0.00000f),
                    new Vector4(0.00000f, 0.00000f, -1.00000f, 0.02835f),
                    new Vector4(0.00000f, 0.00000f, 0.00000f, 1.00000f)));
                bindposesMatrices.Add("Base", new Matrix4x4(
                    new Vector4(1.00000f, 0.00000f, 0.00000f, 0.00000f),
                    new Vector4(0.00000f, 1.00000f, 0.00000f, 0.00000f),
                    new Vector4(0.00000f, 0.00000f, 1.00000f, 0.00000f),
                    new Vector4(0.00000f, 0.00000f, 0.00000f, 1.00000f)));
                bindposesMatrices.Add("Attach_Backpack", new Matrix4x4(
                    new Vector4(1.00000f, 0.00000f, 0.00000f, -0.00459f),
                    new Vector4(0.00000f, 1.00000f, 0.00000f, -0.43413f),
                    new Vector4(0.00000f, 0.00000f, 1.00000f, 0.22962f),
                    new Vector4(0.00000f, 0.00000f, 0.00000f, 1.00000f)));
                bindposesMatrices.Add("LeftWrist", new Matrix4x4(
                    new Vector4(1.00000f, 0.00000f, 0.00000f, 0.49847f),
                    new Vector4(0.00000f, 1.00000f, 0.00000f, -0.44045f),
                    new Vector4(0.00000f, 0.00000f, 1.00000f, -0.10106f),
                    new Vector4(0.00000f, 0.00000f, 0.00000f, 1.00000f)));
                bindposesMatrices.Add("RightWrist", new Matrix4x4(
                    new Vector4(1.00000f, 0.00000f, 0.00000f, -0.54411f),
                    new Vector4(0.00000f, 1.00000f, 0.00000f, -0.46030f),
                    new Vector4(0.00000f, 0.00000f, 1.00000f, -0.09431f),
                    new Vector4(0.00000f, 0.00000f, 0.00000f, 1.00000f)));
            }
            return bindposesMatrices.SafeGet(name, Matrix4x4.identity).transpose;
        }
    }
}
