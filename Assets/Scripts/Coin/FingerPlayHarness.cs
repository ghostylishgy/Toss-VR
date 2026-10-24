using System;
using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;

namespace Toss.Coin
{
    /// <summary>
    /// Release profile mode for P1.2C Release / Settle minimal A/B evaluation:
    /// Freeze: Instant hold-pose baseline, no dynamic physics (P1.2A/B behavior).
    /// ResidualSettle: Subtle damped residual motion transitioning to settled kinematic state.
    /// </summary>
    public enum ReleaseProfile
    {
        Freeze,
        ResidualSettle
    }

    /// <summary>
    /// Minimal internal lifecycle states for Finger Play (strictly no premature Toss state machine).
    /// </summary>
    public enum FingerPlayState
    {
        Showcase,
        Grabbed,
        Settling,
        Settled
    }

    /// <summary>
    /// P1.2 Finger Play Experimental Harness:
    /// Detachable, experimental harness verifying Grab, Manipulate, and Release / Settle in VR / Meta XR Simulator.
    /// Manages interaction state, authority handoff with CoinPresenter, motion sampling, and enforces single-owner policy.
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
        [SerializeField] private FingerPlayState _state = FingerPlayState.Showcase;
        [SerializeField] private string _activeHand = "None";
        [SerializeField] private int _currentOwnerId = -1;
        [SerializeField] private GameObject _currentOwnerInteractorGo = null;
        [SerializeField] private bool _selectionRecoveryLocked = false;

        /// <summary>
        /// SDK Actual Selection State is the Single Source of Truth.
        /// </summary>
        public bool IsGrabbed => GetSelectingPointsCount() > 0;
        public FingerPlayState CurrentState => _state;
        public string ActiveHand => _activeHand;
        public int CurrentOwnerId => _currentOwnerId;
        public GameObject CurrentOwnerInteractorGo => _currentOwnerInteractorGo;
        public bool IsSelectionRecoveryLocked => _selectionRecoveryLocked;

        [Header("Physics Configuration")]
        [Tooltip("Rigidbody interpolation mode during manipulation.")]
        public RigidbodyInterpolation interpolationMode = RigidbodyInterpolation.Interpolate;

        [Header("Release / Settle Configuration (P1.2C)")]
        [Tooltip("Release profile selection: Freeze (instant hold-pose baseline) or ResidualSettle (subtle damped residual motion).")]
        [SerializeField] private ReleaseProfile _releaseProfile = ReleaseProfile.ResidualSettle;

        [Tooltip("Fraction of estimated hand linear velocity inherited at release (0.0 to 1.0).")]
        [Range(0f, 1f)]
        [SerializeField] private float _linearReleaseScale = 0.2f;

        [Tooltip("Fraction of estimated hand angular velocity inherited at release (0.0 to 1.0).")]
        [Range(0f, 1f)]
        [SerializeField] private float _angularReleaseScale = 0.2f;

        [Tooltip("Maximum allowed linear release speed (m/s) to prevent sudden movement.")]
        [SerializeField] private float _maxLinearReleaseSpeed = 0.4f;

        [Tooltip("Maximum allowed angular release speed (rad/s) to prevent sudden spin.")]
        [SerializeField] private float _maxAngularReleaseSpeed = 6.28f;

        [Tooltip("Linear damping applied to the Rigidbody during settling.")]
        [SerializeField] private float _linearDamping = 4.0f;

        [Tooltip("Angular damping applied to the Rigidbody during settling.")]
        [SerializeField] private float _angularDamping = 4.0f;

        [Tooltip("Linear speed threshold (m/s) below which linear motion is considered settled.")]
        [SerializeField] private float _linearSettleThreshold = 0.02f;

        [Tooltip("Angular speed threshold (rad/s) below which angular motion is considered settled.")]
        [SerializeField] private float _angularSettleThreshold = 0.35f;

        [Tooltip("Duration (seconds) speeds must remain below settle thresholds before freezing.")]
        [SerializeField] private float _settleHoldTime = 0.08f;

