using System;
using System.Collections.Generic;
using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using System.Reflection;
using QSB.RespawnSync;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace CampfireMod;

public class CampfireMod : ModBehaviour
{
    public static CampfireMod Instance;
    
    public RespawnManager RespawnManager;
    private IQSBAPI QsbApi;

    private bool _isRagdolled;

    public void Awake()
    {
        Instance = this;
    }

    public void Start()
    {
        ModHelper.Console.WriteLine($"My mod {nameof(CampfireMod)} is loaded!", MessageType.Success);

        new Harmony("oreotrollturbo.CampfireMod").PatchAll(Assembly.GetExecutingAssembly());
        
        QsbApi = ModHelper.Interaction.TryGetModApi<IQSBAPI>("Raicuparta.QuantumSpaceBuddies");

        OnCompleteSceneLoad(OWScene.TitleScreen, OWScene.TitleScreen);
        LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
    }

    public void Update()
    {
        if (QsbApi == null || RespawnManager == null)
            return;

        if (!Keyboard.current.gKey.isPressed)
            return;
        
        if (Keyboard.current.rKey.wasPressedThisFrame && QsbApi.GetIsHost() && QsbApi.GetIsInMultiplayer())
            RespawnManager.RespawnSomePlayer();

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
        RespawnManager = FindObjectOfType<RespawnManager>();
        
        
        QsbApi.GetIsHost();
    }
}