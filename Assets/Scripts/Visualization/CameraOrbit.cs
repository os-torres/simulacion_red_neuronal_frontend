using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

/// <summary>
/// Controlador de cámara orbital para la visualización 3D.
/// Usa el nuevo Input System (com.unity.inputsystem).
///
///  - Click izquierdo + arrastrar  → orbitar alrededor del target
///  - Rueda del ratón              → zoom
///  - Click derecho  + arrastrar   → pan (mover el target)
///
/// El input solo se procesa cuando el cursor está sobre el viewportRect asignado.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraOrbit : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────────
    [Header("Punto de órbita")]
    [SerializeField] private Transform target;

    [Header("Sensibilidad")]
    [SerializeField] private float orbitSpeed = 200f;
    [SerializeField] private float zoomSpeed  = 5f;
    [SerializeField] private float panSpeed   = 0.02f;

    [Header("Límites de distancia")]
    [SerializeField] private float minDistance = 3f;
    [SerializeField] private float maxDistance = 25f;

    [Header("Restricción de viewport")]
    [SerializeField] private RectTransform viewportRect;   // RawImage que muestra la vista 3D

    // ── Estado privado ─────────────────────────────────────────────────────────
    private float   _distance = 12f;
    private float   _yaw      = 45f;
    private float   _pitch    = 30f;
    private Vector3 _targetPos;

    private const float DefaultDistance = 12f;
    private const float DefaultYaw      = 45f;
    private const float DefaultPitch    = 30f;

    // Cache de controles del nuevo Input System
    private Mouse    _mouse;
    private Vector2  _prevMousePos;

    // ── Lifecycle ──────────────────────────────────────────────────────────────
    private void Awake()
    {
        _targetPos = (target != null) ? target.position : Vector3.zero;
        _mouse     = Mouse.current;
        ApplyCameraTransform();
    }

    private void OnEnable()
    {
        // Refresca la referencia al mouse por si cambia el dispositivo
        _mouse = Mouse.current;
    }

    private void LateUpdate()
    {
        _mouse = Mouse.current;
        if (_mouse == null) return;
        if (!IsMouseOverViewport()) return;

        bool changed = false;

        Vector2 currentPos = _mouse.position.ReadValue();
        Vector2 delta      = currentPos - _prevMousePos;

        // ── Órbita (botón izquierdo) ─────────────────────────────────────────
        if (_mouse.leftButton.isPressed)
        {
            _yaw   += delta.x * orbitSpeed * Time.deltaTime;
            _pitch -= delta.y * orbitSpeed * Time.deltaTime;
            _pitch  = Mathf.Clamp(_pitch, -89f, 89f);
            changed = true;
        }

        // ── Pan (botón derecho) ──────────────────────────────────────────────
        if (_mouse.rightButton.isPressed)
        {
            Vector3 right      = transform.right;
            Vector3 up         = transform.up;
            float   scaledPan  = panSpeed * _distance;

            _targetPos -= right * (delta.x * scaledPan);
            _targetPos -= up    * (delta.y * scaledPan);
            changed     = true;
        }

        // ── Zoom (rueda) ─────────────────────────────────────────────────────
        float scroll = _mouse.scroll.ReadValue().y;
        if (!Mathf.Approximately(scroll, 0f))
        {
            // La rueda del nuevo Input System devuelve ±120 por tick; normalizamos
            _distance -= (scroll / 120f) * zoomSpeed;
            _distance  = Mathf.Clamp(_distance, minDistance, maxDistance);
            changed    = true;
        }

        _prevMousePos = currentPos;
        if (changed) ApplyCameraTransform();
    }

    // ── API pública ────────────────────────────────────────────────────────────
    /// <summary>Vuelve a la posición orbital por defecto.</summary>
    public void Reset()
    {
        _distance  = DefaultDistance;
        _yaw       = DefaultYaw;
        _pitch     = DefaultPitch;
        _targetPos = (target != null) ? target.position : Vector3.zero;
        ApplyCameraTransform();
    }

    // ── Helpers privados ───────────────────────────────────────────────────────
    private void ApplyCameraTransform()
    {
        Quaternion rotation    = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3    offset      = rotation * new Vector3(0f, 0f, -_distance);
        transform.position     = _targetPos + offset;
        transform.LookAt(_targetPos, Vector3.up);
    }

    /// <summary>
    /// True si el cursor está dentro del viewportRect.
    /// Si no hay viewportRect asignado, siempre devuelve true.
    /// </summary>
    private bool IsMouseOverViewport()
    {
        if (viewportRect == null) return true;

        Canvas canvas = viewportRect.GetComponentInParent<Canvas>();
        Camera uiCam  = (canvas != null &&
                         canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                        ? canvas.worldCamera
                        : null;

        // Usamos la posición del nuevo Input System
        Vector2 mousePos = (_mouse != null)
                           ? _mouse.position.ReadValue()
                           : Vector2.zero;

        return RectTransformUtility.RectangleContainsScreenPoint(
            viewportRect, mousePos, uiCam);
    }
}
