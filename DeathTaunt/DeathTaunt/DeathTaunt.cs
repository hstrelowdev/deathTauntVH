using BepInEx;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

[BepInPlugin("hanzito.DeathTaunt", "DeathTaunt", "1.0.0")]
public class DeathTaunt : BaseUnityPlugin
{
    static readonly Dictionary<string, AudioClip> Sounds = new Dictionary<string, AudioClip>();
    static AudioClip DefaultSound;

    static BepInEx.Logging.ManualLogSource Log;
    static string currentMsg = "";
    static float msgUntil;
    GUIStyle style;

    void Awake()
    {
        Log = Logger;
        new Harmony("hanzito.DeathTaunt").PatchAll();

        StartCoroutine(LoadClip("default.wav", null));
        StartCoroutine(LoadClip("troll.wav", "Troll"));
        StartCoroutine(LoadClip("skeleton.wav", "Skeleton"));
        StartCoroutine(LoadClip("draugr.wav", "Draugr"));
        StartCoroutine(LoadClip("blob.wav", "Blob"));
        StartCoroutine(LoadClip("wolf.wav", "Wolf"));
        StartCoroutine(LoadClip("dragon.wav", "Dragon"));
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

        style.normal.textColor = Color.black;
        GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), currentMsg, style);

        style.normal.textColor = Color.white;
        GUI.Label(rect, currentMsg, style);
    }

    IEnumerator LoadClip(string file, string prefab)
    {
        string path = Path.Combine(Path.GetDirectoryName(Info.Location), "sounds", file);
        if (!File.Exists(path)) yield break;

        using (var req = UnityWebRequestMultimedia.GetAudioClip("file://" + path, AudioType.WAV))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) yield break;

            var clip = DownloadHandlerAudioClip.GetContent(req);
            if (prefab == null) DefaultSound = clip;
            else Sounds[prefab] = clip;
        }
    }

    // Se ejecuta en todos los clientes que tengan el mod
    public static void RPC_DeathTaunt(long sender, string playerName, string mobToken, string prefabName)
    {
        bool es = Localization.instance.GetSelectedLanguage() == "Spanish";

        string msg;
        if (string.IsNullOrEmpty(mobToken))
        {
            msg = es ? "El player " + playerName + " ha muerto"
                     : "Player " + playerName + " has died";
        }
        else
        {
            string mob = Localization.instance.Localize(mobToken);
            msg = es ? "El player " + playerName + " ha sido asesinado por " + mob
                     : "Player " + playerName + " was killed by " + mob;
        }

        Log.LogInfo("DeathTaunt: " + msg + " [" + prefabName + "]");
        currentMsg = msg;
        msgUntil = Time.time + 5f;

        AudioClip clip = null;
        string prefab = prefabName ?? "";
        foreach (var kv in Sounds)
        {
            if (prefab.StartsWith(kv.Key)) { clip = kv.Value; break; }
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
    static void Prefix(Player __instance)
    {
        if (__instance != Player.m_localPlayer) return;

        var lastHit = Traverse.Create(__instance).Field("m_lastHit").GetValue<HitData>();
        Character attacker = lastHit != null ? lastHit.GetAttacker() : null;

        string token = attacker != null ? attacker.m_name : "";
        string prefab = attacker != null ? Utils.GetPrefabName(attacker.gameObject) : "";

        ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "DeathTaunt",
            __instance.GetPlayerName(), token, prefab);
    }
}