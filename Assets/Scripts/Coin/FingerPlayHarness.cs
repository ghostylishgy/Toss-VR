using System;
using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;

namespace Toss.Coin
{
    /// <summary>
    /// P1.2 Finger Play Experimental Harness:
    /// Detachable, experimental harness verifying Grab and Manipulate in VR / Meta XR Simulator.
    /// Manages interaction state, authority handoff with CoinPresenter, and enforces single-owner policy.
    /// Implements IGameObjectFilter to reject secondary interactors directly at the SDK selection boundary.
    /// Strictly adheres to Zero-UI, no fake inputs, no premature Toss state machine.
    /// </summary>
    [DisallowMultipleComponent]
    public class FingerPlayHarness : MonoBehaviour, IGameObjectFilter
    {
        [Header("Component References")]
        [SerializeField] private Grabbable _grabbable;
        [SerializeField] private CoinPresenter _coinPresenter;
        [SerializeField] private Rigidbody _rigidbody;

        [Header("Interaction State (Read-Only)")]
        [SerializeField] private string _activeHand = "None";
        [SerializeField] private int _currentOwnerId = -1;
        [SerializeField] private GameObject _currentOwnerInteractorGo = null;
        [SerializeField] private bool _selectionRecoveryLocked = false;

        /// <summary>
        /// SDK Actual Selection State is the Single Source of Truth.
        /// </summary>
        public bool IsGrabbed => GetSelectingPointsCount() > 0;
        public string ActiveHand => _activeHand;
        public int CurrentOwnerId => _currentOwnerId;
        public GameObject CurrentOwnerInteractorGo => _currentOwnerInteractorGo;
        public bool IsSelectionRecoveryLocked => _selectionRecoveryLocked;

        [Header("Physics Configuration")]
        [Tooltip("Rigidbody interpolation mode during manipulation.")]
        public RigidbodyInterpolation interpolationMode = RigidbodyInterpolation.Interpolate;

        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private bool _isSubscribed = false;

        private void Awake()
        {
            EnsureReferences();
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
        }

        private void OnEnable()
        {
            EnsureReferences();
            Subscribe();
            ResyncSelectionState();
        }

        private void OnDisable()
        {
            int selectionCount = GetSelectingPointsCount();
            int ownerToCancel = _currentOwnerId;
            if (ownerToCancel == -1 && selectionCount > 0)
            {
                var interactables = GetComponentsInChildren<IInteractableView>(true);
                if (interactables != null)
                {
                    foreach (var interactable in interactables)
                    {
                        if (interactable == null) continue;
                        var selectingViews = interactable.SelectingInteractorViews;
                        if (selectingViews == null) continue;

                        foreach (var view in selectingViews)
                        {
                            if (view != null && view.Identifier >= 0)
                            {
                                ownerToCancel = view.Identifier;
                                break;
                            }
                        }
                        if (ownerToCancel != -1) break;
                    }
                }
            }

            // If active selection exists, cancel via SDK official event pipeline
            if (selectionCount > 0 && ownerToCancel != -1 && _grabbable != null)
            {
                var cancelPose = (_grabbable.GrabPoints != null && _grabbable.GrabPoints.Count > 0)
                    ? _grabbable.GrabPoints[0]
                    : transform.GetPose();
                _grabbable.ProcessPointerEvent(new PointerEvent(ownerToCancel, PointerEventType.Cancel, cancelPose));
            }

            Unsubscribe();

            // Verification after cancellation: do not pretend idle if SDK selection remains
            if (GetSelectingPointsCount() > 0)
            {
                _selectionRecoveryLocked = true;
                Debug.LogWarning($"[FingerPlayHarness] OnDisable: SDK selection still active ({GetSelectingPointsCount()} points) after cancellation attempt. Maintaining safe recovery lock.");
                if (_coinPresenter != null)
                {
                    _coinPresenter.enabled = false;
                }
                if (_rigidbody != null)
                {
                    if (!_rigidbody.isKinematic)
                    {
                        _rigidbody.linearVelocity = Vector3.zero;
                        _rigidbody.angularVelocity = Vector3.zero;
                    }
                    _rigidbody.isKinematic = true;
                }
            }
            else
            {
                _selectionRecoveryLocked = false;
                ExitInteractionAuthority();
            }
        }

        private void Start()
        {
            EnsureReferences();
            Subscribe();
            ResyncSelectionState();

            if (_rigidbody != null)
            {
                _rigidbody.interpolation = interpolationMode;
                _rigidbody.isKinematic = true;
                _rigidbody.useGravity = false;
            }
        }

        private void OnValidate()
        {
            if (_rigidbody != null)
            {
                _rigidbody.interpolation = interpolationMode;
            }
        }

        private int GetSelectingPointsCount()
        {
            if (_grabbable == null) return 0;
            var points = _grabbable.SelectingPoints;
            return points != null ? points.Count : 0;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            // Continual sync with SDK ground truth and self-healing
            if (GetSelectingPointsCount() == 0)
            {
                if (_selectionRecoveryLocked || _currentOwnerId != -1)
                {
                    _selectionRecoveryLocked = false;
                    ExitInteractionAuthority();
                }
            }
        }

