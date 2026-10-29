using System;
using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using System.Reflection;
using QSB.RespawnSync;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CampfireMod;

public class CampfireMod : ModBehaviour
{
    public static CampfireMod Instance;
    
    public RespawnManager RespawnManager;
    private IQSBAPI QsbApi;

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
        if (Keyboard.current.ctrlKey.isPressed && Keyboard.current.rKey.wasPressedThisFrame 
                                               && QsbApi != null && QsbApi.GetIsHost() && RespawnManager != null)
        {
            RespawnManager.RespawnSomePlayer();
        }
    }

    public void OnCompleteSceneLoad(OWScene previousScene, OWScene newScene)
    {
        if (newScene != OWScene.SolarSystem) return;
        RespawnManager = FindObjectOfType<RespawnManager>();
        
        
        QsbApi.GetIsHost();
    }
}