using InventorySystem.Items.Firearms;
using InventorySystem.Items.Pickups;
using LabApi.Features.Wrappers;
using Sunrise.API.Visibility;

namespace Sunrise.Features.AntiWallHack.PickupAntiWallHack;

internal sealed class PickupPoints(ItemPickupBase pickup)
{
    readonly Vector3[] points = new Vector3[6];
    Vector3? position;
    Quaternion rotation;
    Vector3 scale;
    FirearmWorldmodel? _model;
    Renderer[]? _renderers;
    Collider[]? _colliders;
    uint attachments;
    float nextDraw;

    public bool IsVisible(Vector3 camera, Room? observerRoom)
    {
        Room? room = VisibilityData.GetRoom(pickup.Position);

        if (observerRoom != null && !VisibilityData.Get(observerRoom).IsVisible(room))
            return false;

        UpdatePoints();

        int mask = VisibilityData.GetVisionMask(observerRoom, room);

        foreach (Vector3 point in points)
        {
            if (!Physics.Linecast(camera, point, out RaycastHit hit, mask, QueryTriggerInteraction.Ignore) || hit.transform.IsChildOf(pickup.transform))
                return true;
        }

        return false;
    }

    void UpdatePoints()
    {
        FirearmWorldmodel? model = (pickup as InventorySystem.Items.Firearms.FirearmPickup)?._worldmodelInstance;

        uint code = model != null ? model.AttachmentCode : 0;

        Vector3 currentPosition = pickup.Position;
        Vector3 currentScale = pickup.transform.lossyScale;
        Quaternion currentRotation = pickup.Rotation;

        if (position != currentPosition || rotation != currentRotation || scale != currentScale || _model != model || attachments != code)
        {
            position = currentPosition;
            rotation = currentRotation;
            scale = currentScale;
            _model = model;
            attachments = code;

            Bounds bounds = CalculateBounds(model, currentPosition);

            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents + Vector3.one * 0.15f;

            points[0] = center;
            points[1] = center + Vector3.up * extents.y;
            points[2] = center + Vector3.right * extents.x;
            points[3] = center - Vector3.right * extents.x;
            points[4] = center + Vector3.forward * extents.z;
            points[5] = center - Vector3.forward * extents.z;
        }

        if (Config.Instance.DebugPrimitives && Time.time >= nextDraw)
        {
            nextDraw = Time.time + 0.1f;

            foreach (Vector3 point in points)
                Debug.DrawPoint(point, Colors.Orange * 50, 0.11f);
        }
    }

    Bounds CalculateBounds(FirearmWorldmodel? model, Vector3 position)
    {
        Bounds bounds = default;

        foreach (Renderer renderer in model != null ? model.Renderers : (_renderers ??= pickup.GetComponentsInChildren<Renderer>(true)))
        {
            if (renderer != null && renderer is MeshRenderer or SkinnedMeshRenderer && renderer.enabled && renderer.gameObject.activeInHierarchy)
                Include(renderer.bounds);
        }

        if (bounds.size != Vector3.zero)
            return bounds;

        foreach (Collider collider in model != null && model.Colliders.Length > 0 ? model.Colliders : (_colliders ??= pickup.GetComponentsInChildren<Collider>(true)))
        {
            if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
                Include(collider.bounds);
        }

        return bounds.size != Vector3.zero ? bounds : new Bounds(position, Vector3.one * 0.1f);

        void Include(Bounds part)
        {
            if (part.size == Vector3.zero)
                return;

            if (bounds.size == Vector3.zero)
                bounds = part;
            else
                bounds.Encapsulate(part);
        }
    }
}
