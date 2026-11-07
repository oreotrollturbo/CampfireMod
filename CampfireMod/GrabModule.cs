using UnityEngine;
using UnityEngine.InputSystem;

namespace CampfireMod;

public class GrabModule : MonoBehaviour
{
    public const string GrabMessage = "GrabPlayer";
    public const string ReleaseMessage = "ReleasePlayer";
    public const string ResponseMessage = "Response";

    public PlayerAttachPoint currentGrabber;
    
    public uint? currentGrabbee;
    public uint? currentGrabberID;
    
    public void Start()
    {
        CampfireMod.QsbApi.OnPlayerJoin().AddListener(OnPlayerJoin);
        
        CampfireMod.QsbApi.RegisterHandler<bool>(GrabMessage, OnPlayerGrab);
        CampfireMod.QsbApi.RegisterHandler<bool>(ReleaseMessage, OnPlayerRelease);
        CampfireMod.QsbApi.RegisterHandler<bool>(ResponseMessage, OnGrabResponse);
    }

    public void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard.rKey.wasPressedThisFrame && currentGrabbee.HasValue)
        {
            CampfireMod.Instance.ModHelper.Console.WriteLine($"Releasing player {currentGrabbee.Value}");
            CampfireMod.QsbApi.SendMessage(ReleaseMessage, true, currentGrabbee.Value);
            currentGrabbee = null;
        }
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
        attachPoint.transform.localPosition = new Vector3(1.5841f, 0.0944f, 2.0707f);
        attachPoint.transform.localRotation = Quaternion.Euler(343.6542f, 202.9634f, 62.4527f);
        
        CampfireMod.Instance.ModHelper.Console.WriteLine($"AttachPoint for {id}: {attachPoint}");

        return attachPoint;
    }

    public void OnPlayerJoin(uint id)
    {
        CampfireMod.Instance.ModHelper.Console.WriteLine($"Player with id {id} joined.");
        
        var body = CampfireMod.QsbApi.GetPlayerBody(id);
        if (body == null) return;
        
        CampfireMod.Instance.ModHelper.Console.WriteLine($"({id}) Body not null");
        
        var interactReceiver = body.AddComponent<InteractReceiver>();
        var screenPrompt = new ScreenPrompt("<CMD> Grab Player");
        screenPrompt._commandIdList = [InputConsts.InputCommandType.INTERACT];
        interactReceiver._screenPrompt = screenPrompt;
        interactReceiver.EnableInteraction();
        
        CampfireMod.Instance.ModHelper.Console.WriteLine($"({id}) Interact receiver added: {interactReceiver}");

        interactReceiver.OnPressInteract += () => GrabPlayer(id);
        
        GetOrAddAttachPoint(id);
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
    }

    public void OnGrabResponse(uint grabbeeID, bool success)
    {
        if (!success)
        {
            CampfireMod.Instance.ModHelper.Console.WriteLine($"Player {grabbeeID} refused grab request");
            return;
        }
        
        currentGrabbee = grabbeeID;
        
        CampfireMod.Instance.ModHelper.Console.WriteLine($"Successfully grabbed player {grabbeeID}");
    }
}