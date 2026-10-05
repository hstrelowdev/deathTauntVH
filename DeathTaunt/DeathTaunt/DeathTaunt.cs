using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

[BepInPlugin("hansito.DeathTaunt", "DeathTaunt", "1.0.1")]
public class DeathTaunt : BaseUnityPlugin
{
    // Sonidos cargados: clave = nombre del prefab (o causa ambiental) -> clip
    static readonly Dictionary<string, AudioClip> Sounds = new Dictionary<string, AudioClip>();
    static AudioClip DefaultSound;

    // Archivos de causas que no son mobs: nombre del archivo -> clave interna
    static readonly Dictionary<string, string> CauseFiles = new Dictionary<string, string>
    {
        { "fire", "Env_Fire" },
        { "poison", "Env_Poison" },
        { "frost", "Env_Frost" },
        { "drowning", "Env_Drowning" }
    };

    public static BepInEx.Logging.ManualLogSource Log;
    public static string PluginDir;
    public static ConfigEntry<bool> DumpMobs;

    static string currentMsg = "";
    static float msgUntil;
    GUIStyle style;

    void Awake()
    {
        Log = Logger;
        PluginDir = Path.GetDirectoryName(Info.Location);

        DumpMobs = Config.Bind(
            "Debug",
            "DumpMobList",
            true,
            "Al entrar a un mundo, escribe mobs.txt con los nombres internos de todas las criaturas del juego.");

        new Harmony("hansito.DeathTaunt").PatchAll();

        // Carga automatica: cada .wav de la carpeta sounds/ se registra con su nombre
        string dir = Path.Combine(PluginDir, "sounds");
        if (!Directory.Exists(dir))
        {
            Log.LogWarning("DeathTaunt: no existe la carpeta " + dir);
            return;
        }

        foreach (string path in Directory.GetFiles(dir, "*.wav"))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            string lower = name.ToLowerInvariant();

            string key;
            if (lower == "default") key = null;
            else if (!CauseFiles.TryGetValue(lower, out key)) key = name;

            StartCoroutine(LoadClip(path, key));
        }
    }

    void OnGUI()
    {
        if (string.IsNullOrEmpty(currentMsg) || Time.time > msgUntil) return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.fontSize = 32;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
        }

        var rect = new Rect(0, Screen.height * 0.15f, Screen.width, 60);

        // Sombra negra y texto rojo
        style.normal.textColor = Color.black;
        GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), currentMsg, style);

        style.normal.textColor = Color.red;
        GUI.Label(rect, currentMsg, style);
    }

    IEnumerator LoadClip(string path, string key)
    {
        using (var req = UnityWebRequestMultimedia.GetAudioClip("file://" + path, AudioType.WAV))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Log.LogWarning("DeathTaunt: no se pudo cargar " + path);
                yield break;
            }

            var clip = DownloadHandlerAudioClip.GetContent(req);
            if (key == null) DefaultSound = clip;
            else Sounds[key] = clip;

            Log.LogInfo("DeathTaunt: sonido cargado [" + (key ?? "default") + "]");
        }
    }

    static string DeathText(bool es, string name, string cause)
    {
        switch (cause)
        {
            case "Env_Fire":
                return es ? "El player " + name + " ha muerto quemado" : "Player " + name + " burned to death";
            case "Env_Poison":
                return es ? "El player " + name + " ha muerto envenenado" : "Player " + name + " was poisoned to death";
            case "Env_Frost":
                return es ? "El player " + name + " ha muerto congelado" : "Player " + name + " froze to death";
            case "Env_Drowning":
                return es ? "El player " + name + " se ha ahogado" : "Player " + name + " drowned";
            default:
                return es ? "El player " + name + " ha muerto" : "Player " + name + " has died";
        }
    }

    // Se ejecuta en todos los clientes que tengan el mod
    public static void RPC_DeathTaunt(long sender, string playerName, string mobToken, string prefabName)
    {
        bool es = Localization.instance.GetSelectedLanguage() == "Spanish";

        string msg;
        if (string.IsNullOrEmpty(mobToken))
        {
            msg = DeathText(es, playerName, prefabName ?? "");
        }
        else
        {
            string mob = Localization.instance.Localize(mobToken);
            if (es)
                msg = "El player " + playerName + " ha sido asesinado por " + mob;
            else
                msg = "Player " + playerName + " was killed by " + mob;
        }

        Log.LogInfo("DeathTaunt: " + msg + " [" + prefabName + "]");
        currentMsg = msg;
        msgUntil = Time.time + 5f;

        // Busca el sonido por prefijo, sin distinguir mayusculas; gana el mas especifico
        AudioClip clip = null;
        int best = -1;
        string prefab = prefabName ?? "";
        foreach (var kv in Sounds)
        {
            if (prefab.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase) && kv.Key.Length > best)
            {
                clip = kv.Value;
                best = kv.Key.Length;
            }
        }
        if (clip == null) clip = DefaultSound;

        if (clip != null && Player.m_localPlayer != null)
            AudioSource.PlayClipAtPoint(clip, Player.m_localPlayer.transform.position, 1f);
    }
}

