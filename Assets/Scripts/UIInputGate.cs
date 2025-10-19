using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Gates gameplay InputActions while a UI Button is visible,
/// so the same control (right trigger) can click the button.
/// Attach this to any GameObject in your scene (e.g., XR Origin).
/// </summary>
public class UIInputGate : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("The button you want to click with the right trigger.")]
    public Button nextButton;

    [Tooltip("Optional: auto-focus this button when it becomes visible.")]
    public bool autoFocusWhenShown = true;

    [Header("Gameplay Inputs To Gate (consuming the same trigger)")]
    [Tooltip("Any gameplay InputActions that currently listen to the right trigger.")]
    public List<InputActionReference> gameplayActionsToGate = new List<InputActionReference>();

    [Header("Optional Events")]
    public UnityEvent onGateEnabled;   // fired when gating starts (button shown)
    public UnityEvent onGateDisabled;  // fired when gating ends  (button hidden or clicked)

    private bool _isGated;
    private bool _lastButtonActive;

    void Awake()
    {
        if (nextButton != null)
        {
            // When the button is clicked, we’ll drop the gate so gameplay can resume.
            nextButton.onClick.AddListener(DisableGate);
        }
    }

    void OnDestroy()
    {
        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(DisableGate);
        }
    }

    void OnEnable()
    {
        // Make sure gameplay actions start enabled (default)
        SetGameplayActionsEnabled(true);
        _isGated = false;
        _lastButtonActive = IsButtonVisible();
    }

    void Update()
    {
        bool visible = IsButtonVisible();

        // Edge detection on visibility change
        if (visible != _lastButtonActive)
        {
            _lastButtonActive = visible;
            if (visible)
            {
                EnableGate();
            }
            else
            {
                DisableGate();
            }
        }

        // Optional: auto-focus to improve hover/press reliability
        if (autoFocusWhenShown && visible && !_isGated)
        {
            // If we just switched to visible, gate immediately and try to focus
            EnableGate();
        }
    }

    /// <summary>
    /// Call this manually if you want to start “awaiting next” by code.
    /// </summary>
    public void EnableGate()
    {
        if (_isGated) return;
        _isGated = true;

        // Disable gameplay InputActions that could consume the trigger
        SetGameplayActionsEnabled(false);

        // (Optional) move UI focus to the button for keyboard/controller UI systems
        if (autoFocusWhenShown && nextButton != null && nextButton.gameObject.activeInHierarchy)
        {
            nextButton.Select();
        }

        onGateEnabled?.Invoke();
        Debug.Log("[UIInputGate] Gate ENABLED (UI should receive trigger)");
    }

    /// <summary>
    /// Call this from code or it is auto-called when the button is clicked.
    /// </summary>
    public void DisableGate()
    {
        if (!_isGated) return;
        _isGated = false;

        // Re-enable gameplay actions
        SetGameplayActionsEnabled(true);

        onGateDisabled?.Invoke();
        Debug.Log("[UIInputGate] Gate DISABLED (gameplay resumes)");
    }

    private bool IsButtonVisible()
    {
        return nextButton != null && nextButton.gameObject.activeInHierarchy;
    }

    private void SetGameplayActionsEnabled(bool enable)
    {
        foreach (var actionRef in gameplayActionsToGate)
        {
            if (actionRef == null || actionRef.action == null) continue;
            try
            {
                if (enable && !actionRef.action.enabled) actionRef.action.Enable();
                if (!enable && actionRef.action.enabled) actionRef.action.Disable();
            }
            catch { /* ignore if not initialized yet */ }
        }
    }
}
