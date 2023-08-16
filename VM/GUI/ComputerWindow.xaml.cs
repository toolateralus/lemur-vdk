﻿using Microsoft.ClearScript.V8;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using VM;
using VM;
using VM.JS;
using VM.UserInterface;

namespace VM.GUI
{
    public partial class ComputerWindow : Window
    {
        public Computer Computer;
        public readonly List<string> LoadedInstalledApplications = new();
        public readonly Dictionary<string, UserWindow> USER_WINDOW_INSTANCES = new();
        public ComputerWindow(Computer pc)
        {
            InitializeComponent();
            desktopBackground.Source = LoadImage(Runtime.GetResourcePath("Background.png") ?? "background.png");
            KeyDown += Computer_KeyDown;
            Computer = pc;
            Closing += OnClosingCustom;
            IDLabel.Content = $"computer {Computer.ID}";
            CompositionTarget.Rendering += (e, o) => UpdateComputerTime();

        }
        private async void OnClosingCustom(object? sender, CancelEventArgs e)
        {
            foreach (var item in USER_WINDOW_INSTANCES)
                item.Value.Close();

            Dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Normal);

            while (!Dispatcher.HasShutdownFinished)
                await Task.Delay(1);
        }
        private void Computer_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.OemTilde:
                    var cmd = new CommandPrompt();
                    OpenApp(cmd, "Cmd");
                    cmd.LateInit(Computer);
                    break;
            }
        }
        private void ShutdownClick(object sender, RoutedEventArgs e)
        {
            App.Current.Shutdown();
            Close();
        }
        private void RemoveTaskbarButton(string title)
        {
            System.Collections.IList list = TaskbarStackPanel.Children;
            for (int i = 0; i < list.Count; i++)
            {
                object? item = list[i];
                if (item is Button button && button.Content as string == title)
                {
                    TaskbarStackPanel.Children.Remove(button);
                    break;
                }
            }
        }
        private Button GetOSThemeButton(double width = double.NaN, double height = double.NaN)
        {
            var btn = new Button()
            {
                Background = Computer.Theme.Background,
                BorderBrush = Computer.Theme.Border,
                BorderThickness = Computer.Theme.BorderThickness,
                FontFamily = Computer.Theme.Font,
                FontSize = Computer.Theme.FontSize,
                Width = width,
                Height = height,
            };
            return btn;
        }
        private Button GetDesktopIconButton(string appName)
        {
            var btn = GetOSThemeButton(width: 60, height: 60);

            btn.Margin = new Thickness(15, 15, 15, 15);
            btn.Content = appName;
            btn.Name = appName.Split(".")[0];
            return btn;
        }
        private Button GetTaskbarButton(string title, RoutedEventHandler Toggle)
        {
            var btn = GetOSThemeButton(width: 65);

            btn.Content = title;
            btn.Click += Toggle;
            return btn;
        }
        private void UpdateComputerTime()
        {
            DateTime now = DateTime.Now;
            string formattedDateTime = now.ToString("MM/dd/yy || h:mm");
            TimeLabel.Content = formattedDateTime;
        }
        public static BitmapImage LoadImage(string path)
        {
            BitmapImage bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
            bitmapImage.EndInit();
            return bitmapImage;
        }
        private static void SetupIcon(string name, Button btn, Type type) 
        {
            if (GetIcon(type) is BitmapImage img)
            {
                btn.Background = new ImageBrush(img);
            }

            btn.Margin = new Thickness(15, 15, 15, 15);

            var contentBorder = new Border
            {
                Background = new ImageBrush(GetIcon(type)),
                CornerRadius = new CornerRadius(10),
                ToolTip = name,
            };

            btn.Content = contentBorder;
        }
        private static BitmapImage? GetIcon(Type type) 
        {
            var properties = type.GetProperties();

            foreach (var property in properties)
            {
                if (property.Name.Contains("DesktopIcon") &&
                    property.PropertyType == typeof(string) &&
                    property.GetValue(null) is string path &&
                    !string.IsNullOrEmpty(path))
                {
                    return LoadImage(path);
                }
            }

            return null;
        }
        /// <summary>
        /// performs init on LateInit method, explaied in tooltipf or IsValidType (a static method in this class)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instance"></param>
        /// <param name="computer"></param>
        private static void AssignComputer(object instance, Computer computer)
        {
            var methods = instance.GetType().GetMethods();

            foreach (var method in methods)
            {
                if (method.Name.Contains("LateInit") &&
                    method.GetParameters().Length == 1 &&
                    method.GetParameters()[0].ParameterType == typeof(Computer))
                {
                    method.Invoke(instance, new[] { computer });
                }
            }
        }
        /// <summary>
        /// we rely on this <code>('public void LateInit(Computer pc){..}') </code>method being declared in the UserControl to attach the OS to the app
        /// </summary>
        /// <param name="members"></param>
        /// <returns></returns>
        /// 
        private static bool IsValidType(MemberInfo[] members)
        {
            foreach (var member in members)
            {
                if (member.Name.Contains("LateInit"))
                {
                    return true;
                }
            }
            return false;
        }
        public void OpenApp(UserControl control, string title = "window", Brush? background = null, Brush? foreground = null, JavaScriptEngine engine = null)
        {
            background ??= Computer.Theme.Background;
            foreground ??= Computer.Theme.Foreground;

            var window = new UserWindow()
            {
                Background = background,
                Foreground = foreground,
            };

            var frame = new ResizableWindow
            {
                Content = window,
                Width = 800,
                Height = 600,
                Margin = new(),
                Background = background,
                Foreground = foreground,
            };

            window.InitializeUserContent(frame, control, engine);

            USER_WINDOW_INSTANCES[title] = window;

            Desktop.Children.Add(frame);

            Button btn = GetTaskbarButton(title, window.ToggleVisibility);

            TaskbarStackPanel.Children.Add(btn);

            window.OnClosed += () => RemoveTaskbarButton(title);
        }
        public void InstallWPF(string type)
        {
            Dispatcher.Invoke(() => { 
                LoadedInstalledApplications.Add(type);

                var btn = GetDesktopIconButton(type);
              
                btn.MouseDoubleClick += OnDesktopIconPressed;

                async void OnDesktopIconPressed(object? sender, RoutedEventArgs e)
                {
                    await Computer.OpenCustom(type);
                }

                DesktopIconPanel.Children.Add(btn);
                DesktopIconPanel.UpdateLayout();
            
            });
        }
        public void InstallJSHTML(string type)
        {
            Dispatcher.Invoke(() =>
            {
                LoadedInstalledApplications.Add(type);

                var btn = GetDesktopIconButton(type);

                btn.MouseDoubleClick += OnDesktopIconPressed;

                void OnDesktopIconPressed(object? sender, RoutedEventArgs e)
                {
                    var app = new UserWebApplet();

                    // we add the appropriate extension within navigate.
                    app.Path = type.Replace(".web", "");
                    OpenApp(app);
                }

                DesktopIconPanel.Children.Add(btn);
                DesktopIconPanel.UpdateLayout();
            });

        }
        public void InstallCSharpWPFApp(string exePath, Type type)
        {
            var name = exePath.Split('.')[0];

            var btn = GetDesktopIconButton(name);

            btn.MouseDoubleClick += OnDesktopIconPressed;

            void OnDesktopIconPressed(object? sender, RoutedEventArgs e)
            {
                var members = type.GetMethods();

                if (IsValidType(members) && Activator.CreateInstance(type) is object instance && instance is UserControl userControl)
                {
                    AssignComputer(instance, Computer);
                    OpenApp(userControl, name);
                }
            }

            SetupIcon(name, btn, type);

            DesktopIconPanel.Children.Add(btn);
            DesktopIconPanel.UpdateLayout();
        }
        public void Uninstall(string name)
        {
            Dispatcher.Invoke(() =>
            {
                LoadedInstalledApplications.Remove(name);

                System.Collections.IList list = DesktopIconPanel.Children;

                for (int i = 0; i < list.Count; i++)
                {
                    object? item = list[i];
                    if (item is Button btn && btn.Name == name.Replace(".app", "").Replace(".web", ""))
                    {
                        DesktopIconPanel.Children.Remove(btn);
                    }
                }
            });
        }
    }
}
