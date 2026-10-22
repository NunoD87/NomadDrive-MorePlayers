using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace NomadDriveMorePlayers
{
    /// <summary>
    /// Raises the Nomad Drive co-op player limit. Found by reading the playtest assemblies:
    ///   1. Create Game panel: the player-count slider is clamped to 4 in two places
    ///      (CreateGamePanel.ConfigureMultiplayerSelectors and CreateGamePanel.PartySize).
    ///   2. LobbyCreateOptions.MaxPlayers -> EOS lobby MaxLobbyMembers (the real lobby size).
    ///   3. Mirror NetworkManager.maxConnections -> the EOS transport rejects joins when full.
    /// Game types are reached through reflection so the plugin builds without game DLLs.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.nunod87.nomaddrive.moreplayers";
        public const string Name = "Nomad Drive More Players";
        public const string Version = "0.2.0";

        internal const int GameDefault = 4;
        internal const int RecommendedMax = 8;
        internal const int HardMax = 12;

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> SliderMax;
        internal static ConfigEntry<int> DefaultPlayers;
        internal static ConfigEntry<bool> VerboseLog;

        // Last value the Create Game slider produced, so the lobby patches do not override a player's own pick.
        private static int _uiPick;
        private static float _uiPickTime = -1000f;

        private Harmony _harmony;
        private readonly HashSet<string> _patched = new HashSet<string>();

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                "Turn the mod on or off without uninstalling it.");

            SliderMax = Config.Bind("General", "SliderMaxPlayers", RecommendedMax,
                new ConfigDescription(
                    "Top of the player-count slider on the Create Game screen (host included). The game ships with 4. " +
                    "Pick the actual number in game with the slider. Up to " + RecommendedMax + " is recommended: " +
                    "the game's loot/economy scaling is clamped to " + RecommendedMax + ". " + (RecommendedMax + 1) +
                    "-" + HardMax + " is allowed but untested (missing seats, spawn problems, lag). " +
                    "Only the host needs this mod.",
                    new AcceptableValueRange<int>(GameDefault, HardMax)));

            DefaultPlayers = Config.Bind("General", "DefaultPlayers", 6,
                new ConfigDescription(
                    "Where the slider starts, and the lobby size used when a lobby is created without the slider " +
                    "(for example turning a single-player world into multiplayer). Capped by SliderMaxPlayers.",
                    new AcceptableValueRange<int>(2, HardMax)));

            VerboseLog = Config.Bind("Debug", "VerboseLog", false,
                "Log every patched call and value change.");

            if (!Enabled.Value)
            {
                Log.LogInfo("Disabled in config.");
                return;
            }

            if (SliderMax.Value > RecommendedMax)
                Log.LogWarning("SliderMaxPlayers=" + SliderMax.Value + " is above the recommended " +
                               RecommendedMax + ". Untested territory.");

            _harmony = new Harmony(Guid);

            // Game assemblies load lazily; patch what is loaded now and the rest as it loads.
            AppDomain.CurrentDomain.AssemblyLoad += (_, args) => TryPatch(args.LoadedAssembly);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                TryPatch(asm);

            // Force-load the assemblies we patch so we do not depend on load order.
            foreach (var name in new[] { "EvilCore.Networking", "EvilCore.UI", "com.Epic.OnlineServices", "Epic.OnlineServices" })
            {
                try { Assembly.Load(name); }
                catch (Exception e) { if (VerboseLog.Value) Log.LogInfo("Could not preload " + name + ": " + e.Message); }
            }

            Log.LogInfo(Name + " " + Version + " loaded. Slider max: " + SliderMax.Value +
                        ", default: " + EffectiveDefault());
        }

        private static int EffectiveDefault()
        {
            return Math.Min(DefaultPlayers.Value, SliderMax.Value);
        }

        private void TryPatch(Assembly asm)
        {
            string asmName;
            try { asmName = asm.GetName().Name; } catch { return; }

            bool isNet = asmName == "EvilCore.Networking";
            bool isUi = asmName == "EvilCore.UI";
            bool isEos = asmName.EndsWith("Epic.OnlineServices", StringComparison.OrdinalIgnoreCase);
            if (!(isNet || isUi || isEos) || !_patched.Add(asmName)) return;

            try
            {
                if (isNet) { PatchLobbyOptions(asm); PatchMirrorManager(asm); }
                else if (isUi) PatchCreateGamePanel(asm);
                else PatchEosSdk(asm);
            }
            catch (Exception e)
            {
                Log.LogError("Patching failed for " + asmName + ": " + e);
            }
        }

        // ---- Create Game slider ----------------------------------------------------------

        private void PatchCreateGamePanel(Assembly asm)
        {
            var panel = asm.GetType("EvilCore.UI.MainMenu.Panels.CreateGamePanel");
            if (panel == null)
            {
                Log.LogError("CreateGamePanel not found. Game version changed?");
                return;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public |
                                       BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            var configure = panel.GetMethod("ConfigureMultiplayerSelectors", flags);
            var partySize = panel.GetMethod("PartySize", flags);
            if (configure == null || partySize == null)
            {
                Log.LogError("CreateGamePanel.ConfigureMultiplayerSelectors/PartySize not found. Game version changed?");
                return;
            }

            _harmony.Patch(configure, postfix: new HarmonyMethod(typeof(Plugin).GetMethod(
                nameof(ConfigureSelectorsPostfix), BindingFlags.Static | BindingFlags.NonPublic)));
            _harmony.Patch(partySize, postfix: new HarmonyMethod(typeof(Plugin).GetMethod(
                nameof(PartySizePostfix), BindingFlags.Static | BindingFlags.NonPublic)));
            Log.LogInfo("Create Game slider: patched (max " + SliderMax.Value + ").");
        }

        // The game sets the slider range to 1..min(MaxConnections, 4). Re-apply it with our max.
        private static void ConfigureSelectorsPostfix(object __instance)
        {
            try
            {
                var sel = GetField(__instance, "maxPlayersSelector");
                if (sel == null) return; // single-player layout has no selector

                var t = sel.GetType();
                var setRange = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.Name == "SetRange" && m.GetParameters().Length == 5);
                if (setRange == null) { Log.LogError("OptionSelector.SetRange(5 args) not found."); return; }

                float min = Convert.ToSingle(GetField(sel, "_rangeMin") ?? 1f);
                float step = Convert.ToSingle(GetField(sel, "_rangeStep") ?? 1f);
                object suffix = GetField(sel, "_rangeSuffix") ?? "";
                object decimals = GetField(sel, "_rangeDecimals") ?? 0;

                var ps = setRange.GetParameters();
                var args = new object[]
                {
                    Convert.ChangeType(min, ps[0].ParameterType),
                    Convert.ChangeType((float)SliderMax.Value, ps[1].ParameterType),
                    Convert.ChangeType(step, ps[2].ParameterType),
                    suffix,
                    Convert.ChangeType(decimals, ps[4].ParameterType),
                };
                setRange.Invoke(sel, args);

                var setValue = t.GetMethod("SetRangeValueWithoutNotify",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                setValue?.Invoke(sel, new object[] { (float)EffectiveDefault() });

                Log.LogInfo("Create Game slider range 1.." + SliderMax.Value + ", starts at " + EffectiveDefault() + ".");
            }
            catch (Exception e)
            {
                Log.LogError("ConfigureSelectorsPostfix: " + e);
            }
        }

        // The game clamps the picked value to 1..4 again when it builds the lobby options.
        private static void PartySizePostfix(object __instance, ref int __result)
        {
            try
            {
                if (!(GetField(__instance, "multiplayerMode") is bool mp) || !mp) return;
                var sel = GetField(__instance, "maxPlayersSelector");
                if (sel == null) return;

                var prop = sel.GetType().GetProperty("RangeValue",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop == null) return;

                int picked = (int)Math.Round(Convert.ToSingle(prop.GetValue(sel, null)));
                picked = Math.Max(1, Math.Min(SliderMax.Value, picked));
                if (picked != __result)
                    Log.LogInfo("Party size from slider: " + __result + " -> " + picked);

                __result = picked;
                _uiPick = picked;
                _uiPickTime = Time.realtimeSinceStartup;
            }
            catch (Exception e)
            {
                Log.LogError("PartySizePostfix: " + e);
            }
        }

        private static object GetField(object o, string name)
        {
            if (o == null) return null;
            var f = AccessTools.Field(o.GetType(), name);
            return f?.GetValue(o);
        }

        // True when this lobby size is the number the host just picked on the slider.
        private static bool IsFromSlider(long value)
        {
            return _uiPick > 0 && value == _uiPick && Time.realtimeSinceStartup - _uiPickTime < 120f;
        }

        // ---- Final lobby size: the EOS SDK call that actually creates the lobby -----------

        private void PatchEosSdk(Assembly asm)
        {
            var optType = asm.GetType("Epic.OnlineServices.Lobby.CreateLobbyOptions");
            var setter = optType?.GetProperty("MaxLobbyMembers")?.GetSetMethod(true);
            if (setter == null)
            {
                Log.LogWarning("Epic.OnlineServices.Lobby.CreateLobbyOptions.MaxLobbyMembers setter not found.");
                return;
            }

            _harmony.Patch(setter, prefix: new HarmonyMethod(typeof(Plugin).GetMethod(
                nameof(MaxLobbyMembersPrefix), BindingFlags.Static | BindingFlags.NonPublic)));
            Log.LogInfo("Patched EOS CreateLobbyOptions.MaxLobbyMembers (final lobby size).");
        }

        private static void MaxLobbyMembersPrefix(ref uint value)
        {
            try
            {
                Log.LogInfo("EOS lobby MaxLobbyMembers requested: " + value);
                if (value < GameDefault) return;       // single player, or the host picked fewer
                if (IsFromSlider(value)) return;       // the host chose this on the slider
                uint target = (uint)EffectiveDefault();
                if (value == target) return;
                Log.LogInfo("EOS lobby MaxLobbyMembers " + value + " -> " + target + " (no slider pick)");
                value = target;
            }
            catch (Exception e)
            {
                Log.LogError("MaxLobbyMembersPrefix: " + e);
            }
        }

        // ---- LobbyCreateOptions.MaxPlayers (earlier in the same chain) -------------------

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

            var parse = lobbyType.GetMethod("ParseLobbyDetails", flags | BindingFlags.Static);
            if (parse != null)
            {
                try
                {
                    _harmony.Patch(parse, postfix: new HarmonyMethod(typeof(Plugin).GetMethod(
                        nameof(ParsePostfix), BindingFlags.Static | BindingFlags.NonPublic)));
                }
                catch (Exception e) { Log.LogWarning("Could not patch ParseLobbyDetails: " + e.Message); }
            }

            Log.LogInfo("Lobby size: patched " + count + " method(s) on EOSLobbyManager.");
            if (count == 0)
                Log.LogError("No method taking LobbyCreateOptions found. The game build probably changed.");
        }

        // Logs what a lobby advertises (MemberCount / MaxMembers), on host and client.
        private static void ParsePostfix(object __result)
        {
            try
            {
                if (__result == null) return;
                var t = __result.GetType();
                var max = t.GetField("MaxMembers")?.GetValue(__result);
                var cnt = t.GetField("MemberCount")?.GetValue(__result);
                Log.LogInfo("Lobby details: MemberCount=" + cnt + " MaxMembers=" + max);
            }
            catch { }
        }

        // Rewrites LobbyCreateOptions.MaxPlayers in the arguments before the game uses them,
        // unless the host picked the number on the slider.
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

                    // The game declares MaxPlayers as uint, so do not assume int.
                    long current;
                    try { current = Convert.ToInt64(field.GetValue(arg)); }
                    catch { continue; }

                    if (VerboseLog.Value)
                        Log.LogInfo("Lobby create requested MaxPlayers=" + current + " (" + field.FieldType.Name + ")");

                    if (current < GameDefault) continue;   // single player, or the host picked fewer
                    if (IsFromSlider(current)) continue;   // the host chose this on the slider

                    int target = EffectiveDefault();
                    if (current == target) continue;

                    field.SetValue(arg, Convert.ChangeType(target, field.FieldType)); // arg is the boxed copy
                    __args[i] = arg;
                    Log.LogInfo("Lobby MaxPlayers " + current + " -> " + target + " (no slider pick)");
                }
            }
            catch (Exception e)
            {
                Log.LogError("LobbyOptionsPrefix: " + e);
            }
        }

        // ---- Mirror connection cap --------------------------------------------------------

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

        // Mirror only needs to be at least as high as the biggest lobby, so use the slider max.
        private static void MirrorPostfix(object __instance)
        {
            try
            {
                var field = AccessTools.Field(__instance.GetType(), "maxConnections");
                if (field == null || field.FieldType != typeof(int)) return;

                int current = (int)field.GetValue(__instance);
                int target = SliderMax.Value;
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
