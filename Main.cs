using AOSharp.Clientless;
using AOSharp.Clientless.Chat;
using AOSharp.Clientless.Logging;
using AOSharp.Common.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using System;
using System.Linq;

namespace InviteBot
{
    public class Main : ClientlessPluginEntry
    {
        public override void Init(string pluginDir)
        {
            Logger.Information("InviteBot::Init");
            Client.OnUpdate += OnUpdate;
            Client.Chat.PrivateMessageReceived += (_, msg) => HandlePrivateMessage(msg);
            Client.Chat.VicinityMessageReceived += (_, msg) => Logger.Debug($"{msg.SenderName}: {msg.Message}");
            Client.Chat.GroupMessageReceived += (_, msg) => HandleGroupMessage(msg);
            DynelManager.DynelSpawned += OnDynelSpawned;
            DynelManager.DynelDespawned += OnDynelDespawned;
        }

        private void OnUpdate(object _, double deltaTime)
        {
            //Logger.Debug("OnUpdate");
        }

        private void HandlePrivateMessage(PrivateMessage msg)
        {
            if (!msg.Message.StartsWith("!")) return;

            string[] commandParts = msg.Message.Split(' ');
            string command = commandParts[0].Remove(0, 1).ToLower();

            switch (command)
            {
                case "stand":
                case "sit":
                    HandleSitStandRequest(msg, command);
                    break;
                case "orgchat":
                    Client.SendOrgMessage("I've said something in org chat!");
                    break;
                case "invite":
                    HandleInviteRequest(msg, commandParts);
                    break;
                default:
                    Client.SendPrivateMessage(msg.SenderId, "Unknown command. Please use !help to see available commands.");
                    break;
            }
        }

        private void HandleSitStandRequest(PrivateMessage msg, string command)
        {
            Logger.Information($"Received {command} request from {msg.SenderName}");
            DynelManager.LocalPlayer.MovementComponent.ChangeMovement(command == "stand" ? MovementAction.LeaveSit : MovementAction.SwitchToSit);
        }

        private void HandleInviteRequest(PrivateMessage msg, string[] commandParts)
        {
            Logger.Information($"Received invite request from {msg.SenderName}");

            if (commandParts.Length < 2)
            {
                Logger.Error($"Invalid invite command. Usage: !invite <playername>");
                return;
            }

            string playerName = commandParts[1];
            var matchingPlayer = DynelManager.Characters.FirstOrDefault(x => string.Equals(x.Name, playerName, StringComparison.OrdinalIgnoreCase));

            if (matchingPlayer != null)
            {
                Organization.Invite(matchingPlayer);
                Logger.Information($"Invited {matchingPlayer.Name} to the organization.");
            }
            else
            {
                Logger.Error($"Unable to locate player '{playerName}'.");
                Client.SendPrivateMessage(msg.SenderId, $"Player '{playerName}' not found.");
            }
        }

        private void HandleGroupMessage(GroupMsg msg)
        {
            Logger.Information($"Received group message in channel {msg.ChannelName} from {msg.SenderName}: {msg.Message}");

            if (msg.ChannelId != DynelManager.LocalPlayer.OrgId) return;

            if (msg.Message.Trim().Contains("@orginvite"))
            {
                HandleOrgInviteMessage(msg);
            }
            else
            {
                Logger.Error($"No player name found after '@orginvite'.");
            }
        }

        private void HandleOrgInviteMessage(GroupMsg msg)
        {
            Logger.Information($"Message contains '@orginvite': {msg.Message}");

            // Extract the player name after "@orginvite"
            string[] messageParts = msg.Message.Split(new[] { "@orginvite" }, StringSplitOptions.RemoveEmptyEntries);

            if (messageParts.Length <= 1) return;

            string playerName = messageParts[1].Trim();
            Logger.Information($"Player name extracted: {playerName}");

            // Send an invite to the extracted player name
            var matchingPlayer = DynelManager.Characters.FirstOrDefault(x => string.Equals(x.Name, playerName, StringComparison.OrdinalIgnoreCase));

            if (matchingPlayer != null)
            {
                Organization.Invite(matchingPlayer);
                Logger.Information($"Invited {matchingPlayer.Name} to the organization.");
            }
            else
            {
                Logger.Error($"Unable to locate player '{playerName}'.");
                Client.SendOrgMessage($"Unable to locate player '{playerName}'.");
            }
        }

        private void OnDynelSpawned(object _, Dynel dynel)
        {
            if (dynel is PlayerChar player)
                Logger.Information($"Player Spawned: {player.Name} - {player.Transform.Position}");
            else if (dynel is NpcChar npc)
                Logger.Information($"NPC Spawned: {npc.Name} {(npc.Owner.HasValue ? $"- Owner: {npc.Owner}" : "")}- {npc.Transform.Position}");
        }

        private void OnDynelDespawned(object _, Dynel dynel)
        {
            if (dynel is SimpleChar simpleChar)
                Logger.Information($"Character Despawned: {simpleChar.Name} - {simpleChar.Transform.Position}");
        }
    }
}
