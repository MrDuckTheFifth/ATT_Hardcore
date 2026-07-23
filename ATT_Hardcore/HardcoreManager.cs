using Alta.Api.DataTransferModels.Models.Responses;
using Alta.Character;
using Alta.Networking;
using Alta.Networking.Servers;
using Alta.Utilities;
using HarmonyLib;
using MelonLoader;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ATT_Hardcore {
    internal class HC_Json : IAltaFileFormat/*, IAutoSave*/ {
        /// <summary>
        /// string = IpAddress
        /// 
        /// <br></br>
        /// 
        /// int = playerId
        /// </summary>
        internal Dictionary<string, int> deadPlayerIds = new Dictionary<string, int>();

        //public HC_Json() {
        //    StreamerManager.AutoSavingBehaviours.Add(this);
        //}

        //public void AutoSave() {
        //    HardcoreManager.Save();
        //}

        public override IAltaFileFormat Clone() {
            HC_Json clone = new HC_Json();

            clone.deadPlayerIds = new Dictionary<string, int>(deadPlayerIds);

            return clone;
        }

        public override void ReadFrom(FileInfo info, Stream stream) {
            using BinaryReader reader = new BinaryReader(stream);

            int arrayCount = reader.ReadInt32();

            deadPlayerIds.Clear();

            for (int i = 0; i < arrayCount; i++) {
                string IpAddress = reader.ReadString();
                int playerId = reader.ReadInt32();

                deadPlayerIds.Add(IpAddress, playerId);
            }
        }

        public override void WriteTo(Stream stream, CancellationToken token) {
            using BinaryWriter writer = new BinaryWriter(stream);

            writer.Write(deadPlayerIds.Count);

            foreach (var pair in deadPlayerIds) {
                writer.Write(pair.Key);
                writer.Write(pair.Value);
            }
        }
    }

    public static class HardcoreManager {
        private static IAltaFile hardcoreFile;

        private static IAltaFolder savesFolder;

        internal static HC_Json data { get; private set; }

        internal static bool isInitialized { get; private set; }

        private static bool PassesChecks() {
            return NetworkSceneManager.IsServer && isInitialized;
        }

        internal static async void Init(GameServerInfo server) {
            if (!NetworkSceneManager.IsServer)
                return;

            ServerHandler.Current.ServerConfig.Settings.DropAllOnDeath = true;

            IAltaFolder serverFolder = AltaIO.ServersFolder;
            IAltaFolder serverIdFolder = serverFolder.GetSubfolder(server.Identifier.ToString());
            savesFolder = serverIdFolder.GetSubfolder("Save");

            hardcoreFile = savesFolder.GetFile("DeadPlayers.bytes");

            data = await hardcoreFile.ReadAsync<HC_Json>();

            if (data is null) {
                data = new HC_Json();

                hardcoreFile.Content = data;
            }

            isInitialized = true;
        }

        // another one bites the dust
        internal static void PlayerDied(HealthObject healthObject) {
            if (!PassesChecks())
                return;

            Player player = healthObject.Entity.Parent.gameObject.GetComponent<PlayerCharacter>().NetworkPlayer;

            Connection connection = player.ConnectionToRemotePlayer;

            string IpAddress = (string)Traverse.Create(connection).Field("IpAddress").GetValue();

            // MrEvil
            if (string.IsNullOrWhiteSpace(IpAddress)) {
                IpAddress = "127.0.0.1";
            }

            data.deadPlayerIds.Add(IpAddress, (int)player.ConnectionToRemotePlayer.UserInfo.Identifier);

            connection.Disconnect("You died.");

            connection.Socket.DestroyConnection(connection, true);

            MelonLogger.Msg($"{player.UserInfo.Username} has died!");

            Save();
        }

        internal static void Save() {
            if (!PassesChecks())
                return;

            hardcoreFile?.WriteAsync();
        }
    }
}