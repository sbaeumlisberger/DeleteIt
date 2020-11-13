using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DeleteIt
{
    public class WindowsExplorerIntegrationService
    {

        public void InstallContextMenuEntries()
        {
            AddContextMenuEntry(Registry.CurrentUser.CreateSubKey(@"Software\Classes\*\shell", true));
            AddContextMenuEntry(Registry.CurrentUser.CreateSubKey(@"Software\Classes\Directory\shell", true));
        }

        public void UnintsallContextMenuEntries()
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\*\shell\ForceDelete");
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Directory\shell\ForceDelete");
        }

        private void AddContextMenuEntry(RegistryKey parentKey)
        {
            var entryKey = parentKey.CreateSubKey("ForceDelete", true);
            entryKey.SetValue("", "Force Delete");
            var commandKey = entryKey.CreateSubKey("command", true);
            string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeleteIt.exe");
            commandKey.SetValue("", $"{exePath} \"%1\"");
        }

    }

}
