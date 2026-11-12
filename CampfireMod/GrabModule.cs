using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using QSB.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CampfireMod;

public static class GrabCheck {
    public static bool CanGrabPlayer(GameObject grabber, GameObject victim)
    {
        if (grabber == null || victim == null) return false;

        Vector3 localDistance = grabber.transform
            .InverseTransformPoint(victim.transform.position);

        float horizontalDistance = new Vector2(
            localDistance.x,
            localDistance.z
        ).magnitude;

        float angle = Vector3.Angle(
            Vector3.forward,
            new Vector3(localDistance.x, 0f, localDistance.z)
        );

        bool validDistance = horizontalDistance < 4f;
        bool validHeight = localDistance.y is > -2f and < 3f;
        bool validAngle = angle < 20f;

        bool result = validDistance && validHeight && validAngle;

        return result;
    }

    public static GameObject GetClosestPlayer(GameObject grabber)
    {
        if (CampfireMod.QsbApi == null || grabber == null) return null;

        GameObject closest = null;
        float closestDistance = float.MaxValue;

        foreach (var player in QSBPlayerManager.PlayerList)
        {
            var body = player.Body;

            if (body == null)
                continue;

            if (body == grabber)
                continue;

            float distance = (
                body.transform.position - grabber.transform.position
            ).sqrMagnitude;

            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            closest = body;
        }

        return closest;
    }
}

public class GrabModule : MonoBehaviour
{
    public const string GrabMessage = "GrabPlayer";
    public const string ReleaseMessage = "ReleasePlayer";
    public const string ResponseMessage = "Response";

    private readonly ScreenPrompt _releasePrompt = new(
        InputLibrary.cancel,
        "Release Player" + "   <CMD>"
    );
    
    public List<InteractReceiver> interactReceivers = new();

    public PlayerAttachPoint currentGrabber;
    
    public uint? currentGrabbee;
    public uint? currentGrabberID;
    
    public void Start()
    {
        CampfireMod.QsbApi.OnPlayerJoin().AddListener(OnPlayerJoin);
        
        CampfireMod.QsbApi.RegisterHandler<bool>(GrabMessage, OnPlayerGrab);
        CampfireMod.QsbApi.RegisterHandler<bool>(ReleaseMessage, OnPlayerRelease);
        CampfireMod.QsbApi.RegisterHandler<bool>(ResponseMessage, OnGrabResponse);
        
        Locator.GetPromptManager().AddScreenPrompt(_releasePrompt, PromptPosition.UpperRight);
    }

    public void OnDestroy()
    {
        Locator.GetPromptManager()?.RemoveScreenPrompt(_releasePrompt);
    }

    public void Update()
    {
        var canRelease = Keyboard.current.gKey.isPressed && currentGrabbee.HasValue;
        _releasePrompt.SetVisibility(canRelease);

        if (canRelease && OWInput.IsNewlyPressed(InputLibrary.cancel))
        {
            CampfireMod.QsbApi.SendMessage(ReleaseMessage, true, currentGrabbee.Value);
            currentGrabbee = null;
            
            foreach (var receiver in interactReceivers)
                receiver.EnableInteraction();
        }
        
        if (Keyboard.current.gKey.isPressed && currentGrabbee.HasValue)
            _releasePrompt.SetVisibility(true);
    }
    
    public PlayerAttachPoint GetOrAddAttachPoint(uint id)
    {
        var body = CampfireMod.QsbApi.GetPlayerBody(id);
        if (body == null) return null;
        
        var hand = body.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.LeftHand);
        var go = hand?.Find("AttachPoint (Grab)")?.gameObject ?? new GameObject("AttachPoint (Grab)");
        go.transform.SetParent(hand.transform);
        var attachPoint = go.transform.GetOrAddComponent<PlayerAttachPoint>();
        attachPoint._attachOffset = new Vector3(-1f, -1.8f, 2f);
        attachPoint.transform.localPosition = new Vector3(1.5841f, -0.5944f, 2.0707f);
        attachPoint.transform.localRotation = Quaternion.Euler(343.6542f, 202.9634f, 62.4527f);
        
