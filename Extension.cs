using BitStream;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using Team17.Online;

namespace OC2DIYChef.Extension
{
    public static class BitStreamReaderExtension
    {
        private static readonly FieldInfo fieldInfo_bufferLengthInBits = AccessTools.Field(typeof(BitStreamReader), "_bufferLengthInBits");
        private static readonly FieldInfo fieldInfo_cbitsInPartialByte = AccessTools.Field(typeof(BitStreamReader), "_cbitsInPartialByte");
        private static readonly FieldInfo fieldInfo_partialByte = AccessTools.Field(typeof(BitStreamReader), "_partialByte");
        private static readonly FieldInfo fieldInfo_byteArray = AccessTools.Field(typeof(BitStreamReader), "_byteArray");
        private static readonly FieldInfo fieldInfo_byteArrayIndex = AccessTools.Field(typeof(BitStreamReader), "_byteArrayIndex");

        public static byte ReadByteAhead(this BitStreamReader instance, int countOfBits)
        {
            if (instance.EndOfStream) return 0;
            if (countOfBits > 8 || countOfBits <= 0) return 0;
            if ((long)countOfBits > (long)(ulong)(uint)fieldInfo_bufferLengthInBits.GetValue(instance)) return 0;
            byte b;

            int cbitsInPartialByte = (int)fieldInfo_cbitsInPartialByte.GetValue(instance);
            byte partialByte = (byte)fieldInfo_partialByte.GetValue(instance);
            if (cbitsInPartialByte >= countOfBits)
            {
                int num = 8 - countOfBits;
                b = (byte)(partialByte >> num);
            }
            else
            {
                byte[] byteArray = (byte[])fieldInfo_byteArray.GetValue(instance);
                byte b2 = byteArray[(int)fieldInfo_byteArrayIndex.GetValue(instance)];
                int num2 = 8 - countOfBits;
                b = (byte)(partialByte >> num2);
                int num3 = num2 + cbitsInPartialByte;
                b |= (byte)(b2 >> num3);
            }
            return b;
        }
    }

    public static class ClientEmoteWheelExtension
    {
        static readonly FieldInfo fieldInfo_m_downButton = AccessTools.Field(typeof(ClientEmoteWheel), "m_downButton");
        static readonly FieldInfo fieldInfo_m_upButton = AccessTools.Field(typeof(ClientEmoteWheel), "m_upButton");
        static readonly FieldInfo fieldInfo_m_emoteWheel = AccessTools.Field(typeof(ClientEmoteWheel), "m_emoteWheel");
        static readonly FieldInfo fieldInfo_m_emoteSelector = AccessTools.Field(typeof(ClientEmoteWheel), "m_emoteSelector");

        public static bool DownJustPressed(this ClientEmoteWheel instance)
        {
            var button = (ILogicalButton)fieldInfo_m_downButton.GetValue(instance);
            return button.JustPressed();
        }

        public static bool UpJustPressed(this ClientEmoteWheel instance)
        {
            var button = (ILogicalButton)fieldInfo_m_upButton.GetValue(instance);
            return button.JustPressed();
        }

        public static EmoteWheel get_m_emoteWheel(this ClientEmoteWheel instance)
        {
            return (EmoteWheel)fieldInfo_m_emoteWheel.GetValue(instance);
        }

        public static EmoteSelector get_m_emoteSelector(this ClientEmoteWheel instance)
        {
            return (EmoteSelector)fieldInfo_m_emoteSelector.GetValue(instance);
        }
    }

    public static class MultiplayerControllerExtension
    {
        private static readonly MethodInfo methodInfo_IsServer = AccessTools.Method(typeof(MultiplayerController), "IsServer");
        
        public static bool IsServer(this MultiplayerController instance)
        {
            return (bool)methodInfo_IsServer.Invoke(instance, null);
        }
    }

    public static class UIPlayerMenuBehaviourExtension
    {
        private static readonly FieldInfo fieldInfo_m_User = AccessTools.Field(typeof(UIPlayerMenuBehaviour), "m_User");
        private static readonly FieldInfo fieldInfo_m_chef = AccessTools.Field(typeof(UIPlayerMenuBehaviour), "m_chef");
        private static readonly FieldInfo fieldInfo_m_AmbientColor = AccessTools.Field(typeof(UIPlayerMenuBehaviour), "m_AmbientColor");

        public static User get_m_User(this UIPlayerMenuBehaviour instance)
        {
            return (User)fieldInfo_m_User.GetValue(instance);
        }

        public static FrontendChef get_m_chef(this UIPlayerMenuBehaviour instance)
        {
            return (FrontendChef)fieldInfo_m_chef.GetValue(instance);
        }

        public static void set_m_chef(this UIPlayerMenuBehaviour instance, FrontendChef chef)
        {
            fieldInfo_m_chef.SetValue(instance, chef);
        }

        public static Color get_m_AmbientColor(this UIPlayerMenuBehaviour instance)
        {
            return (Color)fieldInfo_m_AmbientColor.GetValue(instance);
        }
    }

    public static class ChefMeshReplacerExtension
    {
        private static readonly FieldInfo fieldInfo_m_currentHeadName = AccessTools.Field(typeof(ChefMeshReplacer), "m_currentHeadName");

        public static string get_m_currentHeadName(this ChefMeshReplacer instance)
        {
            return (string)fieldInfo_m_currentHeadName.GetValue(instance) ?? "";
        }
    }

    public static class FrontendChefCustomisationExtension
    {
        private static readonly FieldInfo fieldInfo_m_unlockedAvatars = AccessTools.Field(typeof(FrontendChefCustomisation), "m_unlockedAvatars");
        private static readonly FieldInfo fieldInfo_m_chefSelection = AccessTools.Field(typeof(FrontendChefCustomisation), "m_chefSelection");
        private static readonly FieldInfo fieldInfo_m_actualPlayer = AccessTools.Field(typeof(FrontendChefCustomisation), "m_actualPlayer");

        public static ChefAvatarData[] get_m_unlockedAvatars(this FrontendChefCustomisation instance)
        {
            return (ChefAvatarData[])fieldInfo_m_unlockedAvatars.GetValue(instance);
        }

        public static int get_m_actualPlayer(this FrontendChefCustomisation instance)
        {
            return (int)fieldInfo_m_actualPlayer.GetValue(instance);
        }

        public static void set_m_chefSelection(this FrontendChefCustomisation instance, int index)
        {
            fieldInfo_m_chefSelection.SetValue(instance, index);
        }
    }
}