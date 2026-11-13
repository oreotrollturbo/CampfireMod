using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using System.Reflection;
using QSB.Messaging;
using QSB.RespawnSync;
using QSB.RespawnSync.Messages;
using QSB.Utility;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CampfireMod;

public static class Extensions
{
    public static T GetOrAddComponent<T>(this Transform t) where T : Component =>
        t.GetComponent<T>() ?? t.gameObject.AddComponent<T>();

    public static string GetPath(this Transform current)
    {
        if (current.parent == null) return current.name;
        return current.parent.GetPath() + "/" + current.name;
    }
}

[Serializable]
public struct SVector3
{
    public float x, y, z;
    public static implicit operator SVector3(Vector3 v) => new() { x = v.x, y = v.y, z = v.z };
    public static implicit operator Vector3(SVector3 v) => new() { x = v.x, y = v.y, z = v.z };
}

// TODO:
// Add toggle for anglerfish spawn in solar system

public class CampfireMod : ModBehaviour
{
    public static CampfireMod Instance;
    
    public static IQSBAPI QsbApi;
    public static GrabModule grabModule;
    public static RaycastModule raycastModule;

    private bool _isRagdolled;

    public static GameObject CampfirePrefab;

    private List<GameObject> _spawnedCampfires = new();
    private const string SpawnCampfireMessage = "SpawnCampfire";
    
    public void Awake() => Instance = this;

    public void Start()
    {
        ModHelper.Console.WriteLine("Let's gather 'round the campfire and sing our campfire song.", MessageType.Success);

        new Harmony("oreotrollturbo.CampfireMod").PatchAll(Assembly.GetExecutingAssembly());
        
        // Dependencies
        QsbApi = ModHelper.Interaction.TryGetModApi<IQSBAPI>("Raicuparta.QuantumSpaceBuddies");

        // Optional skin changer (should use this for RUMBLE mods)
        if (ModHelper.Interaction.ModExists("pikpik_carrot.SkinChanger"))
        {
            var campfireSkinChanger = Assembly.LoadFrom(Path.Combine(ModHelper.Manifest.ModFolderPath, "CampfireModSkinChanger.dll"));
            gameObject.AddComponent(campfireSkinChanger.GetType("CampfireModSkinChanger.CampfireModSkinChanger", true));
        }
        
        // ------------

        grabModule = gameObject.AddComponent<GrabModule>();
        
        QsbApi.RegisterHandler<(SVector3, SVector3, string)>(SpawnCampfireMessage, OnCampfireSpawned);

        OnCompleteSceneLoad(OWScene.TitleScreen, OWScene.TitleScreen);
        LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
    }

    public void Update()
    {
        if (QsbApi == null || RespawnManager.Instance == null)
            return;

        if (!Keyboard.current.gKey.isPressed)
            return;
        
        // Respawn oldest dead player
        if (Keyboard.current.rKey.wasPressedThisFrame && QsbApi.GetIsHost() && QsbApi.GetIsInMultiplayer())
            RespawnManager.Instance.RespawnSomePlayer();

        // Ragdolling
        if (Keyboard.current.uKey.wasPressedThisFrame)
        {
            var controller = Locator.GetPlayerBody()?.transform.GetComponent<PlayerCharacterController>();
            if (!_isRagdolled)
                controller?.OnPlayerDeath(DeathType.Impact);
            else
                controller?.OnPlayerResurrection();
                
            _isRagdolled = !_isRagdolled;
        }

        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            if (CampfirePrefab == null) return;

            var raycast = raycastModule.Raycast();
            if (!raycast.hit) return;

            (SVector3, SVector3, string) data = (raycast.pos, raycast.norm, raycast.hitRb.transform.GetPath());
            OnCampfireSpawned(0, data);
            QsbApi.SendMessage(SpawnCampfireMessage, data);
        }
    }

    public void OnCompleteSceneLoad(OWScene previousScene, OWScene newScene)
    {
        if (newScene == OWScene.SolarSystem)
        {
            var campfire = FindObjectOfType<Campfire>().transform.parent;
            CampfirePrefab = Instantiate(campfire.gameObject);
            CampfirePrefab.SetActive(false);
            DontDestroyOnLoad(CampfirePrefab);
            
            grabModule.interactReceivers.Clear();
            foreach (var id in QsbApi.GetPlayerIDs())
                grabModule.OnPlayerJoin(id);

            Delay.RunWhen(() => Locator.GetPlayerBody() != null, () =>
            {
                raycastModule = Locator.GetPlayerBody().gameObject.AddComponent<RaycastModule>();
            });

            GameObject.Find("TimberHearth_Body/Sector_TH/Sector_Village/Interactables_Village/LogLift").AddComponent<ElevatorFunny>();
        }
    }

    public override void Configure(IModConfig config)
    {
        if (!config.GetSettingsValue<bool>("Allow Other Players To Grab") && PlayerState.IsAttached() && grabModule.currentGrabber != null)
        {
            grabModule.currentGrabber.DetachPlayer();
            grabModule.currentGrabber = null;
            QsbApi.SendMessage(GrabModule.ResponseMessage, false, grabModule.currentGrabberID.Value);
        }
    }

    // ----------------------------------------------------------------------

    private void RespawnPlayer(uint playerID) => new PlayerRespawnMessage(playerID).Send();

    private void OnCampfireSpawned(uint playerID, (SVector3 pos, SVector3 norm, string path) data)
    {
        var parent = GameObject.Find(data.path);
        if (parent == null) return;
        
        var campfire = Instantiate(CampfirePrefab, parent.transform);
        campfire.SetActive(true);
        campfire.transform.localPosition = data.pos;
        campfire.transform.localRotation = Quaternion.FromToRotation(Vector3.up, data.norm);
        campfire.GetComponentInChildren<Campfire>().SetState(Campfire.State.LIT);
        campfire.GetComponentInChildren<InteractReceiver>().EnableInteraction();
        campfire.GetComponentInChildren<HeatHazardVolume>().GetComponent<SphereShape>().enabled = true;

        _spawnedCampfires.Add(campfire);

        while (_spawnedCampfires.Count > ModHelper.Config.GetSettingsValue<int>("Maximum number of Campfires"))
        {
            Destroy(_spawnedCampfires[0]);
            _spawnedCampfires.RemoveAt(0);
        }
    }
}