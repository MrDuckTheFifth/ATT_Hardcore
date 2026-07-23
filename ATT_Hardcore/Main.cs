using Alta.Api.Client.HighLevel;
using Alta.Networking;
using Alta.Networking.Servers;
using HarmonyLib;
using MelonLoader;
using System;
using System.Reflection;

[assembly: MelonInfo(typeof(ATT_Hardcore.Main), "Hardcore", "1.0.0", "MrDuckTheFifth")]

namespace ATT_Hardcore {
    public class Main : MelonMod { }

    [HarmonyPatch(typeof(Player), "LinkPlayerControllerToPlayer", new Type[] { typeof(PlayerController), typeof(bool) })]
    public static class PlayerPatch {
        public static void Postfix(Player __instance) {
            __instance.PlayerCharacter.Health.Destroyed.Invoked += HardcoreManager.PlayerDied;
        }
    }

    [HarmonyPatch(typeof(ServerHandler), MethodType.Constructor, new Type[] { typeof(ISocket), typeof(IServerAccess), typeof(bool) })]
    public static class ServerHandlerPatch {
        public static void Postfix(ServerHandler __instance) {
            HardcoreManager.Init(__instance.ServerInfo);
        }
    }

    [HarmonyPatch(typeof(ServerPlayerConnectionHandlerOld), "CheckIfPlayerIsAllowedCustom", new Type[] { typeof(Connection), typeof(int), typeof(string), typeof(string), typeof(PlayerMode), typeof(PlatformTarget) })]
    public static class DenyDeadPatch {
        public static readonly Type ResultType = AccessTools.TypeByName("Alta.Networking.Servers.ServerPlayerConnectionHandlerOld+PlayerJoinResult");

        public static readonly MethodInfo CreateDenied = AccessTools.Method(ResultType, "CreateDeniedResult");

        public static bool Prefix(ref object __result, Connection connection) {
            object createDenied(string custom = null) {
                string reason = custom != null ? custom : "You died.";

                object customDeniedResult = CreateDenied.Invoke(null, new object[] { reason });

                MelonLogger.Msg("Dead player tried to join server. Denying access...");

                return customDeniedResult;
            }

            if (!HardcoreManager.isInitialized) {
                __result = createDenied("Server hasn't properly finished reading hardcore data. Please wait.");

                return false;
            }

            var list = HardcoreManager.data.deadPlayerIds;

            if (list.Count <= 0)
                return true;

            string IpAddress = (string)Traverse.Create(connection).Field("IpAddress").GetValue();

            if (string.IsNullOrWhiteSpace(IpAddress)) {
                IpAddress = "127.0.0.1";
            }

            if (list.ContainsKey(IpAddress) || list.ContainsValue(connection.UserInfo.Identifier)) {
                __result = createDenied();

                return false;
            }

            return true;
        }
    }
}
