using PlatformRacing3.Server.API.Game.Commands;
using PlatformRacing3.Server.Game.Commands;
using PlatformRacing3.Server.Game.Communication.Messages.Outgoing;
using PlatformRacing3.Server.Game.Client;
using System.Drawing;

namespace PlatformRacing3.Server.Game.Commands.Misc;

internal sealed class ListCommandsCommand : ICommand
{
    private readonly CommandManager commandManager;

    public ListCommandsCommand(CommandManager commandManager)
    {
        this.commandManager = commandManager;
    }

    public string Permission => "command.list.use";

    public void OnCommand(ICommandExecutor executor, string label, ReadOnlySpan<string> args)
    {
        if (!executor.HasPermission(this.Permission))
        {
            executor.SendMessage("You don't have permission to use this command!");
            return;
        }

        if (executor is ClientSession session)
        {
            session.SendPacket(new ChatOutgoingMessage("chat-Home", "# Available Admin Commands:", 0, 0, "", Color.Black));
            session.SendPacket(new ChatOutgoingMessage("chat-Home", "# ------------------------", 0, 0, "", Color.Black));

            foreach (var command in this.commandManager.GetAllCommands())
            {
                if (command.Permission != null && executor.HasPermission(command.Permission))
                {
                    string commandName = command.GetType().Name.Replace("Command", "").ToLower();
                    string usageMessage = GetCommandUsage(command, commandName);
                    session.SendPacket(new ChatOutgoingMessage("chat-Home", $"# {usageMessage}", 0, 0, "", Color.Black));
                }
            }
        }
        else
        {
            executor.SendMessage("This command may only be executed by client session");
        }
    }

    private string GetCommandUsage(ICommand command, string commandName)
    {
        // Map of known command usages with descriptions
        var commandUsages = new Dictionary<string, string>
        {
            { "givebonusexpmultiplier", "/givebonusexpmultiplier - Give bonus exp multiplier to a user. Usage: /givebonusexpmultiplier [user] [multiplier] [time in seconds]" },
            { "givebonusexp", "/givebonusexp - Give bonus exp to a user. Usage: /givebonusexp [user] [amount]" },
            { "givehat", "/givehat - Give a hat to a user. Usage: /givehat [user] [id/name] [temporaly(false)]" },
            { "givepart", "/givepart - Give a part to a user. Usage: /givepart [user] [type] [id/name] [temporaly(false)]" },
            { "broadcast", "/broadcast - Broadcast a message to all users. Usage: /broadcast [message]" },
            { "kick", "/kick - Kick a user from the server. Usage: /kick [user] [reason(empty)]" },
            { "alert", "/alert - Send an alert to a specific user. Usage: /alert [user] [message]" },
            { "addhat", "/addhat - Add a hat to a target. Usage: /addhat [hat] <target>" },
            { "fakeprize", "/fakeprize - Give a fake prize. Usage: /fakeprize [category] [id]" },
            { "spawnaliens", "/spawnaliens - Spawn aliens in a match. Usage: /spawnaliens [amount]" },
            { "teleport", "/teleport - Teleport a player to coordinates. Usage: /teleport [x] [y] <target>" },
            { "tournament", "/tournament - Toggle tournament mode for the next match you host. Usage: /tournament" },
            { "broadcaster", "/broadcaster - Toggle broadcaster status in a match. Usage: /broadcaster" },
            { "life", "/life - Set a player's life count. Usage: /life [amount] <target>" },
            { "item", "/item - Give an item to a player. Usage: /item [item] <target>" },
            { "spectate", "/spectate - Join the next match as a spectator. Usage: /spectate" },
            { "mute", "/mute - Mute a user. Usage: /mute [user] [reason(empty)]" },
            { "unmute", "/unmute - Unmute a user. Usage: /unmute [user]" },
            { "removehats", "/removehats - Remove hats from a player. Usage: /removehats [target] [hat1] [hat2] ..." },
            { "delaystart", "/delaystart - Toggle delayed start for a match. Usage: /delaystart" },
            { "teleportto", "/teleportto - Teleport to a target. Usage: /teleportto [target] <who>" },
            { "teleporthere", "/teleporthere - Teleport a target to you. Usage: /teleporthere [target] <to>" },
            { "forfeit", "/forfeit - Force a player to forfeit a match. Usage: /forfeit [target]" },
            { "finish", "/finish - Force a player to finish a match. Usage: /finish [target]" },
            { "shutdown", "/shutdown - Shutdown the server. Usage: /shutdown" }
        };

        // Return the known usage or a default format
        return commandUsages.TryGetValue(commandName, out string usage) ? usage : $"/{commandName} - No description available. Usage: /{commandName}";
    }

    private class CommandExecutor : ICommandExecutor
    {
        public string LastMessage { get; private set; }
        public uint PermissionRank => uint.MaxValue;

        public bool HasPermission(string permission) => true;

        public void SendMessage(string message)
        {
            this.LastMessage = message;
        }
    }
} 