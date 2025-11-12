using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace myro
{
	public enum EGrabbed
	{
		NONE,
		BOTH_HANDS,
		ONE_HANDED
	}

	public enum EGestureMode
	{
		Grab,
		Trigger,
		Both
	}

	public enum EClosingBehaviour
	{
		Closing,
		Respawning
	}

	public enum EForceState
	{
		NONE,
		FORCE_CLOSE,
		FORCE_OPEN
	}

	public enum EAllowedInputs
	{
		GRAB_LEFT,
		GRAB_RIGHT,
		TRIGGER_LEFT,
		TRIGGER_RIGHT
	}

	public enum EConstrained
	{
		None,
		Position,
		View
	}

	[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
	[DefaultExecutionOrder(500)]
	public class PortablePanel : UdonSharpBehaviour
	{
		[Header("See README file for additional help and infos")]
		[Tooltip("The panel GameObject to show/hide and manipulate")]
		public GameObject Panel;
		[Tooltip("When true, holding Tab keeps the panel open. When false, Tab toggles open/closed")]
		public bool TabOnHold = true;
		[Tooltip("When true, the player must be stopped (or moving slow) to open/resize the panel")]
		public bool RequireStopped = true;
		[Tooltip("Choose which hand gestures can be used to interact with the panel")]
		public EGestureMode GestureMode;
		[Tooltip("Determines if the panel is hidden or respawned to original position when closed")]
		public EClosingBehaviour CloseBehaviour;
		[Tooltip("Maximum scale multiplier for the panel")]
		public float MaxScale = 9999.0f;
		[Tooltip("Minimum scale multiplier for the panel")]
		public float MinScale = 0.1f;
		[Tooltip("Distance in meters before the panel auto-closes in VR")]
		public float MaxDistanceBeforeClosingThePanel = 2f;
		[Tooltip("Scale of the panel when placed in front of the player on desktop")]
		public float PanelScaleOnDesktop = 0.5f;
		[Tooltip("When true, ownership of the panel is transferred to the local player on pickup")]
		public bool SetOwnerOnPickup = false;
		[Tooltip("Constrains the panel to follow the player's position and/or view")]
		public EConstrained ConstraintMode = EConstrained.None;
		[Tooltip("When true, the player is able to grab and move the panel.")]
		[SerializeField] private bool _isPickupable = true;
		private bool _isLocked = false; // Should always start false, so no need to expose this. The player can do _ToggleLocked at runtime to manipulate it.
		[Header("Advanced settings, change them only if you really need to")]
		[SerializeField] private bool _delayInitialisation = false;
		private Transform _panelTransf;
		private const float MAX_GRAB_SPEED = 1f; // This could be a slider, but it seems like such a minor thing that it may as well be a private variable.
		private PortablePanelPickupModule _pickupModule;
		private VRCPlayerApi _localPlayer;
		private EGrabbed _grabbed;
		private DataList _triggeredInputList;
		private float _timeStartTrigger, _timeEndTrigger;

		//The variables bellow are used to calculate the position and the scale of the panel once it's hold with both hand
		private float _startDistanceBetweenTwoHands;
		private Vector3 _oneHandedOrigin;
		private float _startScale;
		private float _currentScale;

		//The variables bellow are used to calculate the position of the panel once it's hold with one hand
		private Quaternion _offsetRotation;
		private Vector3 _offsetPosition;
		private VRCPlayerApi.TrackingDataType _panelAttachedToHand;

		//This bool is only used to avoid event spams
		private bool _eventAlreadySend = false;

		//Boleans used to open or close the panel without player inputs
		private EForceState _forceStateOfPanel;

		//Here we are saving the transforms of the panel, so we can respawn it with the original scale.
		private Vector3 _position;
		private Quaternion _rotation;
		private Vector3 _scale;


		//Here we are saving the transforms of the panel for relative offsets when using position or view constrained.
		private Vector3 _constraintOffsetPosition;
		private Quaternion _constraintOffsetRotation;
		private Vector3 _positionConstraintOffset;

		private bool _isPanelOpen;
		private bool _init;
		private bool _isUsingViveControllers;
		private bool _grabCalled, _dropCalled;

		private const float TIME_INTERVAL_HAND_GESTURE = 0.3f;
		private const float MAX_DISTANCE_HAND_GESTURE = 0.25f;
		private const float PLACEMENT_DISTANCE_FROM_HEAD = 0.3f;
		private const float CLOSING_HAND_DISTANCE = 0.24f;

		private Vector3 _pickupOffsetDirection;
		private float _pickupBaseDistance;
		private const float JOYSTICK_DEADZONE = 0.1f;
		private const float PUSH_PULL_SENSITIVITY = 0.5f;

		void OnEnable()
		{
			_localPlayer = Networking.LocalPlayer;
			_triggeredInputList = new DataList();
			_panelTransf = Panel.transform;	
		}

		private void Start()
		{
			
    		_isLocked = false;
			_isUsingViveControllers = IsViveController();

			SetRespawnPoint(_panelTransf.position,
				_panelTransf.rotation,
				_panelTransf.localScale);

			if (!_delayInitialisation)
				Initialisation();
		}

		public override void OnPlayerJoined(VRCPlayerApi player)
		{
			if (_delayInitialisation && player.isLocal)
				Initialisation();
		}

		public void Initialisation()
		{
			CloseOrRespawnPanel();
			OnStart();
			_init = true;
		}

		private void SetPanelScale(float newScale)
		{
			float oldScale = _panelTransf.localScale.x;
			if (oldScale != newScale)
			{
				_panelTransf.localScale = new Vector3(newScale, newScale, newScale);
				OnPanelScaled(oldScale, newScale);
			}
		}

		public void SetPickupModule(PortablePanelPickupModule pickupModule)
		{
			_pickupModule = pickupModule;
		}

		#region Public methods

		/// <summary>
		/// Sets the "Tab on Hold" behavior for the panel.
		/// When true, the panel remains open while holding Tab.
		/// When false, Tab toggles the panel open/closed.
		/// </summary>
		public void SetTabOnHold(bool isTabOnHold)
		{
			if (TabOnHold != isTabOnHold)
			{
				TabOnHold = isTabOnHold;
			}
		}

		/// <summary>
		/// Toggles the "Tab on Hold" behavior of the panel.
		/// </summary>
		public void _ToggleTabOnHold()
		{
			SetTabOnHold(!TabOnHold);
		}

		/// <summary>
		/// Sets whether the player must be stopped to grab/resize the panel.
		/// </summary>
		public void SetRequireStopped(bool requireStopped)
		{
			RequireStopped = requireStopped;
		}


		/// <summary>
		/// Sets the "tab on hold" state of your panel. This will also work for VRCPickups
		/// </summary>
		/// <param name="newPickupableState">The new tab on hold state</param>
		public void SetPickupable(bool newPickupableState)
		{
			_isPickupable = newPickupableState;
			if (_pickupModule != null)
			{
				if (newPickupableState)
				{
					_pickupModule.EnablePickup();
				}
				else
				{
					_pickupModule.DisablePickup();
				}
			}
		}

		/// <summary>
		/// Toggles the "pickupable" state of your panel. This will also work for VRCPickups
		/// </summary>
		public void _TogglePickupable()
		{
			SetPickupable(!_isPickupable);
		}

		/// <summary>
		/// Return true if the panel is pickupable
		/// </summary>
		/// <returns></returns>
		public bool IsPickupable()
		{
			return _isPickupable;
		}

		/// <summary>
		/// Sets the constraint mode for the panel.
		/// NONE: Panel is not constrained
		/// Position: Panel follows player position but maintains its own rotation
		/// View: Panel is locked to head position and rotation
		/// </summary>
		public void SetConstraintMode(EConstrained constraintMode)
		{
			if (ConstraintMode != constraintMode)
			{
				ConstraintMode = constraintMode;
				if (_isPanelOpen && constraintMode != EConstrained.None)
				{
					CacheConstraintOffsets();
				}
			}
		}

		/// <summary>
		/// Sets the view constraint mode for the panel.
		/// When enabled, panel stays locked to head rotation.
		/// </summary>
		public void SetViewConstrained(bool isViewConstrained)
		{
			SetConstraintMode(isViewConstrained ? EConstrained.View : EConstrained.None);
		}

		/// <summary>
		/// Sets the position constraint mode for the panel.
		/// When enabled, panel follows player position but maintains its own rotation.
		/// </summary>
		public void SetPositionConstrained(bool isPositionConstrained)
		{
			SetConstraintMode(isPositionConstrained ? EConstrained.Position : EConstrained.None);
		}

		/// <summary>
		/// Toggles the view constraint mode of the panel.
		/// </summary>
		public void _ToggleViewConstrained()
		{
			if (ConstraintMode == EConstrained.View)
			{
				SetConstraintMode(EConstrained.None);
			}
			else
			{
				SetConstraintMode(EConstrained.View);
			}
		}

		/// <summary>
		/// Toggles the position constraint mode of the panel.
		/// </summary>
		public void _TogglePositionConstrained()
		{
			if (ConstraintMode == EConstrained.Position)
			{
				SetConstraintMode(EConstrained.None);
			}
			else
			{
				SetConstraintMode(EConstrained.Position);
			}
		}

		/// <summary>
		/// Returns true if the panel is view constrained.
		/// </summary>
		public bool IsViewConstrained()
		{
			return ConstraintMode == EConstrained.View;
		}

		/// <summary>
		/// Returns true if the panel is position constrained.
		/// </summary>
		public bool IsPositionConstrained()
		{
			return ConstraintMode == EConstrained.Position;
		}

		/// <summary>
		/// Returns the current constraint mode.
		/// </summary>
		public EConstrained GetConstraintMode()
		{
			return ConstraintMode;
		}

		public bool IsPanelOpen()
		{
			return _isPanelOpen;
		}

		public void ForceClosePanel()
		{
			if (IsPanelOpen())
			{
				CloseOrRespawnPanel();

				if (!_localPlayer.IsUserInVR())
				{
					//Desktop : this is mostly to make sure that the panel doesn't get reopened if the Desktop player keeps the Tab key pressed
					//VR : I guess no need to do anything?
					_forceStateOfPanel = EForceState.FORCE_CLOSE; 
				}
			}
		}

		/// <summary>
		/// Sets the locked state of the panel.
		/// When locked, the panel cannot be grabbed, moved, or resized.
		/// This will reset if the panel is closed or respawned.
		/// </summary>
		public void SetLocked(bool isLocked)
		{
			_isLocked = isLocked;
		}

		/// <summary>
		/// Toggles the locked state of the panel.
		/// </summary>
		public void _ToggleLocked()
		{
			SetLocked(!_isLocked);
		}

		/// <summary>
		/// Returns true if the panel is locked.
		/// </summary>
		public bool IsLocked()
		{
			return _isLocked;
		}

		public void ForceOpenPanel(float unscaledDistance)
		{
			if (!IsPanelOpen())
			{
				OpenPanel();

				PlacePanelInFrontOfPlayer(unscaledDistance);

				if (!_localPlayer.IsUserInVR())
				{
					_forceStateOfPanel = EForceState.FORCE_OPEN;
				}
			}
		}

		public void ForceOpenPanel()
		{
			ForceOpenPanel(PLACEMENT_DISTANCE_FROM_HEAD);
		}

		public bool IsPanelHoldByOneHand()
		{
			return _grabbed == EGrabbed.ONE_HANDED;
		}

		public void SetRespawnPoint(Vector3 position, Quaternion rotation, Vector3 scale)
		{
			_position = position;
			_rotation = rotation;
			_scale = scale;
		}

		private void SetOwner()
		{
			if (!SetOwnerOnPickup) return;

			if (!Networking.IsOwner(Panel))
				Networking.SetOwner(_localPlayer, Panel);
		}

		public void RespawnPanel()
		{
			CloseOrRespawnPanel();
		}

		#endregion

		#region Overridable events

		/// <summary>
		/// Gets called when the panel opens
		/// </summary>
		///  <returns>True if the panel needs to be opened.
		/// If you want to open the panel manually, for instance with an animator, then you can `return false;`
		/// </returns>
		public virtual bool OnPanelOpening()
		{
			return true;
		}

		/// <summary>
		/// Gets called when "Start" is called
		/// </summary>
		public virtual void OnStart()
		{
			
		}

		/// <summary>
		/// Gets called when the panel is about to get closed, so it is called when the panel is not closed yet
		/// </summary>
		/// <returns>True if the panel needs to be closed.
		/// If you want to close the panel manually, for instance with an animator, then you can `return false;` instead
		/// </returns>
		public virtual bool OnPanelClosing()
		{
			return true;
		}

		/// <summary>
		/// Gets called when the panel is getting grabbed, either by one hand or with both hands.
		/// If the parameter "GrabbablePanel" is set to false, the the event will ony be called while scaling the panel 8so when it's grabbed with both hands.
		/// </summary>
		public virtual void OnPanelGrab()
		{
		}

		/// <summary>
		/// Gets called when the panel is dropped
		/// </summary>
		public virtual void OnPanelDrop()
		{
		}

		/// <summary>
		/// Gets called when the panel gets scaled
		/// </summary>
		/// <param name="oldScale">The scale of the panel before it got scaled</param>
		/// <param name="oldScale">The new scale of the panel</param>
		/// <example>You could for instance use that method to change what's written on the panel based on the size, or 
		/// change the color based on the speed it gets scaled.</example>
		public virtual void OnPanelScaled(float oldScale, float newScale)
		{
		}

		#endregion

		#region Main code

		private void CloseOrRespawnPanel()
		{
			_grabbed = EGrabbed.NONE;
			_triggeredInputList = new DataList();

			if (OnPanelClosing())
			{
				if (CloseBehaviour == EClosingBehaviour.Closing)
				{
					Panel.SetActive(false);
				}
				else
				{
					RespawnToOriginalLocation();
				}
			}
			_isPanelOpen = false;
			_isLocked = false;
		}

		private void OpenPanel()
		{
			if (OnPanelOpening())
			{
				Panel.SetActive(true);
			}
			_isPanelOpen = true;
		}

		public void RespawnToOriginalLocation()
		{
			SetOwner();
			_panelTransf.position = _position;
			_panelTransf.rotation = _rotation;
			_panelTransf.localScale = _scale;
		}

		/// <summary>
		/// Scales the value based on the size of the avatar.
		/// If The avatar is 1m80 tall, 1 meter = 1 meter
		/// If the avatar is 1m60 tall, 1 meter will be turned into 1.60*1/1.80 = 0.88 meter
		/// </summary>
		/// <param name="value">The value the needs to be scaled based on the avatar size</param>
		/// <returns></returns>
		protected float ScaleValueToAvatar(float value)
		{
#if UNITY_EDITOR
			return value; // An older Client Sim bug makes the script crash if "GetAvatarEyeHeightAsMeters" gets called.
			// It should be fixed on newer version of ClientSim, but I'll keep it to ensure it still works on older SDKs
#else
			return _localPlayer.GetAvatarEyeHeightAsMeters() * value / 1.80f;
#endif
		}

		private void UpdateTriggeredInputList(EAllowedInputs input, bool isTriggered)
		{
			if (!isTriggered)
			{
				_triggeredInputList.Remove(System.Convert.ToInt32(input));
			}
            else if (IsValidInputToOpenPanel(input))
            {
				_triggeredInputList.Add(System.Convert.ToInt32(input));
				if (_triggeredInputList.Count == 1)
				{
					_timeStartTrigger = Time.time;
				}
				_timeEndTrigger = Time.time;
			}
		}

		private bool IsLeftControllerOn()
		{
			string[] inputs = UnityEngine.Input.GetJoystickNames();
			foreach (string input in inputs)
			{
				if (input.ToLower().Contains("left"))
					return true;
			}
			return false;
		}

		private bool IsRightControllerOn()
		{
			string[] inputs = UnityEngine.Input.GetJoystickNames();
			foreach (string input in inputs)
			{
				if (input.ToLower().Contains("right"))
					return true;
			}
			return false;
		}

		private bool IsOneHanded()
		{
			return !IsRightControllerOn() || !IsLeftControllerOn();
		}

		private DataList GetValidInputs()
		{
			DataList ret = new DataList();

			if (IsLeftControllerOn())
			{
				if (GestureMode == EGestureMode.Both || GestureMode == EGestureMode.Grab || IsOneHanded())
					ret.Add(Convert.ToInt32(EAllowedInputs.GRAB_LEFT));

				if (GestureMode == EGestureMode.Both || GestureMode == EGestureMode.Trigger || IsOneHanded())
					ret.Add(Convert.ToInt32(EAllowedInputs.TRIGGER_LEFT));
			}

			if (IsRightControllerOn())
			{
				if (GestureMode == EGestureMode.Both || GestureMode == EGestureMode.Grab || IsOneHanded())
					ret.Add(Convert.ToInt32(EAllowedInputs.GRAB_RIGHT));

				if (GestureMode == EGestureMode.Both || GestureMode == EGestureMode.Trigger || IsOneHanded())
					ret.Add(Convert.ToInt32(EAllowedInputs.TRIGGER_RIGHT));
			}

			return ret;
		}
		private bool IsValidInputToOpenPanel(EAllowedInputs inputToCheck)
		{
			DataList validInputs = GetValidInputs();
			return validInputs.Contains(Convert.ToInt32(inputToCheck));
		}

		private bool IsDoingOpeningPanelGesture()
		{
			DataList validInputs = GetValidInputs();

			for (int i = 0; i < validInputs.Count; i++)
			{
				if (!_triggeredInputList.Contains(validInputs[i]))
				{
					return false;
				}
			}
			return true;
		}

		private bool IsDoingGrabbingPanelGesture()
		{
			return _triggeredInputList.Contains(Convert.ToInt32(EAllowedInputs.GRAB_RIGHT)) || _triggeredInputList.Contains(Convert.ToInt32(EAllowedInputs.GRAB_LEFT));
		}

		private bool IsDoingGrabbingPanelGestureLeft()
		{
			return _triggeredInputList.Contains(Convert.ToInt32(EAllowedInputs.GRAB_LEFT));
		}

		private bool IsDoingGrabbingPanelGestureRight()
		{
			return _triggeredInputList.Contains(Convert.ToInt32(EAllowedInputs.GRAB_RIGHT));
		}

		private bool IsDoingGesture()
		{
			return _triggeredInputList.Count > 0;
		}

		private Vector3 GetOneHandedControllerPosition()
		{
			if (IsLeftControllerOn())
			{
				return _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position;
			}
			return _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;
		}

		private float CurrentHandsDistance(bool openingPhase)
		{
			if (IsOneHanded())
				return openingPhase
					? 0.001f //at this stage, _oneHandedOrigin isn't set yet, so we basically just assume that left and right hands are together
					: Vector3.Distance(_oneHandedOrigin, GetOneHandedControllerPosition());

			var l = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position;
			var r = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;
			return Vector3.Distance(l, r);
		}

		/// <summary>
		/// Returns true if the player's movement speed is below the grab threshold so they won't accidentally open the panel while flailing and triggering when falling/flying/running.
		/// If the panel is already open/pinned/locked, then this won't close it -- it just limits manipulation to when they're stopped.
		/// </summary>
		private bool IsPlayerSpeedWithinGrabThreshold()
		{
			if (!RequireStopped)
				return true;

			Vector3 velocity = _localPlayer.GetVelocity();
			return velocity.magnitude <= MAX_GRAB_SPEED;
		}

		private VRCPlayerApi.TrackingData GetTrackingDataForPlayerConstraints()
		{
			return _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin);
		}

		/// <summary>
		/// Caches the offset between the panel and the player's head for constraint calculations.
		/// Stores position in head-local space and rotation as a relative quaternion.
		/// </summary>
		private void CacheConstraintOffsets()
		{
			VRCPlayerApi.TrackingData trackingData = GetTrackingDataForPlayerConstraints();
			
			if (ConstraintMode == EConstrained.View)
			{
				_constraintOffsetPosition = Quaternion.Inverse(trackingData.rotation) * (_panelTransf.position - trackingData.position);
				_constraintOffsetRotation = Quaternion.Inverse(trackingData.rotation) * _panelTransf.rotation;
			}
			else if (ConstraintMode == EConstrained.Position)
			{
				_positionConstraintOffset = _panelTransf.position - trackingData.position;
				_constraintOffsetRotation = _panelTransf.rotation;
			}
		}


		/// <summary>
		/// Applies the player constraint to the panel.
		/// </summary>
		private void ApplyPlayerConstraint()
		{
			if (!_localPlayer.IsUserInVR()) return;
			if (ConstraintMode == EConstrained.None || _grabbed != EGrabbed.NONE) return;

			VRCPlayerApi.TrackingData trackingData = GetTrackingDataForPlayerConstraints();

			SetOwner();

			if (ConstraintMode == EConstrained.View)
			{
				_panelTransf.position = trackingData.position + (trackingData.rotation * _constraintOffsetPosition);
				_panelTransf.rotation = trackingData.rotation * _constraintOffsetRotation;
			}
			else if (ConstraintMode == EConstrained.Position)
			{
				_panelTransf.position = trackingData.position + _positionConstraintOffset;
				_panelTransf.rotation = _constraintOffsetRotation;
			}
		}

		private void HandleInput(bool value, UdonInputEventArgs args)
		{
			if (_isLocked)
			{
				return;
			}
			
			if (IsDoingOpeningPanelGesture() 
				&& (!IsPanelOpen() || !IsOneHanded())
				&& Mathf.Abs(_timeStartTrigger - _timeEndTrigger) < TIME_INTERVAL_HAND_GESTURE
				&& IsPlayerSpeedWithinGrabThreshold())
			{
				if (IsPanelOpen() && _grabbed == EGrabbed.BOTH_HANDS) return;
				var dist = CurrentHandsDistance(true);

				//fixed an odd bug where the player had to move away from the panel before they could open it
				if (dist >= ScaleValueToAvatar(MAX_DISTANCE_HAND_GESTURE) && !IsPanelOpen()) return;

				//If the grab gesture is used on both hands, we open the panel
				//but if the panel was already open, we rescale the panel
				OnPanelGrab();

				if (!IsPanelOpen() && _forceStateOfPanel != EForceState.FORCE_CLOSE)
				{
					_startDistanceBetweenTwoHands = dist;
					_oneHandedOrigin = GetOneHandedControllerPosition();
					_startScale = dist;
					OpenPanel();
				}
				else
				{
					_oneHandedOrigin = _panelTransf.transform.position;
					_startScale = _panelTransf.localScale.x;
					_startDistanceBetweenTwoHands = Vector3.Distance(_oneHandedOrigin, GetOneHandedControllerPosition());
				}

				_grabbed = EGrabbed.BOTH_HANDS;
			}
			else if (IsDoingGrabbingPanelGesture() && IsPlayerSpeedWithinGrabThreshold())
			{
				//Grab gesture with one hand :
				//- If the player previously grabbed the panel with both hands, we can add a little delay before "ungrabbing" the panel to avoid accidental 
				//  displacements of the panel
				//- If not, then the player didn't used the grab gesture, in that case we will attach the panel on one hand
				if (_grabbed == EGrabbed.BOTH_HANDS && !_eventAlreadySend)
				{
					_eventAlreadySend = true;
					SendCustomEventDelayedSeconds(nameof(EventEnableOneHandMovement), 0.1f);
					return;
				}

				if (_pickupModule == null && IsPanelOpen())
				{
					AttachToHand();
				}
			}
			else
			{
				//No more gestures => Dropping the panel
				_forceStateOfPanel = EForceState.NONE;

				OnPanelDrop();

				if (IsPanelOpen() && _panelTransf.localScale.x < ScaleValueToAvatar(CLOSING_HAND_DISTANCE))
				{
					CloseOrRespawnPanel();
				}

				_grabbed = EGrabbed.NONE;
				
				if (_isPanelOpen && ConstraintMode != EConstrained.None && IsPlayerSpeedWithinGrabThreshold())
				{
					CacheConstraintOffsets();
				}
				return;
			}
		}

		private void HandleInputGrab(bool value, UdonInputEventArgs args)
		{
			if (!_localPlayer.IsUserInVR())
			{
				return;
			}

			UpdateTriggeredInputList(
				args.handType == HandType.LEFT ? EAllowedInputs.GRAB_LEFT : EAllowedInputs.GRAB_RIGHT,
				value
			);

			HandleInput(value, args);
		}

		private bool IsViveController()
		{
			string[] joystickNames = UnityEngine.Input.GetJoystickNames();
			foreach(var joystickName in joystickNames)
			{
				if (joystickName.ToLower().Contains("vive"))
					return true;
			}
			return false;
		}

		public override void InputDrop(bool value, UdonInputEventArgs args)
		{
			if (!_init || !_isUsingViveControllers) return;

			HandleInputGrab(value, args);
		}

		public override void InputGrab(bool value, UdonInputEventArgs args)
		{
			if (!_init || _isUsingViveControllers) return;

			HandleInputGrab(value, args);
		}

		public override void InputUse(bool value, UdonInputEventArgs args)
		{
			if (!_init) return;

			if (!_localPlayer.IsUserInVR())
			{
				return;
			}

			UpdateTriggeredInputList(
				args.handType == HandType.LEFT ? EAllowedInputs.TRIGGER_LEFT : EAllowedInputs.TRIGGER_RIGHT,
				value
			);

			HandleInput(value, args);
		}

		private void EventEnableOneHandMovement()
		{
			if (_grabbed == EGrabbed.BOTH_HANDS)
			{
				AttachToHand();
			}
			_eventAlreadySend = false;
		}

		private bool PanelTooFarAway()
		{
			return Vector3.Distance(_localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position, _panelTransf.position)
				 > ScaleValueToAvatar(MaxDistanceBeforeClosingThePanel);
		}

		private void AttachToHand()
		{
			if (_pickupModule != null)
			{
				return;
			}
			else
			{
				_isPanelOpen = true;
				_grabbed = EGrabbed.ONE_HANDED;

				if (IsDoingGrabbingPanelGestureLeft())
					_panelAttachedToHand = VRCPlayerApi.TrackingDataType.LeftHand;
				else
					_panelAttachedToHand = VRCPlayerApi.TrackingDataType.RightHand;

				VRCPlayerApi.TrackingData hand = _localPlayer.GetTrackingData(_panelAttachedToHand);
				_offsetRotation = Quaternion.Inverse(hand.rotation) * _panelTransf.rotation;
				_offsetPosition = Quaternion.Inverse(hand.rotation) * (_panelTransf.position - hand.position);

				OnPanelGrab();
			}
		}

		public void _PanelPickedUp()
		{
			_isPanelOpen = true;
			_grabbed = EGrabbed.ONE_HANDED;
			_startScale = _panelTransf.localScale.x;
			_currentScale = _startScale;
			
			if (_localPlayer.IsUserInVR())
			{
				Vector3 headPos = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
				_pickupOffsetDirection = (_panelTransf.position - headPos).normalized;
				_pickupBaseDistance = Vector3.Distance(headPos, _panelTransf.position);
			}
			
			OnPanelGrab();
			
			if (ConstraintMode != EConstrained.None)
			{
				CacheConstraintOffsets();
			}
		}

		public void _PanelDropped()
		{
			_grabbed = EGrabbed.NONE;
			OnPanelDrop();

			if (_isPanelOpen && ConstraintMode != EConstrained.None && IsPlayerSpeedWithinGrabThreshold())
			{
				CacheConstraintOffsets();
			}
		}
		//Couldn't get this to work... It would be nice if this could be push/pulled with the right stick when constrained to local space and held.
		// private float GetRightStickVertical()
		// {
		// 	float input = 0f;
			
		// 	string[] axesToTry;
			
		// 	if (_panelAttachedToHand == VRCPlayerApi.TrackingDataType.LeftHand)
		// 	{
		// 		axesToTry = new string[] {
		// 			"Oculus_CrossPlatform_SecondaryThumbstickVertical",
		// 			"Vertical2",
		// 			"LeftStickVertical",
		// 			"Joy1 Axis 2"
		// 		};
		// 	}
		// 	else
		// 	{
		// 		axesToTry = new string[] {
		// 			"Oculus_CrossPlatform_PrimaryThumbstickVertical", 
		// 			"Vertical",
		// 			"RightStickVertical",
		// 			"Joy1 Axis 5"
		// 		};
		// 	}
			
		// 	foreach (string axis in axesToTry)
		// 	{
		// 		float axisValue = UnityEngine.Input.GetAxis(axis);
		// 		if (Mathf.Abs(axisValue) > JOYSTICK_DEADZONE)
		// 		{
		// 			input = axisValue;
		// 			break;
		// 		}
		// 	}
			
		// 	return input;
		// }

		public override void PostLateUpdate()
{
	if (!_init) return;

	if (!_localPlayer.IsUserInVR())
	{
		if (TabOnHold)
		{
			bool tabPressed = UnityEngine.Input.GetKey(KeyCode.Tab);

			if ((tabPressed && _forceStateOfPanel != EForceState.FORCE_CLOSE && IsPlayerSpeedWithinGrabThreshold())
				|| _forceStateOfPanel == EForceState.FORCE_OPEN)
			{
				if (!IsPanelOpen())
				{
					OpenPanel();
					if (ConstraintMode != EConstrained.None)
					{
						CacheConstraintOffsets();
					}
				}
				PlacePanelInFrontOfPlayer();

				if (tabPressed)
				{
					_forceStateOfPanel = EForceState.NONE;
				}
			}
			else if (!tabPressed || _forceStateOfPanel == EForceState.FORCE_CLOSE)
			{
				if (IsPanelOpen())
				{
					CloseOrRespawnPanel();
				}
				if (!tabPressed)
				{
					_forceStateOfPanel = EForceState.NONE;
				}
			}
		}
		else
		{
			bool tabPressedDown = UnityEngine.Input.GetKeyDown(KeyCode.Tab);

			if (!IsPanelOpen() && ((tabPressedDown && IsPlayerSpeedWithinGrabThreshold()) || _forceStateOfPanel == EForceState.FORCE_OPEN))
			{
				OpenPanel();
				if (ConstraintMode != EConstrained.None)
				{
					CacheConstraintOffsets();
				}
				PlacePanelInFrontOfPlayer();

				_forceStateOfPanel = EForceState.NONE;
			}
			else if (IsPanelOpen() && (tabPressedDown || _forceStateOfPanel == EForceState.FORCE_CLOSE || PanelTooFarAway()))
			{
				CloseOrRespawnPanel();
				_forceStateOfPanel = EForceState.NONE;
			}
		}
	}
	else
	{
		if (_grabbed == EGrabbed.NONE)
		{
			if (IsPanelOpen() && PanelTooFarAway())
			{
				CloseOrRespawnPanel();
			}
			else if (IsPanelOpen() && _localPlayer.IsUserInVR())
			{
				ApplyPlayerConstraint();
			}
			return;
		}
		else if (_grabbed == EGrabbed.ONE_HANDED)
		{
			if (_isPickupable && !_pickupModule)
			{
				VRCPlayerApi.TrackingData hand = _localPlayer.GetTrackingData(_panelAttachedToHand);
				SetOwner();
				
				if (_localPlayer.IsUserInVR())
				{
					// float stickInput = GetRightStickVertical();
					
					// if (Mathf.Abs(stickInput) > JOYSTICK_DEADZONE)
					// {
					// 	_pickupBaseDistance += stickInput * PUSH_PULL_SENSITIVITY * Time.deltaTime;
					// 	_pickupBaseDistance = Mathf.Clamp(_pickupBaseDistance, ScaleValueToAvatar(0.3f), ScaleValueToAvatar(3f));
						
					// 	Vector3 headPos = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
					// 	Vector3 targetPosition = headPos + _pickupOffsetDirection * _pickupBaseDistance;
						
					// 	_panelTransf.position = targetPosition;
					// 	_panelTransf.rotation = hand.rotation * _offsetRotation;
					// }
					// else
					// {
						_panelTransf.position = hand.position + (hand.rotation * _offsetPosition);
						_panelTransf.rotation = hand.rotation * _offsetRotation;
					// }
				}
				else
				{
					_panelTransf.position = hand.position + (hand.rotation * _offsetPosition);
					_panelTransf.rotation = hand.rotation * _offsetRotation;
				}
				
				if (ConstraintMode != EConstrained.None)
				{
					CacheConstraintOffsets();
				}
			}
		}
		else
		{
			float distance = CurrentHandsDistance(false);
			Vector3 origin;
			if (IsOneHanded())
			{
				distance *= 2.0f;
				origin = _oneHandedOrigin;
			}
			else
			{
				Vector3 left = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position;
				Vector3 right = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;
				origin = (left + right) / 2.0f;
			}
			Vector3 headPos = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;

			_currentScale = Mathf.Clamp(_startScale * distance / _startDistanceBetweenTwoHands, ScaleValueToAvatar(MinScale), ScaleValueToAvatar(MaxScale));
			SetOwner();
			_panelTransf.position = origin;
			SetPanelScale(_currentScale);
			_panelTransf.LookAt(headPos);
			_panelTransf.forward = -_panelTransf.forward;
		}
	}
}


		private void PlacePanelInFrontOfPlayer(float unscaledDistance = PLACEMENT_DISTANCE_FROM_HEAD)
		{
			Quaternion headRot = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation;
			Vector3 headPos = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;

			float distance = ScaleValueToAvatar(unscaledDistance);
			float scale = ScaleValueToAvatar(PanelScaleOnDesktop) * unscaledDistance / PLACEMENT_DISTANCE_FROM_HEAD;
			if (distance < 0.08f)
			{
				//If the avatar is really small, we need to place the menu a bit further away so it doesn't get clipped by the camera
				distance = 0.08f;
				scale = distance * PanelScaleOnDesktop / 0.3f;
			}
			if (scale < MinScale)
			{
				//Now, if the scale is smaller than MinScale, we need to readjust the scale and the distance.
				distance = MinScale * distance / scale;
				scale = MinScale;
			}
			SetOwner();
			_panelTransf.position = (headPos + headRot * Vector3.forward * distance);
			SetPanelScale(scale);
			_panelTransf.LookAt(headPos);
			_panelTransf.forward = -_panelTransf.forward;
		}

		#endregion
	}
}
