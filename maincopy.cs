using AOSharp.Clientless;
using AOSharp.Clientless.Chat;
using AOSharp.Clientless.Logging;
using AOSharp.Common.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace InviteBot
{
    public class Main : ClientlessPluginEntry
    {
        private HashSet<string> allowedPlayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> bannedPlayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string adminFilePath = Path.Combine("..", "..", "..", "InviteBot", "ADMIN", "admin.txt"); // Path to the admin file
        private string banFilePath = Path.Combine("..", "..", "..", "InviteBot", "BAN", "ban.txt"); // Path to the ban file

        public override void Init(string pluginDir)
        {
            Logger.Information("InviteBot::Init");

            LoadResourceFromFolder("ADMIN");
            LoadResourceFromFolder("BAN");

            Client.OnUpdate += OnUpdate;
            Client.MessageReceived += OnMessageReceived;
            Client.Chat.PrivateMessageReceived += (e, msg) => HandlePrivateMessage(msg);
            Client.Chat.VicinityMessageReceived += (e, msg) => HandleVicinityMessage(msg);
            Client.Chat.GroupMessageReceived += (e, msg) => HandleGroupMessage(msg);
            DynelManager.DynelSpawned += OnDynelSpawned;
            DynelManager.DynelDespawned += OnDynelDespawned;
            Playfield.TowerUpdate += OnTowerUpdate;

            LoadBannedPlayers();
        }

        private void LoadResourceFromFolder(string folderName)
        {
            string resourceName = $"{GetType().Namespace}.{folderName}";

            string[] resourceNames = Assembly.GetExecutingAssembly().GetManifestResourceNames()
                .Where(x => x.StartsWith(resourceName) && !x.EndsWith(".dll")).ToArray();

            foreach (string fullResourceName in resourceNames)
            {
                string fileName = fullResourceName.Substring(fullResourceName.LastIndexOf('.') + 1);

                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(fullResourceName))
                {
                    if (stream == null)
                    {
                        throw new Exception($"Resource {fileName} not found.");
                    }

                    using (StreamReader reader = new StreamReader(stream))
                    {
                        string resourceContent = reader.ReadToEnd();
                        string[] lines = resourceContent.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string line in lines)
                        {
                            if (folderName.Equals("ADMIN"))
                            {
                                allowedPlayers.Add(line.Trim());
                            }
                            else if (folderName.Equals("BAN"))
                            {
                                bannedPlayers.Add(line.Trim());
                            }
                        }
                    }
                }
            }
        }

        private bool IsAllowed(string playerName)
        {
            return allowedPlayers.Contains(playerName);
        }

        private void OnUpdate(object _, double deltaTime)
        {
            //Logger.Debug("OnUpdate");
        }

        private void HandleVicinityMessage(VicinityMsg msg)
        {
            Logger.Debug($"{msg.SenderName}: {msg.Message}");
        }

        private void HandlePrivateMessage(PrivateMessage msg)
        {
            if (!msg.Message.StartsWith("!"))
            {
                // If the message doesn't start with "!", it's not a command
                // Inform the user to use !help to see available commands
                Client.SendPrivateMessage(msg.SenderId, "Unknown command. Use !help to see available commands.");
                return;
            }

            string[] commandParts = msg.Message.Split(' ');

            switch (commandParts[0].Remove(0, 1).ToLower())
            {
                case "stand":
                    Logger.Information($"Received stand request from {msg.SenderName}");
                    DynelManager.LocalPlayer.MovementComponent.ChangeMovement(MovementAction.LeaveSit);
                    break;
                case "sit":
                    Logger.Information($"Received sit request from {msg.SenderName}");
                    DynelManager.LocalPlayer.MovementComponent.ChangeMovement(MovementAction.SwitchToSit);
                    break;
                case "orgchat":
                    Client.SendOrgMessage("I've said something in org chat!");
                    break;
                case "invite":
                    Logger.Information($"Received invite request from {msg.SenderName}");

                    if (commandParts.Length < 2)
                    {
                        Logger.Error($"Invalid invite command. Usage: !invite <playername>");
                        return;
                    }

                    string playerName = commandParts[1];

                    if (!IsAllowed(msg.SenderName))
                    {
                        Logger.Warning($"{msg.SenderName} is not allowed to use the invite command.");
                        return;
                    }

                    var matchingPlayer = DynelManager.Characters.FirstOrDefault(x => string.Equals(x.Name, playerName, StringComparison.OrdinalIgnoreCase));

                    if (matchingPlayer != null)
                    {
                        Organization.Invite(matchingPlayer);
                        Logger.Information($"Invited {matchingPlayer.Name} to the organization.");
                    }
                    else
                    {
                        Logger.Error($"Unable to locate player '{playerName}'.");
                    }
                    break;
                case "listadmins":
                    Logger.Information($"List of Admins for {msg.SenderName}:");
                    string adminList = string.Join(", ", allowedPlayers);
                    Client.SendPrivateMessage(msg.SenderId, adminList);
                    break;
                case "adminadd":
                    if (commandParts.Length < 2)
                    {
                        Logger.Error($"Invalid adminadd command. Usage: !adminadd <playername>");
                        return;
                    }

                    string adminToAdd = commandParts[1];

                    if (!IsAllowed(msg.SenderName))
                    {
                        Logger.Warning($"{msg.SenderName} is not allowed to use the adminadd command.");
                        return;
                    }

                    if (allowedPlayers.Contains(adminToAdd))
                    {
                        Logger.Information($"{adminToAdd} is already an admin.");
                        Client.SendPrivateMessage(msg.SenderId, $"{adminToAdd} is already an admin.");
                    }
                    else
                    {
                        allowedPlayers.Add(adminToAdd);
                        Logger.Information($"{adminToAdd} has been added as an admin.");
                        Client.SendPrivateMessage(msg.SenderId, $"{adminToAdd} has been added as an admin.");

                        UpdateAdminFile();
                    }
                    break;
                case "adminrem":
                    if (commandParts.Length < 2)
                    {
                        Logger.Error($"Invalid adminrem command. Usage: !adminrem <playername>");
                        return;
                    }

                    string adminToRemove = commandParts[1];

                    if (!IsAllowed(msg.SenderName))
                    {
                        Logger.Warning($"{msg.SenderName} is not allowed to use the adminrem command.");
                        return;
                    }

                    if (allowedPlayers.Contains(adminToRemove))
                    {
                        allowedPlayers.Remove(adminToRemove);
                        Logger.Information($"{adminToRemove} has been removed as an admin.");
                        Client.SendPrivateMessage(msg.SenderId, $"{adminToRemove} has been removed as an admin.");

                        UpdateAdminFile();
                    }
                    else
                    {
                        Logger.Information($"{adminToRemove} is not an admin.");
                        Client.SendPrivateMessage(msg.SenderId, $"{adminToRemove} is not an admin.");
                    }
                    break;
                case "unban":
                    if (commandParts.Length < 2)
                    {
                        Logger.Error($"Invalid unban command. Usage: !unban <playername>");
                        return;
                    }

                    string playerToUnban = commandParts[1];

                    if (!IsAllowed(msg.SenderName))
                    {
                        Logger.Warning($"{msg.SenderName} is not allowed to use the unban command.");
                        return;
                    }

                    if (bannedPlayers.Contains(playerToUnban))
                    {
                        bannedPlayers.Remove(playerToUnban);
                        Logger.Information($"{playerToUnban} has been unbanned.");
                        Client.SendPrivateMessage(msg.SenderId, $"{playerToUnban} has been unbanned.");

                        UpdateBanFile();
                    }
                    else
                    {
                        Logger.Information($"{playerToUnban} is not banned.");
                        Client.SendPrivateMessage(msg.SenderId, $"{playerToUnban} is not banned.");
                    }
                    break;
                case "help":
                    string helpMessage = "Available commands:\n" +
                                         "!stand - Stand up\n" +
                                         "!sit - Sit down\n" +
                                         "!orgchat - Send message to org chat\n" +
                                         "!invite <playername> - Invite player to organization\n" +
                                         "!listadmins - List all admins\n" +
                                         "!adminadd <playername> - Add player as admin\n" +
                                         "!adminrem <playername> - Remove player from admins\n" +
                                         "!unban <playername> - Unban player\n" +
                                         "!listbans - List all banned players\n" +
                                         "!ban <playername> - Ban player\n" +
                                         "!help - Show available commands";

                    Client.SendPrivateMessage(msg.SenderId, helpMessage);
                    break;
                case "listbans":
                    Logger.Information($"List of banned players:");
                    string bannedPlayersList = string.Join(", ", bannedPlayers);
                    Client.SendPrivateMessage(msg.SenderId, bannedPlayersList);
                    break;
                case "ban":
                    if (commandParts.Length < 2)
                    {
                        Logger.Error($"Invalid ban command. Usage: !ban <playername>");
                        return;
                    }

                    string playerToBan = commandParts[1];

                    if (!IsAllowed(msg.SenderName))
                    {
                        Logger.Warning($"{msg.SenderName} is not allowed to use the ban command.");
                        return;
                    }

                    if (bannedPlayers.Contains(playerToBan))
                    {
                        Logger.Information($"{playerToBan} is already banned.");
                        Client.SendPrivateMessage(msg.SenderId, $"{playerToBan} is already banned.");
                    }
                    else
                    {
                        bannedPlayers.Add(playerToBan);
                        Logger.Information($"{playerToBan} has been banned.");
                        Client.SendPrivateMessage(msg.SenderId, $"{playerToBan} has been banned.");

                        UpdateBanFile();
                    }
                    break;
                default:
                    // If the command is not recognized, inform the user to use !help
                    Client.SendPrivateMessage(msg.SenderId, "Unknown command. Use !help to see available commands.");
                    break;
            }
        }

        private void HandleGroupMessage(GroupMsg msg)
        {
            if (msg.ChannelId == DynelManager.LocalPlayer.OrgId)
            {
                Logger.Information($"Received org message!");
            }
            else if (msg.ChannelId == DynelManager.LocalPlayer.TeamId)
            {
                Logger.Information($"Received team message!");
            }

            Logger.Information($"[{msg.ChannelName}]: {msg.SenderName}: {msg.Message}");
        }

        private void OnDynelSpawned(object _, Dynel dynel)
        {
            if (dynel is PlayerChar player)
            {
                Logger.Information($"Player Spawned: {player.Name} - {player.Transform.Position}");
            }
            else if (dynel is NpcChar npc)
            {
                Logger.Information($"NPC Spawned: {npc.Name} {(npc.Owner.HasValue ? $"- Owner: {npc.Owner}" : "")}- {npc.Transform.Position}");
            }
        }

        private void OnDynelDespawned(object _, Dynel dynel)
        {
            if (dynel is SimpleChar simpleChar)
                Logger.Information($"Character Despawned: {simpleChar.Name} - {simpleChar.Transform.Position}");
        }

        private void OnMessageReceived(object _, Message msg)
        {
            if (msg.Header.PacketType == PacketType.PingMessage)
                Logger.Debug($"Plugin detected Ping message!");
        }

        private void OnTowerUpdate(object _, TowerUpdateEventArgs e)
        {
            Logger.Debug($"{e.Tower.Side} {e.Tower.Class} {e.Tower.TowerCharId} at {e.Tower.Position} - {e.UpdateType}");
        }

        private void LoadBannedPlayers()
        {
            try
            {
                if (File.Exists(banFilePath))
                {
                    string[] lines = File.ReadAllLines(banFilePath);

                    foreach (string line in lines)
                    {
                        bannedPlayers.Add(line.Trim());
                    }
                }
                else
                {
                    File.Create(banFilePath).Close();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load banned players: {ex.Message}");
            }
        }

        private void UpdateAdminFile()
        {
            try
            {
                File.WriteAllLines(adminFilePath, allowedPlayers);
                Logger.Information("Admin file updated successfully.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to update admin file: {ex.Message}");
            }
        }

        private void UpdateBanFile()
        {
            try
            {
                File.WriteAllLines(banFilePath, bannedPlayers);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to update ban file: {ex.Message}");
            }
        }

        private void BanPlayer(string playerName)
        {
            bannedPlayers.Add(playerName);
            UpdateBanFile();
        }

        private void UnbanPlayer(string playerName)
        {
            bannedPlayers.Remove(playerName);
            UpdateBanFile();
        }
    }

    public enum RelevantItems
    {
        PremiumHealthAndNanoRecharger = 297274
    }
}
