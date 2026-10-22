using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace NomadDriveMorePlayers
{
    /// <summary>
    /// Raises the Nomad Drive co-op player limit.
    ///
    /// Two places enforce the limit (found by reading the playtest assemblies):
    ///   1. EvilCore.Networking.LobbyCreateOptions.MaxPlayers -> EOS lobby MaxLobbyMembers.
    ///   2. Mirror NetworkManager.maxConnections -> EOS transport rejects joins when full.
    /// Game types are reached through reflection so the plugin builds without game DLLs.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.nunod87.nomaddrive.moreplayers";
        public const string Name = "Nomad Drive More Players";
        public const string Version = "0.1.1";

        internal const int GameDefault = 4;
        internal const int RecommendedMax = 8;
        internal const int HardMax = 12;

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> MaxPlayers;
        internal static ConfigEntry<bool> VerboseLog;

        private Harmony _harmony;
        private readonly HashSet<string> _patched = new HashSet<string>();

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                "Turn the mod on or off without uninstalling it.");

            MaxPlayers = Config.Bind("General", "MaxPlayers", 6,
                new ConfigDescription(
                    "Maximum players in a co-op lobby (host included). The game ships with 4. " +
                    "Up to " + RecommendedMax + " is recommended: the game's own loot/economy scaling " +
                    "is clamped to " + RecommendedMax + ". " + (RecommendedMax + 1) + "-" + HardMax +
                    " is allowed but untested and may cause missing seats, spawn problems or lag. " +
                    "Only the host needs to set this.",
                    new AcceptableValueRange<int>(2, HardMax)));

            VerboseLog = Config.Bind("Debug", "VerboseLog", false,
                "Log every patched call and value change.");

            if (!Enabled.Value)
            {
                Log.LogInfo("Disabled in config.");
                return;
            }

            if (MaxPlayers.Value > RecommendedMax)
                Log.LogWarning("MaxPlayers=" + MaxPlayers.Value + " is above the recommended " +
                               RecommendedMax + ". Untested territory.");

            _harmony = new Harmony(Guid);

            // Game assemblies load lazily; patch what is loaded now and the rest as it loads.
            AppDomain.CurrentDomain.AssemblyLoad += (_, args) => TryPatch(args.LoadedAssembly);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                TryPatch(asm);

            // Force-load the networking assembly so we do not depend on load order.
            try { Assembly.Load("EvilCore.Networking"); }
            catch (Exception e) { Log.LogWarning("Could not preload EvilCore.Networking: " + e.Message); }

            Log.LogInfo(Name + " " + Version + " loaded. Target max players: " + MaxPlayers.Value);
        }

        private void TryPatch(Assembly asm)
        {
            string asmName;
            try { asmName = asm.GetName().Name; } catch { return; }
            if (asmName != "EvilCore.Networking" || !_patched.Add(asmName)) return;

            try
            {
                PatchLobbyOptions(asm);
                PatchMirrorManager(asm);
            }
            catch (Exception e)
            {
                Log.LogError("Patching failed: " + e);
            }
        }

        // ---- 1. Lobby size -------------------------------------------------------------

        private void PatchLobbyOptions(Assembly asm)
        {
            var lobbyType = asm.GetType("EvilCore.Networking.EOSLobbyManager");
            if (lobbyType == null)
            {
                Log.LogError("EvilCore.Networking.EOSLobbyManager not found. Game version changed?");
                return;
            }

            var prefix = new HarmonyMethod(typeof(Plugin).GetMethod(nameof(LobbyOptionsPrefix),
                BindingFlags.Static | BindingFlags.NonPublic));

            int count = 0;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public |
                                       BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var m in lobbyType.GetMethods(flags))
            {
                if (m.IsAbstract || m.IsGenericMethodDefinition) continue;
                if (!m.GetParameters().Any(p => !p.ParameterType.IsByRef &&
                                                p.ParameterType.Name == "LobbyCreateOptions")) continue;
                try
                {
                    _harmony.Patch(m, prefix: prefix);
                    count++;
                    if (VerboseLog.Value) Log.LogInfo("Patched EOSLobbyManager." + m.Name);
                }
                catch (Exception e)
                {
                    Log.LogWarning("Could not patch " + m.Name + ": " + e.Message);
                }
            }

            Log.LogInfo("Lobby size: patched " + count + " method(s) on EOSLobbyManager.");
            if (count == 0)
                Log.LogError("No method taking LobbyCreateOptions found. The game build probably changed.");
        }

        // Rewrites LobbyCreateOptions.MaxPlayers in the arguments before the game uses them.
        // __args lets us replace boxed struct arguments without compile-time access to the type.
        private static void LobbyOptionsPrefix(object[] __args)
        {
            try
            {
                for (int i = 0; i < __args.Length; i++)
                {
                    var arg = __args[i];
                    if (arg == null || arg.GetType().Name != "LobbyCreateOptions") continue;

                    var field = arg.GetType().GetField("MaxPlayers",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (field == null) continue;

                    // The game declares MaxPlayers as uint (it feeds EOS MaxLobbyMembers), so do not
                    // assume int. Convert both ways.
                    long current;
                    try { current = Convert.ToInt64(field.GetValue(arg)); }
                    catch { continue; }

                    // Always log what the game asks for: it tells us the real value the host menu uses.
                    Log.LogInfo("Lobby create requested MaxPlayers=" + current +
                                " (field type " + field.FieldType.Name + ")");

                    // 1 = single player, anything below the game's cap = the player chose fewer.
                    if (current < GameDefault) continue;

                    int target = MaxPlayers.Value;
                    if (current == target) continue;

                    field.SetValue(arg, Convert.ChangeType(target, field.FieldType)); // arg is the boxed copy for structs
                    __args[i] = arg;
                    Log.LogInfo("Lobby MaxPlayers " + current + " -> " + target);
                }
            }
            catch (Exception e)
            {
                Log.LogError("LobbyOptionsPrefix: " + e);
            }
        }

        // ---- 2. Mirror connection cap --------------------------------------------------

        private void PatchMirrorManager(Assembly asm)
        {
            var mgrType = asm.GetType("EvilCore.Networking.MirrorNetworkManager");
            if (mgrType == null)
            {
                Log.LogError("EvilCore.Networking.MirrorNetworkManager not found. Game version changed?");
                return;
            }

            var post = new HarmonyMethod(typeof(Plugin).GetMethod(nameof(MirrorPostfix),
                BindingFlags.Static | BindingFlags.NonPublic));

            foreach (var name in new[] { "Awake", "OnStartServer", "OnStartHost" })
            {
                var m = mgrType.GetMethod(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (m == null) { Log.LogWarning("MirrorNetworkManager." + name + " not found."); continue; }
                _harmony.Patch(m, postfix: post);
                if (VerboseLog.Value) Log.LogInfo("Patched MirrorNetworkManager." + name);
            }
        }

        private static void MirrorPostfix(object __instance)
        {
            try
            {
                var field = AccessTools.Field(__instance.GetType(), "maxConnections");
                if (field == null || field.FieldType != typeof(int)) return;

                int current = (int)field.GetValue(__instance);
                int target = MaxPlayers.Value;
                // <2 is a single-player session; >=target is already high enough (Mirror default is 100).
                if (current < 2 || current >= target) return;

                field.SetValue(__instance, target);
                Log.LogInfo("Mirror maxConnections " + current + " -> " + target);
            }
            catch (Exception e)
            {
                Log.LogError("MirrorPostfix: " + e);
            }
        }
    }
}