        [Tooltip("Maximum settling duration cap (seconds). Coin unconditionally settles when exceeded.")]
        [SerializeField] private float _maxSettleDuration = 0.75f;

        public ReleaseProfile CurrentReleaseProfile
        {
            get => _releaseProfile;
            set => _releaseProfile = value;
        }
        public float LinearReleaseScale { get => _linearReleaseScale; set => _linearReleaseScale = value; }
        public float AngularReleaseScale { get => _angularReleaseScale; set => _angularReleaseScale = value; }
        public float MaxLinearReleaseSpeed { get => _maxLinearReleaseSpeed; set => _maxLinearReleaseSpeed = value; }
        public float MaxAngularReleaseSpeed { get => _maxAngularReleaseSpeed; set => _maxAngularReleaseSpeed = value; }
        public float LinearDamping { get => _linearDamping; set => _linearDamping = value; }
        public float AngularDamping { get => _angularDamping; set => _angularDamping = value; }
        public float LinearSettleThreshold { get => _linearSettleThreshold; set => _linearSettleThreshold = value; }
        public float AngularSettleThreshold { get => _angularSettleThreshold; set => _angularSettleThreshold = value; }
        public float SettleHoldTime { get => _settleHoldTime; set => _settleHoldTime = value; }
        public float MaxSettleDuration { get => _maxSettleDuration; set => _maxSettleDuration = value; }

        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private bool _isSubscribed = false;

        #region Motion Sampling
        private struct MotionSample
        {
            public Vector3 LinearVelocity;
            public Vector3 AngularVelocity;
            public float DeltaTime;

            public MotionSample(Vector3 linearVelocity, Vector3 angularVelocity, float deltaTime)
            {
                LinearVelocity = linearVelocity;
                AngularVelocity = angularVelocity;
                DeltaTime = deltaTime;
            }
        }

        private const int MaxMotionSamples = 6;
        private readonly Queue<MotionSample> _motionSamples = new Queue<MotionSample>(MaxMotionSamples);
        private Vector3 _prevSamplePosition;
        private Quaternion _prevSampleRotation;
        private bool _hasPrevSample = false;

        private float _settleTimer = 0f;
        private float _settleHoldTimer = 0f;
        #endregion

        private void Awake()
        {
            EnsureReferences();
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
            _state = FingerPlayState.Showcase;
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

            // Cancel any active settling or motion sampling
            _state = FingerPlayState.Settled;
            _motionSamples.Clear();
            _hasPrevSample = false;
            _settleTimer = 0f;
            _settleHoldTimer = 0f;

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

            _linearReleaseScale = Mathf.Clamp01(_linearReleaseScale);
            _angularReleaseScale = Mathf.Clamp01(_angularReleaseScale);
            _maxLinearReleaseSpeed = Mathf.Max(0f, _maxLinearReleaseSpeed);
            _maxAngularReleaseSpeed = Mathf.Max(0f, _maxAngularReleaseSpeed);
            _linearDamping = Mathf.Max(0f, _linearDamping);
            _angularDamping = Mathf.Max(0f, _angularDamping);
            _linearSettleThreshold = Mathf.Max(0.001f, _linearSettleThreshold);
            _angularSettleThreshold = Mathf.Max(0.001f, _angularSettleThreshold);
            _settleHoldTime = Mathf.Max(0.01f, _settleHoldTime);
            _maxSettleDuration = Mathf.Max(0.05f, _maxSettleDuration);
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

            // Motion sampling while Grabbed
            if (_state == FingerPlayState.Grabbed)
            {
                UpdateMotionSampling();
            }
        }

