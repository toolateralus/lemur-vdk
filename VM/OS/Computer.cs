﻿using System;
using System.IO;
using VM.GUI;
using VM.OS.Network;
using System.Threading.Tasks;
using System.Reflection;

namespace VM.OS
{
    public class Computer
    {
        // This connects every computer to the lan server
        public NetworkConfiguration Network = new();
        public static string GetParentDir(string targetDirectory)
        {
            string assemblyLocation = Assembly.GetExecutingAssembly().Location;
            string currentDirectory = Path.GetDirectoryName(assemblyLocation);

            while (!Directory.Exists(Path.Combine(currentDirectory, targetDirectory)))
            {
                currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
                if (currentDirectory == null)
                {
                    // Reached the root directory without finding the target
                    return null;
                }
            }

            return Path.Combine(currentDirectory, targetDirectory);
        }
        public Computer(uint id)
        {
            OS = new(id, this);

            string jsDirectory = GetParentDir("VM");

            OS.JavaScriptEngine.LoadModules(jsDirectory + "\\OS-JS");
            _ = OS.JavaScriptEngine.Execute($"os.id = {id}");

            if (Runtime.GetResourcePath("startup.js") is string AbsPath)
            {
                OS.JavaScriptEngine.ExecuteScript(AbsPath);
            }

        }
        public uint ID() => OS.ID;

        public OS OS;

        /// <summary>
        /// this closes the window associated with the pc, if you do so manually before or after this call, it will error.
        /// </summary>
        /// <param name="exitCode"></param>
        internal void Exit(int exitCode)
        {
            ComputerWindow computerWindow = Runtime.GetWindow(this);
            
            computerWindow.Close();

            if (Runtime.Computers.Count > 0 && exitCode != 0)
            {
                Notifications.Now($"Computer {ID()} has exited, most likely due to an error. code:{exitCode}");
            }
        }
       
        internal void Shutdown()
        {
            OS.JavaScriptEngine.Dispose();
        }

        internal void FinishInit(Computer pc, ComputerWindow wnd)
        {
            LoadBackground(pc, wnd);
            InstallCoreApps(pc);

            wnd.Show();

            wnd.Closed += (o, e) =>
            {
                Runtime.Computers.Remove(pc);
                Task.Run(() => pc.OS.SaveConfig());
                pc.Shutdown();
            };
        }

        private static void InstallCoreApps(Computer pc)
        {
            pc.OS.InstallApplication("CommandPrompt.app", typeof(CommandPrompt));
            pc.OS.InstallApplication("FileExplorer.app", typeof(FileExplorer));
            pc.OS.InstallApplication("TextEditor.app", typeof(TextEditor));
        }

        private static void LoadBackground(Computer pc, ComputerWindow wnd)
        {
            string backgroundPath = pc?.OS?.Config?.Value<string>("BACKGROUND") ?? "background.png";
            wnd.desktopBackground.Source = ComputerWindow.LoadImage(Runtime.GetResourcePath(backgroundPath) ?? "background.png");
        }
    }
}