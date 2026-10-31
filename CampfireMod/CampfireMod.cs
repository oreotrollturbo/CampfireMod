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
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace CampfireMod;

public class CampfireMod : ModBehaviour
{
    public static CampfireMod Instance;
    
    public IQSBAPI QsbApi;

    private bool _isRagdolled;

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
    }

    public void OnCompleteSceneLoad(OWScene previousScene, OWScene newScene)
    {
        if (newScene != OWScene.SolarSystem) return;
    }
    
    // ----------------------------------------------------------------------

    private void RespawnPlayer(uint playerID) => new PlayerRespawnMessage(playerID).Send();
}