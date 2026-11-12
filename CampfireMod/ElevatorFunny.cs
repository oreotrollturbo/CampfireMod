using System;
using HarmonyLib;
using QSB.Menus;
using UnityEngine;

namespace CampfireMod;

[RequireComponent(typeof(Elevator))]
public class ElevatorFunny : MonoBehaviour
{
    private Elevator _elevator;
    private GameObject _target;

    private bool _wasMoving;
    private bool _returning;

    private void OnEnable()
    {
        GlobalMessenger<DeathType>.AddListener("PlayerDeath", OnPlayerDeath);
    }

    private void OnDisable()
    {
        GlobalMessenger<DeathType>.RemoveListener("PlayerDeath", OnPlayerDeath);
    }

    private void OnPlayerDeath(DeathType type)
    {
        if (_wasMoving && type == DeathType.Energy && !PlayerData.GetPersistentCondition("CAMPFIRE_INITIATED"))
        {
            AccessTools.Method(typeof(MenuManager), "OpenInfoPopup", [typeof(string), typeof(string)])
                .Invoke(MenuManager.Instance, [
                    "Welcome to Campfire :3",
                    "Oh..."
                ]);
            
            PlayerData.SetPersistentCondition("CAMPFIRE_INITIATED", true);
        }
    }

    private void Start()
    {
        _elevator = GetComponent<Elevator>();
        _elevator._liftDuration = 20f;
        _target = Locator.GetAstroObject(AstroObject.Name.Sun).gameObject;

        Destroy(GetComponentInParent<SectorCullGroup>());
    }
    
    public void Update()
    {
        if (_elevator == null) return;

        if (_wasMoving && !_elevator.enabled)
        {
            if (_returning)
            {
                _returning = false;
            }
            else
            {
                _returning = true;
                _elevator._goingToTheEnd = false;
                _elevator._targetLocalPos = _elevator._startLocalPos;
                _elevator._initLocalPos = _elevator.transform.localPosition;
                _elevator._initLiftTime = Time.time;
                _elevator.enabled = true;
            }
        }

        _wasMoving = _elevator.enabled;
        
        if (!_returning)
            _elevator._targetLocalPos = _elevator.transform.parent.InverseTransformPoint(_target.transform.position);
    }
}