        private void FixedUpdate()
        {
            if (!Application.isPlaying) return;

            if (_state == FingerPlayState.Settling)
            {
                UpdateSettlingPhysics();
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
                if (_state == FingerPlayState.Grabbed)
                {
                    _state = FingerPlayState.Settled;
                }
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
                _state = FingerPlayState.Grabbed;
                EnterInteractionAuthority(recoveredId, recoveredGo);
                Debug.Log($"[FingerPlayHarness] Resynced with active SDK selection (Identifier: {recoveredId}).");
            }
            else if (_currentOwnerId != -1 && _currentOwnerInteractorGo != null)
            {
                // Branch 2 (preservation): Existing known owner ID is preserved
                _selectionRecoveryLocked = false;
                _state = FingerPlayState.Grabbed;
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
                _state = FingerPlayState.Settled;

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

            // If not actively settling, ensure Rigidbody is safely locked kinematic
            if (_state != FingerPlayState.Settling)
            {
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
                    if (evt.Identifier != _currentOwnerId) return;
                    if (isActiveAndEnabled)
                    {
                        OnGrabEnd(evt);
                    }
                    else
                    {
                        OnGrabCancel(evt);
                    }
                    break;

                case PointerEventType.Cancel:
                    if (evt.Identifier != _currentOwnerId && !_selectionRecoveryLocked) return;
                    OnGrabCancel(evt);
                    break;
            }
        }

        private void OnGrabBegin(PointerEvent evt)
        {
            if (_state == FingerPlayState.Settling)
            {
                // Re-grab during settling: immediately cancel settling physics before handing authority to GrabFreeTransformer
                if (_rigidbody != null)
                {
                    if (!_rigidbody.isKinematic)
                    {
                        _rigidbody.linearVelocity = Vector3.zero;
                        _rigidbody.angularVelocity = Vector3.zero;
                    }
                    _rigidbody.isKinematic = true;
                }
                _settleTimer = 0f;
                _settleHoldTimer = 0f;
                Debug.Log("[FingerPlayHarness] Grab during settling: residual physics cancelled, Rigidbody kinematic restored.");
            }

            _motionSamples.Clear();
            _hasPrevSample = false;
            _state = FingerPlayState.Grabbed;

            _activeHand = ResolveActiveHand(evt.Pose.position, out GameObject handGo);
            EnterInteractionAuthority(evt.Identifier, handGo);

            Debug.Log($"[FingerPlayHarness] FINGER_PLAY_GRAB_BEGIN (Hand: {_activeHand}, Identifier: {evt.Identifier})");
        }

        private void OnGrabMove(PointerEvent evt)
        {
            // Hand pose updates are handled directly by SDK Transformer
        }

        private void OnGrabCancel(PointerEvent evt)
        {
            string cancelledHand = _activeHand;
            _state = FingerPlayState.Settled;
            _motionSamples.Clear();
            _hasPrevSample = false;
            _settleTimer = 0f;
            _settleHoldTimer = 0f;

            // SDK selection removal and kinematic unlock precede this callback.
            ExitInteractionAuthority();
            _selectionRecoveryLocked = GetSelectingPointsCount() > 0;

            if (_rigidbody != null)
            {
                _rigidbody.useGravity = false;
            }
            if (_coinPresenter != null)
            {
                _coinPresenter.enabled = false;
            }

            Debug.Log($"[FingerPlayHarness] FINGER_PLAY_GRAB_END (Hand: {cancelledHand}, Identifier: {evt.Identifier}, Cancelled: true, Event: {evt.Type})");
        }

        private void OnGrabEnd(PointerEvent evt)
        {
            string releasedHand = _activeHand;

            // If in locked recovery state, strictly maintain safe kinematic hold
            if (_selectionRecoveryLocked)
            {
                Debug.LogWarning("[FingerPlayHarness] OnGrabEnd occurred while in recovery lock. Bypassing release physics.");
                _state = FingerPlayState.Settled;
                ExitInteractionAuthority();
                Debug.Log($"[FingerPlayHarness] FINGER_PLAY_GRAB_END (Hand: {releasedHand}, Identifier: {evt.Identifier})");
                return;
            }

            if (_releaseProfile == ReleaseProfile.Freeze)
            {
                Debug.Log("[FingerPlayHarness] FINGER_PLAY_RELEASE profile=Freeze");
                if (_rigidbody != null)
                {
                    if (!_rigidbody.isKinematic)
                    {
                        _rigidbody.linearVelocity = Vector3.zero;
                        _rigidbody.angularVelocity = Vector3.zero;
                    }
                    _rigidbody.isKinematic = true;
                    _rigidbody.useGravity = false;
                }
                _state = FingerPlayState.Settled;
                ExitInteractionAuthority();
                Debug.Log($"[FingerPlayHarness] FINGER_PLAY_GRAB_END (Hand: {releasedHand}, Identifier: {evt.Identifier})");
            }
            else // ResidualSettle
            {
                Debug.Log("[FingerPlayHarness] FINGER_PLAY_RELEASE profile=ResidualSettle");

                GetEstimatedVelocities(out Vector3 estLinVel, out Vector3 estAngVel);

                Vector3 scaledLinVel = FiniteVelocityOrZero(estLinVel * _linearReleaseScale);
                Vector3 scaledAngVel = FiniteVelocityOrZero(estAngVel * _angularReleaseScale);

                Vector3 clampedLinVel = FiniteVelocityOrZero(Vector3.ClampMagnitude(scaledLinVel, _maxLinearReleaseSpeed));
                float angSpeed = scaledAngVel.magnitude;
                Vector3 clampedAngVel = FiniteVelocityOrZero((angSpeed > _maxAngularReleaseSpeed)
                    ? (scaledAngVel.normalized * _maxAngularReleaseSpeed)
                    : scaledAngVel);

                // Dynamic Rigidbody switch: set dynamic BEFORE writing velocity (never write velocity to kinematic)
                if (_rigidbody != null)
                {
                    _rigidbody.isKinematic = false;
                    _rigidbody.useGravity = false;
                    _rigidbody.linearDamping = _linearDamping;
                    _rigidbody.angularDamping = _angularDamping;
                    _rigidbody.linearVelocity = FiniteVelocityOrZero(clampedLinVel);
                    _rigidbody.angularVelocity = FiniteVelocityOrZero(clampedAngVel);
                }

                _settleTimer = 0f;
                _settleHoldTimer = 0f;
                _state = FingerPlayState.Settling;

                Debug.Log("[FingerPlayHarness] SETTLE_BEGIN");

                ExitInteractionAuthority();
                Debug.Log($"[FingerPlayHarness] FINGER_PLAY_GRAB_END (Hand: {releasedHand}, Identifier: {evt.Identifier})");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFiniteRotation(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        }

        private static Vector3 FiniteVelocityOrZero(Vector3 velocity)
        {
            return IsFiniteVector(velocity) ? velocity : Vector3.zero;
        }

        private void ResetMotionSampling()
        {
            _motionSamples.Clear();
            _hasPrevSample = false;
        }

        private void UpdateMotionSampling()
        {
            Vector3 currentPos = transform.position;
            Quaternion currentRot = transform.rotation;
            float dt = Time.deltaTime;

            if (!IsFinite(dt) || dt <= 1e-4f || !IsFiniteVector(currentPos) || !IsFiniteRotation(currentRot))
            {
                // Break continuity so paused or invalid tracking cannot become a release impulse.
                ResetMotionSampling();
                return;
            }

            if (!_hasPrevSample)
            {
                _prevSamplePosition = currentPos;
                _prevSampleRotation = currentRot;
                _hasPrevSample = true;
                _motionSamples.Clear();
                return;
            }

            Vector3 instLinearVel = (currentPos - _prevSamplePosition) / dt;
            Quaternion deltaRot = currentRot * Quaternion.Inverse(_prevSampleRotation);
            if (!IsFiniteVector(instLinearVel) || !IsFiniteRotation(deltaRot))
            {
                ResetMotionSampling();
                return;
            }

            deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
            if (!IsFinite(angleDeg) || !IsFiniteVector(axis))
            {
                ResetMotionSampling();
                return;
            }
            if (angleDeg > 180f) angleDeg -= 360f;
            Vector3 instAngularVel = Vector3.zero;
            if (axis.sqrMagnitude > 1e-6f && !float.IsNaN(axis.x))
            {
                instAngularVel = axis.normalized * (angleDeg * Mathf.Deg2Rad / dt);
            }
            if (!IsFiniteVector(instAngularVel))
            {
                ResetMotionSampling();
                return;
            }

            if (_motionSamples.Count >= MaxMotionSamples)
            {
                _motionSamples.Dequeue();
            }
            _motionSamples.Enqueue(new MotionSample(instLinearVel, instAngularVel, dt));

            _prevSamplePosition = currentPos;
            _prevSampleRotation = currentRot;
        }

        private void GetEstimatedVelocities(out Vector3 linearVel, out Vector3 angularVel)
        {
            if (_motionSamples.Count == 0)
            {
                linearVel = Vector3.zero;
                angularVel = Vector3.zero;
                return;
            }

            Vector3 weightedLinearSum = Vector3.zero;
            Vector3 weightedAngularSum = Vector3.zero;
            float totalWeight = 0f;

            int index = 1;
            foreach (var sample in _motionSamples)
            {
                float weight = index;
                weightedLinearSum += sample.LinearVelocity * weight;
                weightedAngularSum += sample.AngularVelocity * weight;
                totalWeight += weight;
                index++;
            }

            if (totalWeight > 0f)
            {
                linearVel = FiniteVelocityOrZero(weightedLinearSum / totalWeight);
                angularVel = FiniteVelocityOrZero(weightedAngularSum / totalWeight);
            }
            else
            {
                linearVel = Vector3.zero;
                angularVel = Vector3.zero;
            }
        }

        private void UpdateSettlingPhysics()
        {
            if (_rigidbody == null)
            {
                CompleteSettle("Rigidbody missing");
                return;
            }

            _settleTimer += Time.fixedDeltaTime;

            float linSpeed = _rigidbody.linearVelocity.magnitude;
            float angSpeed = _rigidbody.angularVelocity.magnitude;

            if (linSpeed < _linearSettleThreshold && angSpeed < _angularSettleThreshold)
            {
                _settleHoldTimer += Time.fixedDeltaTime;
                if (_settleHoldTimer >= _settleHoldTime)
                {
                    CompleteSettle($"Threshold met (lin={linSpeed:F4}, ang={angSpeed:F4})");
                    return;
                }
            }
            else
            {
                _settleHoldTimer = 0f;
            }

            if (_settleTimer >= _maxSettleDuration)
            {
                CompleteSettle($"Max duration safety cap reached ({_maxSettleDuration:F2}s)");
            }
        }

        private void CompleteSettle(string reason)
        {
            float duration = _settleTimer;
            float finalLinSpeed = _rigidbody != null ? _rigidbody.linearVelocity.magnitude : 0f;
            float finalAngSpeed = _rigidbody != null ? _rigidbody.angularVelocity.magnitude : 0f;

            // Strict settle sequence: zero velocity BEFORE switching to kinematic
            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
                _rigidbody.isKinematic = true;
            }

            _state = FingerPlayState.Settled;
            _settleTimer = 0f;
            _settleHoldTimer = 0f;

            Debug.Log($"[FingerPlayHarness] SETTLE_END duration={duration:F3}s linearSpeed={finalLinSpeed:F4} angularSpeed={finalAngSpeed:F4}");
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

            // Cancel settling if active
            if (_state == FingerPlayState.Settling)
            {
                if (_rigidbody != null)
                {
                    if (!_rigidbody.isKinematic)
                    {
                        _rigidbody.linearVelocity = Vector3.zero;
                        _rigidbody.angularVelocity = Vector3.zero;
                    }
                    _rigidbody.isKinematic = true;
                }
                _settleTimer = 0f;
                _settleHoldTimer = 0f;
            }

            transform.position = _initialPosition;
            transform.rotation = _initialRotation;
            _selectionRecoveryLocked = false;
            _state = FingerPlayState.Showcase;
            ExitInteractionAuthority();

            if (_coinPresenter != null)
            {
                _coinPresenter.enabled = true;
            }

            Debug.Log("[FingerPlayHarness] Reset coin to initial showcase pose.");
        }
    }
}
