
using System;
using System.Collections;
using System.Linq;
using HarmonyLib;
using System.Reflection;
using QSB.Player;
using QSB.Utility;
using SkinChanger.Emote_System;
using UnityEngine;

namespace CampfireModSkinChanger;

public static class Extensions
{
    public static T GetOrAddComponent<T>(this Transform t) where T : Component =>
        t.GetComponent<T>() ?? t.gameObject.AddComponent<T>();
}

public class CampfireModSkinChanger : MonoBehaviour
{
    public static CampfireModSkinChanger Instance;
    public static CampfireMod.CampfireMod campfireModInstance;
    public IQSBAPI QsbAPI;

    public const string ON_GRAB_MESSAGE = "OnGrabKill";

    public void Start()
    {
        Instance = this;
        campfireModInstance = CampfireMod.CampfireMod.Instance;
        QsbAPI = campfireModInstance.QsbApi;

        new Harmony("oreotrollturbo.CampfireModSkinChanger")
            .PatchAll(Assembly.GetExecutingAssembly());

        QsbAPI.RegisterHandler<bool>(ON_GRAB_MESSAGE, OnGrabKill);
    }

    public void OnGrabKill(uint id, bool _)
    {
        var body = QsbAPI.GetPlayerBody(id);
        var emoteController = body.GetComponentInChildren<EmoteController>();
        var hand = emoteController.animator.GetBoneTransform(HumanBodyBones.LeftHand);

        var go = hand?.Find("AttachPoint")?.gameObject ?? new GameObject("AttachPoint");
        go.transform.SetParent(hand.transform);
        var attachPoint = go.transform.GetOrAddComponent<PlayerAttachPoint>();
        attachPoint._attachOffset = new Vector3(-1f, -1.8f, 2f);
        attachPoint.transform.localPosition = new Vector3(1.5841f, 0.0944f, 2.0707f);
        attachPoint.transform.localRotation = Quaternion.Euler(343.6542f, 202.9634f, 62.4527f);

        IEnumerator AttachNextFrame(PlayerAttachPoint ap)
        {
            yield return null;
            ap.AttachPlayer();
        }

        StartCoroutine(AttachNextFrame(attachPoint));
        ReticleController.Hide();
        RumbleManager.PlayGhostGrab();

        StartCoroutine(DelayUntil(() =>
        {
            var state = emoteController.animator.GetCurrentAnimatorStateInfo(3);

            return state.IsName("Ghostbird_LiftPlayer_BlowOut")
                && state.normalizedTime >= 0.838f;
        }, KillPlayer));

        void KillPlayer()
        {
            StartCoroutine(DelayUntil(() => emoteController.animator.GetCurrentAnimatorStateInfo(3).normalizedTime >= 0.95f, () => { attachPoint.DetachPlayer(); }));
            if (Locator.GetDreamWorldController().IsInDream())
                Locator.GetDreamWorldController().ExitDreamWorld(DreamWakeType.LanternBlownOut);
            else
                Locator.GetDeathManager().KillPlayer(DeathType.Dream);
        }
    }

    [HarmonyPatch(typeof(EmoteController), nameof(EmoteController.TryTriggerEmote))]
    public static class EmoteController_TryTriggerEmote_Patch
    {
        public static void Postfix(
            EmoteController __instance,
            bool __result,
            EmoteController.EmoteType type)
        {
            if (type != EmoteController.EmoteType.Inhabitant_Kill) return;

            if (!__result) return;

            var grabber = __instance
                .GetComponentInParent<PlayerCharacterController>()
                .gameObject;

            // This should only be calculated on the inhabitant's client
            if (grabber != Locator.GetPlayerBody().gameObject) return;

            var victim = GetClosestPlayer(grabber);

            if (victim == null || grabber == null) return;

            Action grabCheck = () =>
            {
                if (CanGrabPlayer(grabber, victim))
                {
                    var player = QSBPlayerManager.PlayerList
                        .FirstOrDefault(p => p.Body == victim)!;

                    Instance.QsbAPI.SendMessage(
                        ON_GRAB_MESSAGE,
                        true,
                        player.PlayerId
                    );
                }
            };

            Instance.StartCoroutine(DelayUntil(
                () => __instance.animator.GetCurrentAnimatorStateInfo(3).normalizedTime >= 0.647f && __instance.animator.GetCurrentAnimatorStateInfo(3).IsName("Ghostbird_Grab_v2"),
                grabCheck
            ));
        }

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
            if (Instance.QsbAPI == null || grabber == null) return null;

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

    public static IEnumerator DelayUntil(Func<bool> func, Action callback)
    {
        yield return new WaitUntil(func);
        callback?.Invoke();
    }
}
