using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

[BepInPlugin("hansito.DeathTaunt", "DeathTaunt", "1.0.0")]
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
    }
}