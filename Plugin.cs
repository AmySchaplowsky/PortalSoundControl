using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace PortalSoundControl
{
    [BepInPlugin(GUID, "Portal Sound Control", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "local.portalsoundcontrol";

        private ConfigEntry<bool> _mute;
        private ConfigEntry<float> _volume;
        private ConfigEntry<bool> _affectAmbient;
        private ConfigEntry<bool> _affectTeleport;

        // Portal hum etc.: audio sources on live portal objects, adjusted every frame.
        private class Tracked
        {
            public AudioSource Src;
            public float Base;
            public float LastSet = -1f;
            public bool MutedByUs;
        }
        private readonly List<Tracked> _ambient = new List<Tracked>();
        private readonly HashSet<AudioSource> _ambientKnown = new HashSet<AudioSource>();

        // Teleport / activation sounds: effect prefabs referenced by the portal and player, adjusted at prefab level.
        private readonly HashSet<GameObject> _effectPrefabs = new HashSet<GameObject>();
        private readonly Dictionary<AudioSource, float> _effSrcBase = new Dictionary<AudioSource, float>();
        private readonly Dictionary<ZSFX, float[]> _effZsfxBase = new Dictionary<ZSFX, float[]>();

        private const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly FieldInfo MinVol = typeof(ZSFX).GetField("m_minVol", AnyInstance);
        private static readonly FieldInfo MaxVol = typeof(ZSFX).GetField("m_maxVol", AnyInstance);

        private float _nextEffectScan, _nextAmbientScan;
        private bool _lastMute, _lastAmbient, _lastTeleport;
        private float _lastVolume = -1f;

        private void Awake()
        {
            _mute = Config.Bind("General", "Mute", false, "Mute portal sounds completely.");
            _volume = Config.Bind("General", "Volume", 1f,
                new ConfigDescription("Portal sound volume. 0 = silent, 1 = normal.", new AcceptableValueRange<float>(0f, 1f)));
            _affectAmbient = Config.Bind("General", "AffectAmbientHum", true, "Apply to the sounds portals make while standing (hum, loops).");
            _affectTeleport = Config.Bind("General", "AffectTeleportEffects", true, "Apply to the teleport / activation sound effects.");

            if (MinVol == null || MaxVol == null)
                Logger.LogWarning("ZSFX volume fields not found (m_minVol/m_maxVol). Teleport effect volume scaling will rely on AudioSource volume only.");
            Logger.LogInfo("Loaded. Mute=" + _mute.Value + " Volume=" + _volume.Value);
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            bool changed = _mute.Value != _lastMute || _volume.Value != _lastVolume ||
                           _affectAmbient.Value != _lastAmbient || _affectTeleport.Value != _lastTeleport;
            _lastMute = _mute.Value; _lastVolume = _volume.Value;
            _lastAmbient = _affectAmbient.Value; _lastTeleport = _affectTeleport.Value;

            if (now >= _nextEffectScan) { _nextEffectScan = now + 3f; ApplyEffects(true); }
            else if (changed) ApplyEffects(false);

            if (now >= _nextAmbientScan) { _nextAmbientScan = now + 0.5f; RefreshAmbient(); }
            UpdateAmbient();
        }

        // ---------- ambient (live portal audio sources) ----------

        private void RefreshAmbient()
        {
            _ambientKnown.RemoveWhere(s => s == null);
            foreach (var tw in FindObjectsOfType<TeleportWorld>())
            {
                foreach (var src in tw.GetComponentsInChildren<AudioSource>(true))
                {
                    if (!_ambientKnown.Add(src)) continue;
                    _ambient.Add(new Tracked { Src = src });
                    Logger.LogInfo("Portal audio source: " + src.transform.root.name + "/" + src.name +
                                   " clip=" + (src.clip != null ? src.clip.name : "null") + " loop=" + src.loop);
                }
            }
        }

        private void UpdateAmbient()
        {
            bool affect = _affectAmbient.Value;
            float scale = _mute.Value ? 0f : _volume.Value;
            bool mute = _mute.Value || _volume.Value <= 0f;

            for (int i = _ambient.Count - 1; i >= 0; i--)
            {
                var t = _ambient[i];
                if (t.Src == null) { _ambient.RemoveAt(i); continue; }

                if (!affect)
                {
                    if (t.LastSet >= 0f)
                    {
                        t.Src.volume = t.Base;
                        if (t.MutedByUs) t.Src.mute = false;
                        t.LastSet = -1f;
                        t.MutedByUs = false;
                    }
                    continue;
                }

                // If the game changed the volume since we last set it, that is the new base value.
                if (Mathf.Abs(t.Src.volume - t.LastSet) > 0.0001f) t.Base = t.Src.volume;
                float target = t.Base * scale;
                t.Src.volume = target;
                t.LastSet = target;

                if (mute && !t.Src.mute) { t.Src.mute = true; t.MutedByUs = true; }
                else if (!mute && t.MutedByUs) { t.Src.mute = false; t.MutedByUs = false; }
            }
        }

        // ---------- effects (teleport / activation sound prefabs) ----------

        private void CollectEffectPrefabs()
        {
            var portals = Resources.FindObjectsOfTypeAll<TeleportWorld>();
            foreach (var tw in portals) ScanLists(tw, "TeleportWorld", null);
            foreach (var pl in Resources.FindObjectsOfTypeAll<Player>()) ScanLists(pl, "Player", new[] { "teleport", "portal" });
        }

        private void ScanLists(object owner, string label, string[] nameFilter)
        {
            foreach (var f in AllFields(owner.GetType()))
            {
                if (f.FieldType != typeof(EffectList)) continue;
                if (nameFilter != null)
                {
                    string n = f.Name.ToLowerInvariant();
                    bool ok = false;
                    foreach (var k in nameFilter) if (n.Contains(k)) ok = true;
                    if (!ok) continue;
                }
                var list = f.GetValue(owner) as EffectList;
                if (list == null || list.m_effectPrefabs == null) continue;
                foreach (var ed in list.m_effectPrefabs)
                    if (ed != null && ed.m_prefab != null && _effectPrefabs.Add(ed.m_prefab))
                        Logger.LogInfo("Effect: " + label + "." + f.Name + " -> prefab '" + ed.m_prefab.name + "'");
            }
        }

        private void ApplyEffects(bool rescan)
        {
            if (rescan) CollectEffectPrefabs();

            bool affect = _affectTeleport.Value;
            float scale = affect ? (_mute.Value ? 0f : _volume.Value) : 1f;
            bool mute = affect && (_mute.Value || _volume.Value <= 0f);

            foreach (var prefab in _effectPrefabs)
            {
                if (prefab == null) continue;

                foreach (var src in prefab.GetComponentsInChildren<AudioSource>(true))
                {
                    float b;
                    if (!_effSrcBase.TryGetValue(src, out b)) { b = src.volume; _effSrcBase[src] = b; }
                    src.volume = b * scale;
                    src.mute = mute;
                }
                foreach (var zs in prefab.GetComponentsInChildren<ZSFX>(true))
                {
                    float[] b;
                    if (!_effZsfxBase.TryGetValue(zs, out b)) { b = new[] { GetF(MinVol, zs), GetF(MaxVol, zs) }; _effZsfxBase[zs] = b; }
                    SetF(MinVol, zs, b[0] * scale);
                    SetF(MaxVol, zs, b[1] * scale);
                }
            }
        }

        private static float GetF(FieldInfo f, object o) { return f == null ? 1f : (float)f.GetValue(o); }
        private static void SetF(FieldInfo f, object o, float v) { if (f != null) f.SetValue(o, v); }

        private static IEnumerable<FieldInfo> AllFields(Type t)
        {
            const BindingFlags fl = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            for (; t != null && t != typeof(object) && t != typeof(MonoBehaviour); t = t.BaseType)
                foreach (var f in t.GetFields(fl)) yield return f;
        }
    }
}