        CampfireMod.Instance.ModHelper.Console.WriteLine($"AttachPoint for {id}: {attachPoint}");

        return attachPoint;
    }

    public void OnPlayerJoin(uint id)
    {
        CampfireMod.Instance.ModHelper.Console.WriteLine($"Player with id {id} joined.");
        StartCoroutine(WaitForPlayerBod(id));
    }
    
    public IEnumerator WaitForPlayerBod(uint id)
    {
        CampfireMod.Instance.ModHelper.Console.WriteLine($"({id}) Waiting for player body...");

        GameObject body;
        while ((body = CampfireMod.QsbApi.GetPlayerBody(id)) == null)
            yield return null;
        
        CampfireMod.Instance.ModHelper.Console.WriteLine($"Found player body for ID {id} | Adding attach point & interaction volume.");

        GetOrAddAttachPoint(id);

        GameObject grabObject = new GameObject("Grab Interaction Volume");
        grabObject.transform.SetParent(body.transform, false);
        grabObject.layer = LayerMask.NameToLayer("Interactible");
        
        var collider = grabObject.AddComponent<CapsuleCollider>();
        collider.radius = 0.5f;
        collider.height = 2f;
        collider.center = Vector3.zero;
        
        var receiver = grabObject.AddComponent<InteractReceiver>();

        receiver.SetInteractRange(1.5f);
        receiver.ChangePrompt("Grab Player");
        receiver.OnPressInteract += () => GrabPlayer(id);
        receiver.EnableInteraction();

        interactReceivers.Add(receiver);

        // The attach point does not initialize until a frame later
        yield return null;
    }

    public void GrabPlayer(uint player)
    {
        CampfireMod.Instance.ModHelper.Console.WriteLine($"Grabbing player {player}");
        CampfireMod.QsbApi.SendMessage(GrabMessage, true, player);
    }

    public void OnPlayerGrab(uint grabberID, bool _)
    {
        CampfireMod.Instance.ModHelper.Console.WriteLine($"Player grab request from {grabberID}");

        if (!CampfireMod.Instance.ModHelper.Config.GetSettingsValue<bool>("Allow Other Players To Grab") || currentGrabber != null)
        {
            CampfireMod.QsbApi.SendMessage(ResponseMessage, false, grabberID);
            return;
        }
        
        CampfireMod.Instance.ModHelper.Console.WriteLine($"Activating Grab");
        
        var attachPoint = GetOrAddAttachPoint(grabberID);
        if (attachPoint == null)
        {
            CampfireMod.QsbApi.SendMessage(ResponseMessage, false, grabberID);
            return;
        }
        
        CampfireMod.Instance.ModHelper.Console.WriteLine($"AttachPoint for {grabberID}: {attachPoint} (NOT NULL)");
        
        attachPoint.AttachPlayer();
        currentGrabber = attachPoint;
        currentGrabberID = grabberID;
        
        CampfireMod.QsbApi.SendMessage(ResponseMessage, true, grabberID);
    }

    public void OnPlayerRelease(uint nicePersonID, bool _)
    {
        CampfireMod.Instance.ModHelper.Console.WriteLine($"Player release request from {nicePersonID}");

        if (currentGrabber == null)
        {
            CampfireMod.Instance.ModHelper.Console.WriteLine($"currentGrabber is null despite being grabbed!");
            return;
        }

        if (currentGrabberID != nicePersonID)
            return;
        
        currentGrabber.DetachPlayer();
        currentGrabber = null;
        currentGrabberID = null;
        
        foreach (var receiver in interactReceivers)
            receiver.EnableInteraction();
    }

    public void OnGrabResponse(uint grabbeeID, bool success)
    {
        if (!success)
        {
            CampfireMod.Instance.ModHelper.Console.WriteLine($"Player {grabbeeID} refused grab request");
            
            foreach (var receiver in interactReceivers)
                receiver.EnableInteraction();
            
            return;
        }
        
        currentGrabbee = grabbeeID;
        
        CampfireMod.Instance.ModHelper.Console.WriteLine($"Successfully grabbed player {grabbeeID}");
        
        foreach (var receiver in interactReceivers)
            receiver.DisableInteraction();
    }
}