        private void EnsureReferences()
        {
            if (_grabbable == null)
            {
                _grabbable = GetComponent<Grabbable>();
                if (_grabbable == null) _grabbable = GetComponentInChildren<Grabbable>();
            }

            if (_coinPresenter == null)
            {
                _coinPresenter = GetComponent<CoinPresenter>();
                if (_coinPresenter == null) _coinPresenter = GetComponentInChildren<CoinPresenter>();
            }

            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody>();
                if (_rigidbody == null) _rigidbody = GetComponentInChildren<Rigidbody>();
            }
        }

        private void Subscribe()
        {
            if (_isSubscribed || _grabbable == null) return;
            _grabbable.WhenPointerEventRaised += HandlePointerEvent;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed || _grabbable == null) return;
            _grabbable.WhenPointerEventRaised -= HandlePointerEvent;
            _isSubscribed = false;
        }

        /// <summary>
        /// Resyncs harness state with actual SDK Grabbable selection state.
        /// Handles three explicit branches: Clean Idle, Identified Owner, or Locked Safe State.
        /// </summary>
        private void ResyncSelectionState()
        {
            if (GetSelectingPointsCount() == 0)
            {
                // Branch 1: SDK selection == 0 -> clean idle
                _selectionRecoveryLocked = false;
                ExitInteractionAuthority();
                return;
            }

            // Attempt to recover actual active selector from interactables
            int recoveredId = -1;
            GameObject recoveredGo = null;

            var interactables = GetComponentsInChildren<IInteractableView>(true);
            if (interactables != null)
            {
                foreach (var interactable in interactables)
                {
                    if (interactable == null) continue;
                    var selectingViews = interactable.SelectingInteractorViews;
                    if (selectingViews == null) continue;

                    foreach (var view in selectingViews)
                    {
                        if (view == null) continue;
                        if (view.Identifier < 0) continue;

                        var viewComp = view as Component;
                        if (viewComp == null || viewComp.gameObject == null) continue;

                        recoveredId = view.Identifier;
                        recoveredGo = viewComp.gameObject;
                        break;
                    }

                    if (recoveredId != -1) break;
                }
            }

            if (recoveredId != -1 && recoveredGo != null)
            {
                // Branch 2: Owner successfully identified
                _selectionRecoveryLocked = false;
                _activeHand = ResolveActiveHand(transform.position, out _);
                EnterInteractionAuthority(recoveredId, recoveredGo);
                Debug.Log($"[FingerPlayHarness] Resynced with active SDK selection (Identifier: {recoveredId}).");
            }
            else if (_currentOwnerId != -1 && _currentOwnerInteractorGo != null)
            {
                // Branch 2 (preservation): Existing known owner ID is preserved
                _selectionRecoveryLocked = false;
                EnterInteractionAuthority(_currentOwnerId, _currentOwnerInteractorGo);
                Debug.Log($"[FingerPlayHarness] Resynced with preserved owner (Identifier: {_currentOwnerId}).");
            }
            else
            {
                // Branch 3: SDK selection > 0 but owner cannot be identified -> Locked Safe State
                _selectionRecoveryLocked = true;
                _currentOwnerId = -1;
                _activeHand = "Unknown";
                _currentOwnerInteractorGo = null;

                if (_coinPresenter != null)
                {
                    _coinPresenter.enabled = false;
                }

                if (_rigidbody != null)
                {
                    if (!_rigidbody.isKinematic)
                    {
                        _rigidbody.linearVelocity = Vector3.zero;
                        _rigidbody.angularVelocity = Vector3.zero;
                    }
                    _rigidbody.isKinematic = true;
                }

                Debug.LogWarning("[FingerPlayHarness] Active SDK selection found without identifiable owner. Entered locked safe recovery state.");
            }
        }

        #region IGameObjectFilter (SDK Selection Boundary Enforcement)
        /// <summary>
        /// SDK-level filter called by Interactable.CanBeSelectedBy.
        /// Enforces single-owner policy: rejects any secondary interactor before it can select Grabbable.
        /// Follows strict priority order to prevent multi-owner edge cases.
        /// </summary>
        public bool Filter(GameObject interactorGo)
        {
            // Rule 1: If harness component is disabled or inactive, reject all selectors
            if (!isActiveAndEnabled)
            {
                return false;
            }

            // Rule 2: If in locked recovery state (SDK selected but owner unknown), reject all selectors
            if (_selectionRecoveryLocked)
            {
                return false;
            }

            // Rule 3: If SDK has active selecting points but harness has no valid owner, reject all new selectors
            if (_grabbable != null && _grabbable.SelectingPointsCount > 0 && _currentOwnerId == -1)
            {
                return false;
            }

            // Rule 4: If an owner is active, only the interactor matching the current owner ID is permitted
            if (_currentOwnerId != -1)
            {
                if (interactorGo != null && interactorGo.TryGetComponent<IInteractorView>(out var interactorView))
                {
                    return interactorView.Identifier == _currentOwnerId;
                }

                if (_currentOwnerInteractorGo != null && interactorGo == _currentOwnerInteractorGo)
                {
                    return true;
                }

                // Reject secondary selector
                return false;
            }

            // Rule 5: Completely idle (no selection, no owner, not recovery locked): allow first selector
            return true;
        }
        #endregion

        #region Authority Protocol
        public void EnterInteractionAuthority(int ownerId, GameObject interactorGo)
        {
            _selectionRecoveryLocked = false;
            _currentOwnerId = ownerId;
            _currentOwnerInteractorGo = interactorGo;

            // Yield transform authority cleanly: disable CoinPresenter so it doesn't overwrite rotation
            if (_coinPresenter != null)
            {
                _coinPresenter.enabled = false;
            }

            // Ensure Rigidbody remains kinematic during manipulation to eliminate physics jitter
            if (_rigidbody != null)
            {
                if (!_rigidbody.isKinematic)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                }
                _rigidbody.isKinematic = true;
            }
        }

        public void ExitInteractionAuthority()
        {
            _selectionRecoveryLocked = false;
            _currentOwnerId = -1;
            _currentOwnerInteractorGo = null;
            _activeHand = "None";

            // Settle: keep coin stably in place at release pose without entering Toss or snapping back
            if (_rigidbody != null)
            {
                if (!_rigidbody.isKinematic)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                }
                _rigidbody.isKinematic = true;
            }

            // CoinPresenter remains disabled in Settle state (user placed coin in 3D space)
        }
        #endregion

        private void HandlePointerEvent(PointerEvent evt)
        {
            switch (evt.Type)
            {
                case PointerEventType.Select:
                    if (_currentOwnerId != -1 && evt.Identifier != _currentOwnerId)
                    {
                        Debug.LogWarning($"[FingerPlayHarness] Secondary grab from pointer {evt.Identifier} rejected; owner is {_currentOwnerId}.");
                        return;
                    }
                    OnGrabBegin(evt);
                    break;

                case PointerEventType.Move:
                    if (evt.Identifier != _currentOwnerId) return;
                    OnGrabMove(evt);
                    break;

                case PointerEventType.Unselect:
                case PointerEventType.Cancel:
                    if (evt.Identifier != _currentOwnerId) return;
                    OnGrabEnd(evt);
                    break;
            }
        }

        private void OnGrabBegin(PointerEvent evt)
        {
            _activeHand = ResolveActiveHand(evt.Pose.position, out GameObject handGo);
            EnterInteractionAuthority(evt.Identifier, handGo);

            Debug.Log($"[FingerPlayHarness] FINGER_PLAY_GRAB_BEGIN (Hand: {_activeHand}, Identifier: {evt.Identifier})");
        }

        private void OnGrabMove(PointerEvent evt)
        {
            // Hand pose updates are handled directly by SDK Transformer
        }

        private void OnGrabEnd(PointerEvent evt)
        {
            string releasedHand = _activeHand;
            ExitInteractionAuthority();

            Debug.Log($"[FingerPlayHarness] FINGER_PLAY_GRAB_END (Hand: {releasedHand}, Identifier: {evt.Identifier})");
        }

        private string ResolveActiveHand(Vector3 interactionPos, out GameObject handGo)
        {
            handGo = null;
            try
            {
                var hands = FindObjectsByType<OVRHand>(FindObjectsSortMode.None);
                float minDistance = float.MaxValue;
                string closestHand = "Unknown";

                foreach (var hand in hands)
                {
                    float dist = Vector3.Distance(hand.transform.position, interactionPos);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closestHand = hand.name.Contains("Left") ? "Left" : "Right";
                        handGo = hand.gameObject;
                    }
                }

                if (closestHand != "Unknown") return closestHand;
            }
            catch
            {
                // Fallback gracefully if hands cannot be queried
            }

            return "Hand";
        }

        /// <summary>
        /// Resets the coin back to its initial inspection pose and re-enables CoinPresenter.
        /// Guarded against corruption during active manipulation or unknown owner recovery lock.
        /// Strictly checks SelectingPointsCount == 0, _currentOwnerId == -1, and _selectionRecoveryLocked == false.
        /// </summary>
        public void ResetToInitialPose()
        {
            if (_selectionRecoveryLocked || GetSelectingPointsCount() > 0 || _currentOwnerId != -1)
            {
                Debug.LogWarning("[FingerPlayHarness] ResetToInitialPose rejected: coin is currently held by active selector or in locked recovery state.");
                return;
            }

            transform.position = _initialPosition;
            transform.rotation = _initialRotation;
            _selectionRecoveryLocked = false;
            ExitInteractionAuthority();

            if (_coinPresenter != null)
            {
                _coinPresenter.enabled = true;
            }

            Debug.Log("[FingerPlayHarness] Reset coin to initial showcase pose.");
        }
    }
}