[HarmonyPatch(typeof(Game), "Start")]
static class RegisterRpc
{
    static void Postfix()
    {
        ZRoutedRpc.instance.Register<string, string, string>("DeathTaunt", DeathTaunt.RPC_DeathTaunt);
    }
}

[HarmonyPatch(typeof(Player), "OnDeath")]
static class PlayerDeathPatch
{
    static string CauseFromHit(HitData hit)
    {
        if (hit == null) return "";

        switch (hit.m_hitType.ToString())
        {
            case "Drowning": return "Env_Drowning";
            case "Burning": return "Env_Fire";
            case "Poisoned": return "Env_Poison";
            case "Freezing": return "Env_Frost";
        }

        if (hit.m_damage.m_fire > 0f) return "Env_Fire";
        if (hit.m_damage.m_poison > 0f) return "Env_Poison";
        if (hit.m_damage.m_frost > 0f) return "Env_Frost";
        return "";
    }

    static void Prefix(Player __instance)
    {
        if (__instance != Player.m_localPlayer) return;

        var lastHit = Traverse.Create(__instance).Field("m_lastHit").GetValue<HitData>();
        Character attacker = lastHit != null ? lastHit.GetAttacker() : null;

        string token = "";
        string prefab;

        if (attacker != null)
        {
            token = attacker.m_name;
            prefab = Utils.GetPrefabName(attacker.gameObject);
        }
        else
        {
            prefab = CauseFromHit(lastHit);
        }

        DeathTaunt.Log.LogInfo("DeathTaunt hitType: " + (lastHit != null ? lastHit.m_hitType.ToString() : "null"));

        ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "DeathTaunt",
            __instance.GetPlayerName(), token, prefab);
    }
}

// Depuracion: al entrar a un mundo escribe mobs.txt con todas las criaturas del juego
[HarmonyPatch(typeof(ZNetScene), "Awake")]
static class DumpMobsPatch
{
    static void Postfix(ZNetScene __instance)
    {
        if (!DeathTaunt.DumpMobs.Value) return;

        try
        {
            var lines = new List<string>();
            foreach (GameObject prefab in __instance.m_prefabs)
            {
                if (prefab == null) continue;

                Character c = prefab.GetComponent<Character>();
                if (c == null || c is Player) continue;

                lines.Add(prefab.name + " | " + c.m_name + " | " + c.m_faction);
            }

            lines.Sort(StringComparer.OrdinalIgnoreCase);

            string file = Path.Combine(DeathTaunt.PluginDir, "mobs.txt");
            File.WriteAllLines(file, lines.ToArray());

            DeathTaunt.Log.LogInfo("DeathTaunt: mobs.txt escrito con " + lines.Count + " criaturas en " + file);
        }
        catch (Exception e)
        {
            DeathTaunt.Log.LogWarning("DeathTaunt: no se pudo escribir mobs.txt: " + e.Message);
        }
    }
}