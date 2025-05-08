using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using OC2DIYChef.Extension;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using Team17.Online.Multiplayer;
using UnityEngine;
using BitStream;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine.SceneManagement;

namespace OC2DIYChef
{
    public static class OnlinePatch
    {
        public static void Patch(Harmony harmony)
        {
            harmony.PatchAll(typeof(OnlinePatch));
            harmony.Patch(AccessTools.Method("Mailbox:OnMessageReceived"), new HarmonyMethod(typeof(OnlinePatch).GetMethod("MailboxOnMessageReceivedPrefix")), null);
            harmony.Patch(AccessTools.Method("ClientMessenger:OnClientStarted"), null, new HarmonyMethod(typeof(OnlinePatch).GetMethod("ClientMessengerOnClientStartedPostfix")));
            harmony.Patch(AccessTools.Method("ServerMessenger:OnServerStarted"), null, new HarmonyMethod(typeof(OnlinePatch).GetMethod("ServerMessengerOnServerStartedPostfix")));
            harmony.Patch(AccessTools.Method("ServerMessenger:OnServerStopped"), null, new HarmonyMethod(typeof(OnlinePatch).GetMethod("ServerMessengerOnServerStoppedPostfix")));
        }

        public static bool MailboxOnMessageReceivedPrefix(MessageType type, Serialisable message)
        {
            if (type != DIYChefCustomisation.diyChefMessageType)
                return true;
            DIYChefMessage diyChefMessage = (DIYChefMessage)message;
            DIYChefCustomisation.OnMessageReceived(diyChefMessage);
            return false;
        }

        public static void ClientMessengerOnClientStartedPostfix(Client client)
        {
            DIYChefCustomisation.localClient = client;
        }

        public static void ServerMessengerOnServerStartedPostfix(Server server)
        {
            DIYChefCustomisation.localServer = server;
        }

