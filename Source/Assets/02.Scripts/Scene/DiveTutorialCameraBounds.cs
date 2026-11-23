using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DiveTutorialCameraBounds : CinemachineExtension
{
    [SerializeField] BoxCollider2D _cameraZone;

    const float EdgePadding = 0.005f;

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize || _cameraZone == null || state.Lens.Orthographic) return;

        Bounds zone = _cameraZone.bounds;
        Vector3 position = state.GetFinalPosition();
        float distance = zone.center.z - position.z;
        var lens = state.Lens;
        if (distance <= 0f || lens.Aspect <= 0f) return;

        // Check the final view after the Cinemachine impulse has rotated or moved the camera.
        Quaternion orientation = state.GetFinalOrientation();
        float halfHeight = Mathf.Tan(lens.FieldOfView * 0.5f * Mathf.Deg2Rad);
        float width = zone.size.x - 2f * EdgePadding;
        float height = zone.size.y - 2f * EdgePadding;
        Rect view = ProjectView(orientation, distance, lens.Aspect, halfHeight);
        if (view.width > width || view.height > height)
        {
            // Only narrow the lens when the full view cannot fit, such as on an ultrawide display.
            float lower = 0f;
            float upper = halfHeight;
            for (int i = 0; i < 14; i++)
            {
                float middle = (lower + upper) * 0.5f;
                view = ProjectView(orientation, distance, lens.Aspect, middle);
                if (view.width <= width && view.height <= height) lower = middle;
                else upper = middle;
            }

            lens.FieldOfView = 2f * Mathf.Atan(lower) * Mathf.Rad2Deg;
            state.Lens = lens;
            view = ProjectView(orientation, distance, lens.Aspect, lower);
        }

        float x = Mathf.Clamp(0f, zone.min.x + EdgePadding - position.x - view.xMin,
            zone.max.x - EdgePadding - position.x - view.xMax);
        float y = Mathf.Clamp(0f, zone.min.y + EdgePadding - position.y - view.yMin,
            zone.max.y - EdgePadding - position.y - view.yMax);
        state.PositionCorrection += new Vector3(x, y, 0f);
    }

    static Rect ProjectView(Quaternion orientation, float distance, float aspect, float halfHeight)
    {
        Vector2 min = Vector2.positiveInfinity;
        Vector2 max = Vector2.negativeInfinity;
        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                Vector3 ray = orientation * new Vector3(x * halfHeight * aspect, y * halfHeight, 1f);
                Vector2 corner = (Vector2)ray * (distance / ray.z);
                min = Vector2.Min(min, corner);
                max = Vector2.Max(max, corner);
            }
        }

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
