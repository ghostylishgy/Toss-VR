using System;
using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.Grab;
using Toss.Experiments.TossIntent;

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

    public enum FingerPlayState
    {
        Showcase,
        Grabbed,
        Settling,
        Settled,
        ReleasePending,
        Flight
    }

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

        private int _provenanceOwnerId = -1;
        private Component _provenanceSource;
        private string _provenanceSourceMatch = "Missing";
        private string _provenanceSourceIdentity = "Unknown";
        private string _provenancePoseInput = "Unknown";
        private string _provenanceSelectionApi = "Unknown";
        private bool _provenanceUnselectLogged;
        private bool _provenanceCancelObserved;
        private int _provenanceLastMotionFrame = -1;

        [Header("Experimental Near Pinch Toss (Not Runtime Validated)")]
        [Tooltip("Off preserves P1.2C. On permits conservative near-hand pinch Toss only; no result or catch.")]
        [SerializeField] private bool _enableToss = false;

        private const double TossInputWindow = 0.10;
        private const float TossLaunchSpeed = 3f;
        private const float TossFlightSpeed = 6f;
        private const float TossSpinSpeed = 25f;
        private const double TossFlightDuration = 1.25;
        private const float TossRadius = 0.75f;
        private const float TossHeight = 0.8f;
        private TossIntentGate _tossGate;
        private HandGrabInteractor _tossSource;
        private HandGrabInteractable _tossTarget;
        private HandGrabAPI _tossApi;
        private IHand _tossHand;
        private FromOVRHandDataSource _tossRaw;
        private int _tossOwnerId = -1;
        private bool _tossBlocked;
        private bool _tossReleaseConsumed;
        private bool _rawPinching;
        private bool _sdkPinching;
        private Pose _lastRawPose;
        private HandFingerFlags _lastRawPinches;
        private double _rawChangedAt;
        private double _rawObservedAt;
        private double _rawReleaseAt = double.NegativeInfinity;
        private double _sdkReleaseAt = double.NegativeInfinity;
        private double _tossSampleAt;
        private double _pendingAt;
        private int _pendingFrame;
        private Vector3 _tossVelocity;
        private Vector3 _tossAngularVelocity;
        private Vector3 _flightOrigin;
        private Quaternion _flightRotation;
        private double _flightStartedAt;
        private float _savedMaxAngularVelocity;

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
            if (_state == FingerPlayState.Flight || _state == FingerPlayState.ReleasePending)
                RecoverToss("HarnessDisabled");
            ResetTossObservation();
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

            if (_state == FingerPlayState.Flight)
                UpdateTossFlight();
            else if (_state == FingerPlayState.Settling)
            {
                UpdateSettlingPhysics();
            }
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            if (_state == FingerPlayState.ReleasePending)
            {
                if (Time.frameCount > _pendingFrame) ResolvePendingToss();
                return;
            }
            if (_state == FingerPlayState.Flight)
            {
                UpdateTossFlight();
                return;
            }
            if (!_enableToss || _tossSource == null || _state != FingerPlayState.Grabbed) return;
            ObserveTossInput(true);
            double now = _rawObservedAt;
            if (_tossBlocked || !_rawPinching || !_sdkPinching || now <= _tossSampleAt || _rawChangedAt != now) return;
            _tossSampleAt = now;
            GetEstimatedVelocities(out Vector3 velocity, out _);
            bool valid = _hasPrevSample && _provenanceLastMotionFrame == Time.frameCount;
            _tossGate.Process(GateEvent.Sample(now,
                new System.Numerics.Vector3(transform.position.x, transform.position.y, transform.position.z),
                new System.Numerics.Vector3(velocity.x, velocity.y, velocity.z), MotionFrame.WorldYUp, valid));
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
                CacheReleaseProvenance(-1);
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

            CacheReleaseProvenance(-1, true);
        }

        #region IGameObjectFilter (SDK Selection Boundary Enforcement)
        /// <summary>
        /// SDK-level filter called by Interactable.CanBeSelectedBy.
        /// Enforces single-owner policy: rejects any secondary interactor before it can select Grabbable.
        /// Follows strict priority order to prevent multi-owner edge cases.
        /// </summary>
        public bool Filter(GameObject interactorGo)
        {
            if (_state == FingerPlayState.ReleasePending || _state == FingerPlayState.Flight) return false;
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

            // Flight and settling are mutually exclusive dynamic owners.
            if (_state != FingerPlayState.Settling && _state != FingerPlayState.Flight)
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
            if (evt.Type == PointerEventType.Cancel && evt.Identifier == _tossOwnerId)
            {
                _tossBlocked = true;
                if (_state == FingerPlayState.ReleasePending || _state == FingerPlayState.Flight)
                    RecoverToss("Cancel");
            }
            if (evt.Type == PointerEventType.Select && _currentOwnerId != -1)
                _tossBlocked = true;
            ObserveReleaseProvenance(evt);
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
            if (_state == FingerPlayState.Flight || _state == FingerPlayState.ReleasePending)
                RecoverToss("SelectionDuringFlight");
            ResetTossObservation();
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
            CacheReleaseProvenance(evt.Identifier);
            BeginTossObservation();

            Debug.Log($"[FingerPlayHarness] FINGER_PLAY_GRAB_BEGIN (Hand: {_activeHand}, Identifier: {evt.Identifier})");
        }

        private void OnGrabMove(PointerEvent evt)
        {
            // Hand pose updates are handled directly by SDK Transformer
        }

        private void CacheReleaseProvenance(int ownerId, bool preserveSameSelection = false)
        {
            Component matchedSource = null;
            int matchedOwnerId = ownerId;
            bool ambiguous = false;
            if (_grabbable != null && (ownerId >= 0 || GetSelectingPointsCount() > 0))
            {
                foreach (var interactable in GetComponentsInChildren<IInteractableView>(true))
                {
                    IPointableElement target = interactable is HandGrabInteractable near ? near.PointableElement
                        : interactable is DistanceHandGrabInteractable far ? far.PointableElement
                        : interactable is GazeInteractable gaze ? gaze.PointableElement : null;
                    if (!ReferenceEquals(target, _grabbable) || interactable.SelectingInteractorViews == null) continue;

                    foreach (var view in interactable.SelectingInteractorViews)
                    {
                        if (view == null || view.Identifier < 0 || (ownerId >= 0 && view.Identifier != ownerId)) continue;
                        var source = view as Component;
                        if (source == null || (matchedSource != null && !ReferenceEquals(matchedSource, source)))
                        {
                            ambiguous = true;
                            break;
                        }
                        matchedSource = source;
                        matchedOwnerId = view.Identifier;
                    }
                    if (ambiguous) break;
                }
            }

            if (ambiguous)
            {
                matchedSource = null;
                matchedOwnerId = ownerId;
            }
            if (preserveSameSelection && matchedSource != null && ReferenceEquals(matchedSource, _provenanceSource)
                && matchedOwnerId == _provenanceOwnerId) return;

            _provenanceOwnerId = matchedOwnerId;
            _provenanceSource = matchedSource;
            _provenanceSourceMatch = ambiguous ? "UnresolvedOrAmbiguous" : matchedSource != null ? "MatchedAtSelection" : "Missing";
            _provenanceSourceIdentity = DescribeProvenanceObject(matchedSource);
            _provenancePoseInput = "Unknown";
            _provenanceSelectionApi = "Unknown";
            _provenanceUnselectLogged = false;
            _provenanceCancelObserved = false;
            _provenanceLastMotionFrame = -1;
            if (matchedSource is HandGrabInteractor handGrab)
            {
                _provenancePoseInput = DescribeProvenanceObject(handGrab.Hand);
                _provenanceSelectionApi = DescribeProvenanceObject(handGrab.HandGrabApi);
            }
            else if (matchedSource is DistanceHandGrabInteractor distanceGrab)
            {
                _provenancePoseInput = DescribeProvenanceObject(distanceGrab.Hand);
                _provenanceSelectionApi = DescribeProvenanceObject(distanceGrab.HandGrabApi);
            }
        }

        private static string DescribeProvenanceObject(object source)
        {
            if (source == null) return "Unknown";
            if (source is UnityEngine.Object unityObject)
                return unityObject != null ? $"{source.GetType().Name}#{unityObject.GetInstanceID()}" : "Destroyed";
            return source.GetType().Name;
        }

        private void ObserveReleaseProvenance(PointerEvent evt)
        {
            if (_provenanceOwnerId < 0 || evt.Identifier != _provenanceOwnerId) return;
            if (evt.Type == PointerEventType.Cancel)
            {
                if (_provenanceCancelObserved) return;
                _provenanceCancelObserved = true;
            }
            else if (evt.Type == PointerEventType.Unselect)
            {
                if (_provenanceUnselectLogged) return;
                _provenanceUnselectLogged = true;
            }
            else return;

            bool? sourceActive = _provenanceSource is Behaviour source && source != null
                ? source.isActiveAndEnabled : (bool?)null;
            string reason = _provenanceCancelObserved ? "ExplicitCancelObserved"
                : !isActiveAndEnabled ? "HarnessInactiveObserved"
                : sourceActive == false ? "SourceInactiveObserved" : "NoExplicitReleaseCause";
            string classification = reason == "NoExplicitReleaseCause" ? "Unknown" : "NonUserExit";
            // IHand validity getters can advance lazy SDK data; pose input is not proof of selector input.
            Debug.Log($"[FingerPlayHarness] RELEASE_PROVENANCE owner={_provenanceOwnerId} event={evt.Type} "
                + $"sourceMatch={_provenanceSourceMatch} source={_provenanceSourceIdentity} "
                + $"poseInputAtSelection={_provenancePoseInput} selectionApiAtSelection={_provenanceSelectionApi} "
                + $"selectionInput=Unknown inputValidity=Unknown_NotRead "
                + $"sourceActive={(sourceActive.HasValue ? sourceActive.Value.ToString() : "Unknown")} "
                + $"harnessActive={isActiveAndEnabled} cancelObserved={_provenanceCancelObserved} "
                + $"sdkDisableCause=Unknown lastMotionFrame={_provenanceLastMotionFrame} "
                + $"eventFrame={Time.frameCount} sampleContinuity={_hasPrevSample} samples={_motionSamples.Count} "
                + $"classification={classification} reason={reason}");
        }

        private void OnGrabCancel(PointerEvent evt)
        {
            _tossBlocked = true;
            _tossReleaseConsumed = true;
            if (_state == FingerPlayState.Flight || _state == FingerPlayState.ReleasePending)
                RecoverToss("GrabCancelled");
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
            if (_state == FingerPlayState.ReleasePending || _state == FingerPlayState.Flight) return;
            if (_enableToss && _tossSource != null && evt.Identifier == _tossOwnerId)
            {
                if (_tossReleaseConsumed) return;
                _tossReleaseConsumed = true;
                if (!IsFiniteVector(transform.position) || !IsFiniteRotation(transform.rotation)
                    || (_hasPrevSample && Vector3.Distance(transform.position, _prevSamplePosition) > 0.25f))
                {
                    _flightOrigin = _hasPrevSample ? _prevSamplePosition : _initialPosition;
                    _flightRotation = _hasPrevSample ? _prevSampleRotation : _initialRotation;
                    RecoverToss("ReleasePoseDiscontinuity");
                    return;
                }
                ObserveTossInput(false);
                if (_tossBlocked || _selectionRecoveryLocked)
                {
                    OnGrabCancel(evt);
                    return;
                }
                double now = Time.realtimeSinceStartupAsDouble;
                var intent = _tossGate.Process(GateEvent.Release(now));
                if (intent.TossIntentAccepted && now - _rawReleaseAt <= TossInputWindow
                    && now - _sdkReleaseAt <= TossInputWindow && !_rawPinching && !_sdkPinching
                    && now - _rawChangedAt <= TossInputWindow && _rigidbody != null
                    && IsFiniteVector(transform.position) && IsFiniteRotation(transform.rotation))
                {
                    GetEstimatedVelocities(out _tossVelocity, out _tossAngularVelocity);
                    if (_tossVelocity.y >= 0.25f)
                    {
                        _flightOrigin = transform.position;
                        _flightRotation = transform.rotation;
                        _pendingAt = now;
                        _pendingFrame = Time.frameCount;
                        _state = FingerPlayState.ReleasePending;
                        ExitInteractionAuthority();
                        Debug.Log("[FingerPlayHarness] TOSS_PENDING evidence=NearPinchRawEdgeAndIntent heuristic=True");
                        return;
                    }
                }
            }
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

        private void ResetTossObservation()
        {
            _tossGate?.Process(GateEvent.Reset());
            _tossSource = null;
            _tossTarget = null;
            _tossApi = null;
            _tossHand = null;
            _tossRaw = null;
            _tossOwnerId = -1;
            _tossBlocked = false;
            _tossReleaseConsumed = false;
            _rawReleaseAt = _sdkReleaseAt = double.NegativeInfinity;
        }

        private static FromOVRHandDataSource FindRawTossSource(IHand hand)
        {
            object current = hand;
            for (int depth = 0; depth < 16; depth++)
            {
                if (current is FromOVRHandDataSource raw) return raw;
                if (current is HandRef reference) current = reference.Hand;
                else if (current is DataModifier<HandDataAsset> modifier) current = modifier.ModifyDataFromSource;
                else return null;
            }
            return null;
        }

        private void BeginTossObservation()
        {
            if (!_enableToss || !(_provenanceSource is HandGrabInteractor source)) return;
            var target = source.TargetInteractable as HandGrabInteractable;
            var api = source.HandGrabApi;
            if (target == null || !ReferenceEquals(target.PointableElement, _grabbable) || api == null
                || !ReferenceEquals(source.Hand, api.Hand) || (target.SupportedGrabTypes & GrabTypeFlags.Pinch) == 0
                || !api.IsHandPinchGrabbing(target.PinchGrabRules) || api.IsHandPalmGrabbing(target.PalmGrabRules)) return;
            var raw = FindRawTossSource(source.Hand);
            if (raw == null) return;
            _tossSource = source;
            _tossTarget = target;
            _tossApi = api;
            _tossHand = source.Hand;
            _tossRaw = raw;
            _tossOwnerId = _currentOwnerId;
            _tossGate ??= new TossIntentGate(TossIntentParameters.Experimental(ArmedHoldPolicy.HoldForWindow, 0.10));
            double now = Time.realtimeSinceStartupAsDouble;
            _tossGate.Process(GateEvent.GrabBegin(now));
            _rawChangedAt = _rawObservedAt = _tossSampleAt = now;
            _rawPinching = false;
            _sdkPinching = true;
            ObserveTossInput(true);
        }

        private void ObserveTossInput(bool held)
        {
            if (_tossBlocked) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (!_enableToss || !isActiveAndEnabled || _tossSource == null || !_tossSource.isActiveAndEnabled
                || _tossSource.State == InteractorState.Disabled || _tossSource.Identifier != _tossOwnerId
                || _tossTarget == null || !_tossTarget.isActiveAndEnabled || _tossApi == null || !_tossApi.isActiveAndEnabled
                || _tossRaw == null || !_tossRaw.isActiveAndEnabled || _provenanceCancelObserved
                || !ReferenceEquals(_tossSource.Hand, _tossHand) || !ReferenceEquals(_tossSource.HandGrabApi, _tossApi)
                || !ReferenceEquals(_tossApi.Hand, _tossHand) || !ReferenceEquals(FindRawTossSource(_tossHand), _tossRaw)
                || !ReferenceEquals(_tossTarget.PointableElement, _grabbable)
                || (held && (_currentOwnerId != _tossOwnerId || GetSelectingPointsCount() != 1
                    || !ReferenceEquals(_tossSource.SelectedInteractable, _tossTarget)))
                || now < _rawObservedAt || now - _rawObservedAt > TossInputWindow)
            {
                _tossBlocked = true;
                return;
            }
            var data = _tossRaw.GetData();
            if (!data.IsDataValid || !data.IsConnected || !data.IsTracked || !data.IsHighConfidence
                || data.RootPoseOrigin != PoseOrigin.RawTrackedPose || !IsFiniteVector(data.Root.position)
                || !IsFiniteRotation(data.Root.rotation))
            {
                _tossBlocked = true;
                return;
            }
            HandFingerFlags pinches = HandFingerFlags.None;
            for (int finger = 0; finger < 5; finger++)
            {
                if (!data.IsFingerHighConfidence[finger]) { _tossBlocked = true; return; }
                if (data.IsFingerPinching[finger]) pinches |= (HandFingerFlags)(1 << finger);
            }
            // Content changes are a conservative freshness proxy, never a sensor timestamp or DataVersion proof.
            if (!data.Root.position.Equals(_lastRawPose.position) || !data.Root.rotation.Equals(_lastRawPose.rotation)
                || pinches != _lastRawPinches) _rawChangedAt = now;
            _lastRawPose = data.Root;
            _lastRawPinches = pinches;
            _rawObservedAt = now;
            bool rawPinching = _tossApi.IsSustainingGrab(_tossTarget.PinchGrabRules, pinches);
            bool sdkPinching = _tossApi.IsHandPinchGrabbing(_tossTarget.PinchGrabRules);
            if (_tossApi.IsHandPalmGrabbing(_tossTarget.PalmGrabRules)) _tossBlocked = true;
            if (_rawPinching && !rawPinching) _rawReleaseAt = now;
            if (_sdkPinching && !sdkPinching && _tossApi.IsHandUnselectPinchFingersChanged(_tossTarget.PinchGrabRules))
                _sdkReleaseAt = now;
            _rawPinching = rawPinching;
            _sdkPinching = sdkPinching;
        }

        private void ResolvePendingToss()
        {
            ObserveTossInput(false);
            double now = Time.realtimeSinceStartupAsDouble;
            if (_tossBlocked || _rawPinching || _sdkPinching || _rigidbody == null
                || GetSelectingPointsCount() != 0 || _tossSource == null || _tossSource.State == InteractorState.Select
                || !IsFiniteVector(transform.position) || !IsFiniteRotation(transform.rotation)
                || Vector3.Distance(transform.position, _flightOrigin) > 0.05f
                || now - _pendingAt > TossInputWindow || now - _rawChangedAt > TossInputWindow
                || !IsFiniteVector(Physics.gravity) || Physics.gravity.magnitude > 20f
                || !IsFiniteVector(_tossVelocity) || !IsFiniteVector(_tossAngularVelocity))
            {
                RecoverToss("ReleaseRejected");
                return;
            }
            _tossVelocity = Vector3.ClampMagnitude(_tossVelocity, TossLaunchSpeed);
            _tossAngularVelocity = Vector3.ClampMagnitude(_tossAngularVelocity, TossSpinSpeed);
            _savedMaxAngularVelocity = _rigidbody.maxAngularVelocity;
            _rigidbody.maxAngularVelocity = TossSpinSpeed;
            _state = FingerPlayState.Flight;
            _flightStartedAt = now;
            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = true;
            _rigidbody.linearDamping = 0.02f;
            _rigidbody.angularDamping = 0.05f;
            _rigidbody.linearVelocity = _tossVelocity;
            _rigidbody.angularVelocity = _tossAngularVelocity;
            ResetMotionSampling();
            Debug.Log("[FingerPlayHarness] TOSS_FLIGHT_BEGIN path=NearHandGrabPinch runtimeValidation=Pending");
        }

        private void UpdateTossFlight()
        {
            double age = Time.realtimeSinceStartupAsDouble - _flightStartedAt;
            if (!_enableToss || _rigidbody == null || _rigidbody.isKinematic || GetSelectingPointsCount() != 0
                || age < 0 || age >= TossFlightDuration || !IsFiniteVector(transform.position)
                || !IsFiniteRotation(transform.rotation) || !IsFiniteVector(_rigidbody.linearVelocity)
                || !IsFiniteVector(_rigidbody.angularVelocity) || !IsFiniteVector(Physics.gravity)
                || Physics.gravity.magnitude > 20f || _rigidbody.linearVelocity.magnitude > TossFlightSpeed
                || _rigidbody.angularVelocity.magnitude > TossSpinSpeed + 0.01f
                || !IsFinite(Time.fixedDeltaTime) || Time.fixedDeltaTime <= 0 || Time.fixedDeltaTime > 0.1f)
            {
                RecoverToss("FlightLimit");
                return;
            }
            Vector3 offset = transform.position - _flightOrigin;
            Vector3 next = offset + _rigidbody.linearVelocity * Time.fixedDeltaTime
                + Physics.gravity * (0.5f * Time.fixedDeltaTime * Time.fixedDeltaTime);
            if (offset.magnitude >= TossRadius || next.magnitude >= TossRadius
                || offset.y >= TossHeight || next.y >= TossHeight || offset.y < -0.15f || next.y < -0.15f
                || (age > 0.05 && offset.y <= 0 && _rigidbody.linearVelocity.y < 0))
                RecoverToss("FlightComplete");
        }

        private void RecoverToss(string reason)
        {
            bool wasFlight = _state == FingerPlayState.Flight;
            _tossBlocked = true;
            _tossReleaseConsumed = true;
            _state = FingerPlayState.Settled;
            if (_rigidbody != null)
            {
                if (!_rigidbody.isKinematic)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                }
                _rigidbody.isKinematic = true;
                _rigidbody.useGravity = false;
                if (wasFlight) _rigidbody.maxAngularVelocity = _savedMaxAngularVelocity;
            }
            if (GetSelectingPointsCount() == 0)
            {
                transform.SetPositionAndRotation(_flightOrigin, _flightRotation);
                if (_rigidbody != null)
                {
                    _rigidbody.position = _flightOrigin;
                    _rigidbody.rotation = _flightRotation;
                }
            }
            ResetMotionSampling();
            _settleTimer = _settleHoldTimer = 0;
            ExitInteractionAuthority();
            _selectionRecoveryLocked = GetSelectingPointsCount() > 0;
            if (_coinPresenter != null) _coinPresenter.enabled = false;
            Debug.Log($"[FingerPlayHarness] TOSS_RECOVERY reason={reason} grabbable={!_selectionRecoveryLocked}");
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
            _provenanceLastMotionFrame = Time.frameCount;

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

            if (_state == FingerPlayState.Flight || _state == FingerPlayState.ReleasePending)
                RecoverToss("Reset");
            ResetTossObservation();
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
