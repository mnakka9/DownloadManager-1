using DownloadManager.Core.Interfaces;
using System.Collections.Generic;

namespace DownloadManager.Core.Classes
{
    class CommandManager
    {
        List<ICommand> commands;
        Stack<ICommand> commandsHistory;

        public CommandManager()
        {
            commands = new List<ICommand>();
            commandsHistory = new Stack<ICommand>();
        }

        public void AddCommand(ICommand c)
        {
            commands.Add(c);
        }

        public void RunCommand(int number)
        {
            commands[number].Execute();
            commandsHistory.Push(commands[number]);
        }
        public void Undo()
        {
            if (commandsHistory.Count > 0)
            {
                ICommand undoCommand = commandsHistory.Pop();
                undoCommand.Undo();
            }
        }
    }
}