        public static void ServerMessengerOnServerStoppedPostfix()
        {
            DIYChefCustomisation.localServer = null;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Message), "Deserialise")]
        public static bool MessageDeserialisePatch(BitStreamReader reader, Message __instance, ref bool __result, ref bool __runOriginal)
        {
            if (!__runOriginal) return false;
            var messageType = (MessageType)reader.ReadByteAhead(8);
            if (messageType == DIYChefCustomisation.diyChefMessageType)
            {
                __instance.Type = (MessageType)reader.ReadByte(8);
                __instance.Payload = new DIYChefMessage();
                __instance.Payload.Deserialise(reader);
                __result = true;
                return false;
            }
            else
            {
                return true;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NetworkMessageTracker), "TrackSentGlobalEvent")]
        public static bool NetworkMessageTrackerTrackSentGlobalEventPatch(MessageType type)
        {
            return type != DIYChefCustomisation.diyChefMessageType;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NetworkMessageTracker), "TrackReceivedGlobalEvent")]
        public static bool NetworkMessageTrackerTrackReceivedGlobalEventPatch(MessageType type)
        {
            return type != DIYChefCustomisation.diyChefMessageType;
        }

    }

    public static class Patch
    {
        //[HarmonyPostfix]
        //[HarmonyPatch(typeof(PlayerRespawnBehaviour), "Initialise")]
        //public static void PlayerRespawnBehaviourInitialisePatch(PlayerRespawnBehaviour __instance)
        //{
        //    Shader shader = Shader.Find("Overcooked_2/OC2_Character_Skin");
        //    if (shader != null)
        //        __instance.m_fadeShader = shader;
        //}

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIPlayerRootMenu), "Start")]
        public static void UIPlayerRootMenuStartPatch(UIPlayerRootMenu __instance)
        {
            Light[] lights = GameObject.FindObjectsOfType<Light>();
            bool hasHatLight = lights.Any(x => x.name == "hat light");

            foreach (var light in lights)
            {
                if (light.name == "day light" && (light.gameObject.scene.name == "InGameMenu" || light.gameObject.scene.name == "Lobbies"))
                {
                    light.GetComponent<Light>().intensity = 0.5f;
                    if (!hasHatLight)
                    {
                        var hatLight = GameObject.Instantiate(light.gameObject);
                        SceneManager.MoveGameObjectToScene(hatLight, light.gameObject.scene);
                        hatLight.SetObjectLayer(light.gameObject.layer);
                        hatLight.name = "hat light";
                        hatLight.transform.SetParent(light.transform.parent);
                        hatLight.GetComponent<Light>().intensity = 1.8f;
                        hatLight.GetComponent<Light>().cullingMask = 1 << HatData.hatLayer;
                    }
                    break;
                }
            }
            Camera camera = (Camera)AccessTools.Field(typeof(UIPlayerRootMenu), "m_chefCamera").GetValue(__instance);
            camera.cullingMask |= 1 << HatData.hatLayer;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(FrontendChef), "SetChefHat")]
        public static void FrontendChefSetChefHatPatch(FrontendChef __instance, ref HatMeshVisibility.VisState _hat)
        {
            HatData.SetChefHat(__instance.gameObject, ref _hat);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BodyMeshVisibility), "Awake")]
        public static void BodyMeshVisibilityAwakePatch(BodyMeshVisibility __instance)
        {
            if (!__instance.m_meshes.Contains("Body_NeckTie"))
                __instance.m_meshes = __instance.m_meshes.AddRangeToArray(new string[]
                {
                    "Body_NeckTie",
                    "Body_Top",
                    "Body_Bottom",
                    "Body_Tail",
                    "Body_Body",
                });
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(ChefMeshReplacer), "ReplaceModel")]
        public static IEnumerable<CodeInstruction> ChefMeshReplacerReplaceModelPatch(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            codes.RemoveRange(267, 3);
            codes.RemoveRange(258, 6);
            return codes.AsEnumerable();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ChefMeshReplacer), "AssignBodyColour")]
        public static bool ChefMeshReplacerAssignBodyColourPatch(GameObject _root, GameSession.SelectedChefData _data)
        {
            _root.SetActive(true);  // prefab is an inactive gameobject
            int maskParamID = Shader.PropertyToID("_MaskColor");
            Transform tMesh = _root.transform.Find("Mesh");
            for (int i = 0; i < tMesh.childCount; i++)
                if (tMesh.GetChild(i).name.StartsWith("Body"))
                {
                    GameObject gameObject = tMesh.GetChild(i).gameObject;
                    if (gameObject.name.Equals("Body") && // diy body uses SwapColourValue
                        _data.Character.ColourisationMode == ChefMeshReplacer.ChefColourisationMode.SwapMaterial)
                    {
                        gameObject.RequireComponent<SkinnedMeshRenderer>().material = _data.Colour.ChefMaterial;
                    }
                    else 
                    {
                        Material material = gameObject.RequireComponent<SkinnedMeshRenderer>().material;
                        material.SetColor(maskParamID, _data.Colour.MaskColour);
                    }
                }
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ClientMeshVisibilityBase<HatMeshVisibility.VisState>), "Setup")]
        public static void ClientHatMeshVisibilitySetupPatch(ClientHatMeshVisibility __instance, ref HatMeshVisibility.VisState _state)
        {
            HatData.SetChefHat(__instance.gameObject, ref _state);
        }

        public static void PatchInternal(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method("DisconnectionHandler:GoOffline"), new HarmonyMethod(typeof(Patch).GetMethod("DisconnectionHandlerGoOfflinePrefix")), null);
        }

        public static void DisconnectionHandlerGoOfflinePrefix()
        {
            byte[] newDIYChefID = Enumerable.Repeat((byte)255, 4).ToArray();
            int i1 = 0;
            FastList<User> clientUsers = ClientUserSystem.m_Users;
            for (int i = 0; i < clientUsers.Count; i++)
            {
                User user = clientUsers._items[i];
                if (user.IsLocal) newDIYChefID[i1++] = DIYChefCustomisation.userDIYChefID[i];
            }
            Array.Copy(newDIYChefID, DIYChefCustomisation.userDIYChefID, 4);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ServerUserSystem), "ResetUsersToOfflineState")]
        public static void ServerUserSystemResetUsersToOfflineStatePrefix(out byte[] __state)
        {
            __state = Enumerable.Repeat((byte)255, 4).ToArray();
            int i1 = 0;
            FastList<User> clientUsers = ClientUserSystem.m_Users;
            for (int i = 0; i < clientUsers.Count; i++)
            {
                User user = clientUsers._items[i];
                if (user.IsLocal) __state[i1++] = DIYChefCustomisation.userDIYChefID[i];
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ServerUserSystem), "ResetUsersToOfflineState")]
        public static void ServerUserSystemResetUsersToOfflineStatePostfix(byte[] __state)
        {
            Array.Copy(__state, DIYChefCustomisation.userDIYChefID, 4);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(NetworkSystemConfigurator), "Client")]
        public static void NetworkSystemConfiguratorClientPatch()
        {
            for (int i = 0; i < ClientUserSystem.m_Users.Count; i++)
            {
                User user = ClientUserSystem.m_Users._items[i];
                if (user.IsLocal)
                    DIYChefCustomisation.SendClientDIYChefMessage(DIYChefCustomisation.userDIYChefID[i], user);
            }
        }

        public static bool flag_ClientUserSystemOnUsersChangedPatch = true;
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ClientUserSystem), "OnUsersChanged")]
        public static void ClientUserSystemOnUsersChangedPatch(Serialisable message)
        {
            if (flag_ClientUserSystemOnUsersChangedPatch)
            {
                var multiplayerController = GameUtils.RequestManager<MultiplayerController>();
                if (multiplayerController == null || multiplayerController.IsServer()) return;
                var usersChangedMessage = (UsersChangedMessage)message;
                byte[] newDIYChefID = Enumerable.Repeat((byte)255, 4).ToArray();
                FastList<User> users = ClientUserSystem.m_Users;

                for (int i = 0; i < usersChangedMessage.m_Users.Count; i++)
                {
                    var userData = usersChangedMessage.m_Users._items[i];
                    var Machine = userData.machine;
                    var Engagement = userData.slot;
                    var Split = userData.splitStatus;
                    User user = UserSystemUtils.FindUser(users, null, Machine, Engagement, TeamID.Count, Split);
                    if (user == null && Split != User.SplitStatus.SplitPadGuest)
                        user = UserSystemUtils.FindUser(users, null, Machine, Engagement, TeamID.Count);
                    if (user != null)
                    {
                        int i1 = users.FindIndex(x => x == user);
                        newDIYChefID[i] = DIYChefCustomisation.userDIYChefID[i1];
                    }
                }
                Array.Copy(newDIYChefID, DIYChefCustomisation.userDIYChefID, 4);
            }
            flag_ClientUserSystemOnUsersChangedPatch = true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIPlayerMenuBehaviour), "SetupChefModel")]
        public static bool UIPlayerMenuBehaviourSetupChefModelPatch(UIPlayerMenuBehaviour __instance, bool _force)
        {
            User user = __instance.get_m_User();
            FrontendChef chef = __instance.get_m_chef();
            if (user == null)
            {
                if (chef != null && chef.ChefModel != null)
                    chef.ChefModel.SetActive(false);
                return false;
            }
            if (chef != null && chef.ChefModel != null)
                chef.ChefModel.SetActive(true);
            __instance.gameObject.layer = LayerMask.NameToLayer(__instance.m_rootMenu.m_uiChefLayer);
            chef = __instance.gameObject.RequireComponent<FrontendChef>();
            __instance.set_m_chef(chef);
            GameObject chefModel = chef.ChefModel;

            GameSession.SelectedChefData selectedChefData = user.SelectedChefData;
            int player = ClientUserSystem.m_Users.FindIndex(x => x == user);
            if (player != -1)
            {
                byte diyChefID = DIYChefCustomisation.userDIYChefID[player];
                var diyChef = DIYChefCustomisation.diyChefs.Find(x => x.id == diyChefID);
                if (diyChef != null)
                {
                    selectedChefData = new GameSession.SelectedChefData(diyChef, selectedChefData.Colour);
                }
            }

            chef.SetChefData(selectedChefData, _force);
            chef.SetChefHat(HatMeshVisibility.VisState.Fancy);
            chef.SetShaderMode(FrontendChef.ShaderMode.eUI);
            chef.SetUIChefAmbientLighting(__instance.get_m_AmbientColor());
            if (chef.ChefModel != chefModel && player != -1)
                chef.SetAnimationSet((FrontendChef.AnimationSet)player);

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(FrontendPlayerLobby), "RecalculateChefAvatars")]
        public static bool FrontendPlayerLobbyRecalculateChefAvatarsPatch(FrontendPlayerLobby __instance, bool _force)
        {
            if (__instance.m_PlayerChefs == null) return false;
            for (int i = 0; i < __instance.m_PlayerChefs.Length; i++)
            {
                FrontendChef y = __instance.m_PlayerChefs[i];
                if (y == null) continue;
                if (i < ClientUserSystem.m_Users.Count)
                {
                    User user = ClientUserSystem.m_Users._items[i];
                    GameSession.SelectedChefData selectedChefData = user.SelectedChefData;
                    byte diyChefID = DIYChefCustomisation.userDIYChefID[i];
                    var diyChef = DIYChefCustomisation.diyChefs.Find(x => x.id == diyChefID);
                    if (diyChef != null)
                    {
                        selectedChefData = new GameSession.SelectedChefData(diyChef, selectedChefData.Colour);
                    }
                    if (selectedChefData != null)
                        __instance.m_PlayerChefs[i].SetChefData(selectedChefData, _force);
                    __instance.m_PlayerChefs[i].SetChefHat(__instance.m_ChefHat);
                    __instance.m_PlayerChefs[i].SetAnimationSet((FrontendChef.AnimationSet)i);
                    __instance.m_PlayerChefs[i].gameObject.SetActive(true);
                }
                else
                {
                    __instance.m_PlayerChefs[i].gameObject.SetActive(false);
                }
            }
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ClientKitchenLoader), "ReplaceMesh")]
        public static bool ClientKitchenLoaderReplaceMeshPatch(uint uEntityID, ref GameSession.SelectedChefData selectedChef)
        {
            FastList<User> users = ClientUserSystem.m_Users;
            int player = users.FindIndex(x => x.EntityID == uEntityID || x.Entity2ID == uEntityID);
            byte diyChefID = DIYChefCustomisation.userDIYChefID[player];
            var diyChef = DIYChefCustomisation.diyChefs.Find(x => x.id == diyChefID);
            if (diyChef != null)
            {
                selectedChef = new GameSession.SelectedChefData(diyChef, selectedChef.Colour);
            }
            return true;
        }

        private static readonly MethodInfo methodInfo_ClientMessengerChefAvatar = AccessTools.Method("ClientMessenger:ChefAvatar");

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NetworkUtils), "SelectRandomAvatar")]
        public static bool NetworkUtilsSelectRandomAvatarPatch()
        {
            if (!DIYChefCustomisation.Enabled)
                return true;
            for (int i = 0; i < ClientUserSystem.m_Users.Count; i++)
            {
                User user = ClientUserSystem.m_Users._items[i];
                if (user.IsLocal && user.SelectedChefAvatar == 127U)
                    methodInfo_ClientMessengerChefAvatar.Invoke(null, new object[] { DIYChefCustomisation.defaultChefID, user });
            }
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ClientEmoteWheel), "Update")]
        public static void ClientEmoteWheelUpdatePatch(ClientEmoteWheel __instance)
        {
            if (!DIYChefCustomisation.Enabled || !DIYChefCustomisation.enableLobbySwitchChef) return;
            EmoteWheel emoteWheel = __instance.get_m_emoteWheel();
            EmoteSelector emoteSelector = __instance.get_m_emoteSelector();
            if (!emoteWheel.ForUI || !emoteWheel.IsLocal || emoteSelector.IsActive()) return;
            int step = __instance.UpJustPressed() ? 1 : (__instance.DownJustPressed() ? -1 : 0);
            if (step == 0) return;
            if (Mathf.Abs(__instance.XValue()) > 0.01 || Mathf.Abs(__instance.YValue()) < 0.99) return;

            int player = (int)emoteWheel.m_player;
            FastList<User> users = ClientUserSystem.m_Users;
            if (player >= users.Count) return;
            MetaGameProgress metaGameProgress = GameUtils.GetMetaGameProgress();
            if (metaGameProgress == null) return;
            var unlockedAvatars = metaGameProgress.GetUnlockedAvatars();
            if (unlockedAvatars.Length == 0) return;
            DIYChefCustomisation.SwitchChef(player, step, out uint uAvatarID, out byte uDIYAvatarID);

            User user = users._items[player];
            var m_ChefAvatar = new ChefAvatarMessage();
            m_ChefAvatar.Initialise(uAvatarID, user.Machine, user.Engagement, user.Split);
            DIYChefCustomisation.localClient.SendMessageToServer(MessageType.ChefAvatar, m_ChefAvatar, true);
            DIYChefCustomisation.userDIYChefID[player] = uDIYAvatarID;
            DIYChefCustomisation.SendClientDIYChefMessage(uDIYAvatarID, user);
            ClientUserSystem.usersChanged();
        }

        public static class FrontendChefCustomisationPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch(typeof(FrontendChefCustomisation), "OnClickLeftButton")]
            public static bool OnClickLeftButtonPrefix(FrontendChefCustomisation __instance)
            {
                OnClickLeftOrRightButtonPrefix(__instance, -1);
                return true;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(FrontendChefCustomisation), "OnClickRightButton")]
            public static bool OnClickRightButtonPrefix(FrontendChefCustomisation __instance)
            {
                OnClickLeftOrRightButtonPrefix(__instance, 1);
                return true;
            }

            public static void OnClickLeftOrRightButtonPrefix(FrontendChefCustomisation __instance, int step)
            {
                if (!DIYChefCustomisation.Enabled) return;
                int player = __instance.get_m_actualPlayer();
                FastList<User> users = ClientUserSystem.m_Users;
                if (player >= users.Count) return;
                MetaGameProgress metaGameProgress = GameUtils.GetMetaGameProgress();
                if (metaGameProgress == null) return;
                var allAvatars = GameUtils.GetAvatarDirectoryData().Avatars;
                var unlockedAvatars = __instance.get_m_unlockedAvatars();
                if (unlockedAvatars.Length == 0) return;

                DIYChefCustomisation.SwitchChef(player, step, out uint uAvatarID, out byte uDIYAvatarID);
                int newIndex = unlockedAvatars.FindIndex_Predicate(x => x == allAvatars[uAvatarID]);
                __instance.set_m_chefSelection(newIndex - step);
                DIYChefCustomisation.userDIYChefID[player] = uDIYAvatarID;
                DIYChefCustomisation.SendClientDIYChefMessage(uDIYAvatarID, users._items[player]);
                ClientUserSystem.usersChanged();
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MetaGameProgress), "ByteLoad")]
        public static void MetaGameProgressByteLoadPatch(MetaGameProgress __instance)
        {
            DIYChefCustomisation.OnAwake(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ChefAvatarData), "IsAvailableOnThisPlatform")]
        public static bool ChefAvatarDataIsAvailableOnThisPlatformPatch(ref bool __result)
        {
            __result = true;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TimeManager), "Update")]
        public static bool TimeManagerUpdatePatch() => false;

        private static readonly MethodInfo methodInfoDeltaTime = AccessTools.PropertyGetter(typeof(Time), "deltaTime");
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(MaterialTimeController), "Update")]
        public static IEnumerable<CodeInstruction> MaterialTimeControllerUpdatePatch(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            codes[6] = new CodeInstruction(OpCodes.Call, methodInfoDeltaTime);
            return codes.AsEnumerable();
        }
    }
}
