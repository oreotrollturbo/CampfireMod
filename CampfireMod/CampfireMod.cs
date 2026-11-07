using System.Collections;
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
using static UnityEngine.Mathf;

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
        if (newScene != OWScene.TitleScreen) return;

        StartCoroutine(WaitForTittleScreen());
    }

    // I do not care enough to put them flush against the planet
    // BREAK MY SPINE
    public IEnumerator WaitForTittleScreen()
    {
        var titleScreenPlanet = GameObject.Find("Scene/Background/PlanetPivot/PlanetRoot");

        GameObject campfirePrefab;
        while ((campfirePrefab = titleScreenPlanet.transform.Find("Prefab_HEA_Campfire")?.gameObject) == null)
            yield return null;

        for (float y = 0; y < 90; y += 8f)
        {
            float r = Sin(y * Deg2Rad);
            int count = Max(1, RoundToInt(36 * r));

            for (int i = 0; i < count; i++)
            {
                float x = i * 360f / count;
                Quaternion rot = Quaternion.Euler(y, x, 0);
                
                GameObject campfire = Instantiate(campfirePrefab, titleScreenPlanet.transform);
                campfire.transform.localPosition = rot * new Vector3(-9.6813f, -57.453f, -14.6575f);
                campfire.transform.localRotation = rot * campfirePrefab.transform.localRotation;

                yield return null;

                campfire.GetComponentInChildren<Campfire>(true).SetState(Campfire.State.LIT, true);
            }
        }
    }
    
    // ----------------------------------------------------------------------

    private void RespawnPlayer(uint playerID) => new PlayerRespawnMessage(playerID).Send();
}