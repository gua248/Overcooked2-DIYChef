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

namespace OC2DIYChef
{
    public static class DIYChefCustomisation
    {
        public static KeyValuePair<string, HatMeshVisibility.VisState>[] preferredChefs;
        public static uint defaultChefID = 127;
        public static List<DIYChefAvatarData> diyChefs = new List<DIYChefAvatarData>();
        public static byte[] userDIYChefID = Enumerable.Repeat((byte)255, 4).ToArray();
        private static Dictionary<string, string> dialogText;
        public static bool enableLobbySwitchChef = false;

        public static Client localClient = null;
        public static Server localServer = null;
        public const MessageType diyChefMessageType = (MessageType)67;

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
                    {"IDConflict", "ID 冲突" }
                } :
                new Dictionary<string, string>()
                {
                    {"DefaultInvalid", "Invalid default chef" },
                    {"MissingINFO", "Missing INFO file" },
                    {"MissingTexture", "Missing main texture" },
                    {"IDConflict", "ID conflict" }
                };
            DIYChefAvatarData.bindposesMatrices = LoadBindposesMatrices();
            LoadPreferredChefs(metaGameProgress);
            LoadAllDIYChef(metaGameProgress);
            ServerUserSystem.OnUserRemovedWithIndex = (GenericVoid<User, int>)Delegate.Combine(ServerUserSystem.OnUserRemovedWithIndex, new GenericVoid<User, int>(OnUserRemoved));
            ServerUserSystem.OnUserAdded = (GenericVoid<User>)Delegate.Combine(ServerUserSystem.OnUserAdded, new GenericVoid<User>(OnUserAdded));
        }

        private static void LoadPreferredChefs(MetaGameProgress metaGameProgress)
        {
            preferredChefs = new KeyValuePair<string, HatMeshVisibility.VisState>[0];
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/prefer.txt";
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
                HatMeshVisibility.VisState hatState = (HatMeshVisibility.VisState)(-1);
                if (parts.Length > 1 && parts[1].StartsWith("HAT="))
                {
                    string hat = parts[1].Substring(4);
                    hatState =
                        hat == "None" ? HatMeshVisibility.VisState.None : (
                        hat == "Fancy" ? HatMeshVisibility.VisState.Fancy : (
                        hat == "Festive" ? HatMeshVisibility.VisState.Festive : (
                        hat == "Baseball" ? HatMeshVisibility.VisState.Baseball :
                        (HatMeshVisibility.VisState)(-1))));
                }
                var pair = new KeyValuePair<string, HatMeshVisibility.VisState>(parts[0], hatState);
                if (!preferredChefs.Any(x => x.Key == pair.Key))
                {
                    preferredChefs = preferredChefs.AddItem(pair).ToArray();
                    if (preferredChefs.Length == 1)
                    {
                        ChefAvatarData[] unlocked = metaGameProgress.GetUnlockedAvatars();
                        int num = unlocked.FindIndex_Predicate(x => x.HeadName.Equals(pair.Key));
                        if (num < 0)
                        {
                            preferredChefs.AddItem(new KeyValuePair<string, HatMeshVisibility.VisState>(
                                "Chef_Male_Asian", (HatMeshVisibility.VisState)(-1)));
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

        private static Dictionary<string, Matrix4x4> LoadBindposesMatrices()
        {
            var bindposesMatrices = new Dictionary<string, Matrix4x4>();
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/Resources/bindposes.dat";

            string[] s = File.ReadAllText(path).Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < s.Length / 17; i++)
            {
                string name = s[i * 17];
                float[] m = s.Skip(i * 17 + 1).Take(16).Select(float.Parse).ToArray();
                bindposesMatrices.Add(name, new Matrix4x4()
                {
                    m00 = m[0],
                    m01 = m[1],
                    m02 = m[2],
                    m03 = m[3],
                    m10 = m[4],
                    m11 = m[5],
                    m12 = m[6],
                    m13 = m[7],
                    m20 = m[8],
                    m21 = m[9],
                    m22 = m[10],
                    m23 = m[11],
                    m30 = m[12],
                    m31 = m[13],
                    m32 = m[14],
                    m33 = m[15]
                });
            }
            return bindposesMatrices;
        }

        private static void LoadAllDIYChef(MetaGameProgress metaGameProgress)
        {
            var newPreferredChefs = new List<KeyValuePair<string, HatMeshVisibility.VisState>>();
            diyChefs.Clear();
            if (!Enabled) return;
            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string conflictMessage = string.Empty;
            DIYChefAvatarData.defaultTemplate = metaGameProgress.AvatarDirectory.Avatars[15];

            foreach (var pair in preferredChefs)
            {
                if (metaGameProgress.AvatarDirectory.Avatars.Any(x => x.HeadName == pair.Key))
                {
                    newPreferredChefs.Add(pair);
                    continue;
                }
                string path = dir + "/Resources/" + pair.Key;
                if (!Directory.Exists(path)) continue;
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

                DIYChefAvatarData avatar = new DIYChefAvatarData(pair.Key);
                var conflictChef = diyChefs.Find(x => x.id == avatar.id);
                if (avatar.id == 255)
                    conflictMessage += $"{dialogText["IDConflict"]}: {pair.Key}, 255\n";
                else if (conflictChef != null) 
                    conflictMessage += $"{dialogText["IDConflict"]}: {conflictChef.name}, {pair.Key}\n";
                else
                {
                    diyChefs.Add(avatar);
                    newPreferredChefs.Add(new KeyValuePair<string, HatMeshVisibility.VisState>(avatar.HeadName, pair.Value));
                }
            }
            if (conflictMessage != string.Empty)
                ShowWarningDialog(conflictMessage.TrimEnd());
            preferredChefs = newPreferredChefs.ToArray();
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
    }

    public class DIYChefAvatarData : ChefAvatarData
    {
        public byte id = 255;
        public bool noHat = false;
        public ChefAvatarData template;
        public static ChefAvatarData defaultTemplate;
        public static Dictionary<string, Matrix4x4> bindposesMatrices;
        private static readonly string[] allowedPart = new string[]
        {
            "Eyes", "Eyebrows", "Eyes2_Blinks",
            "Hand_Grip_L", "Hand_Grip_R", "Hand_Open_L", "Hand_Open_R",
            "Head", "Tail", "Head1", "Head2"
        };

        public DIYChefAvatarData(string name)
        {
            string chefName = name.StartsWith("Chef_") ? name : "Chef_" + name;
            this.name = chefName;
            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string path = dir + "/Resources/" + name;
            var textureDict = new Dictionary<string, Texture2D>();
            var meshDict = new Dictionary<string, Mesh>();
            var materialDict = new Dictionary<string, Dictionary<string, float>>();
            DirectoryInfo folder = new DirectoryInfo(path);
            template = defaultTemplate;

            var lines = File.ReadAllLines(path + "/INFO");
            foreach (var line in lines)
            {
                if (line.StartsWith("ID=") && byte.TryParse(line.Substring(3), out byte id))
                    this.id = id;
                if (line.StartsWith("NOHAT=TRUE"))
                    noHat = true;
                if (line.StartsWith("BODY="))
                {
                    var bodyName = line.Substring(5);
                    var allAvatars = GameUtils.GetAvatarDirectoryData().Avatars;
                    int i = allAvatars.FindIndex_Predicate(x => x.HeadName == bodyName);
                    if (i >= 0) template = allAvatars[i];
                }
            }

            foreach (FileInfo file in folder.GetFiles("*.png"))
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
                if (!allowedPart.Any(partName.Equals)) continue;
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
            FrontendModelPrefab = CreatePrefabModel(chefName, template, ChefMeshReplacer.ChefModelType.FrontEnd, textureDict, meshDict, materialDict);
            UIModelPrefab = CreatePrefabModel(chefName, template, ChefMeshReplacer.ChefModelType.UI, textureDict, meshDict, materialDict);
            ModelPrefab = CreatePrefabModel(chefName, template, ChefMeshReplacer.ChefModelType.InGame, textureDict, meshDict, materialDict);
        }

        private static void SetHideFlagsRecursive(GameObject gameObject, HideFlags hideFlags)
        {
            gameObject.hideFlags = hideFlags;
            foreach (Transform child in gameObject.transform)
                SetHideFlagsRecursive(child.gameObject, hideFlags);
        }

        private static GameObject CreatePrefabModel(
            string name,
            ChefAvatarData template,
            ChefMeshReplacer.ChefModelType modelType,
            Dictionary<string, Texture2D> textureDict,
            Dictionary<string, Mesh> meshDict,
            Dictionary<string, Dictionary<string, float>> materialDict)
        {
            GameObject templatePrefab =
                modelType == ChefMeshReplacer.ChefModelType.FrontEnd ? template.FrontendModelPrefab : (
                modelType == ChefMeshReplacer.ChefModelType.UI ? template.UIModelPrefab : 
                template.ModelPrefab);
            GameObject prefab = GameObject.Instantiate(templatePrefab);
            SetHideFlagsRecursive(prefab, HideFlags.HideAndDontSave);
            prefab.SetActive(false);
            prefab.SetObjectLayer(templatePrefab.layer);
            prefab.name = name;
            prefab.transform.SetParent(null, false);
            Transform tMesh = prefab.transform.Find("Mesh");
            for (int i = tMesh.childCount - 1; i >= 0; i--)
            {
                var child = tMesh.GetChild(i);
                if (child.name.StartsWith("Chef_") && !child.name.Equals(template.HeadName))
                    GameObject.DestroyImmediate(child.gameObject);
            }

            SkinnedMeshRenderer head = tMesh.Find(template.HeadName).GetComponent<SkinnedMeshRenderer>();
            head.gameObject.name = name;

            foreach (string partName in meshDict.Keys)
            {
                Transform tPart = partName.Equals("Head") ? head.transform : head.transform.FindChildStartsWithRecursive(partName);
                string boneName =
                    partName.StartsWith("Head") ? "Head" : (
                    partName.Equals("Tail") ? "Jnt_Tail" : (
                    !partName.StartsWith("Hand") ? partName : (
                    partName.EndsWith("L") ? "LeftHand" : "RightHand")));
                if (tPart == null)
                {
                    if (partName.StartsWith("Hand")) continue;
                    Transform[] bones = head.GetComponent<SkinnedMeshRenderer>().bones;
                    if (!bones.Any(x => x.name.Equals(boneName)))
                    {
                        Transform bone = prefab.transform.Find("Skeleton").FindChildRecursive(boneName);
                        if (bone == null) continue;
                        head.GetComponent<SkinnedMeshRenderer>().bones = bones.AddToArray(bone);
                    }
                    GameObject insPart = GameObject.Instantiate(head).gameObject;
                    insPart.hideFlags = HideFlags.HideAndDontSave;
                    insPart.DestroyChildren();
                    insPart.SetObjectLayer(head.gameObject.layer);
                    insPart.name = partName;
                    insPart.transform.SetParent(head.transform, false);
                    insPart.transform.localPosition = Vector3.zero;
                    insPart.transform.localRotation = Quaternion.identity;
                    insPart.transform.localScale = Vector3.one;
                    tPart = insPart.transform;
                }
                
                SkinnedMeshRenderer part = tPart.GetComponent<SkinnedMeshRenderer>();
                Mesh meshPart = meshDict[partName];
                //meshPart.bindposes = part.sharedMesh.bindposes;
                meshPart.bindposes = part.bones.Select(x => bindposesMatrices[x.name]).ToArray();
                int i = part.bones.FindIndex_Predicate(x => x.name.Equals(boneName));
                if (i < 0) i = 0;
                BoneWeight[] boneWeights = new BoneWeight[meshPart.vertexCount];
                for (int j = 0; j < meshPart.vertexCount; j++)
                {
                    boneWeights[j].weight0 = 1;
                    boneWeights[j].boneIndex0 = i;
                }
                meshPart.boneWeights = boneWeights;
                part.sharedMesh = GameObject.Instantiate(meshPart);
                
                Material material = part.material;
                if (textureDict.ContainsKey("t_" + partName))
                    material.SetTexture("_DiffuseMap", textureDict["t_" + partName]);
                else
                    material.SetTexture("_DiffuseMap", textureDict["t_Head"]);
                if (materialDict.ContainsKey("m_" + partName))
                {
                    foreach (var pair in materialDict["m_" + partName])
                        material.SetFloat(pair.Key, pair.Value);
                }
                else
                {
                    foreach (var pair in materialDict["m_Head"])
                        material.SetFloat(pair.Key, pair.Value);
                }
            }

            return prefab;
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
}
