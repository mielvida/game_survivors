using UnityEngine;

namespace Survivors
{
    // Fixed-angle quarter-view camera that follows the hero.
    public sealed class CameraRig : MonoBehaviour
    {
        public Camera Cam;
        Vector3 _focus;

        public void Setup(Camera cam)
        {
            Cam = cam;
            cam.orthographic = false;
            cam.fieldOfView = Config.CameraFov;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.1f, 0.12f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 160f;
            var h = Hero.I;
            _focus = h != null ? new Vector3(h.X, 0f, h.Z) : Vector3.zero;
            Apply();
        }

        void LateUpdate()
        {
            var h = Hero.I;
            if (Cam == null || h == null) return;
            var target = new Vector3(h.X, 0f, h.Z);
            _focus = Vector3.Lerp(_focus, target, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
            Apply();
        }

        void Apply()
        {
            var rot = Quaternion.Euler(Config.CameraPitch, 0f, 0f);
            Cam.transform.rotation = rot;
            Cam.transform.position = _focus + rot * new Vector3(0f, 0f, -Config.CameraDistance);
        }
    }
}
