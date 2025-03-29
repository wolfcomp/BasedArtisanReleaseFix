using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace BasedArtisanReleaseFix
{
    public class Main : IDalamudPlugin
    {
        static IDalamudPluginInterface? Instance { get; set; }
        static ICommandManager? CommandManager { get; set; }
        static IPluginLog? Log { get; set; }
        static IFramework? Framework { get; set; }
        public Main(IDalamudPluginInterface pluginInterface, ICommandManager commandManager, IPluginLog log, IFramework framework)
        {
            commandManager.AddHandler("/barf", new CommandInfo((_, _) => FixArtisan()));
            Instance = pluginInterface;
            CommandManager = commandManager;
            Log = log;
            Framework = framework;
            // FixArtisan();
        }

        private static void FixArtisan()
        {
            if (!TryGetLoadedPlugin("Artisan", out var plugin, out var localPlugin)) return;
            var instance = localPlugin.GetType().GetField("dalamudInterface", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(localPlugin);
            if (instance == null) return;
            try
            {
                plugin.Dispose();
            }
            catch
            {
                // Ignore since Artisan doesn't handle nullables correctly.
            }
            var type = plugin.GetType().Assembly.GetTypes().FirstOrDefault(t => t.Name == "DalamudInfo");
            if (type == null) Log!.Info("Failed to find DalamudInfo");
            else type.GetField("IsStaging", BindingFlags.Static | BindingFlags.Public)?.SetValue(null, false);
            plugin.GetType().GetConstructor([instance.GetType()])?.Invoke([instance]);
        }

        #region Joinked from SimpleHeels
        private static IList? _installedPluginsList;
        private static bool TryGetLoadedPlugin(string internalName, [NotNullWhen(true)] out IDalamudPlugin? plugin, [NotNullWhen(true)] out object? localPlugin)
        {
            plugin = null;
            localPlugin = null;
            if (_installedPluginsList == null)
            {
                var dalamudAssembly = typeof(IDalamudPluginInterface).Assembly;
                var service1T = dalamudAssembly.GetType("Dalamud.Service`1");
                if (service1T == null) throw new Exception("Failed to get Service<T> Type");
                var pluginManagerT = dalamudAssembly.GetType("Dalamud.Plugin.Internal.PluginManager");
                if (pluginManagerT == null) throw new Exception("Failed to get PluginManager Type");

                var serviceInterfaceManager = service1T.MakeGenericType(pluginManagerT);
                var getter = serviceInterfaceManager.GetMethod("Get", BindingFlags.Static | BindingFlags.Public);
                if (getter == null) throw new Exception("Failed to get Get<Service<PluginManager>> method");

                var pluginManager = getter.Invoke(null, null);

                if (pluginManager == null) throw new Exception("Failed to get PluginManager instance");

                var installedPluginsListField = pluginManager.GetType().GetField("installedPluginsList", BindingFlags.NonPublic | BindingFlags.Instance);
                if (installedPluginsListField == null) throw new Exception("Failed to get installedPluginsList field");

                _installedPluginsList = (IList?)installedPluginsListField.GetValue(pluginManager);
                if (_installedPluginsList == null) throw new Exception("Failed to get installedPluginsList value");
            }

            PropertyInfo? internalNameProperty = null;

            foreach (var v in _installedPluginsList)
            {
                internalNameProperty ??= v?.GetType().GetProperty("InternalName");

                if (internalNameProperty == null) continue;
                var installedInternalName = internalNameProperty.GetValue(v) as string;

                if (installedInternalName == internalName && v != null)
                {
                    var t = v.GetType();
                    while (t.Name != "LocalPlugin" && t.BaseType != null) t = t.BaseType;
                    plugin = t.GetField("instance", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(v) as IDalamudPlugin;
                    localPlugin = v;
                    if (plugin != null) return true;
                }
            }

            return false;
        }
        #endregion

        public void Dispose()
        {
            CommandManager!.RemoveHandler("/barf");
        }
    }
}
