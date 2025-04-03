using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
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
        static IChatGui? Chat { get; set; }
        public Main(IDalamudPluginInterface pluginInterface, ICommandManager commandManager, IPluginLog log, IFramework framework, IChatGui chat)
        {
            var commandInterface = new CommandInfo((_, args) =>
            {
                if (args == "fix")
                    FixArtisan();
                else
                    _installedPluginsList = null;
            });
            commandInterface.HelpMessage = "fix - Fix Artisan\nclearfix - Clear installed plugins list and fix Artisan";
            commandManager.AddHandler("/barf", commandInterface);
            Instance = pluginInterface;
            CommandManager = commandManager;
            Log = log;
            Framework = framework;
            Chat = chat;
            // FixArtisan();
        }

        private static void FixArtisan()
        {
            try
            {
                if (!TryGetLoadedPlugin("Artisan", out var plugin, out var localPlugin)) throw new Exception("Failed to get Artisan plugin");
                var instance = localPlugin.GetType().GetField("dalamudInterface", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(localPlugin);
                if (instance == null) throw new Exception("Failed to get DalamudInterface instance");

                var pluginType = plugin.GetType();

                // init fields
                var fields = pluginType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
                var windowSystem = fields.FirstOrDefault(f => f.Name == "ws");
                if (windowSystem == null) throw new Exception("Failed to get ws field");
                var addWindow =
                    windowSystem.FieldType.GetMethod("AddWindow", BindingFlags.Instance | BindingFlags.Public);
                if (addWindow == null) throw new Exception("Failed to get AddWindow method");
                foreach (var field in fields)
                {
                    if (field.Name is "UniversalsisClient" or "cw")
                    {
                        var obj = Activator.CreateInstance(field.FieldType);
                        field.SetValue(plugin, obj);
                        if (field.Name == "cw") addWindow.Invoke(windowSystem.GetValue(plugin), [obj]);
                    }
                
                    if (field.Name == "PluginUi")
                    {
                        var obj = field.GetValue(plugin);
                        field.FieldType.GetProperty("OpenWindow", BindingFlags.Instance | BindingFlags.Public)?.SetValue(obj, 1);
                    }
                }
                
                var types = pluginType.Assembly.GetTypes();
                foreach (var type in types)
                {
                    Log.Info(type.Name);
                    // init static types
                    if (type.Name is "CraftingProcessor" or "EnduranceCraftWatcher")
                    {
                        if (type.GetMethod("Setup", BindingFlags.Static | BindingFlags.Public) is { } info)
                            info.Invoke(null, null);
                        else
                            throw new Exception($"Failed to get Setup method of {type.Name}");
                    }
                
                    if (type.Name is "ConsumableChecker" or "Endurance" or "IPC" or "RetainerInfo"
                        or "CraftingListContextMenu")
                    {
                        if (type.GetMethod("Init",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) is { } info)
                            info.Invoke(null, null);
                        else
                            throw new Exception($"Failed to get Init method of {type.Name}");
                    }
                
                    if (type.Name == "DalamudInfo")
                    {
                        if (type.GetField("IsStaging", BindingFlags.Static | BindingFlags.Public) is { } info)
                            info.SetValue(null, false);
                        else
                            throw new Exception($"Failed to get IsStaging field of {type.Name}");
                    }
                
                    // init windows
                    if (type.Name is "RecipeWindowUI" or "ProcessingWindow" or "QuestHelper")
                        addWindow.Invoke(windowSystem.GetValue(plugin), [Activator.CreateInstance(type)]);
                }

                // setup service functions
                if (!GetAssemblyFromLoadContext("ECommons", localPlugin, out var eCommonsAssembly))
                    throw new Exception("Failed to get ECommons assembly");
                var svcType = eCommonsAssembly.GetTypes().FirstOrDefault(t => t.Name == "Svc");
                if (svcType == null) throw new Exception("Failed to get Svc type");
                var eventPropertyInfo = svcType.GetProperty("Framework", BindingFlags.Static | BindingFlags.Public);
                if (eventPropertyInfo == null) throw new Exception("Failed to get Framework property");
                var methodPropertyInfo =
                    pluginType.GetMethod("OnFrameworkUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                if (methodPropertyInfo == null) throw new Exception("Failed to get OnFrameworkUpdate method");
                var eventInfo = eventPropertyInfo.PropertyType.GetEvent("Update", BindingFlags.Instance | BindingFlags.Public);
                if (eventInfo == null) throw new Exception("Failed to get Update event");
                eventInfo.AddEventHandler(eventPropertyInfo.GetValue(null), 
                    methodPropertyInfo.CreateDelegate(eventInfo.EventHandlerType!, plugin));
                eventPropertyInfo = svcType.GetProperty("ClientState", BindingFlags.Static | BindingFlags.Public);
                if (eventPropertyInfo == null) throw new Exception("Failed to get ClientState property");
                methodPropertyInfo = pluginType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                    .FirstOrDefault(t => t.Name == "DisableEndurance" && t.GetParameters().Length == 2);
                if (methodPropertyInfo == null) throw new Exception("Failed to get DisableEndurance method");
                eventInfo = eventPropertyInfo.PropertyType.GetEvent("Logout", BindingFlags.Instance | BindingFlags.Public);
                if (eventInfo == null) throw new Exception("Failed to get Logout event");
                eventInfo.AddEventHandler(eventPropertyInfo.GetValue(null),
                    methodPropertyInfo.CreateDelegate(eventInfo.EventHandlerType!, plugin));
                methodPropertyInfo = pluginType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                    .FirstOrDefault(t => t.Name == "DisableEndurance" && t.GetParameters().Length == 0);
                if (methodPropertyInfo == null) throw new Exception("Failed to get DisableEndurance method");
                eventInfo = eventPropertyInfo.PropertyType.GetEvent("Login", BindingFlags.Instance | BindingFlags.Public);
                if (eventInfo == null) throw new Exception("Failed to get Login event");
                eventInfo.AddEventHandler(eventPropertyInfo.GetValue(null),
                    methodPropertyInfo.CreateDelegate(eventInfo.EventHandlerType!, plugin));
                eventPropertyInfo = svcType.GetProperty("Condition", BindingFlags.Static | BindingFlags.Public);
                if (eventPropertyInfo == null) throw new Exception("Failed to get Condition property");
                methodPropertyInfo = pluginType.GetMethod("Condition_ConditionChange", BindingFlags.Instance | BindingFlags.NonPublic);
                if (methodPropertyInfo == null) throw new Exception("Failed to get Condition_ConditionChange method");
                eventInfo = eventPropertyInfo.PropertyType.GetEvent("ConditionChange", BindingFlags.Instance | BindingFlags.Public);
                if (eventInfo == null) throw new Exception("Failed to get ConditionChange event");
                eventInfo.AddEventHandler(eventPropertyInfo.GetValue(null),
                    methodPropertyInfo.CreateDelegate(eventInfo.EventHandlerType!, plugin));

                pluginType.GetMethod("ConvertCraftingLists", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(plugin, null);

                Log.Info("Artisan fixed! Suck it up Taurenkey! >:D");
                Chat.Print("Artisan fixed! Suck it up Taurenkey! >:D");
            }
            catch (Exception ex)
            {
                Log!.Error(ex, "Failed to fix Artisan");
            }

            // Old way
            // var type = types.FirstOrDefault(t => t.Name == "DalamudInfo");
            // if (type == null) Log!.Info("Failed to find DalamudInfo");
            // else type.GetField("IsStaging", BindingFlags.Static | BindingFlags.Public)?.SetValue(null, false);
            // var newPlugin = plugin.GetType().GetConstructor([instance.GetType()])?.Invoke([instance]);
            // if (newPlugin == null) return;
            // SetLoadedPlugin("Artisan", newPlugin);
        }

        #region Reflection Helpers
        // Joinked from SimpleHeels
        private static IList? _installedPluginsList;
        private static bool TryGetLoadedPlugin(string internalName, [NotNullWhen(true)] out IDalamudPlugin? plugin, [NotNullWhen(true)] out object? localPlugin)
        {
            plugin = null;
            localPlugin = null;
            PopulatePlugins();

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

        private static void PopulatePlugins()
        {
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
        }

        private static bool GetAssemblyFromLoadContext(string name, object localPlugin, [NotNullWhen(true)] out Assembly? assembly)
        {
            assembly = null;
            var type = localPlugin.GetType();
            var loader = type.GetField("loader", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(localPlugin);
            if (loader is null) return false;
            type = loader.GetType();

            var loadContext = type.GetProperty("LoadContext", BindingFlags.Public | BindingFlags.Instance)!.GetValue(loader) as AssemblyLoadContext;
            if (loadContext is null) return false;

            foreach (var loadContextAssembly in loadContext.Assemblies)
            {
                if (loadContextAssembly.GetName().Name != name) continue;
                assembly = loadContextAssembly;
                return true;
            }

            return false;
        }

        private static void SetLoadedPlugin(string internalName, object plugin)
        {
            PopulatePlugins();
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
                    t.GetField("instance", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.SetValue(v, plugin);
                }
            }
        }
        #endregion

        public void Dispose()
        {
            CommandManager!.RemoveHandler("/barf");
        }
    }
}
