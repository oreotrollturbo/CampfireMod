using UnityEngine;

namespace CampfireMod;

[RequireComponent(typeof(OWRigidbody))]
public class RaycastModule : MonoBehaviour
{
    private OWRigidbody _rb;

    public void Start()
    {
        _rb = this.GetRequiredComponent<OWRigidbody>();
    }
    
    // Taken from New Horizons lol
    public struct RaycastData
    {
        public bool hit;
        public Vector3 pos;
        public Vector3 norm;

        public Rigidbody hitRb;
    }

    public RaycastData Raycast()
    {
        var data = new RaycastData();
        
        _rb.DisableCollisionDetection();

        try
        {
            int layerMask = OWLayerMask.physicalMask;
            var actCam = Locator.GetActiveCamera();
            var origin = actCam.transform.position;
            var direction = actCam.transform.forward;

            data.hit = Physics.Raycast(origin, direction, out var hitInfo, 100f, layerMask);
            if (data.hit)
            {
                data.pos = hitInfo.rigidbody.transform.InverseTransformPoint(hitInfo.point);
                data.norm = hitInfo.rigidbody.transform.InverseTransformDirection(hitInfo.normal);

                data.hitRb = hitInfo.rigidbody;
            }
        } catch { }
        
        _rb.EnableCollisionDetection();
        return data;
    }
}